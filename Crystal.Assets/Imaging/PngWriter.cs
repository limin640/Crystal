using System.IO.Compression;

namespace Crystal.Assets.Imaging;

/// <summary>
/// Minimal PNG writer (8-bit RGBA). No System.Drawing dependency.
/// </summary>
public static class PngWriter
{
    static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static void WriteRgba(string path, int width, int height, byte[] bgra)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        stream.Write(Signature);

        WriteChunk(stream, "IHDR", Ihdr(width, height));
        WriteChunk(stream, "IDAT", Idat(width, height, bgra));
        WriteChunk(stream, "IEND", Array.Empty<byte>());
    }

    static byte[] Ihdr(int width, int height)
    {
        var data = new byte[13];
        WriteBe32(data, 0, width);
        WriteBe32(data, 4, height);
        data[8] = 8;
        data[9] = 6; // RGBA
        return data;
    }

    static byte[] Idat(int width, int height, byte[] bgra)
    {
        int stride = width * 4;
        var raw = new byte[(stride + 1) * height];
        int dst = 0;
        for (int y = 0; y < height; y++)
        {
            raw[dst++] = 0;
            int src = y * stride;
            for (int x = 0; x < width; x++)
            {
                // BGRA → RGBA
                raw[dst++] = bgra[src + 2];
                raw[dst++] = bgra[src + 1];
                raw[dst++] = bgra[src];
                raw[dst++] = bgra[src + 3];
                src += 4;
            }
        }

        using var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x01);
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(raw, 0, raw.Length);

        uint adler = Adler32(raw);
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);
        return output.ToArray();
    }

    static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var len = new byte[4];
        WriteBe32(len, 0, data.Length);
        stream.Write(len);
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = Crc32(typeBytes, data);
        var crcBytes = new byte[4];
        WriteBe32(crcBytes, 0, (int)crc);
        stream.Write(crcBytes);
    }

    static void WriteBe32(byte[] dest, int offset, int value)
    {
        dest[offset] = (byte)(value >> 24);
        dest[offset + 1] = (byte)(value >> 16);
        dest[offset + 2] = (byte)(value >> 8);
        dest[offset + 3] = (byte)value;
    }

    static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (byte v in data)
        {
            a = (a + v) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte v in type) crc = CrcTable[(crc ^ v) & 0xFF] ^ (crc >> 8);
        foreach (byte v in data) crc = CrcTable[(crc ^ v) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    static readonly uint[] CrcTable = MakeCrcTable();

    static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
