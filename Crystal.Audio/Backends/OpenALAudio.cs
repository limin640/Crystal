using System.Runtime.InteropServices;
using Silk.NET.OpenAL;

namespace Crystal.Audio.Backends;

/// <summary>Silk.NET OpenAL (OpenAL Soft on Linux). Windows Client still uses NAudio.</summary>
public sealed class OpenALAudio : IAudio
{
    public AudioBackendKind Kind => AudioBackendKind.OpenAL;
    public string BackendName => "Silk.NET OpenAL";
    public bool IsHeadless => false;
    public bool PlayOk { get; private set; }
    public string? LastError { get; private set; }
    public string? LastFile { get; private set; }

    public bool PlayWav(string path)
    {
        LastFile = path;
        PlayOk = false;
        LastError = null;
        if (!WavFile.TryLoadPcm(path, out var wav, out var loadErr))
        {
            LastError = loadErr;
            return false;
        }

        EnsureSoftNullDriver();

        AL? al = null;
        ALContext? alc = null;
        unsafe
        {
            Device* device = null;
            Context* context = null;
            uint buffer = 0;
            uint source = 0;
            try
            {
                al = AL.GetApi(true);
                alc = ALContext.GetApi(true);
                device = alc.OpenDevice("");
                if (device == null)
                    device = alc.OpenDevice("OpenAL Soft");
                if (device == null)
                {
                    LastError = "alcOpenDevice failed (no OpenAL Soft device)";
                    return false;
                }

                context = alc.CreateContext(device, null);
                if (context == null || !alc.MakeContextCurrent(context))
                {
                    LastError = "alcCreateContext failed";
                    return false;
                }

                BufferFormat format = (wav.Channels, wav.BitsPerSample) switch
                {
                    (1, 8) => BufferFormat.Mono8,
                    (1, 16) => BufferFormat.Mono16,
                    (2, 8) => BufferFormat.Stereo8,
                    (2, 16) => BufferFormat.Stereo16,
                    _ => 0
                };
                if (format == 0)
                {
                    LastError = $"unsupported OpenAL format ch={wav.Channels} bits={wav.BitsPerSample}";
                    return false;
                }

                buffer = al.GenBuffer();
                source = al.GenSource();
                fixed (byte* p = wav.Data)
                    al.BufferData(buffer, format, p, wav.Data.Length, wav.SampleRate);
                al.SetSourceProperty(source, SourceInteger.Buffer, buffer);
                al.SetSourceProperty(source, SourceFloat.Gain, 0.4f);
                al.SourcePlay(source);

                int wait = Math.Clamp(wav.DurationMs + 80, 50, 2000);
                var until = DateTime.UtcNow.AddMilliseconds(wait);
                int state = (int)SourceState.Playing;
                while (DateTime.UtcNow < until)
                {
                    al.GetSourceProperty(source, GetSourceInteger.SourceState, out state);
                    if (state != (int)SourceState.Playing && state != (int)SourceState.Initial)
                        break;
                    Thread.Sleep(10);
                }

                PlayOk = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (al != null)
                    {
                        if (source != 0)
                        {
                            al.SourceStop(source);
                            al.DeleteSource(source);
                        }
                        if (buffer != 0)
                            al.DeleteBuffer(buffer);
                    }
                    if (alc != null)
                    {
                        alc.MakeContextCurrent(null);
                        if (context != null)
                            alc.DestroyContext(context);
                        if (device != null)
                            alc.CloseDevice(device);
                    }
                }
                catch
                {
                    // device teardown must not break headless hosts
                }
                al?.Dispose();
                alc?.Dispose();
            }
        }
    }

    public void Dispose() { }

    /// <summary>
    /// Soft reads drivers at native load. Point ALSOFT_CONF at a null-output
    /// config when the operator has not chosen a driver (no speaker on CI).
    /// </summary>
    public static void EnsureSoftNullDriver()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALSOFT_DRIVERS")))
            SetProcessEnv("ALSOFT_DRIVERS", "null");

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALSOFT_CONF")))
        {
            foreach (string probe in ConfCandidates())
            {
                if (!File.Exists(probe)) continue;
                SetProcessEnv("ALSOFT_CONF", Path.GetFullPath(probe));
                break;
            }
        }
    }

    // libopenal reads the libc environ, not only .NET's copy.
    static void SetProcessEnv(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            _ = setenv(name, value, 1);
    }

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
    static extern int setenv(string name, string value, int overwrite);

    static IEnumerable<string> ConfCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "alsoft-headless.conf");
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6 && dir != null; i++)
        {
            yield return Path.Combine(dir, "Crystal.Audio", "alsoft-headless.conf");
            dir = Directory.GetParent(dir)?.FullName;
        }
        yield return Path.Combine(Environment.CurrentDirectory, "Crystal.Audio", "alsoft-headless.conf");
    }
}
