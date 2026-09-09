using Crystal.Graphics;
using SlimDX.Direct3D9;

namespace Client.MirGraphics.Rendering
{
    /// <summary>
    /// Wraps a SlimDX texture so library draw calls go through <see cref="IGpuTexture"/>.
    /// </summary>
    public sealed class SlimDXGpuTexture : IGpuTexture
    {
        SlimDXGpuSurface _surface;

        public Texture Texture { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsRenderTarget { get; }
        public bool IsDisposed => Texture == null || Texture.Disposed;
        public bool Disposed => IsDisposed;

        public SlimDXGpuTexture(Texture texture, int width, int height, bool renderTarget = false)
        {
            Texture = texture;
            Width = width;
            Height = height;
            IsRenderTarget = renderTarget;
        }

        public IGpuSurface GetSurface()
        {
            if (_surface != null && !_surface.IsDisposed)
                return _surface;

            Surface native = Texture.GetSurfaceLevel(0);
            _surface = new SlimDXGpuSurface(native, this, Width, Height);
            return _surface;
        }

        public void Dispose()
        {
            _surface?.DisposeNative();
            _surface = null;
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
        public bool Disposed => IsDisposed;
        public IGpuTexture Texture { get; }

        public SlimDXGpuSurface(Surface surface, IGpuTexture texture, int width, int height)
        {
            Surface = surface;
            Texture = texture;
            Width = width;
            Height = height;
        }

        public void DisposeNative()
        {
            if (Surface != null && !Surface.Disposed)
                Surface.Dispose();
        }

        public void Dispose()
        {
            // Owned by the texture; callers must not release the RT.
        }
    }
}
