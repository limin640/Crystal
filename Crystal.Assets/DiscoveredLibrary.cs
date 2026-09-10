namespace Crystal.Assets;

/// <summary>
/// A logical library on disk. Paired index files (Wix/Wzx/Mix) are not counted separately.
/// </summary>
public sealed class DiscoveredLibrary
{
    public required string Path { get; init; }
    public required string RelativePath { get; init; }
    public required LibraryKind Kind { get; init; }
    public string? IndexPath { get; init; }
}
