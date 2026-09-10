using System.Drawing;
using Crystal.Assets;
using Crystal.Assets.Atlas;
using Crystal.Assets.Parsers;
using Crystal.Graphics;

namespace Client.Linux;

/// <summary>
/// Optional MMap.Lib / MagIcon.Lib tiles through <see cref="MLibParser"/> or a bake catalog.
/// Missing files are skipped — no invented texels. Operator <c>--data</c> / <c>CRYSTAL_DATA</c>.
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
    public int MMapImages { get; private set; }
    public int MagIconImages { get; private set; }
    public string? MMapSource { get; private set; }
    public string? MagIconSource { get; private set; }
    public int MMapTileDraws { get; private set; }
    public int MagIconTileDraws { get; private set; }
    public int MMapDrawIndex { get; private set; } = -1;
    public int MagIconDrawIndex { get; private set; } = -1;

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

        Bind("MMap.Lib", dataRoot, mmap: true);
        Bind("MagIcon.Lib", dataRoot, mag: true);
        if (!MagIconOk)
            Bind("MagIcon2.Lib", dataRoot, mag: true);

        Console.WriteLine($"hud-lib ready MMapOk={MMapOk} MagIconOk={MagIconOk} images={MMapImages}/{MagIconImages}");
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
        string lib = _parsed.ContainsKey("MagIcon.Lib") || HasCatalog("MagIcon.Lib")
            ? "MagIcon.Lib"
            : "MagIcon2.Lib";
        if (TryDraw(lib, preferredIndex, x, y, w, h))
        {
            MagIconTileDraws++;
            MagIconDrawIndex = preferredIndex;
            return true;
        }

        int first = FirstDecoded(lib);
        if (first >= 0 && TryDraw(lib, first, x, y, w, h))
        {
            MagIconTileDraws++;
            MagIconDrawIndex = first;
            return true;
        }

        return false;
    }

    void Bind(string fileName, string? dataRoot, bool mmap = false, bool mag = false)
    {
        bool fromCatalog = HasCatalog(fileName);
        if (fromCatalog)
        {
            int n = _catalogSprites.Keys.Count(k => string.Equals(k.Library, fileName, StringComparison.OrdinalIgnoreCase));
            Mark(fileName, "catalog", n, mmap, mag);
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
        Mark(fileName, path, parsed.DecodedCount, mmap, mag);
    }

    void Mark(string fileName, string source, int images, bool mmap, bool mag)
    {
        if (mmap)
        {
            MMapOk = images > 0;
            MMapImages = images;
            MMapSource = source;
        }

        if (mag)
        {
            MagIconOk = images > 0;
            MagIconImages = images;
            MagIconSource = source;
        }

        Console.WriteLine($"hud-lib {(mmap ? "MMap" : "MagIcon")} file={fileName} ok={(mmap ? MMapOk : MagIconOk)} images={images} src={source}");
    }

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
