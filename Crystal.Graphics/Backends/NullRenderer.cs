using System.Drawing;

namespace Crystal.Graphics.Backends;

/// <summary>
/// Headless backend used for Linux compile/run evidence without a display.
/// Records draw calls so bake/host smoke tests can assert the abstraction is live.
/// </summary>
public sealed class NullRenderer : IRenderer
{
    readonly List<string> _log = new();
    MemorySurface _main;
    MemorySurface _current;
    int _width, _height;

    public NullRenderer(int width, int height)
    {
        _width = width;
        _height = height;
        var rt = new MemoryTexture(width, height, new byte[width * height * 4], true);
        _main = new MemorySurface(width, height, rt);
        _current = _main;
    }

    public RendererBackendKind Kind => RendererBackendKind.Null;
    public string BackendName => "Null (headless)";
    public bool IsHeadless => true;
    public float Opacity { get; private set; } = 1f;
    public bool Blending { get; private set; }
    public IGpuSurface MainSurface => _main;
    public IGpuSurface CurrentSurface => _current;
    public int DrawCount { get; private set; }
    public IReadOnlyList<string> Log => _log;

    public void BeginFrame(int width, int height)
    {
        _width = width;
        _height = height;
        _log.Add($"BeginFrame {width}x{height}");
    }

    public void EndFrame() => _log.Add("EndFrame");
    public void Present() => _log.Add("Present");
    public void Flush() => _log.Add("Flush");

    public void Clear(Color color) => _log.Add($"Clear {color.Name}");

    public void SetOpacity(float opacity) => Opacity = opacity;

    public void SetBlend(bool enabled, float rate = 1, BlendMode mode = BlendMode.NORMAL)
    {
        Blending = enabled;
        _log.Add($"Blend {enabled} {mode} {rate}");
    }

    public void SetGrayscale(bool enabled) => _log.Add($"Grayscale {enabled}");

    public void SetSurface(IGpuSurface surface) => _current = (MemorySurface)surface;

    public IGpuTexture CreateTexture(int width, int height, ReadOnlySpan<byte> bgra)
    {
        var data = bgra.ToArray();
        _log.Add($"CreateTexture {width}x{height} bytes={data.Length}");
        return new MemoryTexture(width, height, data);
    }

    public IGpuTexture CreateRenderTarget(int width, int height)
        => new MemoryTexture(width, height, new byte[width * height * 4], true);

    public IGpuTexture CreateSolidTexture(int width, int height, Color color)
    {
        var data = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            data[i * 4] = color.B;
            data[i * 4 + 1] = color.G;
            data[i * 4 + 2] = color.R;
            data[i * 4 + 3] = color.A;
        }
        return new MemoryTexture(width, height, data);
    }

    public void DrawQuad(IGpuTexture texture, Rectangle? source, float destX, float destY, float destW, float destH, Color color, float opacity = 1)
    {
        DrawCount++;
        _log.Add($"DrawQuad tex={texture.Width}x{texture.Height} dest=({destX},{destY},{destW},{destH})");
    }

    public void DrawLine(ReadOnlySpan<PointF> points, Color color, float width = 1)
        => _log.Add($"DrawLine n={points.Length}");

    public void Dispose()
    {
        _main.Dispose();
        _main.Texture?.Dispose();
    }
}
