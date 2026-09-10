using Crystal.Graphics.Backends;
using Silk.NET.OpenGL;

namespace Crystal.Graphics;

public static class RendererFactory
{
    public static IRenderer CreateNull(int width = 1024, int height = 768)
        => new NullRenderer(width, height);

    public static IRenderer CreateOpenGL(GL gl, int width, int height)
        => new OpenGLRenderer(gl, width, height);

    public static IRenderer Create(RendererBackendKind kind, GL? gl = null, int width = 1024, int height = 768)
    {
        return kind switch
        {
            RendererBackendKind.OpenGL when gl != null => CreateOpenGL(gl, width, height),
            RendererBackendKind.OpenGL => throw new InvalidOperationException("OpenGL backend requires a live GL context."),
            RendererBackendKind.SlimDX => throw new InvalidOperationException("SlimDX backend is constructed from the Windows Client (SlimDXRenderer)."),
            _ => CreateNull(width, height)
        };
    }
}
