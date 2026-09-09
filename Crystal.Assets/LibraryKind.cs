namespace Crystal.Assets;

/// <summary>
/// On-disk library formats used by Client (runtime .Lib) and LibraryEditor (WIL/WZL/WTL/MIZ).
/// </summary>
public enum LibraryKind
{
    Unknown = 0,
    MLib,
    Wil,
    Wzl,
    Wtl,
    Miz
}
