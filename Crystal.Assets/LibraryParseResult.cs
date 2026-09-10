namespace Crystal.Assets;

public sealed class LibraryParseResult
{
    public required string Path { get; init; }
    public required string RelativePath { get; init; }
    public required LibraryKind Kind { get; init; }
    public bool HeaderParsed { get; init; }
    public int Version { get; init; }
    public int ImageCount { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<ParsedImage> Images { get; init; } = Array.Empty<ParsedImage>();

    public int DecodedCount => Images.Count(i => i.Decoded);
    public int ListedCount => HeaderParsed ? (Images.Count > 0 ? Images.Count : ImageCount) : 0;
}
