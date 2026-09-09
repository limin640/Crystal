namespace Crystal.Assets.Atlas;

public sealed class AtlasCatalog
{
    public int Version { get; set; } = 1;
    public int AtlasSize { get; set; }
    public string Compression { get; set; } = "rgba8";
    public string SourceRoot { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<AtlasSheet> Atlases { get; set; } = new();
    public List<AtlasSprite> Sprites { get; set; } = new();
}

public sealed class AtlasSheet
{
    public int Id { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Png { get; set; } = "";
    public string? Bc3 { get; set; }
    public int SpriteCount { get; set; }
}

public sealed class AtlasSprite
{
    public string Library { get; set; } = "";
    public int Index { get; set; }
    public int Atlas { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public bool Blank { get; set; }
}

public sealed class BakeCoverage
{
    public int LibrariesDiscovered { get; set; }
    public int LibrariesParsed { get; set; }
    public int ImagesListed { get; set; }
    public int ImagesDecoded { get; set; }
    public int ImagesPacked { get; set; }
    public int ImagesBlank { get; set; }
    public int CatalogExpected { get; set; }
    public int CatalogPresent { get; set; }
    public double LibraryParsePercent { get; set; }
    public double ImageDecodePercent { get; set; }
    public double ImagePackPercent { get; set; }
    public double CatalogPresentPercent { get; set; }
    public string Note { get; set; } = "";
    public List<string> ParseFailures { get; set; } = new();
    public List<string> MissingCatalogSlots { get; set; } = new();
}
