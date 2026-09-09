namespace Crystal.Assets;

/// <summary>
/// One image slot from a library. Pixels are BGRA8888, top-down, when decode succeeded.
/// Blank slots (0x0) are valid parsed images, not missing art.
/// </summary>
public sealed class ParsedImage
{
    public int Index { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public int ShadowX { get; init; }
    public int ShadowY { get; init; }
    public byte Shadow { get; init; }
    public bool HasMask { get; init; }
    public bool IsBlank { get; init; }

    /// <summary>BGRA8888 pixels, length = Width * Height * 4. Null if header parsed but pixels were not decoded, or after <see cref="ReleasePixels"/>.</summary>
    public byte[]? Bgra { get; set; }

    bool _decoded;

    /// <summary>True once pixels were decoded, even after they are released to keep a large Data tree streaming.</summary>
    public bool Decoded
    {
        get => _decoded || Bgra is { Length: > 0 };
        set => _decoded = value;
    }

    public void ReleasePixels()
    {
        if (Bgra is { Length: > 0 })
            _decoded = true;
        Bgra = null;
    }
}
