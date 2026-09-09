using System.Drawing;
using System.Text.Json;
using Crystal.Assets.Atlas;
using Crystal.Graphics;
using Crystal.Graphics.Backends;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Client.Linux;

/// <summary>
/// Linux-capable Crystal client entry. Protocol/scenes stay on the Windows Client for now;
/// this host proves the graphics stack compiles and runs on Linux.
/// </summary>
internal static class Program
{
    static int Main(string[] args)
    {
        bool headless = args.Any(a => a is "--headless" or "-h");
        string? catalogPath = GetOption(args, "--catalog");
        int width = 1024, height = 768;

        if (headless)
            return RunHeadless(catalogPath, width, height);

        return RunWindow(catalogPath, width, height);
    }

    static int RunHeadless(string? catalogPath, int width, int height)
    {
        using IRenderer renderer = RendererFactory.CreateNull(width, height);
        renderer.BeginFrame(width, height);
        renderer.Clear(Color.Black);
        renderer.SetBlend(true, 1f, Crystal.Graphics.BlendMode.NORMAL);

        var probe = renderer.CreateSolidTexture(8, 8, Color.CornflowerBlue);
        renderer.DrawQuad(probe, new System.Drawing.Rectangle(0, 0, 8, 8), 16, 16, 64, 64, Color.White);

        if (catalogPath != null && File.Exists(catalogPath))
            LoadCatalogInto(renderer, catalogPath);

        renderer.EndFrame();
        renderer.Present();

        int draws = renderer is NullRenderer n ? n.DrawCount : 0;
        Console.WriteLine($"Client.Linux headless OK");
        Console.WriteLine($"  backend : {renderer.BackendName}");
        Console.WriteLine($"  kind    : {renderer.Kind}");
        Console.WriteLine($"  draws   : {draws}");
        Console.WriteLine($"  catalog : {(catalogPath == null ? "(none)" : catalogPath)}");
        Console.WriteLine("Runtime parity (login→select→walk→fight→loot→equip) is a later gate.");
        return 0;
    }

    static int RunWindow(string? catalogPath, int width, int height)
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = "Crystal Client (Linux / OpenGL)"
        };

        using var window = Window.Create(options);
        IRenderer? renderer = null;
        GL? gl = null;

        window.Load += () =>
        {
            gl = window.CreateOpenGL();
            renderer = RendererFactory.CreateOpenGL(gl, window.Size.X, window.Size.Y);
            if (catalogPath != null && File.Exists(catalogPath))
                LoadCatalogInto(renderer, catalogPath);
        };

        window.Render += _ =>
        {
            if (renderer == null) return;
            renderer.BeginFrame(window.Size.X, window.Size.Y);
            renderer.Clear(Color.FromArgb(255, 16, 16, 24));
            var tex = renderer.CreateSolidTexture(2, 2, Color.White);
            renderer.DrawQuad(tex, null, 32, 32, 128, 128, Color.MediumPurple);
            tex.Dispose();
            renderer.EndFrame();
        };

        window.Closing += () =>
        {
            renderer?.Dispose();
            gl?.Dispose();
        };

        try
        {
            window.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("OpenGL window failed (use --headless on servers without a display):");
            Console.Error.WriteLine(ex.Message);
            return 4;
        }
    }

    static void LoadCatalogInto(IRenderer renderer, string catalogPath)
    {
        string json = File.ReadAllText(catalogPath);
        var catalog = JsonSerializer.Deserialize<AtlasCatalog>(json);
        if (catalog == null)
            return;

        Console.WriteLine($"Loaded atlas catalog v{catalog.Version} sheets={catalog.Atlases.Count} sprites={catalog.Sprites.Count} compression={catalog.Compression}");
        // Full GPU upload of every atlas sheet is the next increment after bake lands.
        _ = renderer;
    }

    static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }
}
