using System.Security.Cryptography;

namespace Client.Linux;

/// <summary>
/// Same MD5-of-file as WinForms <c>LoginScene.SendVersion</c> (<c>Application.ExecutablePath</c>).
/// Operator supplies a file (<c>--version-file</c> / <c>CRYSTAL_VERSION_FILE</c>) — often
/// <c>Mir2.Exe</c> or this host's <c>Crystal.Client.Linux.dll</c>. Do not vendor the exe.
/// </summary>
internal static class VersionHash
{
    public static byte[] Resolve(string? filePath, string? hexOverride, out string source)
    {
        if (!string.IsNullOrWhiteSpace(hexOverride))
        {
            string hex = hexOverride.Trim().Replace("-", "");
            if (hex.Length == 32)
            {
                try
                {
                    source = "hex";
                    return Convert.FromHexString(hex);
                }
                catch (FormatException)
                {
                    // fall through
                }
            }
        }

        foreach (string? probe in CandidateFiles(filePath))
        {
            if (string.IsNullOrWhiteSpace(probe) || !File.Exists(probe))
                continue;
            source = Path.GetFullPath(probe);
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(source);
            return md5.ComputeHash(stream);
        }

        source = "(none)";
        return new byte[16];
    }

    public static string ToHex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();

    static IEnumerable<string?> CandidateFiles(string? filePath)
    {
        yield return filePath;
        string? assembly = typeof(Program).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assembly))
            yield return assembly;
        yield return Path.Combine(AppContext.BaseDirectory, "Crystal.Client.Linux.dll");
    }
}
