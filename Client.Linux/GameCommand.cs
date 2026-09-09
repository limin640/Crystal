namespace Client.Linux;

/// <summary>Crystal Shared action from keyboard/mouse or an injected input script.</summary>
internal enum GameCommandKind
{
    None,
    Walk,
    Attack,
    PickUp,
    Chat,
    Talk,
    Buy,
    Sell
}

internal readonly struct GameCommand
{
    public GameCommandKind Kind { get; init; }
    public MirDirection Direction { get; init; }
    public string Text { get; init; }
    public int Slot { get; init; }

    public static GameCommand Walk(MirDirection d) => new() { Kind = GameCommandKind.Walk, Direction = d, Text = "", Slot = 0 };
    public static GameCommand Attack(MirDirection d) => new() { Kind = GameCommandKind.Attack, Direction = d, Text = "", Slot = 0 };
    public static GameCommand PickUp() => new() { Kind = GameCommandKind.PickUp, Text = "", Slot = 0 };
    public static GameCommand Chat(string text) => new() { Kind = GameCommandKind.Chat, Text = text, Slot = 0 };
    public static GameCommand Talk() => new() { Kind = GameCommandKind.Talk, Text = "", Slot = 0 };
    public static GameCommand Buy(int goodsIndex) => new() { Kind = GameCommandKind.Buy, Text = "", Slot = goodsIndex };
    public static GameCommand Sell(int bagIndex = -1) => new() { Kind = GameCommandKind.Sell, Text = "", Slot = bagIndex };
}
