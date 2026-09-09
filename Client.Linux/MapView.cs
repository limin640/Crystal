using System.Drawing;
using Crystal.Assets;
using Crystal.Assets.Atlas;
using Crystal.Assets.Maps;
using Crystal.Assets.Parsers;
using Crystal.Graphics;

namespace Client.Linux;

/// <summary>
/// Low-fidelity GameScene floor/object draw: Crystal <c>MapReader</c> cells
/// resolved through a bake catalog and/or existing <c>.Lib</c> via <see cref="IRenderer"/>.
/// Missing slots are skipped — no invented texels.
/// </summary>
internal sealed class MapView : IDisposable
{
    public const int CellWidth = 48;
    public const int CellHeight = 32;

    readonly IRenderer _renderer;
    readonly Dictionary<int, IGpuTexture> _atlases;
    readonly Dictionary<(string Library, int Index), AtlasSprite> _sprites = new(StringPairComparer.Instance);
    readonly Dictionary<string, List<AtlasSprite>> _byLibrary = new(StringComparer.OrdinalIgnoreCase);
    readonly string? _dataRoot;
    readonly Dictionary<(string Library, int Index), IGpuTexture> _libTextures = new();
    readonly Dictionary<string, LibraryParseResult> _parsedLibs = new(StringComparer.OrdinalIgnoreCase);
    AtlasSprite? _fallbackSprite;

    public int FloorDraws { get; private set; }
    public int ObjectDraws { get; private set; }
    public int SkippedCells { get; private set; }
    public int LowFiRemaps { get; private set; }
    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }
    public bool MapLoaded { get; private set; }
    public string? MapPath { get; private set; }
    public MapCell[,]? Cells { get; private set; }

    public MapView(IRenderer renderer, Dictionary<int, IGpuTexture> atlases, IEnumerable<AtlasSprite> sprites, string? dataRoot)
    {
        _renderer = renderer;
        _atlases = atlases;
        _dataRoot = dataRoot;

        foreach (var sprite in sprites)
        {
            if (sprite.Blank || sprite.Width <= 0 || sprite.Height <= 0)
                continue;
            string lib = Normalize(sprite.Library);
            _sprites[(lib, sprite.Index)] = sprite;
            if (!_byLibrary.TryGetValue(lib, out var list))
            {
                list = new List<AtlasSprite>();
                _byLibrary[lib] = list;
            }
            list.Add(sprite);
            _fallbackSprite ??= sprite;
        }
    }

    public bool LoadMap(string mapsRoot, string fileName)
    {
        string stem = fileName.EndsWith(".map", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^4]
            : fileName;
        string path = Path.Combine(mapsRoot, stem + ".map");
        if (!File.Exists(path))
        {
            // Some Jev maps live in subfolders or use the raw FileName.
            path = Directory.Exists(mapsRoot)
                ? Directory.EnumerateFiles(mapsRoot, stem + ".map", SearchOption.AllDirectories).FirstOrDefault()
                  ?? path
                : path;
        }

        MapPath = path;
        if (!File.Exists(path))
        {
            Console.WriteLine($"Map file missing: {path}");
            return false;
        }

        var reader = new MapReader(path);
        Cells = reader.Cells;
        MapWidth = reader.Width;
        MapHeight = reader.Height;
        MapLoaded = reader.LoadedFromFile && MapWidth > 0 && MapHeight > 0;
        Console.WriteLine($"Map loaded {path} {MapWidth}x{MapHeight} fromFile={reader.LoadedFromFile}");
        return MapLoaded;
    }

    public void Draw(int viewW, int viewH, Point origin, IReadOnlyList<WorldObject> objects)
    {
        FloorDraws = 0;
        ObjectDraws = 0;
        SkippedCells = 0;
        LowFiRemaps = 0;

        int cellsX = Math.Max(4, viewW / CellWidth);
        int cellsY = Math.Max(4, viewH / CellHeight);
        int startX = Math.Max(0, origin.X - cellsX / 2);
        int startY = Math.Max(0, origin.Y - cellsY / 2);
        int endX = Cells == null ? startX : Math.Min(MapWidth - 1, startX + cellsX);
        int endY = Cells == null ? startY : Math.Min(MapHeight - 1, startY + cellsY);

        if (Cells != null)
        {
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    var cell = Cells[x, y];
                    float dx = (x - startX) * CellWidth;
                    float dy = (y - startY) * CellHeight;

                    bool drew = false;
                    if (y % 2 == 0 && x % 2 == 0 && cell.BackImage != 0 && cell.BackIndex != -1)
                    {
                        int index = (cell.BackImage & 0x1FFFFFFF) - 1;
                        drew |= TryDrawSprite(cell.BackIndex, index, dx, dy, CellWidth * 2, CellHeight * 2);
                    }

                    int mid = cell.MiddleImage - 1;
                    if (mid >= 0 && cell.MiddleIndex != -1)
                        drew |= TryDrawSprite(cell.MiddleIndex, mid, dx, dy, CellWidth, CellHeight);

                    int front = (cell.FrontImage & 0x7FFF) - 1;
                    if (front >= 0 && cell.FrontIndex != -1 && cell.FrontIndex != 200)
                        drew |= TryDrawSprite(cell.FrontIndex, front, dx, dy, CellWidth, CellHeight);

                    if (drew)
                        FloorDraws++;
                    else
                        SkippedCells++;
                }
            }
        }

        foreach (var obj in objects)
        {
            float dx = (obj.Location.X - startX) * CellWidth;
            float dy = (obj.Location.Y - startY) * CellHeight;
            if (TryDrawNamed(obj.SpriteLibrary, obj.SpriteIndex, dx, dy - 16, 24, 24)
                || TryDrawFallback(dx, dy - 16, 24, 24))
                ObjectDraws++;
        }
    }

    bool TryDrawSprite(int libraryIndex, int imageIndex, float x, float y, float w, float h)
    {
        if (imageIndex < 0)
            return false;
        string? rel = MapLibraryIndex.RelativePath(libraryIndex);
        if (rel == null)
            return false;
        return TryDrawNamed(rel, imageIndex, x, y, w, h);
    }

    bool TryDrawNamed(string library, int imageIndex, float x, float y, float w, float h)
    {
        string lib = Normalize(library);
        if (_sprites.TryGetValue((lib, imageIndex), out var sprite) && DrawAtlas(sprite, x, y, w, h))
            return true;

        if (TryDrawFromLib(lib, imageIndex, x, y, w, h))
            return true;

        if (_byLibrary.TryGetValue(lib, out var list) && list.Count > 0)
        {
            var remap = list[Math.Abs(imageIndex) % list.Count];
            LowFiRemaps++;
            return DrawAtlas(remap, x, y, w, h);
        }

        return false;
    }

    bool TryDrawFallback(float x, float y, float w, float h)
    {
        if (_fallbackSprite == null)
            return false;
        LowFiRemaps++;
        return DrawAtlas(_fallbackSprite, x, y, w, h);
    }

    bool DrawAtlas(AtlasSprite sprite, float x, float y, float w, float h)
    {
        if (!_atlases.TryGetValue(sprite.Atlas, out var tex))
            return false;
        var src = new Rectangle(sprite.X, sprite.Y, sprite.Width, sprite.Height);
        _renderer.DrawQuad(tex, src, x, y, w, h, Color.White);
        return true;
    }

    bool TryDrawFromLib(string library, int imageIndex, float x, float y, float w, float h)
    {
        if (string.IsNullOrWhiteSpace(_dataRoot))
            return false;

        var key = (library, imageIndex);
        if (!_libTextures.TryGetValue(key, out var tex))
        {
            if (!_parsedLibs.TryGetValue(library, out var parsed))
            {
                string path = Path.Combine(_dataRoot, library.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    return false;
                parsed = LibraryParser.Parse(new DiscoveredLibrary
                {
                    Path = path,
                    RelativePath = library,
                    Kind = LibraryKind.MLib
                });
                _parsedLibs[library] = parsed;
            }

            var image = parsed.Images.FirstOrDefault(i => i.Index == imageIndex && i.Bgra is { Length: > 0 } && i.Width > 0 && i.Height > 0);
            if (image?.Bgra == null)
                return false;
            tex = _renderer.CreateTexture(image.Width, image.Height, image.Bgra);
            _libTextures[key] = tex;
        }

        _renderer.DrawQuad(tex, null, x, y, w, h, Color.White);
        return true;
    }

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

internal sealed class WorldObject
{
    public uint ObjectID;
    public string Name = "";
    public Point Location;
    public string Kind = "";
    public string SpriteLibrary = "CArmour/00.Lib";
    public int SpriteIndex;
}
