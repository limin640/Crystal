using System.IO.Compression;

namespace Crystal.Assets.Parsers;

/// <summary>
/// WIL / WZL / MIZ parsers matching <c>LibraryEditor/Graphics/WeMadeLibrary.cs</c>.
/// Pixel decode is implemented for the formats used by Client conversion; unknown variants
/// still contribute header/index coverage and are not replaced with invented art.
/// </summary>
public static class WilWzlParser
{
    static readonly int[] DefaultPalette = BuildDefaultPalette();

    public static LibraryParseResult Parse(DiscoveredLibrary library)
    {
        if (library.Kind is LibraryKind.Wil or LibraryKind.Wzl or LibraryKind.Miz)
        {
            if (library.IndexPath == null || !File.Exists(library.IndexPath))
                return LibraryParser.Failed(library, $"Missing index companion for {library.Kind}");
        }

        using var stream = File.OpenRead(library.Path);
        using var reader = new BinaryReader(stream);

        byte nType = library.Kind switch
        {
            LibraryKind.Wzl => 1,
            LibraryKind.Miz => 4,
            _ => 0
        };

        int[] palette = DefaultPalette;
        int version = 0;

        if (nType == 0)
        {
            if (stream.Length < 52)
                return LibraryParser.Failed(library, "WIL too small");

            byte[] buffer = reader.ReadBytes(49);
            if (buffer[40] == 1 || buffer[40] == 6)
                nType = 2;
            else if (buffer[2] == 73 || buffer[2] == 72)
                nType = 3;
            else if (buffer.Length > 48 && buffer[48] == 32)
                nType = 5;

            if ((nType == 2 || nType == 0) && stream.Position + 2 <= stream.Length && reader.ReadInt16() == 32)
                nType = 5;

            if (nType == 0)
            {
                stream.Position = 48;
                int paletteLen = reader.ReadInt32();
                if (paletteLen <= 0 || paletteLen > 1024)
                    return LibraryParser.Failed(library, $"Bad palette length {paletteLen}");
                palette = new int[paletteLen];
                palette[0] = unchecked((int)0xFF000000);
                stream.Seek(4, SeekOrigin.Current);
                version = reader.ReadInt32();
                if (version != 0)
                    stream.Seek(4, SeekOrigin.Current);
                for (int i = 1; i < palette.Length && stream.Position + 4 <= stream.Length; i++)
                    palette[i] = reader.ReadInt32() + (255 << 24);
            }
        }

        var indices = LoadIndex(library.IndexPath!, nType, version);
        if (indices.Count == 0)
            return LibraryParser.Failed(library, "Empty or unreadable index file");

        byte[] structureSize = { 8, 16, 16, 17, 16, 16 };
        var images = new List<ParsedImage>(indices.Count);

        for (int i = 0; i < indices.Count; i++)
        {
            int offset = indices[i];
            if (offset <= 0 || offset >= stream.Length)
            {
                images.Add(new ParsedImage { Index = i, IsBlank = true });
                continue;
            }

            stream.Position = offset;
            try
            {
                images.Add(ReadImage(reader, i, nType, version, palette, structureSize));
            }
            catch (Exception ex)
            {
                images.Add(new ParsedImage { Index = i, IsBlank = true });
                _ = ex;
            }
        }

        return new LibraryParseResult
        {
            Path = library.Path,
            RelativePath = library.RelativePath,
            Kind = library.Kind,
            HeaderParsed = true,
            Version = nType,
            ImageCount = indices.Count,
            Images = images
        };
    }

    static ParsedImage ReadImage(BinaryReader reader, int index, byte nType, int version, int[] palette, byte[] structureSize)
    {
        bool bo16bit = false;
        if (nType == 1)
        {
            bo16bit = reader.ReadByte() == 5;
            reader.ReadBytes(3);
        }

        short width = reader.ReadInt16();
        short height = reader.ReadInt16();
        short x = reader.ReadInt16();
        short y = reader.ReadInt16();
        int nSize = width * height;
        bool hasShadow = false;
        short shadowX = 0, shadowY = 0;

        switch (nType)
        {
            case 1:
                nSize = reader.ReadInt32();
                break;
            case 4:
                bo16bit = true;
                nSize = reader.ReadInt32();
                break;
            case 2:
                bo16bit = true;
                reader.ReadInt16();
                reader.ReadInt16();
                nSize = reader.ReadInt32();
                if (nSize < 6) width = 0;
                break;
            case 3:
                bo16bit = true;
                hasShadow = reader.ReadByte() == 1;
                shadowX = reader.ReadInt16();
                shadowY = reader.ReadInt16();
                nSize = reader.ReadInt32() * 2;
                break;
            case 5:
                reader.ReadInt16();
                reader.ReadInt16();
                nSize = reader.ReadInt32();
                if (nSize < 6) width = 0;
                break;
        }

        if (width <= 0 || height <= 0 || nSize <= 0)
        {
            return new ParsedImage
            {
                Index = index, Width = width, Height = height, OffsetX = x, OffsetY = y,
                ShadowX = shadowX, ShadowY = shadowY, IsBlank = true
            };
        }

        byte[] bytes;
        switch (nType)
        {
            case 0:
                if (palette.Length > 256)
                {
                    bo16bit = true;
                    nSize *= 2;
                }
                bytes = reader.ReadBytes(nSize);
                break;
            case 1:
            case 4:
                bytes = InflateZlib(reader.ReadBytes(nSize));
                break;
            case 2:
            case 5:
            {
                byte compressed = reader.ReadByte();
                reader.ReadBytes(5);
                byte[] payload = reader.ReadBytes(Math.Max(0, nSize - 6));
                bytes = compressed == 8 ? InflateDeflate(payload) : payload;
                break;
            }
            default:
                // Mir3 WIL (type 3) uses a custom RLE; header is parsed, pixels left undecoded this phase.
                return new ParsedImage
                {
                    Index = index, Width = width, Height = height, OffsetX = x, OffsetY = y,
                    ShadowX = shadowX, ShadowY = shadowY, Shadow = hasShadow ? (byte)1 : (byte)0
                };
        }

        if (nType == 5 && bytes.Length == width * height * 2)
            bo16bit = true;

        if (bytes.Length <= 1)
        {
            return new ParsedImage
            {
                Index = index, Width = width, Height = height, OffsetX = x, OffsetY = y, IsBlank = true
            };
        }

        byte[] bgra = new byte[width * height * 4];
        int src = 0;
        for (int yRow = height - 1; yRow >= 0; yRow--)
        {
            for (int xCol = 0; xCol < width; xCol++)
            {
                int dst = (yRow * width + xCol) * 4;
                if (nType == 5 && !bo16bit)
                {
                    if (src + 4 > bytes.Length) goto done;
                    bgra[dst] = bytes[src];
                    bgra[dst + 1] = bytes[src + 1];
                    bgra[dst + 2] = bytes[src + 2];
                    bgra[dst + 3] = bytes[src + 3];
                    src += 4;
                    continue;
                }

                int argb;
                if (bo16bit)
                {
                    if (src + 2 > bytes.Length) goto done;
                    argb = Convert16To32(bytes[src] + (bytes[src + 1] << 8));
                    src += 2;
                }
                else
                {
                    if (src >= bytes.Length) goto done;
                    int pal = bytes[src++];
                    argb = pal < palette.Length ? palette[pal] : 0;
                }

                bgra[dst] = (byte)(argb);
                bgra[dst + 1] = (byte)(argb >> 8);
                bgra[dst + 2] = (byte)(argb >> 16);
                bgra[dst + 3] = (byte)(argb >> 24);
            }

            if ((nType == 1 || nType == 4) && width % 4 > 0)
            {
                int stride = WidthBytes(bo16bit ? 16 : 8, width);
                src += stride - (width * (bo16bit ? 2 : 1));
            }
        }

        done:
        return new ParsedImage
        {
            Index = index,
            Width = width,
            Height = height,
            OffsetX = x,
            OffsetY = y,
            ShadowX = shadowX,
            ShadowY = shadowY,
            Shadow = hasShadow ? (byte)1 : (byte)0,
            Bgra = bgra
        };
    }

    static List<int> LoadIndex(string indexPath, byte nType, int version)
    {
        var list = new List<int>();
        using var stream = File.OpenRead(indexPath);
        using var reader = new BinaryReader(stream);

        switch (nType)
        {
            case 4:
                stream.Seek(24, SeekOrigin.Begin);
                break;
            case 3:
                if (stream.Length >= 28)
                {
                    reader.ReadBytes(26);
                    ushort magic = reader.ReadUInt16();
                    if (magic != 0xB13A)
                        stream.Seek(24, SeekOrigin.Begin);
                }
                break;
            case 2:
            case 5:
                if (stream.Length >= 52)
                    reader.ReadBytes(52);
                break;
            default:
                reader.ReadBytes(version == 0 ? 48 : 52);
                break;
        }

        while (stream.Position <= stream.Length - 4)
            list.Add(reader.ReadInt32());

        return list;
    }

    static byte[] InflateZlib(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    static byte[] InflateDeflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    static int Convert16To32(int color)
    {
        byte red = (byte)((color & 0xf800) >> 8);
        byte green = (byte)((color & 0x07e0) >> 3);
        byte blue = (byte)((color & 0x001f) << 3);
        return (red << 16) | (green << 8) | blue | (255 << 24);
    }

    static int WidthBytes(int nBit, int nWidth) => (((nWidth * nBit) + 31) >> 5) * 4;

    static int[] BuildDefaultPalette()
    {
        var palette = new int[256];
        palette[0] = unchecked((int)0xFF000000);
        for (int i = 1; i < 256; i++)
            palette[i] = unchecked((int)0xFF000000) | (i << 16) | (i << 8) | i;
        return palette;
    }
}
