namespace Crystal.Assets;

/// <summary>
/// Enumerates every library in a Crystal Data tree. Does not invent files that are not present.
/// Index companions (.Wix/.Wzx/.Mix) are attached to their payload, not listed as their own libraries.
/// </summary>
public static class DataTreeWalker
{
    public static readonly string[] PayloadExtensions =
    {
        ".lib", ".wil", ".wzl", ".wtl", ".miz"
    };

    public static readonly HashSet<string> IndexExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wix", ".wzx", ".mix"
    };

    public static IReadOnlyList<DiscoveredLibrary> Enumerate(string dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot) || !Directory.Exists(dataRoot))
            return Array.Empty<DiscoveredLibrary>();

        string root = Path.GetFullPath(dataRoot);
        var results = new List<DiscoveredLibrary>();

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file);
            if (IndexExtensions.Contains(ext))
                continue;

            LibraryKind kind = KindFromExtension(ext);
            if (kind == LibraryKind.Unknown)
                continue;

            string? index = FindIndex(file, kind);
            results.Add(new DiscoveredLibrary
            {
                Path = file,
                RelativePath = Path.GetRelativePath(root, file).Replace('\\', '/'),
                Kind = kind,
                IndexPath = index
            });
        }

        results.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    public static LibraryKind KindFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".lib" => LibraryKind.MLib,
            ".wil" => LibraryKind.Wil,
            ".wzl" => LibraryKind.Wzl,
            ".wtl" => LibraryKind.Wtl,
            ".miz" => LibraryKind.Miz,
            _ => LibraryKind.Unknown
        };
    }

    static string? FindIndex(string payloadPath, LibraryKind kind)
    {
        string? expected = kind switch
        {
            LibraryKind.Wil => Path.ChangeExtension(payloadPath, ".Wix"),
            LibraryKind.Wzl => Path.ChangeExtension(payloadPath, ".Wzx"),
            LibraryKind.Miz => Path.ChangeExtension(payloadPath, ".Mix"),
            _ => null
        };

        if (expected == null)
            return null;

        if (File.Exists(expected))
            return expected;

        string alt = Path.ChangeExtension(payloadPath, expected.EndsWith(".Wix", StringComparison.OrdinalIgnoreCase) ? ".wix"
            : expected.EndsWith(".Wzx", StringComparison.OrdinalIgnoreCase) ? ".wzx" : ".mix");
        return File.Exists(alt) ? alt : null;
    }
}
