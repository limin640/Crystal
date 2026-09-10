using System.IO.Compression;
using Crystal.Assets.Parsers;

namespace Crystal.Assets.SampleData;

/// <summary>
/// Writes a miniature Crystal Data tree with real library bytes (not game art).
/// Used to exercise full-tree enumeration + coverage on Linux without a client pack.
/// </summary>
public static class SampleDataWriter
{
    public static string Write(string dataRoot)
    {
        Directory.CreateDirectory(dataRoot);

        WriteLib(Path.Combine(dataRoot, "Prguse.Lib"), 4, 8);
        WriteLib(Path.Combine(dataRoot, "Title.Lib"), 2, 16);
        // WinForms Libraries.MiniMap / MagIcon live at DataPath + "MMap" / "MagIcon" (.Lib).
        // Synthetic checkers — not game art. Generate under /tmp via init-sample; do not vendor.
        WriteLib(Path.Combine(dataRoot, "MMap.Lib"), 4, 32);
        WriteLib(Path.Combine(dataRoot, "MagIcon.Lib"), 4, 16);
        WriteLib(Path.Combine(dataRoot, "Map", "WemadeMir2", "Tiles.Lib"), 3, 48);
        WriteLib(Path.Combine(dataRoot, "Monster", "000.Lib"), 2, 32);
        WriteLib(Path.Combine(dataRoot, "CArmour", "00.Lib"), 1, 24);
        WriteWil(Path.Combine(dataRoot, "Extra", "sample.Wil"), 2, 8);
        WriteWzl(Path.Combine(dataRoot, "Extra", "sample.Wzl"), 1, 8);
        WriteWtlV1(Path.Combine(dataRoot, "Extra", "sample.Wtl"), 2, 4);
        WriteWtlV2(Path.Combine(dataRoot, "Extra", "dxt-v2.Wtl"), 1, 4);

        Directory.CreateDirectory(Path.Combine(dataRoot, "Extra"));
        File.WriteAllBytes(Path.Combine(dataRoot, "Extra", "broken.Lib"), new byte[] { 1, 0, 0, 0 });
        File.WriteAllBytes(Path.Combine(dataRoot, "Extra", "orphan.Wil"), new byte[60]);

        return dataRoot;
    }

    public static void WriteLib(string path, int count, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var headers = new List<byte[]>();
        for (int i = 0; i < count; i++)
        {
            byte[] bgra = SolidBgra(size, size, (byte)(40 + i * 40), (byte)(80 + i * 20), (byte)(160 - i * 10), 255);
            byte[] gz = MLibParser.DeflateGzip(bgra);
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((short)size);
            w.Write((short)size);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((byte)0);
            w.Write(gz.Length);
            w.Write(gz);
            headers.Add(ms.ToArray());
        }

        int indexBase = 4 + 4 + 4 + count * 4;
        int running = 0;
        var indices = new int[count];
        for (int i = 0; i < count; i++)
        {
            indices[i] = indexBase + running;
            running += headers[i].Length;
        }

        int frameSeek = indexBase + running;

        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);
        writer.Write(3); // version
        writer.Write(count);
        writer.Write(frameSeek);
        foreach (int idx in indices)
            writer.Write(idx);
        foreach (byte[] blob in headers)
            writer.Write(blob);
        writer.Write(0); // no frames
    }

    static void WriteWil(string path, int count, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string wix = Path.ChangeExtension(path, ".Wix");

        using var wil = File.Create(path);
        using var w = new BinaryWriter(wil);

        // 48-byte header, then palette length / skip / version / 255 colors.
        w.Write(new byte[48]);
        w.Write(256);
        w.Write(0);
        w.Write(0); // version 0
        for (int i = 1; i < 256; i++)
            w.Write(unchecked((int)0xFF000000) | (i << 16) | (i << 8) | i);

        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = (int)wil.Position;
            w.Write((short)size);
            w.Write((short)size);
            w.Write((short)0);
            w.Write((short)0);
            // type 0 8-bit, bottom-up palette indices
            for (int p = 0; p < size * size; p++)
                w.Write((byte)(20 + i * 8 + (p % 8)));
        }

        using var idx = File.Create(wix);
        using var iw = new BinaryWriter(idx);
        iw.Write(new byte[48]);
        foreach (int o in offsets)
            iw.Write(o);
    }

    static void WriteWzl(string path, int count, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string wzx = Path.ChangeExtension(path, ".Wzx");

        using var wzl = File.Create(path);
        using var w = new BinaryWriter(wzl);
        w.Write(new byte[16]);

        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = (int)wzl.Position;
            byte[] raw = new byte[size * size];
            Array.Fill(raw, (byte)(30 + i));
            byte[] zlib = DeflateZlib(raw);

            w.Write((byte)0); // 8-bit
            w.Write(new byte[3]);
            w.Write((short)size);
            w.Write((short)size);
            w.Write((short)0);
            w.Write((short)0);
            w.Write(zlib.Length);
            w.Write(zlib);
        }

        using var idx = File.Create(wzx);
        using var iw = new BinaryWriter(idx);
        iw.Write(new byte[48]);
        foreach (int o in offsets)
            iw.Write(o);
    }

    /// <summary>
    /// Writes every Crystal catalog slot as a 1-image 4×4 .Lib. Synthetic stress tree only —
    /// do not vendor a real client pack. Intended for /tmp, not git.
    /// </summary>
    public static string WriteStress(string dataRoot)
    {
        Directory.CreateDirectory(dataRoot);
        foreach (string relative in CrystalCatalog.ExpectedRelativePaths())
            WriteLib(Path.Combine(dataRoot, relative.Replace('/', Path.DirectorySeparatorChar)), 1, 4);

        for (int i = 0; i < 32; i++)
            WriteLib(Path.Combine(dataRoot, "Monster", $"{i:D3}.Lib"), 1, 4);
        for (int i = 0; i < 8; i++)
            WriteLib(Path.Combine(dataRoot, "CArmour", $"{i:D2}.Lib"), 1, 4);

        return dataRoot;
    }

    static void WriteWtlV1(string path, int count, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        byte[] payload = EncodeWtlV1Payload(size);
        int imageBytes = 16 + payload.Length;
        int indexOffset = 32 + count * 4;

        using var file = File.Create(path);
        using var w = new BinaryWriter(file);
        w.Write((short)0);
        var ver = new byte[20];
        System.Text.Encoding.ASCII.GetBytes("ILIB v1.0-SAMPLE").CopyTo(ver, 0);
        w.Write(ver);
        w.Write(new byte[6]);
        w.Write(count);

        int cursor = indexOffset;
        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = cursor;
            cursor += imageBytes;
        }
        foreach (int o in offsets)
            w.Write(o);

        for (int i = 0; i < count; i++)
        {
            w.Write((short)size);
            w.Write((short)size);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((short)0);
            int length = payload.Length;
            w.Write((byte)(length & 0xFF));
            w.Write((byte)((length >> 8) & 0xFF));
            w.Write((byte)((length >> 16) & 0xFF));
            w.Write((byte)0); // shadow
            w.Write(payload);
        }
    }

    static void WriteWtlV2(string path, int count, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        byte[] dxt = EncodeSolidDxt1((byte)(31), 0, 0);
        byte[] zlib = DeflateZlib(dxt);
        int padded = zlib.Length + (zlib.Length % 4 == 0 ? 0 : 4 - zlib.Length % 4);
        int imageHeader = 12 + 4 + 4; // dims + types + length
        int imageBytes = imageHeader + padded;
        int indexBase = 32;

        using var file = File.Create(path);
        using var w = new BinaryWriter(file);
        w.Write((short)0);
        var ver = new byte[20];
        System.Text.Encoding.ASCII.GetBytes("ILIB v2.0-WEMADE").CopyTo(ver, 0);
        w.Write(ver);
        w.Write(new byte[6]);
        w.Write(count);

        int cursor = indexBase;
        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = cursor;
            cursor += imageBytes;
        }

        for (int i = 0; i < count; i++)
        {
            w.Write((short)size);
            w.Write((short)size);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((byte)0);
            w.Write((byte)1); // DXT1
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write(zlib.Length);
            w.Write(zlib);
            for (int p = zlib.Length; p < padded; p++)
                w.Write((byte)0);
        }

        foreach (int o in offsets)
            w.Write(o);
    }

    static byte[] EncodeWtlV1Payload(int size)
    {
        // 8 count bytes (skip 0, write 1 block) + one 8-byte DXT-like block.
        // Enough for a 4×4 image; larger sizes still prove the decoder path.
        _ = size;
        var payload = new byte[16];
        payload[1] = 1;
        byte[] block = EncodeSolidDxt1(31, 0, 0);
        Buffer.BlockCopy(block, 0, payload, 8, 8);
        return payload;
    }

    static byte[] EncodeSolidDxt1(byte r5, byte g6, byte b5)
    {
        ushort c0 = (ushort)((r5 << 11) | (g6 << 5) | b5);
        ushort c1 = (ushort)((Math.Max(0, r5 - 8) << 11) | (g6 << 5) | b5);
        if (c0 <= c1)
            c0 = (ushort)(c1 + 1);
        return new byte[]
        {
            (byte)(c0 & 0xFF), (byte)(c0 >> 8),
            (byte)(c1 & 0xFF), (byte)(c1 >> 8),
            0, 0, 0, 0
        };
    }

    static byte[] SolidBgra(int w, int h, byte b, byte g, byte r, byte a)
    {
        var data = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            data[i * 4] = b;
            data[i * 4 + 1] = g;
            data[i * 4 + 2] = r;
            data[i * 4 + 3] = a;
        }
        // checker so packing/UV can be visually verified
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (((x / 2) + (y / 2)) % 2 == 0) continue;
            int i = (y * w + x) * 4;
            data[i] = (byte)(b / 2);
            data[i + 1] = (byte)(g / 2);
            data[i + 2] = (byte)(r / 2);
        }
        return data;
    }

    static byte[] DeflateZlib(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw, 0, raw.Length);
        return output.ToArray();
    }
}
