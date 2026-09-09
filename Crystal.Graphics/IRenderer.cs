using System.Drawing;

namespace Crystal.Graphics;

public enum RendererBackendKind
{
    Null = 0,
    OpenGL = 1,
    SlimDX = 2
}

/// <summary>
/// Batched atlas/sprite renderer. Windows SlimDX and Linux OpenGL both implement this.
/// Game code should prefer this over Device/Sprite/Texture.
/// </summary>
public interface IRenderer : IDisposable
{
    RendererBackendKind Kind { get; }
    string BackendName { get; }
    bool IsHeadless { get; }

    float Opacity { get; }
    bool Blending { get; }

    void BeginFrame(int width, int height);
    void EndFrame();
    void Present();
    void Flush();

    void Clear(Color color);
    void SetOpacity(float opacity);
    void SetBlend(bool enabled, float rate = 1f, BlendMode mode = BlendMode.NORMAL);
    void SetGrayscale(bool enabled);
    void SetSurface(IGpuSurface surface);
    IGpuSurface MainSurface { get; }
    IGpuSurface CurrentSurface { get; }

    IGpuTexture CreateTexture(int width, int height, ReadOnlySpan<byte> bgra);
    IGpuTexture CreateRenderTarget(int width, int height);
    IGpuTexture CreateSolidTexture(int width, int height, Color color);
    void UpdateTexture(IGpuTexture texture, ReadOnlySpan<byte> bgra);
    IGpuSurface GetSurface(IGpuTexture texture);

    void DrawQuad(IGpuTexture texture, Rectangle? source, float destX, float destY, float destW, float destH, Color color, float opacity = 1f);

    void DrawLine(ReadOnlySpan<PointF> points, Color color, float width = 1f);

    /// <summary>Light overlay: dest *= source (D3D Zero / SourceColor).</summary>
    void SetMultiplyBlend();
}

/// <summary>Mirrors Shared.BlendMode so Crystal.Graphics stays free of the Shared WinForms graph.</summary>
public enum BlendMode : sbyte
{
    NONE = -1,
    NORMAL = 0,
    LIGHT = 1,
    LIGHTINV = 2,
    INVNORMAL = 3,
    INVLIGHT = 4,
    INVLIGHTINV = 5,
    INVCOLOR = 6,
    INVBACKGROUND = 7
}
