using System.Collections.Concurrent;

namespace Crystal.Assets;

/// <summary>
/// Opens files under an operator Data tree. Linux packs often ship <c>mmap.Lib</c>
/// while WinForms asks for <c>MMap.Lib</c>. Try the exact path first, then a
/// case-fold match in the directory (and each path segment). Does not invent files.
/// </summary>
public static class DataPath
{
    static readonly ConcurrentDictionary<string, string?> Cache = new(StringComparer.Ordinal);

    /// <summary>Resolve <paramref name="relative"/> under <paramref name="root"/> (e.g. <c>MMap.Lib</c> or <c>Map/WemadeMir2/Tiles.Lib</c>).</summary>
    public static string? ResolveFile(string? root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relative))
            return null;
        if (!Directory.Exists(root))
            return null;

        string absRoot = Path.GetFullPath(root);
        string rel = relative.Replace('\\', '/').TrimStart('/');
        string key = absRoot + "\0" + rel;
        return Cache.GetOrAdd(key, _ => ResolveCore(absRoot, rel));
    }

    /// <summary>Resolve an already-combined path; exact <see cref="File.Exists"/> then case-fold the last segment.</summary>
    public static string? ResolveExisting(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (File.Exists(path))
            return Path.GetFullPath(path);

        string? dir = Path.GetDirectoryName(path);
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name))
            return null;
        return MatchFile(dir, name);
    }

    static string? ResolveCore(string root, string rel)
    {
        string[] parts = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        string current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            string want = parts[i];
            bool last = i == parts.Length - 1;
            if (last)
                return MatchFile(current, want);

            string exact = Path.Combine(current, want);
            if (Directory.Exists(exact))
            {
                current = exact;
                continue;
            }

            string? folded = MatchDir(current, want);
            if (folded == null)
                return null;
            current = folded;
        }

        return null;
    }

    static string? MatchFile(string dir, string want)
    {
        foreach (string candidate in FileCandidates(dir, want))
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        if (!Directory.Exists(dir))
            return null;

        string[] files = Directory.GetFiles(dir);
        foreach (string variant in FileNameVariants(want))
        {
            foreach (string file in files)
            {
                if (string.Equals(Path.GetFileName(file), variant, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(file);
            }
        }

        return null;
    }

    static IEnumerable<string> FileCandidates(string dir, string want)
    {
        yield return Path.Combine(dir, want);
        string stem = Path.GetFileNameWithoutExtension(want);
        yield return Path.Combine(dir, stem + ".Lib");
        yield return Path.Combine(dir, stem + ".lib");
        if (!string.Equals(stem, want, StringComparison.Ordinal))
            yield return Path.Combine(dir, stem);
    }

    static IEnumerable<string> FileNameVariants(string want)
    {
        yield return want;
        string stem = Path.GetFileNameWithoutExtension(want);
        yield return stem + ".Lib";
        yield return stem + ".lib";
        if (!string.Equals(stem, want, StringComparison.Ordinal))
            yield return stem;
    }

    static string? MatchDir(string parent, string want)
    {
        string exact = Path.Combine(parent, want);
        if (Directory.Exists(exact))
            return exact;
        if (!Directory.Exists(parent))
            return null;

        foreach (string d in Directory.GetDirectories(parent))
        {
            if (string.Equals(Path.GetFileName(d), want, StringComparison.OrdinalIgnoreCase))
                return d;
        }

        return null;
    }
}
