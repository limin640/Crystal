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
    Sell,
    AllowTrade,
    Trade,
    TradeAccept,
    TradeGold,
    TradeItem,
    TradeConfirm,
    Face,
    Wait,
    Move,
    Drag,
    Merge,
    QuestAccept,
    QuestFinish,
    QuestAbandon,
    QuestShare,
    Mag,
    MagTarget
}

internal readonly struct GameCommand
{
    public GameCommandKind Kind { get; init; }
    public MirDirection Direction { get; init; }
    public string Text { get; init; }
    public int Slot { get; init; }
    public int Dest { get; init; }

    public static GameCommand Walk(MirDirection d) => new() { Kind = GameCommandKind.Walk, Direction = d, Text = "", Slot = 0 };
    public static GameCommand Attack(MirDirection d) => new() { Kind = GameCommandKind.Attack, Direction = d, Text = "", Slot = 0 };
    public static GameCommand PickUp() => new() { Kind = GameCommandKind.PickUp, Text = "", Slot = 0 };
    public static GameCommand Chat(string text) => new() { Kind = GameCommandKind.Chat, Text = text, Slot = 0 };
    public static GameCommand Talk() => new() { Kind = GameCommandKind.Talk, Text = "", Slot = 0 };
    public static GameCommand Buy(int goodsIndex) => new() { Kind = GameCommandKind.Buy, Text = "", Slot = goodsIndex };
    public static GameCommand Sell(int bagIndex = -1) => new() { Kind = GameCommandKind.Sell, Text = "", Slot = bagIndex };
    public static GameCommand AllowTrade() => new() { Kind = GameCommandKind.AllowTrade, Text = "", Slot = 0 };
    public static GameCommand Trade() => new() { Kind = GameCommandKind.Trade, Text = "", Slot = 0 };
    public static GameCommand TradeAccept() => new() { Kind = GameCommandKind.TradeAccept, Text = "", Slot = 0 };
    public static GameCommand TradeGold(int amount) => new() { Kind = GameCommandKind.TradeGold, Text = "", Slot = amount };
    public static GameCommand TradeItem(int bagIndex = -1) => new() { Kind = GameCommandKind.TradeItem, Text = "", Slot = bagIndex };
    public static GameCommand TradeConfirm() => new() { Kind = GameCommandKind.TradeConfirm, Text = "", Slot = 0 };
    public static GameCommand Face(MirDirection d) => new() { Kind = GameCommandKind.Face, Direction = d, Text = "", Slot = 0 };
    public static GameCommand Wait(int milliseconds) => new() { Kind = GameCommandKind.Wait, Text = "", Slot = milliseconds };
    public static GameCommand Move(int x, int y) => new() { Kind = GameCommandKind.Move, Text = $"{x} {y}", Slot = 0 };
    public static GameCommand Drag(int from = -1, int to = -1) => new() { Kind = GameCommandKind.Drag, Text = "", Slot = from, Dest = to };
    public static GameCommand Merge(int from, int to) => new() { Kind = GameCommandKind.Merge, Text = "", Slot = from, Dest = to };
    public static GameCommand QuestAccept(int questIndex = -1) => new() { Kind = GameCommandKind.QuestAccept, Text = "", Slot = questIndex };
    public static GameCommand QuestFinish(int questIndex = -1, int selectedItem = -1) => new() { Kind = GameCommandKind.QuestFinish, Text = "", Slot = questIndex, Dest = selectedItem };
    public static GameCommand QuestAbandon(int questIndex) => new() { Kind = GameCommandKind.QuestAbandon, Text = "", Slot = questIndex };
    public static GameCommand QuestShare(int questIndex) => new() { Kind = GameCommandKind.QuestShare, Text = "", Slot = questIndex };
    /// <summary><c>C.Magic</c> — optional spell name/id in <see cref="Text"/>, optional target ObjectID in <see cref="Dest"/>.</summary>
    public static GameCommand Mag(string spell = "", int targetId = 0) => new() { Kind = GameCommandKind.Mag, Text = spell ?? "", Slot = 0, Dest = targetId };
    /// <summary><c>C.Magic</c> with <c>SpellTargetLock=true</c> (WinForms targeting).</summary>
    public static GameCommand MagTarget(string spell = "", int targetId = 0) => new() { Kind = GameCommandKind.MagTarget, Text = spell ?? "", Slot = 0, Dest = targetId };
}
