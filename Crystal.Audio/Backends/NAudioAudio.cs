using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Crystal.Audio.Backends;

/// <summary>
/// Windows NAudio host (WaveOutEvent + mixer). Same graph SoundManager used
/// before the IAudio fold. Linux never constructs this type (Null / OpenAL).
/// </summary>
public sealed class NAudioAudio : IAudio
{
    readonly Dictionary<string, CachedClip> _oneShots = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<int, LoopVoice> _loops = new();
    readonly WaveOutEvent _mixerOut;
    readonly MixingSampleProvider _mixer;
    LoopVoice? _music;
    float _sfxVol = 1f;
    float _musicVol = 1f;

    public AudioBackendKind Kind => AudioBackendKind.NAudio;
    public string BackendName => "NAudio WaveOutEvent";
    public bool IsHeadless => false;
    public bool PlayOk { get; private set; }
    public string? LastError { get; private set; }
    public string? LastFile { get; private set; }

    public NAudioAudio()
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
        {
            ReadFully = true
        };
        _mixerOut = new WaveOutEvent();
        _mixerOut.Init(_mixer);
        _mixerOut.Volume = _sfxVol;
        _mixerOut.Play();
    }

    public bool PlayWav(string path)
    {
        LastFile = path;
        PlayOk = false;
        LastError = null;
        if (!File.Exists(path))
        {
            LastError = "missing " + path;
            return false;
        }

        try
        {
            using var reader = new AudioFileReader(path);
            using var output = new WaveOutEvent();
            output.Init(reader);
            output.Volume = _sfxVol;
            output.Play();
            int wait = Math.Clamp((int)reader.TotalTime.TotalMilliseconds + 80, 50, 2000);
            var until = DateTime.UtcNow.AddMilliseconds(wait);
            while (DateTime.UtcNow < until && output.PlaybackState == PlaybackState.Playing)
                Thread.Sleep(10);
            PlayOk = true;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    public bool PlayOneShot(string path)
    {
        LastFile = path;
        LastError = null;
        if (!TryLoadClip(path, out var clip) || clip.Samples.Length == 0)
        {
            LastError = "missing or empty " + path;
            PlayOk = false;
            return false;
        }

        clip.LastPlayMs = Environment.TickCount64;
        _mixer.AddMixerInput(ConvertChannels(new ClipProvider(clip)));
        PlayOk = true;
        return true;
    }

    public void SetSfxVolume(float volume01)
    {
        _sfxVol = Math.Clamp(volume01, 0f, 1f);
        _mixerOut.Volume = _sfxVol;
        foreach (var loop in _loops.Values)
            loop.SetVolume(_sfxVol);
    }

    public void SetMusicVolume(float volume01)
    {
        _musicVol = Math.Clamp(volume01, 0f, 1f);
        _music?.SetVolume(_musicVol);
    }

    public bool PlayLoop(int key, string path, float volume01)
    {
        LastFile = path;
        StopLoop(key);
        var voice = LoopVoice.TryStart(path, volume01, loop: true);
        if (voice == null)
        {
            LastError = "missing " + path;
            return false;
        }

        _loops[key] = voice;
        PlayOk = true;
        return true;
    }

    public void StopLoop(int key)
    {
        if (_loops.Remove(key, out var voice))
            voice.Dispose();
    }

    public bool PlayMusic(string path, bool loop, float volume01)
    {
        LastFile = path;
        StopMusic();
        _music = LoopVoice.TryStart(path, volume01, loop);
        if (_music == null)
        {
            LastError = "missing " + path;
            return false;
        }

        PlayOk = true;
        return true;
    }

    public void StopMusic()
    {
        _music?.Dispose();
        _music = null;
    }

    public void PumpExpired(long nowMs, long ttlMs)
    {
        if (ttlMs <= 0) return;
        // Wall clock for cache TTL. Host may pass CMain.Time; clips stamp TickCount64.
        _ = nowMs;
        long now = Environment.TickCount64;
        var deadClips = _oneShots
            .Where(kv => now - kv.Value.LastPlayMs > ttlMs)
            .Select(kv => kv.Key)
            .ToList();
        foreach (string key in deadClips)
            _oneShots.Remove(key);

        var deadLoops = _loops
            .Where(kv => now - kv.Value.LastPlayMs > ttlMs)
            .Select(kv => kv.Key)
            .ToList();
        foreach (int key in deadLoops)
            StopLoop(key);
    }

    public void Dispose()
    {
        StopMusic();
        foreach (var loop in _loops.Values)
            loop.Dispose();
        _loops.Clear();
        _mixerOut.Dispose();
    }

    bool TryLoadClip(string path, out CachedClip clip)
    {
        if (_oneShots.TryGetValue(path, out clip!))
            return true;
        if (!File.Exists(path))
        {
            clip = new CachedClip(Array.Empty<float>(), WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            return false;
        }

        using var reader = new AudioFileReader(path);
        var samples = new List<float>((int)(reader.Length / 4));
        var buf = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
        int read;
        while ((read = reader.Read(buf, 0, buf.Length)) > 0)
            samples.AddRange(buf.Take(read));
        clip = new CachedClip(samples.ToArray(), reader.WaveFormat);
        _oneShots[path] = clip;
        return true;
    }

    ISampleProvider ConvertChannels(ISampleProvider input)
    {
        if (input.WaveFormat.Channels == _mixer.WaveFormat.Channels)
            return input;
        if (input.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
            return new MonoToStereoSampleProvider(input);
        throw new NotSupportedException($"channel count {input.WaveFormat.Channels} → {_mixer.WaveFormat.Channels}");
    }

    sealed class CachedClip
    {
        public float[] Samples { get; }
        public WaveFormat Format { get; }
        public long LastPlayMs { get; set; }

        public CachedClip(float[] samples, WaveFormat format)
        {
            Samples = samples;
            Format = format;
        }
    }

    sealed class ClipProvider : ISampleProvider
    {
        readonly CachedClip _clip;
        long _position;

        public ClipProvider(CachedClip clip) => _clip = clip;
        public WaveFormat WaveFormat => _clip.Format;

        public int Read(float[] buffer, int offset, int count)
        {
            long available = _clip.Samples.Length - _position;
            int copy = (int)Math.Min(available, count);
            if (copy > 0)
            {
                Array.Copy(_clip.Samples, _position, buffer, offset, copy);
                _position += copy;
            }
            return copy;
        }
    }

    sealed class LoopVoice : IDisposable
    {
        readonly WaveOutEvent _out = new();
        readonly AudioFileReader _reader;
        readonly bool _loop;
        bool _disposing;
        float _volume;

        public long LastPlayMs { get; private set; } = Environment.TickCount64;

        public static LoopVoice? TryStart(string path, float volume, bool loop)
        {
            if (!File.Exists(path)) return null;
            return new LoopVoice(path, volume, loop);
        }

        LoopVoice(string path, float volume, bool loop)
        {
            _loop = loop;
            _volume = Math.Clamp(volume, 0f, 1f);
            _reader = new AudioFileReader(path);
            _out.PlaybackStopped += (_, _) =>
            {
                if (_loop && !_disposing)
                    Play();
            };
            _out.Init(_reader);
            Play();
        }

        public void SetVolume(float volume01)
        {
            _volume = Math.Clamp(volume01, 0f, 1f);
            _out.Volume = _volume;
        }

        void Play()
        {
            LastPlayMs = Environment.TickCount64;
            if (_out.PlaybackState == PlaybackState.Playing)
                return;
            if (_out.PlaybackState == PlaybackState.Stopped)
            {
                try
                {
                    _reader.Seek(0, SeekOrigin.Begin);
                }
                catch
                {
                    try { _reader.Position = 0; } catch { /* keep current cursor */ }
                }
            }

            _out.Volume = _volume;
            _out.Play();
        }

        public void Dispose()
        {
            _disposing = true;
            _out.Dispose();
            _reader.Dispose();
        }
    }
}
