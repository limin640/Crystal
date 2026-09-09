namespace Crystal.Graphics.Backends;

internal sealed class MemoryTexture : IGpuTexture
{
    public int Width { get; }
    public int Height { get; }
    public bool IsRenderTarget { get; }
    public bool IsDisposed { get; private set; }
    public byte[] Bgra { get; }

    public MemoryTexture(int width, int height, byte[] bgra, bool renderTarget = false)
    {
        Width = width;
        Height = height;
        Bgra = bgra;
        IsRenderTarget = renderTarget;
    }

    public void Dispose() => IsDisposed = true;
}

internal sealed class MemorySurface : IGpuSurface
{
    public int Width { get; }
    public int Height { get; }
    public bool IsDisposed { get; private set; }
    public IGpuTexture? Texture { get; }

    public MemorySurface(int width, int height, IGpuTexture? texture)
    {
        Width = width;
        Height = height;
        Texture = texture;
    }

    public void Dispose() => IsDisposed = true;
}
