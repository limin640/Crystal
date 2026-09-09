namespace Client.Linux;

/// <summary>Crystal Shared action from keyboard/mouse or an injected input script.</summary>
internal enum GameCommandKind
{
    None,
    Walk,
    Attack,
    PickUp,
    Chat
}

internal readonly struct GameCommand
{
    public GameCommandKind Kind { get; init; }
    public MirDirection Direction { get; init; }
    public string Text { get; init; }

    public static GameCommand Walk(MirDirection d) => new() { Kind = GameCommandKind.Walk, Direction = d, Text = "" };
    public static GameCommand Attack(MirDirection d) => new() { Kind = GameCommandKind.Attack, Direction = d, Text = "" };
    public static GameCommand PickUp() => new() { Kind = GameCommandKind.PickUp, Text = "" };
    public static GameCommand Chat(string text) => new() { Kind = GameCommandKind.Chat, Text = text };
}
