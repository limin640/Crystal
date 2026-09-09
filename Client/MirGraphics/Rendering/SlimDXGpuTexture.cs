using Crystal.Graphics;
using SlimDX.Direct3D9;

namespace Client.MirGraphics.Rendering
{
    /// <summary>
    /// Wraps a SlimDX texture so library draw calls go through <see cref="IGpuTexture"/>.
    /// </summary>
    public sealed class SlimDXGpuTexture : IGpuTexture
    {
        public Texture Texture { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsRenderTarget { get; }
        public bool IsDisposed => Texture == null || Texture.Disposed;

        public SlimDXGpuTexture(Texture texture, int width, int height, bool renderTarget = false)
        {
            Texture = texture;
            Width = width;
            Height = height;
            IsRenderTarget = renderTarget;
        }

        public void Dispose()
        {
            if (Texture != null && !Texture.Disposed)
                Texture.Dispose();
        }
    }

    public sealed class SlimDXGpuSurface : IGpuSurface
    {
        public Surface Surface { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed => Surface == null || Surface.Disposed;
        public IGpuTexture Texture { get; }

        public SlimDXGpuSurface(Surface surface, IGpuTexture texture, int width, int height)
        {
            Surface = surface;
            Texture = texture;
            Width = width;
            Height = height;
        }

        public void Dispose()
        {
            if (Surface != null && !Surface.Disposed)
                Surface.Dispose();
        }
    }
}
