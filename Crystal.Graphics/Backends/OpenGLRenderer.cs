using System.Drawing;
using Silk.NET.OpenGL;

namespace Crystal.Graphics.Backends;

/// <summary>
/// Batched textured-quad renderer on Silk.NET OpenGL. Viable on Linux once a GL context exists.
/// </summary>
public sealed class OpenGLRenderer : IRenderer
{
    const int MaxQuads = 4096;

    readonly GL _gl;
    readonly uint _program;
    readonly uint _vao;
    readonly uint _vbo;
    readonly uint _ebo;
    readonly float[] _vertices = new float[MaxQuads * 4 * 8];
    readonly uint[] _indices;

    uint _whiteTexture;
    GlTexture? _bound;
    GlSurface _main;
    GlSurface _current;
    int _quads;
    int _width;
    int _height;
    bool _blend = true;
    BlendMode _blendMode = BlendMode.NORMAL;

    public OpenGLRenderer(GL gl, int width, int height)
    {
        _gl = gl;
        _width = width;
        _height = height;
        _program = Compile(VertexSource, FragmentSource);
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();
        _indices = BuildIndices();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), null, BufferUsageARB.DynamicDraw);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* ip = _indices)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(_indices.Length * sizeof(uint)), ip, BufferUsageARB.StaticDraw);

            const uint stride = 8 * sizeof(float);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(4 * sizeof(float)));
        }

        _whiteTexture = CreateGlTexture(1, 1, new byte[] { 255, 255, 255, 255 });
        var mainTex = new GlTexture(_gl, CreateGlTexture(width, height, new byte[width * height * 4]), width, height, true);
        _main = new GlSurface(mainTex, 0);
        _current = _main;

        _gl.Disable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Blend);
        ApplyBlend();
        _gl.Viewport(0, 0, (uint)width, (uint)height);
    }

    public RendererBackendKind Kind => RendererBackendKind.OpenGL;
    public string BackendName => "Silk.NET OpenGL";
    public bool IsHeadless => false;
    public float Opacity { get; private set; } = 1f;
    public bool Blending => _blend;
    public IGpuSurface MainSurface => _main;
    public IGpuSurface CurrentSurface => _current;

    public void BeginFrame(int width, int height)
    {
        _width = width;
        _height = height;
        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _quads = 0;
    }

    public void EndFrame() => Flush();
    public void Present() { }
    public void Flush()
    {
        if (_quads == 0)
            return;

        _gl.UseProgram(_program);
        SetOrtho();
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            nuint byteCount = (nuint)(_quads * 4 * 8 * sizeof(float));
            fixed (float* vp = _vertices)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, byteCount, vp);
        }

        uint tex = _bound?.Handle ?? _whiteTexture;
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        nint drawOffset = 0;
        _gl.DrawElements(PrimitiveType.Triangles, (uint)(_quads * 6), DrawElementsType.UnsignedInt, in drawOffset);
        _quads = 0;
    }

    public void Clear(Color color)
    {
        Flush();
        _gl.ClearColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void SetOpacity(float opacity) => Opacity = opacity;

    public void SetBlend(bool enabled, float rate = 1, BlendMode mode = BlendMode.NORMAL)
    {
        Flush();
        _blend = enabled;
        _blendMode = mode;
        ApplyBlend();
    }

    public void SetGrayscale(bool enabled)
    {
        Flush();
        int loc = _gl.GetUniformLocation(_program, "uGray");
        _gl.UseProgram(_program);
        _gl.Uniform1(loc, enabled ? 1f : 0f);
    }

    public void SetSurface(IGpuSurface surface)
    {
        Flush();
        _current = (GlSurface)surface;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _current.Framebuffer);
        _gl.Viewport(0, 0, (uint)_current.Width, (uint)_current.Height);
    }

    public IGpuTexture CreateTexture(int width, int height, ReadOnlySpan<byte> bgra)
    {
        byte[] copy = bgra.ToArray();
        uint handle = CreateGlTexture(width, height, copy);
        return new GlTexture(_gl, handle, width, height, false);
    }

    public IGpuTexture CreateRenderTarget(int width, int height)
    {
        uint handle = CreateGlTexture(width, height, new byte[width * height * 4]);
        uint fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, handle, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        var tex = new GlTexture(_gl, handle, width, height, true);
        tex.Framebuffer = fbo;
        return tex;
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

    public void DrawQuad(IGpuTexture texture, Rectangle? source, float destX, float destY, float destW, float destH, Color color, float opacity = 1)
    {
        if (texture is not GlTexture gltex || gltex.IsDisposed)
            return;

        if (_bound != null && _bound != gltex)
            Flush();
        if (_quads >= MaxQuads)
            Flush();

        _bound = gltex;
        float u0 = 0, v0 = 0, u1 = 1, v1 = 1;
        if (source is Rectangle src && gltex.Width > 0 && gltex.Height > 0)
        {
            u0 = src.X / (float)gltex.Width;
            v0 = src.Y / (float)gltex.Height;
            u1 = (src.X + src.Width) / (float)gltex.Width;
            v1 = (src.Y + src.Height) / (float)gltex.Height;
        }

        float a = (color.A / 255f) * opacity * Opacity;
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
        int i = _quads * 4 * 8;
        WriteVertex(ref i, destX, destY, u0, v0, r, g, b, a);
        WriteVertex(ref i, destX + destW, destY, u1, v0, r, g, b, a);
        WriteVertex(ref i, destX + destW, destY + destH, u1, v1, r, g, b, a);
        WriteVertex(ref i, destX, destY + destH, u0, v1, r, g, b, a);
        _quads++;
    }

    public void DrawLine(ReadOnlySpan<PointF> points, Color color, float width = 1)
    {
        if (points.Length < 2)
            return;
        var white = new GlTexture(_gl, _whiteTexture, 1, 1, false);
        for (int i = 0; i < points.Length - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
            float nx = -dy / len * width * 0.5f;
            float ny = dx / len * width * 0.5f;
            DrawQuad(white, null, a.X + nx, a.Y + ny, 0, 0, color);
            // degenerate fallback: draw a 1px quad along the segment
            DrawQuad(white, null, Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(1, Math.Abs(dx)), Math.Max(1, Math.Abs(dy)), color);
        }
    }

    public void Dispose()
    {
        Flush();
        _gl.DeleteProgram(_program);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteTexture(_whiteTexture);
    }

    void WriteVertex(ref int i, float x, float y, float u, float v, float r, float g, float b, float a)
    {
        _vertices[i++] = x;
        _vertices[i++] = y;
        _vertices[i++] = u;
        _vertices[i++] = v;
        _vertices[i++] = r;
        _vertices[i++] = g;
        _vertices[i++] = b;
        _vertices[i++] = a;
    }

    void SetOrtho()
    {
        int loc = _gl.GetUniformLocation(_program, "uProjection");
        float w = Math.Max(1, _width);
        float h = Math.Max(1, _height);
        // Column-major ortho: x[0..w] y[0..h] with y-down.
        float[] m =
        {
            2f / w, 0, 0, 0,
            0, -2f / h, 0, 0,
            0, 0, -1, 0,
            -1, 1, 0, 1
        };
        _gl.UniformMatrix4(loc, 1, false, m.AsSpan());
    }

    void ApplyBlend()
    {
        if (!_blend)
        {
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            return;
        }

        switch (_blendMode)
        {
            case BlendMode.INVLIGHT:
                _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcColor);
                break;
            default:
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                break;
        }
    }

    uint CreateGlTexture(int width, int height, byte[] bgra)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        unsafe
        {
            fixed (byte* p = bgra)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, p);
        }
        return tex;
    }

    uint Compile(string vs, string fs)
    {
        uint v = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(v, vs);
        _gl.CompileShader(v);
        CheckShader(v);
        uint f = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(f, fs);
        _gl.CompileShader(f);
        CheckShader(f);
        uint p = _gl.CreateProgram();
        _gl.AttachShader(p, v);
        _gl.AttachShader(p, f);
        _gl.LinkProgram(p);
        _gl.DeleteShader(v);
        _gl.DeleteShader(f);
        return p;
    }

    void CheckShader(uint shader)
    {
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException(_gl.GetShaderInfoLog(shader));
    }

    static uint[] BuildIndices()
    {
        var idx = new uint[MaxQuads * 6];
        for (uint i = 0, v = 0; i < MaxQuads; i++, v += 4)
        {
            idx[i * 6 + 0] = v;
            idx[i * 6 + 1] = v + 1;
            idx[i * 6 + 2] = v + 2;
            idx[i * 6 + 3] = v;
            idx[i * 6 + 4] = v + 2;
            idx[i * 6 + 5] = v + 3;
        }
        return idx;
    }

    const string VertexSource = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aUv;
        layout(location = 2) in vec4 aColor;
        uniform mat4 uProjection;
        out vec2 vUv;
        out vec4 vColor;
        void main() {
            gl_Position = uProjection * vec4(aPos, 0.0, 1.0);
            vUv = aUv;
            vColor = aColor;
        }
        """;

    const string FragmentSource = """
        #version 330 core
        in vec2 vUv;
        in vec4 vColor;
        uniform sampler2D uTex;
        uniform float uGray;
        out vec4 frag;
        void main() {
            vec4 c = texture(uTex, vUv) * vColor;
            float g = dot(c.rgb, vec3(0.299, 0.587, 0.114));
            frag = vec4(mix(c.rgb, vec3(g), uGray), c.a);
        }
        """;
}

sealed class GlTexture : IGpuTexture
{
    readonly GL _gl;
    public uint Handle { get; }
    public uint Framebuffer { get; set; }
    public int Width { get; }
    public int Height { get; }
    public bool IsRenderTarget { get; }
    public bool IsDisposed { get; private set; }

    public GlTexture(GL gl, uint handle, int width, int height, bool rt)
    {
        _gl = gl;
        Handle = handle;
        Width = width;
        Height = height;
        IsRenderTarget = rt;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _gl.DeleteTexture(Handle);
        if (Framebuffer != 0)
            _gl.DeleteFramebuffer(Framebuffer);
    }
}

sealed class GlSurface : IGpuSurface
{
    public int Width { get; }
    public int Height { get; }
    public bool IsDisposed { get; private set; }
    public IGpuTexture? Texture { get; }
    public uint Framebuffer { get; }

    public GlSurface(GlTexture texture, uint framebuffer)
    {
        Texture = texture;
        Width = texture.Width;
        Height = texture.Height;
        Framebuffer = framebuffer;
    }

    public void Dispose() => IsDisposed = true;
}
