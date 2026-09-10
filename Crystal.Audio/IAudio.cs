namespace Crystal.Audio;

public enum AudioBackendKind
{
    Null = 0,
    OpenAL = 1,
    NAudio = 2
}

/// <summary>
/// Device-agnostic playback. Linux: Silk.NET OpenAL / Null.
/// Windows Client <c>SoundManager</c> calls through this via the NAudio backend
/// (same split as <c>IRenderer</c> / SlimDX).
/// </summary>
public interface IAudio : IDisposable
{
    AudioBackendKind Kind { get; }
    string BackendName { get; }
    bool IsHeadless { get; }
    bool PlayOk { get; }
    string? LastError { get; }
    string? LastFile { get; }

    /// <summary>Play a PCM WAV (Crystal Sound pack or the Tools fixture). Null backend records a skip.</summary>
    bool PlayWav(string path);

    /// <summary>Non-blocking overlapping one-shot. Default: <see cref="PlayWav"/>.</summary>
    bool PlayOneShot(string path) => PlayWav(path);

    void SetSfxVolume(float volume01) { }

    void SetMusicVolume(float volume01) { }

    bool PlayLoop(int key, string path, float volume01) => false;

    void StopLoop(int key) { }

    bool PlayMusic(string path, bool loop, float volume01) => false;

    void StopMusic() { }

    /// <summary>Drop idle cached clips / loops. <paramref name="nowMs"/> is host clock (CMain.Time on Windows).</summary>
    void PumpExpired(long nowMs, long ttlMs) { }
}
