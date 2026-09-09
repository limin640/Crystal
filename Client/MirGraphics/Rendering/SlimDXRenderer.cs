using System.Drawing;
using System.Runtime.InteropServices;
using Crystal.Graphics;
using SlimDX;
using SlimDX.Direct3D9;
using BlendMode = Crystal.Graphics.BlendMode;
using Color = System.Drawing.Color;

namespace Client.MirGraphics.Rendering
{
    /// <summary>
    /// Windows SlimDX implementation of <see cref="IRenderer"/>.
    /// Keeps the existing D3D9 device alive while library draws migrate off raw Sprite.Draw.
    /// </summary>
    public sealed class SlimDXRenderer : IRenderer
    {
        Device _device;
        Sprite _sprite;
        SlimDXGpuSurface _main;
        SlimDXGpuSurface _current;

        public SlimDXRenderer(Device device, Sprite sprite, int width, int height)
        {
            _device = device;
            _sprite = sprite;
            _main = new SlimDXGpuSurface(device.GetBackBuffer(0, 0), null, width, height);
            _current = _main;
        }

        public RendererBackendKind Kind => RendererBackendKind.SlimDX;
        public string BackendName => "SlimDX Direct3D9";
        public bool IsHeadless => false;
        public float Opacity { get; private set; } = 1f;
        public bool Blending { get; private set; }
        public IGpuSurface MainSurface => _main;
        public IGpuSurface CurrentSurface => _current;

        public void Rebind(Device device, Sprite sprite, int width, int height)
        {
            _device = device;
            _sprite = sprite;
            _main = new SlimDXGpuSurface(device.GetBackBuffer(0, 0), null, width, height);
            _current = _main;
        }

        public void BeginFrame(int width, int height)
        {
            _device.BeginScene();
            _sprite.Begin(SpriteFlags.AlphaBlend);
        }

        public void EndFrame()
        {
            Flush();
            try { _sprite.End(); } catch { }
            try { _device.EndScene(); } catch { }
        }

        public void Present()
        {
            try { _device.Present(); } catch { }
        }

        public void Flush()
        {
            try { _sprite?.Flush(); }
            catch { /* device lost */ }
        }

        public void Clear(Color color)
        {
            _device.Clear(ClearFlags.Target, color, 0, 0);
        }

        public void SetOpacity(float opacity)
        {
            Opacity = opacity;
            DXManager.SetOpacity(opacity);
        }

        public void SetBlend(bool enabled, float rate = 1, BlendMode mode = BlendMode.NORMAL)
        {
            Blending = enabled;
            DXManager.SetBlend(enabled, rate, (global::BlendMode)(sbyte)mode);
        }

        public void SetGrayscale(bool enabled) => DXManager.SetGrayscale(enabled);

        public void SetSurface(IGpuSurface surface)
        {
            Flush();
            _current = (SlimDXGpuSurface)surface;
            if (_current.Surface != null)
                _device.SetRenderTarget(0, _current.Surface);
        }

        public IGpuTexture CreateTexture(int width, int height, ReadOnlySpan<byte> bgra)
        {
            var texture = new Texture(_device, width, height, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
            DataRectangle rect = texture.LockRectangle(0, LockFlags.Discard);
            try
            {
                int pitch = rect.Pitch;
                IntPtr dest = rect.Data.DataPointer;
                if (pitch == width * 4)
                {
                    Marshal.Copy(bgra.ToArray(), 0, dest, width * height * 4);
                }
                else
                {
                    byte[] copy = bgra.ToArray();
                    for (int y = 0; y < height; y++)
                        Marshal.Copy(copy, y * width * 4, dest + y * pitch, width * 4);
                }
            }
            finally
            {
                texture.UnlockRectangle(0);
            }

            return new SlimDXGpuTexture(texture, width, height);
        }

        public IGpuTexture CreateRenderTarget(int width, int height)
        {
            var texture = new Texture(_device, width, height, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
            return new SlimDXGpuTexture(texture, width, height, true);
        }

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
            return CreateTexture(width, height, data);
        }

        public void UpdateTexture(IGpuTexture texture, ReadOnlySpan<byte> bgra)
        {
            if (texture is not SlimDXGpuTexture slim || slim.IsDisposed)
                return;

            DataRectangle rect = slim.Texture.LockRectangle(0, LockFlags.Discard);
            try
            {
                int pitch = rect.Pitch;
                IntPtr dest = rect.Data.DataPointer;
                byte[] copy = bgra.ToArray();
                if (pitch == slim.Width * 4)
                    Marshal.Copy(copy, 0, dest, slim.Width * slim.Height * 4);
                else
                {
                    for (int y = 0; y < slim.Height; y++)
                        Marshal.Copy(copy, y * slim.Width * 4, dest + y * pitch, slim.Width * 4);
                }
            }
            finally
            {
                slim.Texture.UnlockRectangle(0);
            }
        }

        public IGpuSurface GetSurface(IGpuTexture texture) => texture.GetSurface();

        public void SetMultiplyBlend()
        {
            Flush();
            _device.SetRenderState(RenderState.AlphaBlendEnable, true);
            _device.SetRenderState(RenderState.SourceBlend, SlimDX.Direct3D9.Blend.Zero);
            _device.SetRenderState(RenderState.DestinationBlend, SlimDX.Direct3D9.Blend.SourceColor);
        }

        public void DrawQuad(IGpuTexture texture, Rectangle? source, float destX, float destY, float destW, float destH, Color color, float opacity = 1)
        {
            if (texture is not SlimDXGpuTexture slim || slim.IsDisposed)
                return;

            bool scaled = destW != (source?.Width ?? texture.Width) || destH != (source?.Height ?? texture.Height);
            Matrix old = _sprite.Transform;
            if (scaled && texture.Width > 0 && texture.Height > 0)
            {
                float sx = destW / (source?.Width ?? texture.Width);
                float sy = destH / (source?.Height ?? texture.Height);
                _sprite.Transform = Matrix.Scaling(sx, sy, 0);
                destX /= sx;
                destY /= sy;
            }

            Color4 c = color;
            c.Alpha *= opacity * Opacity;
            _sprite.Draw(slim.Texture, source, Vector3.Zero, new Vector3(destX, destY, 0), c);

            if (scaled)
                _sprite.Transform = old;
        }

        public void DrawLine(ReadOnlySpan<PointF> points, Color color, float width = 1)
        {
            if (DXManager.Line == null || points.Length < 2)
                return;
            DXManager.Line.Width = width;
            var vectors = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
                vectors[i] = new Vector2(points[i].X, points[i].Y);
            DXManager.Line.Draw(vectors, color);
        }

        public void DrawLegacy(Texture texture, Rectangle? sourceRect, Vector3? position, Color4 color)
        {
            _sprite.Draw(texture, sourceRect, Vector3.Zero, position, color);
        }

        public void Dispose() { }
    }
}
