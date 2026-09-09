namespace Crystal.Audio.Backends;

/// <summary>Headless no-op. Decodes nothing; PlayWav records a skip so --headless never opens a device.</summary>
public sealed class NullAudio : IAudio
{
    public AudioBackendKind Kind => AudioBackendKind.Null;
    public string BackendName => "Null (headless)";
    public bool IsHeadless => true;
    public bool PlayOk { get; private set; }
    public string? LastError { get; private set; }
    public string? LastFile { get; private set; }
    public int SkipCount { get; private set; }

    public bool PlayWav(string path)
    {
        LastFile = path;
        SkipCount++;
        PlayOk = false;
        LastError = "skipped (null backend)";
        return false;
    }

    public void Dispose() { }
}
