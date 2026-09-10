namespace Crystal.Assets.Parsers;

public static class LibraryParser
{
    public static LibraryParseResult Parse(DiscoveredLibrary library, Action<ParsedImage>? onImage = null)
    {
        try
        {
            return library.Kind switch
            {
                LibraryKind.MLib => MLibParser.Parse(library, onImage),
                LibraryKind.Wil or LibraryKind.Wzl or LibraryKind.Miz => WilWzlParser.Parse(library, onImage),
                LibraryKind.Wtl => WtlParser.Parse(library, onImage),
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
