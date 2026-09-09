using System.IO.Compression;

namespace Crystal.Assets.Imaging;

/// <summary>
/// Minimal 8-bit RGBA PNG reader matching <see cref="PngWriter"/>. No System.Drawing.
/// </summary>
public static class PngReader
{
    static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static (int Width, int Height, byte[] Bgra) ReadBgra(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> sig = stackalloc byte[8];
        if (stream.Read(sig) != 8 || !sig.SequenceEqual(Signature))
            throw new InvalidDataException($"Not a PNG: {path}");

        int width = 0, height = 0;
        byte bitDepth = 0, colorType = 0;
        using var idat = new MemoryStream();

        while (stream.Position + 8 <= stream.Length)
        {
            int length = ReadBe32(stream);
            string type = ReadType(stream);
            if (length < 0 || stream.Position + length + 4 > stream.Length)
                throw new InvalidDataException($"Truncated PNG chunk {type}");

            byte[] data = new byte[length];
            if (length > 0 && stream.Read(data, 0, length) != length)
                throw new InvalidDataException($"Truncated PNG chunk payload {type}");
            stream.Position += 4; // CRC

            switch (type)
            {
                case "IHDR":
                    if (data.Length < 13)
                        throw new InvalidDataException("Bad IHDR");
                    width = ReadBe32(data, 0);
                    height = ReadBe32(data, 4);
                    bitDepth = data[8];
                    colorType = data[9];
                    break;
                case "IDAT":
                    idat.Write(data, 0, data.Length);
                    break;
                case "IEND":
                    goto inflate;
            }
        }

        inflate:
        if (width <= 0 || height <= 0)
            throw new InvalidDataException("PNG missing IHDR");
        if (bitDepth != 8 || colorType != 6)
            throw new InvalidDataException($"Unsupported PNG {bitDepth}-bit type {colorType} (need 8-bit RGBA)");

        byte[] raw = InflateZlib(idat.ToArray());
        return (width, height, PaethToBgra(raw, width, height));
    }

    static byte[] PaethToBgra(byte[] raw, int width, int height)
    {
        int stride = width * 4;
        var bgra = new byte[stride * height];
        int src = 0;
        byte[] prev = new byte[stride];
        byte[] row = new byte[stride];

        for (int y = 0; y < height; y++)
        {
            if (src >= raw.Length)
                break;
            byte filter = raw[src++];
            int need = stride;
            if (src + need > raw.Length)
                need = Math.Max(0, raw.Length - src);
            Array.Clear(row);
            Buffer.BlockCopy(raw, src, row, 0, need);
            src += need;

            UndoFilter(filter, row, prev, stride);
            for (int x = 0; x < width; x++)
            {
                int s = x * 4;
                int d = (y * width + x) * 4;
                bgra[d] = row[s + 2];
                bgra[d + 1] = row[s + 1];
                bgra[d + 2] = row[s];
                bgra[d + 3] = row[s + 3];
            }
            Buffer.BlockCopy(row, 0, prev, 0, stride);
        }

        return bgra;
    }

    static void UndoFilter(byte filter, byte[] row, byte[] prev, int stride)
    {
        switch (filter)
        {
            case 0:
                return;
            case 1:
                for (int i = 4; i < stride; i++)
                    row[i] = (byte)(row[i] + row[i - 4]);
                return;
            case 2:
                for (int i = 0; i < stride; i++)
                    row[i] = (byte)(row[i] + prev[i]);
                return;
            case 3:
                for (int i = 0; i < stride; i++)
                {
                    int left = i >= 4 ? row[i - 4] : 0;
                    row[i] = (byte)(row[i] + ((left + prev[i]) / 2));
                }
                return;
            case 4:
                for (int i = 0; i < stride; i++)
                {
                    int left = i >= 4 ? row[i - 4] : 0;
                    int up = prev[i];
                    int upLeft = i >= 4 ? prev[i - 4] : 0;
                    row[i] = (byte)(row[i] + Paeth(left, up, upLeft));
                }
                return;
            default:
                throw new InvalidDataException($"Unsupported PNG filter {filter}");
        }
    }

    static byte Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return (byte)a;
        if (pb <= pc) return (byte)b;
        return (byte)c;
    }

    static byte[] InflateZlib(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    static int ReadBe32(Stream stream)
    {
        Span<byte> b = stackalloc byte[4];
        if (stream.Read(b) != 4)
            throw new EndOfStreamException();
        return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    }

    static int ReadBe32(byte[] data, int offset)
        => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    static string ReadType(Stream stream)
    {
        Span<byte> b = stackalloc byte[4];
        if (stream.Read(b) != 4)
            throw new EndOfStreamException();
        return System.Text.Encoding.ASCII.GetString(b);
    }
}
