namespace Client.Linux;

/// <summary>Crystal Shared action from keyboard/mouse or an injected input script.</summary>
internal enum GameCommandKind
{
    None,
    Walk,
    Attack,
    PickUp
}

internal readonly struct GameCommand
{
    public GameCommandKind Kind { get; init; }
    public MirDirection Direction { get; init; }

    public static GameCommand Walk(MirDirection d) => new() { Kind = GameCommandKind.Walk, Direction = d };
    public static GameCommand Attack(MirDirection d) => new() { Kind = GameCommandKind.Attack, Direction = d };
    public static GameCommand PickUp() => new() { Kind = GameCommandKind.PickUp };
}
