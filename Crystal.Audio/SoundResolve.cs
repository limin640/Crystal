namespace Crystal.Audio;

public static class SoundResolve
{
    public static string FixtureWav =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Tools", "Crystal.Audio", "fixtures", "tone.wav"));

    /// <summary>
    /// Prefer an operator Sound pack (--sound / CRYSTAL_SOUND). Fall back to the Tools fixture.
    /// Never invent audio; missing pack is not an error here.
    /// </summary>
    public static string Resolve(string? cliPath, string? extraSearchRoot = null)
    {
        foreach (string? candidate in new[]
                 {
                     cliPath,
                     Environment.GetEnvironmentVariable("CRYSTAL_SOUND"),
                     extraSearchRoot
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (File.Exists(candidate) && LooksLikeWav(candidate))
                return Path.GetFullPath(candidate);
            if (Directory.Exists(candidate))
            {
                string? found = FirstPackWav(candidate);
                if (found != null)
                    return found;
            }
        }

        foreach (string probe in FixtureCandidates())
        {
            if (File.Exists(probe))
                return Path.GetFullPath(probe);
        }

        return Path.GetFullPath(Path.Combine("Tools", "Crystal.Audio", "fixtures", "tone.wav"));
    }

    static IEnumerable<string> FixtureCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "fixtures", "tone.wav");
        yield return Path.Combine(AppContext.BaseDirectory, "Tools", "Crystal.Audio", "fixtures", "tone.wav");
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6 && dir != null; i++)
        {
            yield return Path.Combine(dir, "Tools", "Crystal.Audio", "fixtures", "tone.wav");
            dir = Directory.GetParent(dir)?.FullName;
        }
        yield return Path.Combine(Environment.CurrentDirectory, "Tools", "Crystal.Audio", "fixtures", "tone.wav");
    }

    static string? FirstPackWav(string dir)
    {
        string[] preferred = { "000-0.wav", "000-0.WAV", "001-0.wav", "100-0.wav" };
        foreach (string name in preferred)
        {
            string p = Path.Combine(dir, name);
            if (File.Exists(p)) return Path.GetFullPath(p);
        }
        return Directory.EnumerateFiles(dir, "*.wav", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(dir, "*.WAV", SearchOption.TopDirectoryOnly))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    static bool LooksLikeWav(string path)
        => path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
}
