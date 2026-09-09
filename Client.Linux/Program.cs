using System.Drawing;
using System.Text.Json;
using Crystal.Assets.Atlas;
using Crystal.Assets.Imaging;
using Crystal.Graphics;
using Crystal.Graphics.Backends;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Client.Linux;

/// <summary>
/// Linux-capable Crystal client entry. Shared packets drive login → select → StartGame;
/// floor/objects draw through IRenderer from a bake catalog or existing .Lib.
/// </summary>
internal static class Program
{
    static int Main(string[] args)
    {
        bool headless = args.Any(a => a is "--headless" or "-h");
        bool connect = args.Contains("--connect");
        string? catalogPath = GetOption(args, "--catalog");
        string? mapsRoot = GetOption(args, "--maps") ?? Environment.GetEnvironmentVariable("CRYSTAL_MAPS");
        string? dataRoot = GetOption(args, "--data") ?? Environment.GetEnvironmentVariable("CRYSTAL_DATA");
        int width = GetInt(args, "--width") ?? 1024;
        int height = GetInt(args, "--height") ?? 768;
        int? frames = GetInt(args, "--frames");

        CrystalSession? session = null;
        int connectCode = 0;
        if (connect)
        {
            session = RunConnect(args);
            connectCode = session.ExitCode;
        }

        try
        {
            if (headless)
            {
                int drawCode = RunHeadless(catalogPath, mapsRoot, dataRoot, width, height, frames ?? 1, session);
                return connect ? (connectCode != 0 ? connectCode : drawCode) : drawCode;
            }

            if (connect && !args.Contains("--window"))
                return connectCode;

            return RunWindow(catalogPath, mapsRoot, dataRoot, width, height, frames, session);
        }
        finally
        {
            session?.Dispose();
        }
    }

    static CrystalSession RunConnect(string[] args)
    {
        var opt = new ConnectOptions();
        string iniPath = GetOption(args, "--ini") ?? Path.Combine(AppContext.BaseDirectory, "Mir2Test.ini");
        opt.WaitMs = GetInt(args, "--wait-ms") ?? 1500;
        opt.EnterWaitMs = GetInt(args, "--enter-wait-ms") ?? 5000;
        opt.LoginOnly = args.Contains("--login-only");
        opt.Walk = !args.Contains("--no-walk");
        opt.CreateAccount = args.Contains("--new-account");
        if (GetOption(args, "--character") is string character)
            opt.CharacterName = character;

        if (File.Exists(iniPath))
        {
            var ini = new InIReader(iniPath);
            opt.Host = ini.ReadString("Network", "IPAddress", opt.Host);
            opt.Port = ini.ReadInt32("Network", "Port", opt.Port);
            opt.Account = ini.ReadString("Login", "AccountID", opt.Account);
            opt.Password = ini.ReadString("Login", "Password", opt.Password);
            if (!args.Contains("--new-account"))
                opt.CreateAccount = ini.ReadBoolean("Login", "NewAccount", opt.CreateAccount);
            Console.WriteLine($"Loaded {iniPath}");
        }
        else
            Console.WriteLine($"No ini at {iniPath}; using CLI/defaults.");

        if (GetOption(args, "--ip") is string ipOverride)
            opt.Host = ipOverride;
        if (GetInt(args, "--port") is int portOverride)
            opt.Port = portOverride;
        if (GetOption(args, "--account") is string accOverride)
            opt.Account = accOverride;
        if (GetOption(args, "--password") is string pwOverride)
            opt.Password = pwOverride;

        return CrystalSession.Run(opt);
    }

    static int RunHeadless(string? catalogPath, string? mapsRoot, string? dataRoot, int width, int height, int frames, CrystalSession? session)
    {
        using IRenderer renderer = RendererFactory.CreateNull(width, height);
        CatalogGpu? catalog = null;
        if (catalogPath != null && File.Exists(catalogPath))
            catalog = LoadCatalogInto(renderer, catalogPath);

        MapView? mapView = null;
        if (catalog != null)
            mapView = new MapView(renderer, catalog.Textures, catalog.Sprites, dataRoot);

        bool drewMap = TryLoadAndDrawMap(mapView, mapsRoot, session, renderer, width, height, frames, catalog);

        if (!drewMap)
        {
            for (int f = 0; f < Math.Max(1, frames); f++)
            {
                renderer.BeginFrame(width, height);
                renderer.Clear(Color.Black);
                renderer.SetBlend(true, 1f, Crystal.Graphics.BlendMode.NORMAL);

                if (catalog == null)
                {
                    var probe = renderer.CreateSolidTexture(8, 8, Color.CornflowerBlue);
                    renderer.DrawQuad(probe, new System.Drawing.Rectangle(0, 0, 8, 8), 16, 16, 64, 64, Color.White);
                }
                else
                {
                    DrawCatalog(renderer, catalog, width, height);
                }

                renderer.EndFrame();
                renderer.Present();
            }
        }

        int draws = renderer is NullRenderer n ? n.DrawCount : 0;
        Console.WriteLine($"Client.Linux headless OK");
        Console.WriteLine($"  backend : {renderer.BackendName}");
        Console.WriteLine($"  kind    : {renderer.Kind}");
        Console.WriteLine($"  frames  : {frames}");
        Console.WriteLine($"  draws   : {draws}");
        Console.WriteLine($"  atlases : {catalog?.Textures.Count ?? 0}");
        Console.WriteLine($"  sprites : {catalog?.Sprites.Count ?? 0}");
        Console.WriteLine($"  catalog : {(catalogPath == null ? "(none)" : catalogPath)}");
        if (mapView != null)
        {
            Console.WriteLine($"  map     : {mapView.MapPath ?? "(none)"} {mapView.MapWidth}x{mapView.MapHeight} loaded={mapView.MapLoaded}");
            Console.WriteLine($"  floor   : {mapView.FloorDraws} objectDraws={mapView.ObjectDraws} skipped={mapView.SkippedCells} lowFi={mapView.LowFiRemaps}");
        }
        Console.WriteLine("Runtime parity (login→select→walk→fight→loot→equip) is a later gate.");
        mapView?.Dispose();
        catalog?.Dispose();
        return 0;
    }

    static bool TryLoadAndDrawMap(MapView? mapView, string? mapsRoot, CrystalSession? session, IRenderer renderer, int width, int height, int frames, CatalogGpu? catalog)
    {
        if (mapView == null || session is not { InMap: true } || string.IsNullOrWhiteSpace(session.MapFileName))
            return false;

        string root = mapsRoot
                      ?? Environment.GetEnvironmentVariable("CRYSTAL_MAPS")
                      ?? "";
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("In-map but no --maps / CRYSTAL_MAPS; drawing catalog sprites only.");
            return false;
        }

        if (!mapView.LoadMap(root, session.MapFileName))
            return false;

        var objects = session.Objects.ToList();
        for (int f = 0; f < Math.Max(1, frames); f++)
        {
            renderer.BeginFrame(width, height);
            renderer.Clear(Color.FromArgb(255, 8, 12, 8));
            renderer.SetBlend(true, 1f, Crystal.Graphics.BlendMode.NORMAL);
            mapView.Draw(width, height, session.UserLocation, objects);
            renderer.EndFrame();
            renderer.Present();
        }

        Console.WriteLine($"in-map draw: file={session.MapFileName} title={session.MapTitle} origin={session.UserLocation.X},{session.UserLocation.Y} objects={objects.Count}");
        _ = catalog;
        return true;
    }

    static int RunWindow(string? catalogPath, string? mapsRoot, string? dataRoot, int width, int height, int? frames, CrystalSession? session)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            Console.Error.WriteLine("No DISPLAY/WAYLAND_DISPLAY. Use --headless on servers without a display.");
            return 4;
        }

        try
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(width, height),
                Title = "Crystal Client (Linux / OpenGL)"
            };

            using var window = Window.Create(options);
            IRenderer? renderer = null;
            GL? gl = null;
            CatalogGpu? catalog = null;
            MapView? mapView = null;
            int frameCount = 0;

            window.Load += () =>
            {
                gl = window.CreateOpenGL();
                renderer = RendererFactory.CreateOpenGL(gl, window.Size.X, window.Size.Y);
                if (catalogPath != null && File.Exists(catalogPath))
                    catalog = LoadCatalogInto(renderer, catalogPath);
                if (catalog != null)
                    mapView = new MapView(renderer, catalog.Textures, catalog.Sprites, dataRoot);
                if (mapView != null && session is { InMap: true } && !string.IsNullOrWhiteSpace(session.MapFileName) && !string.IsNullOrWhiteSpace(mapsRoot))
                    mapView.LoadMap(mapsRoot, session.MapFileName);
            };

            window.Render += _ =>
            {
                if (renderer == null) return;
                renderer.BeginFrame(window.Size.X, window.Size.Y);
                renderer.Clear(Color.FromArgb(255, 16, 16, 24));
                if (mapView is { MapLoaded: true } && session != null)
                    mapView.Draw(window.Size.X, window.Size.Y, session.UserLocation, session.Objects);
                else if (catalog != null)
                    DrawCatalog(renderer, catalog, window.Size.X, window.Size.Y);
                else
                {
                    var tex = renderer.CreateSolidTexture(2, 2, Color.MediumPurple);
                    renderer.DrawQuad(tex, null, 32, 32, 128, 128, Color.MediumPurple);
                    tex.Dispose();
                }
                renderer.EndFrame();

                frameCount++;
                if (frames is int max && max > 0 && frameCount >= max)
                    window.Close();
            };

            window.Closing += () =>
            {
                mapView?.Dispose();
                catalog?.Dispose();
                renderer?.Dispose();
                gl?.Dispose();
            };

            window.Run();
            Console.WriteLine($"Client.Linux windowed OK frames={frameCount} backend={renderer?.BackendName}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("OpenGL window failed (use --headless on servers without a display):");
            Console.Error.WriteLine(ex.Message);
            return 4;
        }
    }

    static CatalogGpu LoadCatalogInto(IRenderer renderer, string catalogPath)
    {
        string json = File.ReadAllText(catalogPath);
        var catalog = JsonSerializer.Deserialize<AtlasCatalog>(json)
                      ?? throw new InvalidDataException($"Could not read catalog {catalogPath}");
        string root = Path.GetDirectoryName(Path.GetFullPath(catalogPath)) ?? ".";

        var textures = new Dictionary<int, IGpuTexture>();
        foreach (var sheet in catalog.Atlases)
        {
            string png = Path.Combine(root, sheet.Png.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(png))
            {
                Console.Error.WriteLine($"  missing atlas PNG {sheet.Png}");
                continue;
            }

            var (w, h, bgra) = PngReader.ReadBgra(png);
            textures[sheet.Id] = renderer.CreateTexture(w, h, bgra);
        }

        var sprites = catalog.Sprites.Where(s => !s.Blank && s.Width > 0 && s.Height > 0).ToList();
        Console.WriteLine($"Loaded atlas catalog v{catalog.Version} sheets={catalog.Atlases.Count} uploaded={textures.Count} sprites={sprites.Count} compression={catalog.Compression}");
        return new CatalogGpu(textures, sprites);
    }

    static void DrawCatalog(IRenderer renderer, CatalogGpu catalog, int width, int height)
    {
        int x = 8, y = 8, rowH = 0;
        foreach (var sprite in catalog.Sprites)
        {
            if (!catalog.Textures.TryGetValue(sprite.Atlas, out var tex))
                continue;

            var src = new System.Drawing.Rectangle(sprite.X, sprite.Y, sprite.Width, sprite.Height);
            float dw = Math.Clamp(sprite.Width, 4, 48);
            float dh = Math.Clamp(sprite.Height, 4, 48);
            renderer.DrawQuad(tex, src, x, y, dw, dh, Color.White);

            x += (int)dw + 4;
            rowH = Math.Max(rowH, (int)dh);
            if (x > width - 56)
            {
                x = 8;
                y += rowH + 4;
                rowH = 0;
            }
            if (y > height - 8)
            {
                x = 8;
                y = 8;
            }
        }
    }

    static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }

    static int? GetInt(string[] args, string name)
    {
        string? raw = GetOption(args, name);
        return int.TryParse(raw, out int value) ? value : null;
    }

    sealed class CatalogGpu : IDisposable
    {
        public Dictionary<int, IGpuTexture> Textures { get; }
        public List<AtlasSprite> Sprites { get; }

        public CatalogGpu(Dictionary<int, IGpuTexture> textures, List<AtlasSprite> sprites)
        {
            Textures = textures;
            Sprites = sprites;
        }

        public void Dispose()
        {
            foreach (var tex in Textures.Values)
                tex.Dispose();
            Textures.Clear();
        }
    }
}
