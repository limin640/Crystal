using Crystal.Audio.Backends;

namespace Crystal.Audio;

public static class AudioFactory
{
    public static IAudio CreateNull() => new NullAudio();

    public static IAudio CreateOpenAL() => new OpenALAudio();

    /// <summary>
    /// Headless CI uses Null (no device). <paramref name="forcePlay"/> selects OpenAL even without a window.
    /// </summary>
    public static IAudio Create(bool headless, bool forcePlay)
    {
        if (headless && !forcePlay)
            return CreateNull();
        return CreateOpenAL();
    }
}
