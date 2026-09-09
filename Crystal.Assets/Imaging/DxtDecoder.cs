namespace Crystal.Assets.Imaging;

public enum DxtFormat
{
    Dxt1 = 1,
    Dxt3 = 3,
    Dxt5 = 5
}

/// <summary>
/// Software DXT1/3/5 → BGRA8888. Used by WTL v2 (after zlib) and as a shared block decoder.
/// Does not invent texels: unknown formats throw; short payloads yield a partial decode.
/// </summary>
public static class DxtDecoder
{
    public static byte[] Decompress(ReadOnlySpan<byte> blocks, int width, int height, DxtFormat format)
    {
        if (width <= 0 || height <= 0)
            return Array.Empty<byte>();

        int w = (width + 3) & ~3;
        int h = (height + 3) & ~3;
        var bgra = new byte[w * h * 4];
        int blockSize = format == DxtFormat.Dxt1 ? 8 : 16;
        int blocksX = w / 4;
        int blocksY = h / 4;
        int src = 0;

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                if (src + blockSize > blocks.Length)
                    return Crop(bgra, w, h, width, height);

                DecodeBlock(blocks.Slice(src, blockSize), bgra, w, bx * 4, by * 4, format);
                src += blockSize;
            }
        }

        return Crop(bgra, w, h, width, height);
    }

    public static byte[] Crop(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        if (dstW <= 0 || dstH <= 0)
            return Array.Empty<byte>();
        if (srcW == dstW && srcH == dstH)
            return src;

        var dst = new byte[dstW * dstH * 4];
        int copyW = Math.Min(dstW, srcW);
        int copyH = Math.Min(dstH, srcH);
        for (int y = 0; y < copyH; y++)
            Buffer.BlockCopy(src, y * srcW * 4, dst, y * dstW * 4, copyW * 4);
        return dst;
    }

    static void DecodeBlock(ReadOnlySpan<byte> block, byte[] dest, int destW, int x, int y, DxtFormat format)
    {
        Span<byte> colors = stackalloc byte[16];
        Span<byte> alphas = stackalloc byte[16];

        int colorOff = format == DxtFormat.Dxt1 ? 0 : 8;
        UnpackColors(block.Slice(colorOff, 8), colors, interpolate4: format != DxtFormat.Dxt1 || Color565(block, colorOff) > Color565(block, colorOff + 2));

        if (format == DxtFormat.Dxt1)
        {
            bool opaque = Color565(block, 0) > Color565(block, 2);
            if (!opaque)
            {
                // c3 is transparent black
                colors[12] = 0;
                colors[13] = 0;
                colors[14] = 0;
                colors[15] = 0;
            }
            for (int i = 0; i < 16; i++)
                alphas[i] = 255;
        }
        else if (format == DxtFormat.Dxt3)
        {
            for (int i = 0; i < 8; i++)
            {
                byte packed = block[i];
                alphas[i * 2] = (byte)((packed & 0x0F) * 17);
                alphas[i * 2 + 1] = (byte)((packed >> 4) * 17);
            }
        }
        else
        {
            UnpackDxt5Alphas(block, alphas);
        }

        int lookup = colorOff + 4;
        for (int py = 0; py < 4; py++)
        {
            byte packed = block[lookup + py];
            for (int px = 0; px < 4; px++)
            {
                int idx = (packed >> (px * 2)) & 0x3;
                int dx = x + px;
                int dy = y + py;
                if ((uint)dx >= (uint)destW)
                    continue;
                int destIndex = (dy * destW + dx) * 4;
                if ((uint)destIndex + 4 > (uint)dest.Length)
                    continue;
                int ci = idx * 4;
                dest[destIndex] = colors[ci];
                dest[destIndex + 1] = colors[ci + 1];
                dest[destIndex + 2] = colors[ci + 2];
                dest[destIndex + 3] = format == DxtFormat.Dxt1 ? colors[ci + 3] : alphas[py * 4 + px];
            }
        }
    }

    static int Color565(ReadOnlySpan<byte> block, int offset)
        => block[offset] | (block[offset + 1] << 8);

    static void UnpackColors(ReadOnlySpan<byte> block, Span<byte> colors, bool interpolate4)
    {
        Unpack565(block, 0, colors, 0);
        Unpack565(block, 2, colors, 4);

        if (interpolate4)
        {
            for (int i = 0; i < 3; i++)
            {
                int c0 = colors[i];
                int c1 = colors[4 + i];
                colors[8 + i] = (byte)((2 * c0 + c1) / 3);
                colors[12 + i] = (byte)((c0 + 2 * c1) / 3);
            }
            colors[11] = 255;
            colors[15] = 255;
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                int c0 = colors[i];
                int c1 = colors[4 + i];
                colors[8 + i] = (byte)((c0 + c1) / 2);
                colors[12 + i] = 0;
            }
            colors[11] = 255;
            colors[15] = 0;
        }
    }

    static void Unpack565(ReadOnlySpan<byte> packed, int srcOffset, Span<byte> colour, int dst)
    {
        int value = packed[srcOffset] | (packed[srcOffset + 1] << 8);
        byte red = (byte)((value >> 11) & 0x1F);
        byte green = (byte)((value >> 5) & 0x3F);
        byte blue = (byte)(value & 0x1F);
        colour[dst] = (byte)((blue << 3) | (blue >> 2));
        colour[dst + 1] = (byte)((green << 2) | (green >> 4));
        colour[dst + 2] = (byte)((red << 3) | (red >> 2));
        colour[dst + 3] = 255;
    }

    static void UnpackDxt5Alphas(ReadOnlySpan<byte> block, Span<byte> alphas)
    {
        int a0 = block[0];
        int a1 = block[1];
        Span<byte> table = stackalloc byte[8];
        table[0] = (byte)a0;
        table[1] = (byte)a1;
        if (a0 > a1)
        {
            for (int i = 1; i <= 6; i++)
                table[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
        }
        else
        {
            for (int i = 1; i <= 4; i++)
                table[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
            table[6] = 0;
            table[7] = 255;
        }

        ulong bits = 0;
        for (int i = 0; i < 6; i++)
            bits |= (ulong)block[2 + i] << (8 * i);

        for (int i = 0; i < 16; i++)
        {
            int idx = (int)((bits >> (3 * i)) & 0x7);
            alphas[i] = table[idx];
        }
    }

    /// <summary>RGB565 + 2-bit indices, BGRA, matching LibraryEditor WTL v1 <c>DecompressBlock</c>.</summary>
    public static void DecompressWtlV1Block(ReadOnlySpan<byte> block, Span<byte> newPixels)
    {
        Span<byte> codes = stackalloc byte[16];
        int a = UnpackWtl565(block, 0, codes, 0);
        int b = UnpackWtl565(block, 2, codes, 4);

        for (int i = 0; i < 3; i++)
        {
            int c = codes[i];
            int d = codes[4 + i];
            if (a <= b)
            {
                codes[8 + i] = (byte)((c + d) / 2);
                codes[12 + i] = 0;
            }
            else
            {
                codes[8 + i] = (byte)((2 * c + d) / 3);
                codes[12 + i] = (byte)((c + 2 * d) / 3);
            }
        }

        codes[11] = 255;
        codes[15] = a <= b ? (byte)0 : (byte)255;
        for (int i = 0; i < 4; i++)
        {
            if (codes[i * 4] == 0 && codes[i * 4 + 1] == 0 && codes[i * 4 + 2] == 0 && codes[i * 4 + 3] == 255)
            {
                codes[i * 4] = 1;
                codes[i * 4 + 1] = 1;
                codes[i * 4 + 2] = 1;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            byte packed = block[4 + i];
            for (int px = 0; px < 4; px++)
            {
                int idx = (packed >> (px * 2)) & 0x3;
                int dest = (i * 4 + px) * 4;
                int src = idx * 4;
                newPixels[dest] = codes[src];
                newPixels[dest + 1] = codes[src + 1];
                newPixels[dest + 2] = codes[src + 2];
                newPixels[dest + 3] = codes[src + 3];
            }
        }
    }

    static int UnpackWtl565(ReadOnlySpan<byte> packed, int srcOffset, Span<byte> colour, int dst)
    {
        int value = packed[srcOffset] | (packed[srcOffset + 1] << 8);
        byte red = (byte)((value >> 11) & 0x1F);
        byte green = (byte)((value >> 5) & 0x3F);
        byte blue = (byte)(value & 0x1F);
        colour[dst] = (byte)((blue << 3) | (blue >> 2));
        colour[dst + 1] = (byte)((green << 2) | (green >> 4));
        colour[dst + 2] = (byte)((red << 3) | (red >> 2));
        colour[dst + 3] = 255;
        return value;
    }
}
