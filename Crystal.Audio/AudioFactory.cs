using Crystal.Audio.Backends;

namespace Crystal.Audio;

public static class AudioFactory
{
    public static IAudio CreateNull() => new NullAudio();

    public static IAudio CreateOpenAL() => new OpenALAudio();

    /// <summary>Windows Client device. Linux PlayGate never calls this (Null / OpenAL).</summary>
    public static IAudio CreateNAudio() => new NAudioAudio();

    /// <summary>
    /// Headless CI uses Null (no device). <paramref name="forcePlay"/> selects OpenAL even without a window.
    /// Windows GameScene uses <see cref="CreateNAudio"/> from SoundManager, not this helper.
    /// </summary>
    public static IAudio Create(bool headless, bool forcePlay)
    {
        if (headless && !forcePlay)
            return CreateNull();
        return CreateOpenAL();
    }
}
