namespace Crystal.Audio;

/// <summary>Minimal PCM WAV reader (fmt + data chunks). No invented samples — file must exist.</summary>
public readonly struct PcmWav
{
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public int BitsPerSample { get; init; }
    public byte[] Data { get; init; }
    public int DurationMs => SampleRate <= 0 || Channels <= 0 || BitsPerSample <= 0
        ? 0
        : (int)(Data.Length * 1000.0 / (SampleRate * Channels * (BitsPerSample / 8.0)));
}

public static class WavFile
{
    public static bool TryLoadPcm(string path, out PcmWav wav, out string? error)
    {
        wav = default;
        error = null;
        if (!File.Exists(path))
        {
            error = "missing " + path;
            return false;
        }

        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            if (new string(br.ReadChars(4)) != "RIFF") { error = "not RIFF"; return false; }
            br.ReadInt32();
            if (new string(br.ReadChars(4)) != "WAVE") { error = "not WAVE"; return false; }

            int sampleRate = 0, channels = 0, bits = 0;
            byte[]? data = null;
            while (fs.Position + 8 <= fs.Length)
            {
                string id = new string(br.ReadChars(4));
                int size = br.ReadInt32();
                if (size < 0 || fs.Position + size > fs.Length)
                {
                    error = "truncated chunk " + id;
                    return false;
                }

                if (id == "fmt ")
                {
                    short format = br.ReadInt16();
                    channels = br.ReadInt16();
                    sampleRate = br.ReadInt32();
                    br.ReadInt32();
                    br.ReadInt16();
                    bits = br.ReadInt16();
                    int consumed = 16;
                    if (size > consumed)
                        br.ReadBytes(size - consumed);
                    if (format != 1)
                    {
                        error = "not PCM format=" + format;
                        return false;
                    }
                }
                else if (id == "data")
                {
                    data = br.ReadBytes(size);
                    if ((size & 1) == 1 && fs.Position < fs.Length)
                        br.ReadByte();
                }
                else
                {
                    br.ReadBytes(size);
                    if ((size & 1) == 1 && fs.Position < fs.Length)
                        br.ReadByte();
                }
            }

            if (data == null || data.Length == 0) { error = "no data chunk"; return false; }
            if (sampleRate <= 0 || channels is < 1 or > 2 || bits is not (8 or 16))
            {
                error = $"unsupported fmt rate={sampleRate} ch={channels} bits={bits}";
                return false;
            }

            wav = new PcmWav
            {
                SampleRate = sampleRate,
                Channels = channels,
                BitsPerSample = bits,
                Data = data
            };
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
