namespace Crystal.Assets.Imaging;

/// <summary>
/// GPU-friendly BC3/DXT5 compressor (range-fit). Emits a raw BC3 blob plus a tiny header
/// so the Linux renderer can upload compressed atlases later without inventing texels.
/// </summary>
public static class Bc3Compressor
{
    public const uint Magic = 0x33434243; // "CBC3"

    public static byte[] CompressAtlas(int width, int height, byte[] bgra)
    {
        int bw = (width + 3) / 4;
        int bh = (height + 3) / 4;
        var output = new byte[16 + bw * bh * 16];
        BitConverter.GetBytes(Magic).CopyTo(output, 0);
        BitConverter.GetBytes(width).CopyTo(output, 4);
        BitConverter.GetBytes(height).CopyTo(output, 8);
        BitConverter.GetBytes(16).CopyTo(output, 12);

        int dest = 16;
        var block = new byte[16 * 4];
        for (int by = 0; by < bh; by++)
        {
            for (int bx = 0; bx < bw; bx++)
            {
                GatherBlock(bgra, width, height, bx * 4, by * 4, block);
                EncodeBlock(block, output, dest);
                dest += 16;
            }
        }

        return output;
    }

    static void GatherBlock(byte[] bgra, int width, int height, int x0, int y0, byte[] block)
    {
        for (int y = 0; y < 4; y++)
        {
            int sy = Math.Min(height - 1, y0 + y);
            for (int x = 0; x < 4; x++)
            {
                int sx = Math.Min(width - 1, x0 + x);
                int src = (sy * width + sx) * 4;
                int dst = (y * 4 + x) * 4;
                block[dst] = bgra[src];
                block[dst + 1] = bgra[src + 1];
                block[dst + 2] = bgra[src + 2];
                block[dst + 3] = bgra[src + 3];
            }
        }
    }

    static void EncodeBlock(byte[] block, byte[] dest, int offset)
    {
        EncodeAlpha(block, dest, offset);
        EncodeColor(block, dest, offset + 8);
    }

    static void EncodeAlpha(byte[] block, byte[] dest, int offset)
    {
        byte min = 255, max = 0;
        for (int i = 0; i < 16; i++)
        {
            byte a = block[i * 4 + 3];
            if (a < min) min = a;
            if (a > max) max = a;
        }

        dest[offset] = max;
        dest[offset + 1] = min;

        ulong indices = 0;
        int range = Math.Max(1, max - min);
        for (int i = 0; i < 16; i++)
        {
            int t = (block[i * 4 + 3] - min) * 7 / range;
            int idx = 7 - Math.Clamp(t, 0, 7);
            if (idx == 0) idx = 0;
            else if (idx == 7) idx = 1;
            else idx = idx + 1;
            indices |= (ulong)idx << (i * 3);
        }

        dest[offset + 2] = (byte)indices;
        dest[offset + 3] = (byte)(indices >> 8);
        dest[offset + 4] = (byte)(indices >> 16);
        dest[offset + 5] = (byte)(indices >> 24);
        dest[offset + 6] = (byte)(indices >> 32);
        dest[offset + 7] = (byte)(indices >> 40);
    }

    static void EncodeColor(byte[] block, byte[] dest, int offset)
    {
        int minL = int.MaxValue, maxL = int.MinValue;
        int minI = 0, maxI = 0;
        for (int i = 0; i < 16; i++)
        {
            int b = block[i * 4], g = block[i * 4 + 1], r = block[i * 4 + 2];
            int l = r * 2 + g * 3 + b;
            if (l < minL) { minL = l; minI = i; }
            if (l > maxL) { maxL = l; maxI = i; }
        }

        ushort c0 = To565(block, maxI * 4);
        ushort c1 = To565(block, minI * 4);
        if (c0 == c1) c0 = (ushort)(c0 == 0 ? 1 : c0);

        dest[offset] = (byte)c0;
        dest[offset + 1] = (byte)(c0 >> 8);
        dest[offset + 2] = (byte)c1;
        dest[offset + 3] = (byte)(c1 >> 8);

        int r0 = (c0 >> 11) * 255 / 31, g0 = ((c0 >> 5) & 63) * 255 / 63, b0 = (c0 & 31) * 255 / 31;
        int r1 = (c1 >> 11) * 255 / 31, g1 = ((c1 >> 5) & 63) * 255 / 63, b1 = (c1 & 31) * 255 / 31;

        uint bits = 0;
        for (int i = 0; i < 16; i++)
        {
            int b = block[i * 4], g = block[i * 4 + 1], r = block[i * 4 + 2];
            int d0 = Dist(r, g, b, r0, g0, b0);
            int d1 = Dist(r, g, b, r1, g1, b1);
            int d2 = Dist(r, g, b, (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3);
            int d3 = Dist(r, g, b, (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3);
            int idx = 0, best = d0;
            if (d2 < best) { best = d2; idx = 2; }
            if (d3 < best) { best = d3; idx = 3; }
            if (d1 < best) idx = 1;
            bits |= (uint)idx << (i * 2);
        }

        dest[offset + 4] = (byte)bits;
        dest[offset + 5] = (byte)(bits >> 8);
        dest[offset + 6] = (byte)(bits >> 16);
        dest[offset + 7] = (byte)(bits >> 24);
    }

    static ushort To565(byte[] block, int offset)
    {
        int r = block[offset + 2] >> 3;
        int g = block[offset + 1] >> 2;
        int b = block[offset] >> 3;
        return (ushort)((r << 11) | (g << 5) | b);
    }

    static int Dist(int r, int g, int b, int r2, int g2, int b2)
    {
        int dr = r - r2, dg = g - g2, db = b - b2;
        return dr * dr + dg * dg + db * db;
    }
}
