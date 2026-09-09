namespace Crystal.Assets.Parsers;

public static class LibraryParser
{
    public static LibraryParseResult Parse(DiscoveredLibrary library)
    {
        try
        {
            return library.Kind switch
            {
                LibraryKind.MLib => MLibParser.Parse(library),
                LibraryKind.Wil or LibraryKind.Wzl or LibraryKind.Miz => WilWzlParser.Parse(library),
                LibraryKind.Wtl => WtlParser.Parse(library),
                _ => Failed(library, "Unsupported library kind")
            };
        }
        catch (Exception ex)
        {
            return Failed(library, ex.Message);
        }
    }

    public static LibraryParseResult Failed(DiscoveredLibrary library, string error) => new()
    {
        Path = library.Path,
        RelativePath = library.RelativePath,
        Kind = library.Kind,
        HeaderParsed = false,
        Error = error
    };
}
