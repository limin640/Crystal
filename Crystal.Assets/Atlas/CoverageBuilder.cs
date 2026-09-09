namespace Crystal.Assets.Atlas;

public static class CoverageBuilder
{
    public static BakeCoverage Build(
        string dataRoot,
        IReadOnlyList<DiscoveredLibrary> discovered,
        IReadOnlyList<LibraryParseResult> parsed,
        AtlasCatalog catalog)
    {
        var present = new HashSet<string>(discovered.Select(d => d.RelativePath), StringComparer.OrdinalIgnoreCase);
        var expected = CrystalCatalog.ExpectedRelativePaths();
        var missing = expected.Where(e => !present.Contains(e)).ToList();

        // Numbered folders: count files that exist; do not invent missing indices.
        foreach (var (folder, _, _) in CrystalCatalog.NumberedFolders)
        {
            string dir = Path.Combine(dataRoot, folder);
            if (!Directory.Exists(dir))
                continue;
            foreach (string file in Directory.EnumerateFiles(dir, "*.Lib"))
                present.Add(Path.GetRelativePath(dataRoot, file).Replace('\\', '/'));
        }

        int listed = parsed.Sum(p => p.HeaderParsed ? Math.Max(p.ImageCount, p.Images.Count) : 0);
        int decoded = parsed.Sum(p => p.DecodedCount);
        int blank = parsed.SelectMany(p => p.Images).Count(i => i.IsBlank);
        int packed = catalog.Sprites.Count(s => !s.Blank && s.Width > 0 && s.Height > 0);
        int libParsed = parsed.Count(p => p.HeaderParsed);

        return new BakeCoverage
        {
            LibrariesDiscovered = discovered.Count,
            LibrariesParsed = libParsed,
            ImagesListed = listed,
            ImagesDecoded = decoded,
            ImagesPacked = packed,
            ImagesBlank = blank,
            CatalogExpected = expected.Count,
            CatalogPresent = expected.Count(e => present.Contains(e)),
            LibraryParsePercent = Percent(libParsed, discovered.Count),
            ImageDecodePercent = Percent(decoded, listed),
            ImagePackPercent = Percent(packed, decoded),
            CatalogPresentPercent = Percent(expected.Count(e => present.Contains(e)), expected.Count),
            Note = "Coverage is measured against files that exist. Missing Crystal catalog slots are reported, not synthesized.",
            ParseFailures = parsed.Where(p => !p.HeaderParsed).Select(p => $"{p.RelativePath}: {p.Error}").ToList(),
            MissingCatalogSlots = missing
        };
    }

    static double Percent(int num, int den) => den == 0 ? 0 : Math.Round(100.0 * num / den, 2);
}
