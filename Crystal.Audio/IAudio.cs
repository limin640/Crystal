namespace Crystal.Audio;

public enum AudioBackendKind
{
    Null = 0,
    OpenAL = 1,
    NAudio = 2
}

/// <summary>
/// Batched one-shot playback. Linux uses Silk.NET OpenAL; Windows Client keeps NAudio
/// <c>SoundManager</c> until GameScene folds onto this interface.
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
}
