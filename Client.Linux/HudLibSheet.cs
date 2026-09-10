using System.Drawing;
using Crystal.Assets;
using Crystal.Assets.Atlas;
using Crystal.Assets.Parsers;
using Crystal.Graphics;

namespace Client.Linux;

/// <summary>
/// Optional HUD <c>.Lib</c> tiles through <see cref="MLibParser"/> or a bake catalog.
/// Missing files are skipped — no invented texels. Operator <c>--data</c> / <c>CRYSTAL_DATA</c>.
/// Title / Prguse2 are WinForms big-map / world-overlay chrome (<c>Index 820</c>, <c>1360/1365/1366</c>).
/// </summary>
internal sealed class HudLibSheet : IDisposable
{
    readonly IRenderer _renderer;
    readonly Dictionary<int, IGpuTexture> _catalogTextures;
    readonly Dictionary<(string Library, int Index), AtlasSprite> _catalogSprites = new(StringPairComparer.Instance);
    readonly Dictionary<string, LibraryParseResult> _parsed = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<(string Library, int Index), IGpuTexture> _libTextures = new(StringPairComparer.Instance);

    public bool MMapOk { get; private set; }
    public bool MagIconOk { get; private set; }
    public bool MagIcon2Ok { get; private set; }
    public bool MapLinkIconOk { get; private set; }
    public bool TitleOk { get; private set; }
    public bool Prguse2Ok { get; private set; }
    public int MMapImages { get; private set; }
    public int MagIconImages { get; private set; }
    public int MagIcon2Images { get; private set; }
    public int MapLinkIconImages { get; private set; }
    public int TitleImages { get; private set; }
    public int Prguse2Images { get; private set; }
    public string? MMapSource { get; private set; }
    public string? MagIconSource { get; private set; }
    public string? MagIcon2Source { get; private set; }
    public string? MapLinkIconSource { get; private set; }
    public string? TitleSource { get; private set; }
    public string? Prguse2Source { get; private set; }
    public int MMapTileDraws { get; private set; }
    public int MagIconTileDraws { get; private set; }
    public int MagIcon2TileDraws { get; private set; }
    public int MapLinkIconTileDraws { get; private set; }
    public int TitleTileDraws { get; private set; }
    public int Prguse2TileDraws { get; private set; }
    public int MMapDrawIndex { get; private set; } = -1;
    public int MagIconDrawIndex { get; private set; } = -1;
    public int MagIcon2DrawIndex { get; private set; } = -1;
    public int MapLinkIconDrawIndex { get; private set; } = -1;
    public int TitleDrawIndex { get; private set; } = -1;
    public int Prguse2DrawIndex { get; private set; } = -1;

    public HudLibSheet(
        IRenderer renderer,
        string? dataRoot,
        IReadOnlyDictionary<int, IGpuTexture>? catalogTextures = null,
        IEnumerable<AtlasSprite>? catalogSprites = null)
    {
        _renderer = renderer;
        _catalogTextures = catalogTextures != null
            ? new Dictionary<int, IGpuTexture>(catalogTextures)
            : new Dictionary<int, IGpuTexture>();

        if (catalogSprites != null)
        {
            foreach (var sprite in catalogSprites)
            {
                if (sprite.Blank || sprite.Width <= 0 || sprite.Height <= 0)
                    continue;
                string lib = Normalize(sprite.Library);
                _catalogSprites[(lib, sprite.Index)] = sprite;
            }
        }

        Bind("MMap.Lib", dataRoot, BindKind.MMap);
        Bind("MagIcon.Lib", dataRoot, BindKind.MagIcon);
        Bind("MagIcon2.Lib", dataRoot, BindKind.MagIcon2);
        Bind("MapLinkIcon.Lib", dataRoot, BindKind.MapLinkIcon);
        Bind("Title.Lib", dataRoot, BindKind.Title);
        Bind("Prguse2.Lib", dataRoot, BindKind.Prguse2);

        Console.WriteLine($"hud-lib ready MMapOk={MMapOk} MagIconOk={MagIconOk} MagIcon2Ok={MagIcon2Ok} MapLinkIconOk={MapLinkIconOk} TitleOk={TitleOk} Prguse2Ok={Prguse2Ok} images={MMapImages}/{MagIconImages}/{MagIcon2Images}/{MapLinkIconImages}/{TitleImages}/{Prguse2Images}");
    }

    public bool TryDrawMMap(int preferredIndex, int x, int y, int w, int h)
    {
        if (!MMapOk) return false;
        if (TryDraw("MMap.Lib", preferredIndex, x, y, w, h))
        {
            MMapTileDraws++;
            MMapDrawIndex = preferredIndex;
            return true;
        }

        int first = FirstDecoded("MMap.Lib");
        if (first >= 0 && TryDraw("MMap.Lib", first, x, y, w, h))
        {
            MMapTileDraws++;
            MMapDrawIndex = first;
            return true;
        }

        return false;
    }

    public bool TryDrawMagIcon(int preferredIndex, int x, int y, int w, int h)
    {
        if (!MagIconOk) return false;
        if (!TryDrawPreferred("MagIcon.Lib", preferredIndex, x, y, w, h, out int used))
            return false;
        MagIconTileDraws++;
        MagIconDrawIndex = used;
        return true;
    }

    public bool TryDrawMagIcon2(int preferredIndex, int x, int y, int w, int h)
    {
        if (!MagIcon2Ok) return false;
        if (!TryDrawPreferred("MagIcon2.Lib", preferredIndex, x, y, w, h, out int used))
            return false;
        MagIcon2TileDraws++;
        MagIcon2DrawIndex = used;
        return true;
    }

    public bool TryDrawMapLinkIcon(int preferredIndex, int x, int y, int w, int h)
    {
        if (!MapLinkIconOk) return false;
        if (!TryDrawPreferred("MapLinkIcon.Lib", preferredIndex, x, y, w, h, out int used))
            return false;
        MapLinkIconTileDraws++;
        MapLinkIconDrawIndex = used;
        return true;
    }

    public bool TryDrawTitle(int preferredIndex, int x, int y, int w, int h)
    {
        if (!TitleOk) return false;
        if (!TryDrawPreferred("Title.Lib", preferredIndex, x, y, w, h, out int used))
            return false;
        TitleTileDraws++;
        TitleDrawIndex = used;
        return true;
    }

    public bool TryDrawPrguse2(int preferredIndex, int x, int y, int w, int h)
    {
        if (!Prguse2Ok) return false;
        if (!TryDrawPreferred("Prguse2.Lib", preferredIndex, x, y, w, h, out int used))
            return false;
        Prguse2TileDraws++;
        Prguse2DrawIndex = used;
        return true;
    }

    bool TryDrawPreferred(string lib, int preferredIndex, int x, int y, int w, int h, out int used)
    {
        used = -1;
        if (TryDraw(lib, preferredIndex, x, y, w, h))
        {
            used = preferredIndex;
            return true;
        }

        int first = FirstDecoded(lib);
        if (first >= 0 && TryDraw(lib, first, x, y, w, h))
        {
            used = first;
            return true;
        }

        return false;
    }

    void Bind(string fileName, string? dataRoot, BindKind kind)
    {
        bool fromCatalog = HasCatalog(fileName);
        if (fromCatalog)
        {
            int n = _catalogSprites.Keys.Count(k => string.Equals(k.Library, fileName, StringComparison.OrdinalIgnoreCase));
            Mark(fileName, "catalog", n, kind);
        }

        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            if (!fromCatalog)
                Console.WriteLine($"hud-lib skip {fileName}: missing (no --data / catalog sprite)");
            return;
        }

        string? path = ResolveOnDisk(dataRoot, fileName);
        if (path == null)
        {
            if (!fromCatalog)
                Console.WriteLine($"hud-lib skip {fileName}: missing under --data");
            return;
        }

        var parsed = LibraryParser.Parse(new DiscoveredLibrary
        {
            Path = path,
            RelativePath = fileName,
            Kind = LibraryKind.MLib
        });
        if (!parsed.HeaderParsed)
        {
            Console.WriteLine($"hud-lib skip {fileName}: {parsed.Error ?? "header failed"}");
            return;
        }

        _parsed[fileName] = parsed;
        Mark(fileName, path, parsed.DecodedCount, kind);
    }

    void Mark(string fileName, string source, int images, BindKind kind)
    {
        bool ok = images > 0;
        switch (kind)
        {
            case BindKind.MMap:
                MMapOk = ok;
                MMapImages = images;
                MMapSource = source;
                break;
            case BindKind.MagIcon:
                MagIconOk = ok;
                MagIconImages = images;
                MagIconSource = source;
                break;
            case BindKind.MagIcon2:
                MagIcon2Ok = ok;
                MagIcon2Images = images;
                MagIcon2Source = source;
                break;
            case BindKind.MapLinkIcon:
                MapLinkIconOk = ok;
                MapLinkIconImages = images;
                MapLinkIconSource = source;
                break;
            case BindKind.Title:
                TitleOk = ok;
                TitleImages = images;
                TitleSource = source;
                break;
            case BindKind.Prguse2:
                Prguse2Ok = ok;
                Prguse2Images = images;
                Prguse2Source = source;
                break;
        }

        Console.WriteLine($"hud-lib {kind} file={fileName} ok={ok} images={images} src={source}");
    }

    enum BindKind { MMap, MagIcon, MagIcon2, MapLinkIcon, Title, Prguse2 }

    bool TryDraw(string library, int index, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0 || index < 0)
            return false;

        if (_catalogSprites.TryGetValue((library, index), out var sprite)
            && _catalogTextures.TryGetValue(sprite.Atlas, out var atlas))
        {
            var src = new Rectangle(sprite.X, sprite.Y, sprite.Width, sprite.Height);
            _renderer.DrawQuad(atlas, src, x, y, w, h, Color.White);
            return true;
        }

        var key = (library, index);
        if (!_libTextures.TryGetValue(key, out var tex))
        {
            if (!_parsed.TryGetValue(library, out var parsed))
                return false;
            var image = parsed.Images.FirstOrDefault(i => i.Index == index && i.Bgra is { Length: > 0 } && i.Width > 0);
            if (image?.Bgra == null)
                return false;
            tex = _renderer.CreateTexture(image.Width, image.Height, image.Bgra);
            _libTextures[key] = tex;
        }

        _renderer.DrawQuad(tex, null, x, y, w, h, Color.White);
        return true;
    }

    int FirstDecoded(string library)
    {
        foreach (var kv in _catalogSprites)
        {
            if (string.Equals(kv.Key.Library, library, StringComparison.OrdinalIgnoreCase))
                return kv.Key.Index;
        }

        if (_parsed.TryGetValue(library, out var parsed))
        {
            var img = parsed.Images.FirstOrDefault(i => i.Decoded && i.Width > 0);
            if (img != null)
                return img.Index;
        }

        return -1;
    }

    bool HasCatalog(string library)
        => _catalogSprites.Keys.Any(k => string.Equals(k.Library, library, StringComparison.OrdinalIgnoreCase));

    static string? ResolveOnDisk(string dataRoot, string fileName)
        => DataPath.ResolveFile(dataRoot, fileName);

    static string Normalize(string library) => library.Replace('\\', '/');

    public void Dispose()
    {
        foreach (var tex in _libTextures.Values)
            tex.Dispose();
        _libTextures.Clear();
    }

    sealed class StringPairComparer : IEqualityComparer<(string Library, int Index)>
    {
        public static readonly StringPairComparer Instance = new();
        public bool Equals((string Library, int Index) x, (string Library, int Index) y)
            => x.Index == y.Index && string.Equals(x.Library, y.Library, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Library, int Index) obj)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Library), obj.Index);
    }
}
