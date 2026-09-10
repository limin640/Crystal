using Crystal.Assets.Imaging;

namespace Crystal.Assets.Atlas;

/// <summary>
/// Packs decoded images as they arrive and flushes finished sheets to disk.
/// A ~7GB Data tree must not retain every library's BGRA at once.
/// </summary>
public sealed class StreamingAtlasPacker
{
    readonly AtlasPackerOptions _options;
    readonly AtlasCatalog _catalog;
    readonly string _atlasDir;
    SheetBuilder? _current;
    int _nextId;

    public int Packed { get; private set; }
    public int SheetsFlushed { get; private set; }
    public int BlankRecorded { get; private set; }

    public StreamingAtlasPacker(string sourceRoot, AtlasPackerOptions options)
    {
        _options = options;
        Directory.CreateDirectory(options.OutputDirectory);
        _atlasDir = Path.Combine(options.OutputDirectory, "atlases");
        Directory.CreateDirectory(_atlasDir);
        _catalog = new AtlasCatalog
        {
            AtlasSize = options.AtlasSize,
            Compression = options.CompressBc3 ? "rgba8+bc3" : "rgba8",
            SourceRoot = sourceRoot
        };
    }

    public void Accept(string relativePath, ParsedImage image)
    {
        if (image.IsBlank || image.Width <= 0 || image.Height <= 0)
        {
            _catalog.Sprites.Add(new AtlasSprite
            {
                Library = relativePath,
                Index = image.Index,
                Blank = true,
                OffsetX = image.OffsetX,
                OffsetY = image.OffsetY
            });
            BlankRecorded++;
            image.ReleasePixels();
            return;
        }

        if (image.Bgra == null)
        {
            image.ReleasePixels();
            return;
        }

        Place(relativePath, image);
        image.ReleasePixels();
    }

    public void AddLibrary(LibraryParseResult lib)
    {
        foreach (var image in lib.Images)
            Accept(lib.RelativePath, image);
    }

    public AtlasCatalog Finish()
    {
        FlushCurrent();
        return _catalog;
    }

    void Place(string relativePath, ParsedImage image)
    {
        if (image.Width > _options.AtlasSize || image.Height > _options.AtlasSize)
        {
            int size = NextPow2(Math.Max(image.Width, image.Height));
            var solo = new SheetBuilder(_nextId++, size, _options.Padding);
            if (solo.TryPlace(relativePath, image))
            {
                Flush(solo);
                Packed++;
            }
            return;
        }

        _current ??= new SheetBuilder(_nextId++, _options.AtlasSize, _options.Padding);
        if (!_current.TryPlace(relativePath, image))
        {
            FlushCurrent();
            _current = new SheetBuilder(_nextId++, _options.AtlasSize, _options.Padding);
            if (!_current.TryPlace(relativePath, image))
                return;
        }
        Packed++;
    }

    void FlushCurrent()
    {
        if (_current == null || _current.Count == 0)
        {
            _current = null;
            return;
        }
        Flush(_current);
        _current = null;
    }

    void Flush(SheetBuilder sheet)
    {
        string pngName = $"atlas_{sheet.Id:D4}.png";
        string pngPath = Path.Combine(_atlasDir, pngName);
        PngWriter.WriteRgba(pngPath, sheet.Size, sheet.Size, sheet.Pixels);

        string? bc3Name = null;
        if (_options.CompressBc3)
        {
            bc3Name = $"atlas_{sheet.Id:D4}.bc3";
            File.WriteAllBytes(Path.Combine(_atlasDir, bc3Name), Bc3Compressor.CompressAtlas(sheet.Size, sheet.Size, sheet.Pixels));
        }

        _catalog.Atlases.Add(new AtlasSheet
        {
            Id = sheet.Id,
            Width = sheet.Size,
            Height = sheet.Size,
            Png = $"atlases/{pngName}",
            Bc3 = bc3Name == null ? null : $"atlases/{bc3Name}",
            SpriteCount = sheet.Count
        });
        _catalog.Sprites.AddRange(sheet.Sprites);
        SheetsFlushed++;
        sheet.Release();
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
        public byte[] Pixels { get; private set; }
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

        public bool TryPlace(string relativePath, ParsedImage image)
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
                Library = relativePath,
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

        public void Release() => Pixels = Array.Empty<byte>();

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
