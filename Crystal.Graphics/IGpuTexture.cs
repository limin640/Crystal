namespace Crystal.Graphics;

public interface IGpuTexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    bool IsDisposed { get; }
    bool IsRenderTarget { get; }
}

public interface IGpuSurface : IDisposable
{
    int Width { get; }
    int Height { get; }
    bool IsDisposed { get; }
    IGpuTexture? Texture { get; }
}
