using System.Drawing;
using System.Text.Json;
using Crystal.Assets.Atlas;
using Crystal.Assets.Imaging;
using Crystal.Graphics;
using Crystal.Graphics.Backends;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Client.Linux;

/// <summary>
/// Linux-capable Crystal client entry. Shared packets drive login → select → StartGame;
/// Silk.NET input (or --input-script) walks/attacks; HUD + map through IRenderer.
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
            session.AutoTradeReply = args.Contains("--auto-trade-reply");
            session.AutoTradeConfirm = args.Contains("--auto-trade-confirm");
            if (GetOption(args, "--input-script") is string script && session.InMap)
            {
                int stepMs = GetInt(args, "--input-step-ms") ?? 400;
                int inputCode = session.RunInputScript(InputMap.ParseScript(script), stepMs);
                if (connectCode == 0)
                    connectCode = inputCode;
            }
            if (GetInt(args, "--keep-alive") is int keepMs && keepMs > 0 && session.InMap)
            {
                Console.WriteLine($"keep-alive {keepMs}ms");
                session.Pump(keepMs);
            }
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
        opt.PlayGate = !args.Contains("--no-gate");
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
        using var hud = new SceneHud(renderer);

        bool drewMap = TryLoadAndDrawMap(mapView, mapsRoot, session, renderer, width, height, frames, catalog, hud);

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
        if (session != null)
        {
            Console.WriteLine($"  input   : walks={session.InputWalks} attacks={session.InputAttacks} pickups={session.InputPickups} chats={session.InputChats} talks={session.InputTalks} buys={session.InputBuys} sells={session.InputSells} trades={session.InputTrades} NpcTalkOk={session.NpcTalkOk} BuyOk={session.BuyOk} SellOk={session.SellOk} TradeHandshake={session.TradeHandshakeOk} TradeDone={session.TradeDone}");
            Console.WriteLine($"  items   : bag={session.BagCount} gold={session.UserGold} equip={session.EquippedFilled} magics={session.Magics.Count} chat={session.ChatLines.Count}");
            if (session.BuyEvidence != null)
                Console.WriteLine($"  buy     : {session.BuyEvidence}");
            if (session.SellEvidence != null)
                Console.WriteLine($"  sell    : {session.SellEvidence}");
            if (session.TradeEvidence != null)
                Console.WriteLine($"  trade   : {session.TradeEvidence}");
        }
        Console.WriteLine("Hard-gate verbs stay evidenced; this host adds input-driven walk/attack + IRenderer inventory/equip HUD.");
        mapView?.Dispose();
        catalog?.Dispose();
        return 0;
    }

    static bool TryLoadAndDrawMap(MapView? mapView, string? mapsRoot, CrystalSession? session, IRenderer renderer, int width, int height, int frames, CatalogGpu? catalog, SceneHud? hud)
    {
        if (session is { LoginSuccess: true, InMap: false } && hud != null)
        {
            renderer.BeginFrame(width, height);
            renderer.Clear(Color.FromArgb(255, 12, 12, 20));
            hud.DrawSelect(width, height, session);
            renderer.EndFrame();
            renderer.Present();
            Console.WriteLine("select HUD drawn (IRenderer)");
            return true;
        }

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

        session.NoteMapSize(mapView.MapWidth, mapView.MapHeight);

        var objects = session.Objects.ToList();
        for (int f = 0; f < Math.Max(1, frames); f++)
        {
            renderer.BeginFrame(width, height);
            renderer.Clear(Color.FromArgb(255, 8, 12, 8));
            renderer.SetBlend(true, 1f, Crystal.Graphics.BlendMode.NORMAL);
            mapView.Draw(width, height, session.UserLocation, objects);
            hud?.DrawGame(width, height, session, mapView);
            renderer.EndFrame();
            renderer.Present();
        }

        Console.WriteLine($"in-map draw: file={session.MapFileName} title={session.MapTitle} origin={session.UserLocation.X},{session.UserLocation.Y} objects={objects.Count}");
        hud?.WriteEvidence(session);
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
            SceneHud? hud = null;
            IInputContext? input = null;
            int frameCount = 0;
            DateTime nextHeld = DateTime.UtcNow;
            var held = new HashSet<Key>();

            window.Load += () =>
            {
                gl = window.CreateOpenGL();
                renderer = RendererFactory.CreateOpenGL(gl, window.Size.X, window.Size.Y);
                hud = new SceneHud(renderer);
                if (catalogPath != null && File.Exists(catalogPath))
                    catalog = LoadCatalogInto(renderer, catalogPath);
                if (catalog != null)
                    mapView = new MapView(renderer, catalog.Textures, catalog.Sprites, dataRoot);
                if (mapView != null && session is { InMap: true } && !string.IsNullOrWhiteSpace(session.MapFileName) && !string.IsNullOrWhiteSpace(mapsRoot))
                {
                    mapView.LoadMap(mapsRoot, session.MapFileName);
                    session.NoteMapSize(mapView.MapWidth, mapView.MapHeight);
                }

                input = window.CreateInput();
                foreach (var kb in input.Keyboards)
                {
                    kb.KeyChar += (_, ch) =>
                    {
                        if (session is { ChatComposing: true })
                            session.AppendChat(ch);
                    };
                    kb.KeyDown += (_, key, _) =>
                    {
                        held.Add(key);
                        if (session is not { InMap: true }) return;
                        if (session.ChatComposing)
                        {
                            if (key is Key.Enter or Key.KeypadEnter)
                                session.CommitChat();
                            else if (key is Key.Escape)
                                session.CancelChat();
                            else if (key is Key.Backspace)
                                session.ChatBackspace();
                            return;
                        }
                        if (key is Key.Enter or Key.KeypadEnter)
                            session.BeginChat();
                        else if (key is Key.Space or Key.ControlLeft or Key.Z)
                            session.Drive(GameCommand.Attack(session.Facing));
                        else if (key is Key.T)
                            session.Drive(GameCommand.Talk());
                        else if (key is Key.G or Key.F)
                            session.Drive(GameCommand.PickUp());
                        else if (TrySilkWalk(key, out var dir))
                            session.Drive(GameCommand.Walk(dir));
                    };
                    kb.KeyUp += (_, key, _) => held.Remove(key);
                }
                foreach (var mouse in input.Mice)
                {
                    mouse.MouseDown += (_, btn) =>
                    {
                        if (session is not { InMap: true } || renderer == null) return;
                        if (btn == MouseButton.Right)
                        {
                            session.Drive(GameCommand.Attack(session.Facing));
                            return;
                        }
                        if (btn != MouseButton.Left) return;
                        var pos = mouse.Position;
                        int cellX = session.UserLocation.X + (int)(pos.X / MapView.CellWidth) - (window.Size.X / MapView.CellWidth / 2);
                        int cellY = session.UserLocation.Y + (int)(pos.Y / MapView.CellHeight) - (window.Size.Y / MapView.CellHeight / 2);
                        var dest = new Point(cellX, cellY);
                        var dir = Functions.DirectionFromPoint(session.UserLocation, dest);
                        session.Drive(GameCommand.Walk(dir));
                        Console.WriteLine($"input mouse walk {dir} toward {cellX},{cellY}");
                    };
                }
            };

            window.Update += _ =>
            {
                session?.Pump(0);
                if (session is not { InMap: true }) return;
                if (session.ChatComposing) return;
                if (DateTime.UtcNow < nextHeld) return;
                foreach (var key in held)
                {
                    if (TrySilkWalk(key, out var dir))
                    {
                        session.Drive(GameCommand.Walk(dir));
                        nextHeld = DateTime.UtcNow.AddMilliseconds(350);
                        break;
                    }
                }
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
                if (hud != null && session != null)
                {
                    if (session.InMap) hud.DrawGame(window.Size.X, window.Size.Y, session, mapView);
                    else if (session.LoginSuccess) hud.DrawSelect(window.Size.X, window.Size.Y, session);
                }
                renderer.EndFrame();

                frameCount++;
                if (frames is int max && max > 0 && frameCount >= max)
                    window.Close();
            };

            window.Closing += () =>
            {
                input?.Dispose();
                hud?.Dispose();
                mapView?.Dispose();
                catalog?.Dispose();
                renderer?.Dispose();
                gl?.Dispose();
            };

            window.Run();
            if (hud != null && session != null)
                hud.WriteEvidence(session);
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

    static bool TrySilkWalk(Key key, out MirDirection dir)
    {
        dir = MirDirection.Right;
        switch (key)
        {
            case Key.W or Key.Up or Key.Keypad8:
                dir = MirDirection.Up; return true;
            case Key.S or Key.Down or Key.Keypad2:
                dir = MirDirection.Down; return true;
            case Key.A or Key.Left or Key.Keypad4:
                dir = MirDirection.Left; return true;
            case Key.D or Key.Right or Key.Keypad6:
                dir = MirDirection.Right; return true;
            case Key.Q or Key.Keypad7:
                dir = MirDirection.UpLeft; return true;
            case Key.E or Key.Keypad9:
                dir = MirDirection.UpRight; return true;
            case Key.Keypad1:
                dir = MirDirection.DownLeft; return true;
            case Key.Keypad3:
                dir = MirDirection.DownRight; return true;
            default:
                return false;
        }
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
