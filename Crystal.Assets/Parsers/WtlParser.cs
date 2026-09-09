using System.IO.Compression;
using Crystal.Assets.Imaging;

namespace Crystal.Assets.Parsers;

/// <summary>
/// WTL header + pixel decode. v1 is RLE + 8-byte DXT-like blocks (LibraryEditor WTLLibrary).
/// v2 (<c>ILIB v2.0-WEMADE</c>) is zlib then DXT1/3/5. Missing/unknown blocks stay undecoded.
/// </summary>
public static class WtlParser
{
    public static LibraryParseResult Parse(DiscoveredLibrary library, Action<ParsedImage>? onImage = null)
    {
        using var stream = File.OpenRead(library.Path);
        using var reader = new BinaryReader(stream);

        if (stream.Length < 32)
            return LibraryParser.Failed(library, "WTL too small");

        stream.Seek(2, SeekOrigin.Begin);
        string versionText = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(20)).TrimEnd('\0');
        bool isNew = versionText == "ILIB v2.0-WEMADE";

        stream.Seek(28, SeekOrigin.Begin);
        int count = reader.ReadInt32();
        if (count < 0 || count > 2_000_000)
            return LibraryParser.Failed(library, $"Implausible WTL count {count}");

        if (isNew)
            stream.Seek(stream.Length - count * 4L, SeekOrigin.Begin);

        var indices = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (stream.Position + 4 > stream.Length)
                return LibraryParser.Failed(library, "Truncated WTL index table");
            indices[i] = reader.ReadInt32();
        }

        var images = new List<ParsedImage>(count);
        for (int i = 0; i < count; i++)
        {
            int offset = indices[i];
            if (offset <= 0 || offset + 16 > stream.Length)
            {
                var blank = new ParsedImage { Index = i, IsBlank = true };
                images.Add(blank);
                onImage?.Invoke(blank);
                continue;
            }

            stream.Position = offset;
            short width = reader.ReadInt16();
            short height = reader.ReadInt16();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            short sx = reader.ReadInt16();
            short sy = reader.ReadInt16();

            byte textureType = 0;
            int length;
            byte shadow = 0;
            bool hasMask;

            if (isNew)
            {
                reader.ReadByte();
                textureType = reader.ReadByte();
                reader.ReadByte();
                byte maskType = reader.ReadByte();
                hasMask = maskType > 0;
                length = reader.ReadInt32();
                if (length % 4 > 0)
                    length += 4 - length % 4;
            }
            else
            {
                if (stream.Position + 4 > stream.Length)
                {
                    var headerOnly = new ParsedImage
                    {
                        Index = i, Width = width, Height = height, OffsetX = x, OffsetY = y,
                        ShadowX = sx, ShadowY = sy, IsBlank = width <= 0 || height <= 0
                    };
                    images.Add(headerOnly);
                    onImage?.Invoke(headerOnly);
                    continue;
                }

                length = reader.ReadByte() | reader.ReadByte() << 8 | reader.ReadByte() << 16;
                shadow = reader.ReadByte();
                hasMask = (shadow >> 7) == 1;
            }

            if (width <= 0 || height <= 0)
            {
                var blank = new ParsedImage
                {
                    Index = i, Width = width, Height = height, OffsetX = x, OffsetY = y,
                    ShadowX = sx, ShadowY = sy, Shadow = shadow, HasMask = hasMask, IsBlank = true
                };
                images.Add(blank);
                onImage?.Invoke(blank);
                continue;
            }

            byte[]? bgra = null;
            if (length > 0 && stream.Position + length <= stream.Length)
            {
                try
                {
                    bgra = isNew
                        ? DecompressV2(reader, length, width, height, textureType)
                        : DecompressV1(reader, length, width, height);
                }
                catch
                {
                    bgra = null;
                }
            }

            var image = new ParsedImage
            {
                Index = i,
                Width = width,
                Height = height,
                OffsetX = x,
                OffsetY = y,
                ShadowX = sx,
                ShadowY = sy,
                Shadow = shadow,
                HasMask = hasMask,
                IsBlank = false,
                Bgra = bgra,
                Decoded = bgra is { Length: > 0 }
            };
            images.Add(image);
            onImage?.Invoke(image);
        }

        return new LibraryParseResult
        {
            Path = library.Path,
            RelativePath = library.RelativePath,
            Kind = LibraryKind.Wtl,
            HeaderParsed = true,
            Version = isNew ? 2 : 1,
            ImageCount = count,
            Images = images
        };
    }

    static byte[]? DecompressV1(BinaryReader reader, int imageLength, short width, short height)
    {
        const int size = 8;
        int offset = 0, blockOffSet = 0;
        var countList = new byte[8];
        int tWidth = 2;
        while (tWidth < width)
            tWidth *= 2;

        int outputHeight = height + (4 - height % 4) % 4;
        int outputWidth = width + (4 - width % 4) % 4;
        byte[] bytes = reader.ReadBytes(imageLength);
        if (bytes.Length != imageLength)
            return null;

        var pixels = new byte[outputWidth * outputHeight * 4];
        int cap = pixels.Length;
        int currentx = 0;
        Span<byte> newPixels = stackalloc byte[64];

        while (blockOffSet < imageLength)
        {
            if (blockOffSet + 8 > imageLength)
                break;
            for (int i = 0; i < 8; i++)
                countList[i] = bytes[blockOffSet++];

            for (int i = 0; i < 8; i++)
            {
                int count = countList[i];
                if (i % 2 == 0)
                {
                    if (currentx >= tWidth)
                        currentx -= tWidth;

                    for (int off = 0; off < count; off++)
                    {
                        if (currentx < outputWidth)
                            offset++;
                        currentx += 4;
                        if (currentx >= tWidth)
                            currentx -= tWidth;
                    }
                    continue;
                }

                for (int c = 0; c < count; c++)
                {
                    if (blockOffSet + size > bytes.Length)
                        break;

                    DxtDecoder.DecompressWtlV1Block(bytes.AsSpan(blockOffSet, size), newPixels);
                    blockOffSet += size;

                    int pixelOffSet = 0;
                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int blockx = outputWidth == 0 ? 0 : offset % (outputWidth / 4);
                            int blocky = outputWidth == 0 ? 0 : offset / (outputWidth / 4);
                            int dx = blockx * 4 + px;
                            int dy = blocky * 4 + py;
                            int destPixel = (dy * outputWidth + dx) * 4;
                            if (destPixel + 4 > cap)
                                break;
                            pixels[destPixel] = newPixels[pixelOffSet];
                            pixels[destPixel + 1] = newPixels[pixelOffSet + 1];
                            pixels[destPixel + 2] = newPixels[pixelOffSet + 2];
                            pixels[destPixel + 3] = newPixels[pixelOffSet + 3];
                            pixelOffSet += 4;
                        }
                    }
                    offset++;
                    if (currentx >= outputWidth)
                        currentx -= outputWidth;
                    currentx += 4;
                }
            }
        }

        return DxtDecoder.Crop(pixels, outputWidth, outputHeight, width, height);
    }

    static byte[]? DecompressV2(BinaryReader reader, int imageLength, short width, short height, byte textureType)
    {
        byte[] buffer = reader.ReadBytes(imageLength);
        int w = width + (4 - width % 4) % 4;
        int a = 1;
        while (true)
        {
            a *= 2;
            if (a >= w)
            {
                w = a;
                break;
            }
        }
        int h = height + (4 - height % 4) % 4;

        DxtFormat format = textureType switch
        {
            0 or 1 => DxtFormat.Dxt1,
            3 => DxtFormat.Dxt3,
            5 => DxtFormat.Dxt5,
            _ => throw new NotSupportedException($"WTL v2 texture type {textureType}")
        };

        byte[] blocks = InflateFlexible(buffer);
        byte[] padded = DxtDecoder.Decompress(blocks, w, h, format);
        return DxtDecoder.Crop(padded, w, h, width, height);
    }

    static byte[] InflateFlexible(byte[] compressed)
    {
        try
        {
            using var input = new MemoryStream(compressed);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }
    }
}
