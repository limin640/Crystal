namespace Crystal.Graphics;

public interface IGpuTexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    bool IsDisposed { get; }
    /// <summary>SlimDX-shaped alias so existing Client checks keep compiling.</summary>
    bool Disposed { get; }
    bool IsRenderTarget { get; }
    IGpuSurface GetSurface();
}

public interface IGpuSurface : IDisposable
{
    int Width { get; }
    int Height { get; }
    bool IsDisposed { get; }
    bool Disposed { get; }
    IGpuTexture? Texture { get; }
}
