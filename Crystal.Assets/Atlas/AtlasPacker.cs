using Crystal.Assets.Imaging;

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
        Directory.CreateDirectory(options.OutputDirectory);
        string atlasDir = Path.Combine(options.OutputDirectory, "atlases");
        Directory.CreateDirectory(atlasDir);

        var spritesToPack = new List<(LibraryParseResult Lib, ParsedImage Image)>();
        var catalog = new AtlasCatalog
        {
            AtlasSize = options.AtlasSize,
            Compression = options.CompressBc3 ? "rgba8+bc3" : "rgba8",
            SourceRoot = sourceRoot
        };

        foreach (var lib in parsed.Where(p => p.HeaderParsed))
        {
            foreach (var image in lib.Images)
            {
                if (image.IsBlank || image.Width <= 0 || image.Height <= 0)
                {
                    catalog.Sprites.Add(new AtlasSprite
                    {
                        Library = lib.RelativePath,
                        Index = image.Index,
                        Blank = true,
                        OffsetX = image.OffsetX,
                        OffsetY = image.OffsetY
                    });
                    continue;
                }

                if (image.Bgra == null)
                    continue;

                spritesToPack.Add((lib, image));
            }
        }

        spritesToPack.Sort((a, b) => b.Image.Height.CompareTo(a.Image.Height));

        var sheets = new List<SheetBuilder>();
        var current = new SheetBuilder(0, options.AtlasSize, options.Padding);
        sheets.Add(current);

        foreach (var (lib, image) in spritesToPack)
        {
            if (image.Width > options.AtlasSize || image.Height > options.AtlasSize)
            {
                // Dedicated sheet sized to the image (no scaling, no invented texels).
                int size = NextPow2(Math.Max(image.Width, image.Height));
                var solo = new SheetBuilder(sheets.Count, size, options.Padding);
                if (!solo.TryPlace(lib, image))
                    continue;
                sheets.Add(solo);
                continue;
            }

            if (!current.TryPlace(lib, image))
            {
                current = new SheetBuilder(sheets.Count, options.AtlasSize, options.Padding);
                sheets.Add(current);
                if (!current.TryPlace(lib, image))
                    continue;
            }
        }

        foreach (var sheet in sheets)
        {
            if (sheet.Count == 0)
                continue;

            string pngName = $"atlas_{sheet.Id:D4}.png";
            string pngPath = Path.Combine(atlasDir, pngName);
            PngWriter.WriteRgba(pngPath, sheet.Size, sheet.Size, sheet.Pixels);

            string? bc3Name = null;
            if (options.CompressBc3)
            {
                bc3Name = $"atlas_{sheet.Id:D4}.bc3";
                File.WriteAllBytes(Path.Combine(atlasDir, bc3Name), Bc3Compressor.CompressAtlas(sheet.Size, sheet.Size, sheet.Pixels));
            }

            catalog.Atlases.Add(new AtlasSheet
            {
                Id = sheet.Id,
                Width = sheet.Size,
                Height = sheet.Size,
                Png = $"atlases/{pngName}",
                Bc3 = bc3Name == null ? null : $"atlases/{bc3Name}",
                SpriteCount = sheet.Count
            });
            catalog.Sprites.AddRange(sheet.Sprites);
        }

        return catalog;
    }

    static int NextPow2(int value)
    {
        int p = 1;
        while (p < value) p <<= 1;
        return Math.Max(4, p);
    }

    sealed class SheetBuilder
    {
        public int Id { get; }
        public int Size { get; }
        readonly int _padding;
        public byte[] Pixels { get; }
        public List<AtlasSprite> Sprites { get; } = new();
        public int Count => Sprites.Count;

        int _x, _y, _shelfH;

        public SheetBuilder(int id, int size, int padding)
        {
            Id = id;
            Size = size;
            _padding = padding;
            Pixels = new byte[size * size * 4];
        }

        public bool TryPlace(LibraryParseResult lib, ParsedImage image)
        {
            int w = image.Width + _padding;
            int h = image.Height + _padding;

            if (_x + w > Size)
            {
                _x = 0;
                _y += _shelfH;
                _shelfH = 0;
            }

            if (_y + h > Size)
                return false;

            Blit(image.Bgra!, image.Width, image.Height, _x, _y);
            Sprites.Add(new AtlasSprite
            {
                Library = lib.RelativePath,
                Index = image.Index,
                Atlas = Id,
                X = _x,
                Y = _y,
                Width = image.Width,
                Height = image.Height,
                OffsetX = image.OffsetX,
                OffsetY = image.OffsetY
            });

            _x += w;
            _shelfH = Math.Max(_shelfH, h);
            return true;
        }

        void Blit(byte[] src, int w, int h, int dx, int dy)
        {
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w * 4;
                int dstRow = ((dy + y) * Size + dx) * 4;
                Buffer.BlockCopy(src, srcRow, Pixels, dstRow, w * 4);
            }
        }
    }
}
