using System.IO.Compression;

namespace Crystal.Assets.Parsers;

/// <summary>
/// Crystal Client <c>MLibrary</c> (.Lib) v2/v3 gzip-BGRA, plus v0/v1 header listing.
/// Matches <c>Client/MirGraphics/MLibrary.cs</c> and LibraryEditor V0/V2.
/// </summary>
public static class MLibParser
{
    public static LibraryParseResult Parse(DiscoveredLibrary library)
    {
        using var stream = File.OpenRead(library.Path);
        using var reader = new BinaryReader(stream);

        if (stream.Length < 8)
            return LibraryParser.Failed(library, "File too small to be an MLibrary");

        int first = reader.ReadInt32();

        // Client MLibrary v2/v3 starts with version (>= 2). V0 starts with Count.
        bool looksLikeVersioned = first >= 2 && first <= 8;
        if (looksLikeVersioned)
            return ParseVersioned(library, reader, first);

        return ParseV0(library, reader, first);
    }

    static LibraryParseResult ParseVersioned(DiscoveredLibrary library, BinaryReader reader, int version)
    {
        if (version < 2)
            return LibraryParser.Failed(library, $"Unsupported Lib version {version}");

        int count = reader.ReadInt32();
        if (count < 0 || count > 2_000_000)
            return LibraryParser.Failed(library, $"Implausible image count {count}");

        int frameSeek = 0;
        if (version >= 3)
            frameSeek = reader.ReadInt32();

        var indices = new int[count];
        for (int i = 0; i < count; i++)
            indices[i] = reader.ReadInt32();

        var images = new List<ParsedImage>(count);
        Stream stream = reader.BaseStream;

        for (int i = 0; i < count; i++)
        {
            int offset = indices[i];
            if (offset <= 0 || offset + 17 > stream.Length)
            {
                images.Add(new ParsedImage { Index = i, IsBlank = true });
                continue;
            }

            stream.Position = offset;
            short width = reader.ReadInt16();
            short height = reader.ReadInt16();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            short sx = reader.ReadInt16();
            short sy = reader.ReadInt16();
            byte shadow = reader.ReadByte();
            int length = reader.ReadInt32();
            bool hasMask = (shadow >> 7) == 1;

            if (width <= 0 || height <= 0 || length <= 0)
            {
                images.Add(new ParsedImage
                {
                    Index = i, Width = width, Height = height, OffsetX = x, OffsetY = y,
                    ShadowX = sx, ShadowY = sy, Shadow = shadow, HasMask = hasMask, IsBlank = true
                });
                continue;
            }

            byte[]? bgra = null;
            try
            {
                byte[] compressed = reader.ReadBytes(length);
                bgra = InflateGzip(compressed);
                int expected = width * height * 4;
                if (bgra.Length < expected)
                    bgra = null;
                else if (bgra.Length > expected)
                    Array.Resize(ref bgra, expected);
            }
            catch
            {
                bgra = null;
            }

            images.Add(new ParsedImage
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
                Bgra = bgra
            });
        }

        return new LibraryParseResult
        {
            Path = library.Path,
            RelativePath = library.RelativePath,
            Kind = LibraryKind.MLib,
            HeaderParsed = true,
            Version = version,
            ImageCount = count,
            Images = images
        };
    }

    static LibraryParseResult ParseV0(DiscoveredLibrary library, BinaryReader reader, int count)
    {
        if (count < 0 || count > 2_000_000)
            return LibraryParser.Failed(library, $"Implausible V0 image count {count}");

        var indices = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                return LibraryParser.Failed(library, "Truncated V0 index table");
            indices[i] = reader.ReadInt32();
        }

        var images = new List<ParsedImage>(count);
        for (int i = 0; i < count; i++)
        {
            int offset = indices[i];
            if (offset <= 0 || offset + 16 > reader.BaseStream.Length)
            {
                images.Add(new ParsedImage { Index = i, IsBlank = true });
                continue;
            }

            reader.BaseStream.Position = offset;
            short width = reader.ReadInt16();
            short height = reader.ReadInt16();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            images.Add(new ParsedImage
            {
                Index = i,
                Width = width,
                Height = height,
                OffsetX = x,
                OffsetY = y,
                IsBlank = width <= 0 || height <= 0
            });
        }

        return new LibraryParseResult
        {
            Path = library.Path,
            RelativePath = library.RelativePath,
            Kind = LibraryKind.MLib,
            HeaderParsed = true,
            Version = 0,
            ImageCount = count,
            Images = images
        };
    }

    public static byte[] InflateGzip(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    public static byte[] DeflateGzip(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(raw, 0, raw.Length);
        return output.ToArray();
    }
}
