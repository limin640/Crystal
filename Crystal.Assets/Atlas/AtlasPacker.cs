namespace Crystal.Assets.Atlas;

public sealed class AtlasPackerOptions
{
    public int AtlasSize { get; init; } = 2048;
    public int Padding { get; init; } = 1;
    public bool CompressBc3 { get; init; } = true;
    public string OutputDirectory { get; init; } = "bake-out";
}

public static class AtlasPacker
{
    public static AtlasCatalog Pack(string sourceRoot, IReadOnlyList<LibraryParseResult> parsed, AtlasPackerOptions options)
    {
        var packer = new StreamingAtlasPacker(sourceRoot, options);
        foreach (var lib in parsed)
            packer.AddLibrary(lib);
        return packer.Finish();
    }
}
