namespace Crystal.Assets.Parsers;

/// <summary>
/// WTL header + per-image metadata. Pixel decode for ILIB DXT blocks is a later increment;
/// this phase records every image slot so coverage is measured, not guessed.
/// </summary>
public static class WtlParser
{
    public static LibraryParseResult Parse(DiscoveredLibrary library)
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

            images.Add(new ParsedImage
            {
                Index = i,
                Width = width,
                Height = height,
                OffsetX = x,
                OffsetY = y,
                ShadowX = sx,
                ShadowY = sy,
                IsBlank = width <= 0 || height <= 0
            });
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
}
