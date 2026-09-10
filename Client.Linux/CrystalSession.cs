using System.Collections.Concurrent;
using System.Drawing;
using System.Net.Sockets;
using C = ClientPackets;
using S = ServerPackets;

namespace Client.Linux;

internal sealed class ConnectOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7000;
    public string Account { get; set; } = "linux";
    public string Password { get; set; } = "linux1";
    public bool CreateAccount { get; set; }
    public bool LoginOnly { get; set; }
    public string CharacterName { get; set; } = "LinuxWar";
    public bool Walk { get; set; } = true;
    public bool PlayGate { get; set; } = true;
    public int WaitMs { get; set; } = 1500;
    public int EnterWaitMs { get; set; } = 5000;
    /// <summary>File to MD5 for <c>C.ClientVersion</c> (WinForms uses the running exe). Env: <c>CRYSTAL_VERSION_FILE</c>.</summary>
    public string? VersionFile { get; set; }
    /// <summary>Raw 32-char hex MD5 when the operator has a hash list instead of a file. Env: <c>CRYSTAL_VERSION_HASH</c>.</summary>
    public string? VersionHashHex { get; set; }
}

/// <summary>
/// Shared-protocol TCP session (same framing as <c>Client.MirNetwork.Network</c>).
/// Login → Select (character list / NewCharacter) → StartGame → in-map packets.
/// </summary>
internal sealed class CrystalSession : IDisposable
{
    readonly TcpClient _client = new() { NoDelay = true };
    readonly ConcurrentQueue<Packet> _receive = new();
    readonly byte[] _buffer = new byte[8 * 1024];
    byte[] _raw = Array.Empty<byte>();
    readonly List<string> _log = new();
    readonly Dictionary<uint, WorldObject> _objects = new();
    readonly Dictionary<int, ItemInfo> _itemInfos = new();
    readonly Dictionary<ulong, UserItem> _bag = new();
    UserItem?[] _inventory = Array.Empty<UserItem?>();
    UserItem?[] _equipment = Array.Empty<UserItem?>();
    readonly List<ClientMagic> _magics = new();
    readonly List<string> _chatLines = new();
    readonly List<GroundLoot> _ground = new();
    int _attacksSent;
    uint _fightTargetId;
    uint _goldGained;

    public int ExitCode { get; private set; } = 5;
    public bool SocketConnected => _client.Connected;
    public bool GotConnected { get; private set; }
    public byte? VersionResult { get; private set; }
    public bool VersionCheckOk { get; private set; }
    public string? VersionHashSource { get; private set; }
    public string? VersionHashHex { get; private set; }
    public byte? NewAccountResult { get; private set; }
    public byte? LoginResult { get; private set; }
    public bool LoginSuccess { get; private set; }
    public int CharacterCount { get; private set; }
    public List<SelectInfo> Characters { get; } = new();
    public byte? NewCharacterResult { get; private set; }
    public bool NewCharacterOk { get; private set; }
    public byte? StartGameResult { get; private set; }
    public bool InMap { get; private set; }
    public string? MapFileName { get; private set; }
    public string? MapTitle { get; private set; }
    public int MapIndex { get; private set; }
    public Point UserLocation { get; private set; }
    public string? UserName { get; private set; }
    public uint UserObjectId { get; private set; }
    public bool WalkSent { get; private set; }
    public bool WalkAck { get; private set; }
    public bool FightHit { get; private set; }
    public bool FightDied { get; private set; }
    public bool LootOk { get; private set; }
    public bool EquipOk { get; private set; }
    public string? FightEvidence { get; private set; }
    public string? LootEvidence { get; private set; }
    public string? EquipEvidence { get; private set; }
    public IReadOnlyList<string> Log => _log;
    public IReadOnlyList<WorldObject> Objects => _objects.Values.ToList();
    public MirDirection Facing { get; private set; } = MirDirection.Right;
    public int UserHP { get; private set; }
    public int UserMP { get; private set; }
    public int UserMaxHP { get; private set; }
    public int UserMaxMP { get; private set; }
    public ushort UserLevel { get; private set; }
    public MirClass UserClass { get; private set; }
    public string? LastChat { get; private set; }
    public uint UserGold { get; private set; }
    public int InputWalks { get; private set; }
    public int InputAttacks { get; private set; }
    public int InputPickups { get; private set; }
    public int InputChats { get; private set; }
    public int ChatSent { get; private set; }
    public int ChatRecv { get; private set; }
    public bool ChatEcho { get; private set; }
    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }
    public ushort MiniMapIndex { get; private set; }
    public bool ChatComposing { get; private set; }
    public string ChatDraft { get; private set; } = "";
    string _lastSentChat = "";
    public int InputTalks { get; private set; }
    public bool NpcTalkOk { get; private set; }
    public uint NpcObjectId { get; private set; }
    public string? NpcName { get; private set; }
    public List<string> NpcDialogLines { get; } = new();
    public List<string> NpcGoods { get; } = new();
    public List<string> QuestNames { get; } = new();
    readonly Dictionary<int, ClientQuestInfo> _questInfo = new();
    readonly Dictionary<int, ClientQuestProgress> _takenQuests = new();
    readonly HashSet<int> _completedQuestIds = new();
    int _pendingAccept = -1;
    int _pendingFinish = -1;
    public int InputMags { get; private set; }
    public bool MagicOk { get; private set; }
    public string? MagicEvidence { get; private set; }
    public int InputQuests { get; private set; }
    public bool QuestAcceptOk { get; private set; }
    public bool QuestFinishOk { get; private set; }
    public string? QuestAcceptEvidence { get; private set; }
    public string? QuestFinishEvidence { get; private set; }
    public IReadOnlyCollection<ClientQuestInfo> QuestCatalog => _questInfo.Values;
    public IReadOnlyCollection<ClientQuestProgress> TakenQuests => _takenQuests.Values;
    public IReadOnlyCollection<int> CompletedQuestIds => _completedQuestIds;
    public int NpcCallSent { get; private set; }
    uint _defaultNpcId;
    readonly List<UserItem> _goodsItems = new();
    PanelType _goodsType = PanelType.Buy;
    ulong _lastBoughtUid;
    public bool BuyOk { get; private set; }
    public bool SellOk { get; private set; }
    public string? BuyEvidence { get; private set; }
    public string? SellEvidence { get; private set; }
    public int InputBuys { get; private set; }
    public int InputSells { get; private set; }
    public uint GoldSpent { get; private set; }
    public uint GoldEarned { get; private set; }
    public bool AutoTradeReply { get; set; }
    public bool AutoTradeConfirm { get; set; }
    public int InputTrades { get; private set; }
    public bool TradeHandshakeOk { get; private set; }
    public bool TradeDone { get; private set; }
    public bool TradeGoldOk { get; private set; }
    public bool TradeDepositOk { get; private set; }
    public string? TradePartnerName { get; private set; }
    public string? TradeInviteFrom { get; private set; }
    public string? TradeEvidence { get; private set; }
    public uint TradeGoldSeen { get; private set; }
    DateTime _lastTradeRequestUtc = DateTime.MinValue;
    public int InputDrags { get; private set; }
    public int InputMerges { get; private set; }
    public bool DragOk { get; private set; }
    public bool MergeOk { get; private set; }
    public string? DragEvidence { get; private set; }
    public string? MergeEvidence { get; private set; }
    /// <summary>Source bag slot for SelectedCell ghost (scripted Drag or windowed pick-up).</summary>
    public int SelectedSlot { get; private set; } = -1;
    /// <summary>Hover / drop bag slot while a drag is armed.</summary>
    public int DragHoverSlot { get; private set; } = -1;
    public bool ShowDragGhost { get; private set; }
    public const int BeltSlotCount = 6;
    public IReadOnlyList<UserItem?> InventorySlots => _inventory;
    public IReadOnlyList<UserItem?> EquipmentSlots => _equipment;
    public IReadOnlyList<ClientMagic> Magics => _magics;
    public IReadOnlyList<string> ChatLines => _chatLines;
    public int BagCount => _bag.Count;
    public int EquippedFilled => EquippedCount();

    public string DisplayName(UserItem item) => ItemName(item);

    public void SelectSlot(int slot)
    {
        SelectedSlot = slot;
        DragHoverSlot = -1;
        ShowDragGhost = slot >= 0;
    }

    public void ClearSelection()
    {
        SelectedSlot = -1;
        DragHoverSlot = -1;
        ShowDragGhost = false;
    }

    public bool SlotOccupied(int slot)
        => slot >= 0 && slot < _inventory.Length && _inventory[slot] != null;

    public void Connect(string host, int port, int timeoutMs = 5000)
    {
        Packet.IsServer = false;
        var ar = _client.BeginConnect(host, port, null, null);
        if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
            throw new TimeoutException($"Connect to {host}:{port} timed out");
        _client.EndConnect(ar);
        Note($"socket connected {host}:{port}");
        BeginReceive();
    }

    /// <summary>Player-driven (keyboard/mouse or --input-script) Shared packet. Distinct from the Phase E one-shot gate.</summary>
    public void Drive(GameCommand command)
    {
        if (!InMap || !SocketConnected)
            return;

        switch (command.Kind)
        {
            case GameCommandKind.Walk:
                Facing = command.Direction;
                WalkSent = true;
                InputWalks++;
                Send(new C.Walk { Direction = command.Direction });
                Note($"input Walk {command.Direction} #{InputWalks} loc={UserLocation.X},{UserLocation.Y}");
                break;
            case GameCommandKind.Attack:
                InputAttacks++;
                _attacksSent++;
                Send(new C.Attack { Direction = Facing, Spell = Spell.None });
                Note($"input Attack {Facing} #{InputAttacks}");
                break;
            case GameCommandKind.PickUp:
                InputPickups++;
                Send(new C.PickUp());
                Note($"input PickUp #{InputPickups}");
                break;
            case GameCommandKind.Chat:
                SendChat(command.Text);
                break;
            case GameCommandKind.Talk:
                TryTalk();
                break;
            case GameCommandKind.Buy:
                TryBuy(command.Slot);
                break;
            case GameCommandKind.Sell:
                TrySell(command.Slot);
                break;
            case GameCommandKind.AllowTrade:
                Send(new C.ChangeTrade { AllowTrade = true });
                Note("input AllowTrade C.ChangeTrade AllowTrade=true");
                break;
            case GameCommandKind.Trade:
                TryTradeRequest();
                break;
            case GameCommandKind.TradeAccept:
                SendTradeReply(true);
                break;
            case GameCommandKind.TradeGold:
                TryTradeGold((uint)Math.Max(1, command.Slot));
                break;
            case GameCommandKind.TradeItem:
                TryTradeDeposit(command.Slot);
                break;
            case GameCommandKind.TradeConfirm:
                SendTradeConfirm();
                break;
            case GameCommandKind.Face:
                Facing = command.Direction;
                Send(new C.Turn { Direction = command.Direction });
                Note($"input Face {command.Direction} loc={UserLocation.X},{UserLocation.Y}");
                break;
            case GameCommandKind.Wait:
                Note($"input Wait {command.Slot}ms");
                Pump(Math.Max(0, command.Slot));
                break;
            case GameCommandKind.Move:
                Chat("@MOVE " + command.Text);
                Note($"input Move @MOVE {command.Text}");
                Pump(800);
                break;
            case GameCommandKind.Drag:
                TryDrag(command.Slot, command.Dest);
                break;
            case GameCommandKind.Merge:
                TryMerge(command.Slot, command.Dest);
                break;
            case GameCommandKind.QuestAccept:
                TryQuestAccept(command.Slot);
                break;
            case GameCommandKind.QuestFinish:
                TryQuestFinish(command.Slot, command.Dest);
                break;
            case GameCommandKind.QuestAbandon:
                TryQuestAbandon(command.Slot);
                break;
            case GameCommandKind.QuestShare:
                TryQuestShare(command.Slot);
                break;
            case GameCommandKind.Mag:
                TryMagic(command.Text, command.Dest, lockOn: false);
                break;
            case GameCommandKind.MagTarget:
                TryMagic(command.Text, command.Dest, lockOn: true);
                break;
        }
    }

    public int RunInputScript(IReadOnlyList<GameCommand> commands, int stepMs)
    {
        if (!InMap)
        {
            Console.Error.WriteLine("input-script: not in-map; skip.");
            return 0;
        }

        Console.WriteLine($"input-script {commands.Count} commands stepMs={stepMs}");
        foreach (var cmd in commands)
        {
            var step = cmd;
            if (step.Kind == GameCommandKind.Attack)
                step = GameCommand.Attack(Facing);
            Drive(step);
            Pump(Math.Max(200, stepMs));
        }

        Console.WriteLine($"input-script done walks={InputWalks} attacks={InputAttacks} talks={InputTalks} buys={InputBuys} sells={InputSells} trades={InputTrades} drags={InputDrags} quests={InputQuests} mags={InputMags} BuyOk={BuyOk} SellOk={SellOk} DragOk={DragOk} QuestAcceptOk={QuestAcceptOk} QuestFinishOk={QuestFinishOk} MagicOk={MagicOk} TradeHandshake={TradeHandshakeOk} TradeDone={TradeDone} gold={UserGold} bag={BagCount} NpcTalkOk={NpcTalkOk} partner={TradePartnerName ?? "-"} loc={UserLocation.X},{UserLocation.Y}");
        if (BuyEvidence != null) Console.WriteLine($"  buy : {BuyEvidence}");
        if (SellEvidence != null) Console.WriteLine($"  sell : {SellEvidence}");
        if (TradeEvidence != null) Console.WriteLine($"  trade : {TradeEvidence}");
        if (DragEvidence != null) Console.WriteLine($"  drag : {DragEvidence}");
        if (MergeEvidence != null) Console.WriteLine($"  merge : {MergeEvidence}");
        if (QuestAcceptEvidence != null) Console.WriteLine($"  quest-accept : {QuestAcceptEvidence}");
        if (QuestFinishEvidence != null) Console.WriteLine($"  quest-finish : {QuestFinishEvidence}");
        if (MagicEvidence != null) Console.WriteLine($"  mag : {MagicEvidence}");
        bool wantWalk = commands.Any(c => c.Kind == GameCommandKind.Walk);
        bool wantAtk = commands.Any(c => c.Kind == GameCommandKind.Attack);
        bool wantChat = commands.Any(c => c.Kind == GameCommandKind.Chat);
        bool wantTalk = commands.Any(c => c.Kind == GameCommandKind.Talk);
        bool wantBuy = commands.Any(c => c.Kind == GameCommandKind.Buy);
        bool wantSell = commands.Any(c => c.Kind == GameCommandKind.Sell);
        bool wantTrade = commands.Any(c => c.Kind == GameCommandKind.Trade);
        bool wantConfirm = commands.Any(c => c.Kind == GameCommandKind.TradeConfirm);
        bool wantDrag = commands.Any(c => c.Kind == GameCommandKind.Drag);
        bool wantMerge = commands.Any(c => c.Kind == GameCommandKind.Merge);
        bool wantQuestAccept = commands.Any(c => c.Kind == GameCommandKind.QuestAccept);
        bool wantQuestFinish = commands.Any(c => c.Kind == GameCommandKind.QuestFinish);
        bool wantMag = commands.Any(c => c.Kind is GameCommandKind.Mag or GameCommandKind.MagTarget);
        if (wantWalk && InputWalks == 0) return 10;
        if (wantAtk && InputAttacks == 0) return 10;
        if (wantChat && ChatSent == 0) return 10;
        if (wantTalk && !NpcTalkOk) return 10;
        if (wantBuy && !BuyOk) return 10;
        if (wantSell && !SellOk) return 10;
        if (wantTrade && !TradeHandshakeOk) return 10;
        if (wantConfirm && wantTrade && !TradeDone) return 10;
        if (wantDrag && !DragOk) return 10;
        if (wantMerge && !MergeOk) return 10;
        if (wantQuestAccept && !QuestAcceptOk) return 10;
        if (wantQuestFinish && !QuestFinishOk) return 10;
        if (wantMag && InputMags == 0) return 10;
        return 0;
    }

    public void NoteMapSize(int width, int height)
    {
        if (width > 0) MapWidth = width;
        if (height > 0) MapHeight = height;
        Note($"map size {MapWidth}x{MapHeight} (MapReader / known cells — no invented MMap art)");
    }

    public void BeginChat()
    {
        ChatComposing = true;
        ChatDraft = "";
    }

    public void AppendChat(char ch)
    {
        if (!ChatComposing || char.IsControl(ch)) return;
        if (ChatDraft.Length < 60)
            ChatDraft += ch;
    }

    public void ChatBackspace()
    {
        if (!ChatComposing || ChatDraft.Length == 0) return;
        ChatDraft = ChatDraft[..^1];
    }

    public void CancelChat()
    {
        ChatComposing = false;
        ChatDraft = "";
    }

    public void CommitChat()
    {
        if (!ChatComposing) return;
        string text = ChatDraft.Trim();
        ChatComposing = false;
        ChatDraft = "";
        if (text.Length > 0)
            SendChat(text);
    }

    void SendChat(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !InMap || !SocketConnected)
            return;
        InputChats++;
        ChatSent++;
        _lastSentChat = text;
        _chatLines.Add($"> {text}");
        if (_chatLines.Count > 4)
            _chatLines.RemoveAt(0);
        LastChat = $"> {text}";
        Send(new C.Chat { Message = text });
        Note($"input Chat send '{text}' #{ChatSent}");
    }

    void TryTalk()
    {
        InputTalks++;
        var npcs = NpcsByDistance();
        if (npcs.Count == 0)
        {
            Note("talk: no ObjectNPC in view; @MOVE 289 617 (test-server BorderVillage)");
            Chat("@MOVE 289 617");
            Pump(900);
            npcs = NpcsByDistance();
        }

        foreach (var npc in npcs)
        {
            if (!Functions.InRange(UserLocation, npc.Location, Globals.DataRange))
            {
                Note($"talk: approach {npc.Name} {npc.Location.X},{npc.Location.Y} from {UserLocation.X},{UserLocation.Y}");
                WalkToward(npc.Location, 14);
            }
            if (!Functions.InRange(UserLocation, npc.Location, Globals.DataRange))
            {
                Chat($"@MOVE {npc.Location.X} {npc.Location.Y}");
                Pump(700);
            }

            NpcName = npc.Name;
            NpcObjectId = npc.ObjectID;
            NpcCallSent++;
            Send(new C.CallNPC { ObjectID = npc.ObjectID, Key = "[@Main]" });
            Note($"input Talk CallNPC id={npc.ObjectID} name={npc.Name} key=[@Main] #{InputTalks}");
            Pump(1000);
            if (!NpcTalkOk)
                continue;

            TryBuyKeyFromDialog(npc.ObjectID);
            return;
        }

        if (!NpcTalkOk && _defaultNpcId != 0)
        {
            NpcCallSent++;
            NpcName ??= "DefaultNPC";
            NpcObjectId = _defaultNpcId;
            Send(new C.CallNPC { ObjectID = _defaultNpcId, Key = "[@Main]" });
            Note($"input Talk CallNPC DefaultNPC id={_defaultNpcId} key=[@Main]");
            Pump(1000);
        }

        if (!NpcTalkOk)
            Note("talk: no S.NPCResponse (script missing or out of range)");
    }

    List<WorldObject> NpcsByDistance()
    {
        return _objects.Values
            .Where(o => o.Kind == "npc")
            .OrderBy(o => BoardScore(o.Name))
            .ThenBy(o => Chebyshev(UserLocation, o.Location))
            .ToList();
    }

    static int BoardScore(string name)
    {
        string n = name ?? "";
        if (n.Contains("Jane", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Helper", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Grocery", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Pedlar", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Peddlar", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Gilbert", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Transport", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (n.Contains("Board", StringComparison.OrdinalIgnoreCase))
            return 2;
        return 1;
    }

    static int Chebyshev(Point a, Point b)
        => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    void TryBuy(int goodsIndex)
    {
        InputBuys++;
        if (_goodsItems.Count == 0)
        {
            Note("buy: no NPCGoods yet; Talk first");
            TryTalk();
        }
        if (_goodsItems.Count == 0)
        {
            Note("buy: still no NPCGoods");
            return;
        }

        if (UserGold < 500)
        {
            Note("buy: low gold; @GIVEGOLD 50000 (test-server)");
            Chat("@GIVEGOLD 50000");
            Pump(600);
        }

        int idx = Math.Clamp(goodsIndex, 0, _goodsItems.Count - 1);
        var goods = _goodsItems[idx];
        int bagBefore = _bag.Count;
        uint goldBefore = UserGold;
        Send(new C.BuyItem { ItemIndex = goods.UniqueID, Count = 1, Type = _goodsType == PanelType.BuySub ? PanelType.BuySub : PanelType.Buy });
        Note($"input Buy goods[{idx}] uid={goods.UniqueID} name={ItemName(goods)} gold={UserGold} bag={bagBefore} #{InputBuys}");
        Pump(1000);

        if (_bag.Count > bagBefore || GoldSpent > 0 || UserGold < goldBefore)
        {
            BuyOk = true;
            var bought = _bag.Values.LastOrDefault();
            if (bought != null)
                _lastBoughtUid = bought.UniqueID;
            BuyEvidence = $"BuyItem {ItemName(goods)} gold {goldBefore}→{UserGold} bag {bagBefore}→{_bag.Count}";
            Note("buy evidence: " + BuyEvidence);
        }
        else
            Note($"buy: no bag/gold change after C.BuyItem (gold={UserGold} bag={_bag.Count})");
    }

    void TrySell(int bagIndex)
    {
        InputSells++;
        if (_goodsItems.Count == 0 && NpcObjectId != 0)
        {
            Note("sell: open [@BUYSELL] before C.SellItem");
            TryTalk();
        }

        UserItem? item = null;
        if (_lastBoughtUid != 0 && _bag.TryGetValue(_lastBoughtUid, out var bought))
            item = bought;
        else if (bagIndex >= 0 && bagIndex < _inventory.Length)
            item = _inventory[bagIndex];
        item ??= _bag.Values.FirstOrDefault(i => ItemTypeOf(i) is ItemType.Potion or ItemType.Nothing or ItemType.Meat or ItemType.Ore);
        item ??= _bag.Values.LastOrDefault();
        if (item == null)
        {
            Note("sell: empty bag");
            return;
        }

        uint goldBefore = UserGold;
        int bagBefore = _bag.Count;
        Send(new C.SellItem { UniqueID = item.UniqueID, Count = 1 });
        Note($"input Sell uid={item.UniqueID} name={ItemName(item)} gold={UserGold} bag={bagBefore} #{InputSells}");
        Pump(1000);
        if (SellOk || UserGold > goldBefore || _bag.Count < bagBefore)
        {
            SellOk = true;
            SellEvidence = $"SellItem {ItemName(item)} gold {goldBefore}→{UserGold} bag {bagBefore}→{_bag.Count}";
            Note("sell evidence: " + SellEvidence);
        }
        else
            Note($"sell: no gold/bag change after C.SellItem (gold={UserGold} bag={_bag.Count})");
    }

    void TryDrag(int from, int to)
    {
        InputDrags++;
        EnsureInventorySize();
        if (from < 0)
            from = FirstOccupiedSlot();
        if (to < 0)
            to = FirstEmptySlot(from);
        if (from < 0)
        {
            Note("drag: empty bag");
            return;
        }
        if (to < 0)
        {
            Note("drag: no empty bag slot");
            return;
        }
        if (from == to)
        {
            Note($"drag: from==to slot={from}");
            return;
        }
        if (from >= _inventory.Length || _inventory[from] == null)
        {
            Note($"drag: slot {from} empty");
            return;
        }

        SelectedSlot = from;
        DragHoverSlot = to;
        ShowDragGhost = true;

        string name = ItemName(_inventory[from]!);
        ulong uid = _inventory[from]!.UniqueID;
        string destName = _inventory[to] != null ? ItemName(_inventory[to]!) : "-";
        Send(new C.MoveItem { Grid = MirGridType.Inventory, From = from, To = to });
        Note($"input MoveItem Grid=Inventory from={from} to={to} name={name} uid={uid} dest={destName} #{InputDrags}");
        Pump(800);
        if (DragOk)
        {
            DragEvidence ??= $"MoveItem {name} slot {from}→{to}";
            Note("drag evidence: " + DragEvidence);
        }
        else
            Note($"drag: no S.MoveItem Success from={from} to={to}");
    }

    void TryMerge(int from, int to)
    {
        InputMerges++;
        EnsureInventorySize();
        if (from < 0 || to < 0 || from >= _inventory.Length || to >= _inventory.Length)
        {
            Note($"merge: bad slots from={from} to={to}");
            return;
        }
        var a = _inventory[from];
        var b = _inventory[to];
        if (a == null || b == null)
        {
            Note("merge: one slot empty");
            return;
        }
        Send(new C.MergeItem
        {
            GridFrom = MirGridType.Inventory,
            GridTo = MirGridType.Inventory,
            IDFrom = a.UniqueID,
            IDTo = b.UniqueID
        });
        Note($"input MergeItem from={from} uid={a.UniqueID} to={to} uid={b.UniqueID} #{InputMerges}");
        Pump(800);
        if (MergeOk)
        {
            MergeEvidence ??= $"MergeItem {ItemName(b)} slots {from}+{to}";
            Note("merge evidence: " + MergeEvidence);
        }
        else
            Note("merge: no S.MergeItem Success");
    }

    void TryQuestAccept(int questIndex)
    {
        InputQuests++;
        if (questIndex < 0)
            questIndex = FirstAcceptableQuestId();
        if (questIndex < 0)
        {
            Note("quest-accept: no NewQuestInfo id (not taken/completed)");
            return;
        }

        uint npcId = 0;
        string name = QuestLabel(questIndex);
        if (_questInfo.TryGetValue(questIndex, out var info))
            npcId = info.NPCIndex;

        EnsureNearQuestNpc(npcId, questIndex, "accept");
        if (npcId == 0 && NpcObjectId != 0)
            npcId = NpcObjectId;

        _pendingAccept = questIndex;
        Send(new C.AcceptQuest { NPCIndex = npcId, QuestIndex = questIndex });
        Note($"input AcceptQuest npc={npcId} id={questIndex} name={name} #{InputQuests}");
        Pump(900);
        if (QuestAcceptOk)
            Note("quest-accept evidence: " + QuestAcceptEvidence);
        else
            Note($"quest-accept: no S.ChangeQuest Add id={questIndex} (range/level/already done?)");
    }

    void TryQuestFinish(int questIndex, int selectedItem)
    {
        InputQuests++;
        if (questIndex < 0)
            questIndex = FirstFinishableQuestId();
        if (questIndex < 0)
        {
            Note("quest-finish: no taken completed quest");
            return;
        }

        uint npcId = 0;
        if (_questInfo.TryGetValue(questIndex, out var info))
            npcId = info.FinishNPCIndex != 0 ? info.FinishNPCIndex : info.NPCIndex;

        EnsureNearQuestNpc(npcId, questIndex, "finish");
        _pendingFinish = questIndex;
        Send(new C.FinishQuest { QuestIndex = questIndex, SelectedItemIndex = selectedItem });
        Note($"input FinishQuest id={questIndex} selected={selectedItem} name={QuestLabel(questIndex)} #{InputQuests}");
        Pump(900);
        if (QuestFinishOk)
            Note("quest-finish evidence: " + QuestFinishEvidence);
        else
            Note($"quest-finish: no S.ChangeQuest Remove id={questIndex} (not complete or out of range)");
    }

    void TryQuestAbandon(int questIndex)
    {
        InputQuests++;
        Send(new C.AbandonQuest { QuestIndex = questIndex });
        Note($"input AbandonQuest id={questIndex} #{InputQuests}");
        Pump(600);
    }

    void TryQuestShare(int questIndex)
    {
        InputQuests++;
        Send(new C.ShareQuest { QuestIndex = questIndex });
        Note($"input ShareQuest id={questIndex} #{InputQuests}");
        Pump(400);
    }

    /// <summary>Same <c>C.Magic</c> as WinForms <c>GameScene</c> targeting (<c>SpellTargetLock</c> from MagTarget).</summary>
    void TryMagic(string spellName, int targetId, bool lockOn)
    {
        Spell spell = Spell.Fencing;
        if (!string.IsNullOrWhiteSpace(spellName))
        {
            if (Enum.TryParse(spellName, true, out Spell parsed))
                spell = parsed;
            else if (byte.TryParse(spellName, out byte code) && Enum.IsDefined(typeof(Spell), code))
                spell = (Spell)code;
            else
            {
                var named = _magics.FirstOrDefault(m => string.Equals(m.Name, spellName, StringComparison.OrdinalIgnoreCase));
                if (named != null)
                    spell = named.Spell;
            }
        }
        else if (_magics.Count > 0)
            spell = _magics[0].Spell;

        uint tid = targetId > 0 ? (uint)targetId : 0;
        var loc = UserLocation;
        if (tid == 0)
        {
            var mob = _objects.Values.FirstOrDefault(o => o.Kind == "monster");
            if (mob != null)
            {
                tid = mob.ObjectID;
                loc = mob.Location;
            }
        }
        else if (_objects.TryGetValue(tid, out var obj))
            loc = obj.Location;

        InputMags++;
        Send(new C.Magic
        {
            ObjectID = UserObjectId,
            Spell = spell,
            Direction = Facing,
            TargetID = tid,
            Location = loc,
            SpellTargetLock = lockOn
        });
        MagicEvidence = $"C.Magic spell={spell} target={tid} lock={lockOn} loc={loc.X},{loc.Y} known={_magics.Count}";
        Note($"input Mag {MagicEvidence} #{InputMags}");
        Pump(600);
    }

    int FirstAcceptableQuestId()
    {
        foreach (var info in _questInfo.Values.OrderBy(q => q.Index))
        {
            if (info.NPCIndex == 0) continue;
            if (_takenQuests.ContainsKey(info.Index) || _completedQuestIds.Contains(info.Index))
                continue;
            return info.Index;
        }

        return -1;
    }

    int FirstFinishableQuestId()
    {
        foreach (var q in _takenQuests.Values.OrderBy(q => q.Id))
        {
            if (q.Completed)
                return q.Id;
        }

        return _takenQuests.Keys.OrderBy(id => id).FirstOrDefault(-1);
    }

    string QuestLabel(int id)
    {
        if (_questInfo.TryGetValue(id, out var info) && !string.IsNullOrWhiteSpace(info.Name))
            return info.Name;
        if (_takenQuests.TryGetValue(id, out var taken) && taken.QuestInfo != null)
            return taken.QuestInfo.Name;
        return "?";
    }

    WorldObject? FindNpcByObjectId(uint objectId)
    {
        if (objectId != 0 && _objects.TryGetValue(objectId, out var byId) && byId.Kind == "npc")
            return byId;
        return null;
    }

    void EnsureNearQuestNpc(uint objectId, int questIndex, string why)
    {
        var npc = FindNpcByObjectId(objectId);
        if (npc == null)
        {
            Note($"quest-{why}: NPC id={objectId} not in view; @MOVE 289 617 (Talk BorderVillage)");
            Chat("@MOVE 289 617");
            Pump(1000);
            npc = FindNpcByObjectId(objectId);
        }

        if (npc == null)
        {
            Note($"quest-{why}: still no ObjectNPC id={objectId}; step west (Assistant Jane strip)");
            Chat($"@MOVE {Math.Max(0, UserLocation.X - 12)} {UserLocation.Y - 8}");
            Pump(1000);
            npc = FindNpcByObjectId(objectId);
        }

        if (npc == null)
        {
            Note($"quest-{why}: ObjectNPC id={objectId} still missing for quest {questIndex}");
            return;
        }

        NpcName = npc.Name;
        NpcObjectId = npc.ObjectID;
        if (!Functions.InRange(UserLocation, npc.Location, Globals.DataRange))
        {
            Note($"quest-{why}: approach {npc.Name} {npc.Location.X},{npc.Location.Y} from {UserLocation.X},{UserLocation.Y}");
            WalkToward(npc.Location, 16);
        }

        if (!Functions.InRange(UserLocation, npc.Location, Globals.DataRange))
        {
            Chat($"@MOVE {npc.Location.X} {npc.Location.Y}");
            Pump(800);
        }
    }

    void ApplyInventorySwap(int from, int to)
    {
        EnsureInventorySize();
        if (from < 0 || to < 0)
            return;
        if (from >= _inventory.Length || to >= _inventory.Length)
        {
            int need = Math.Max(from, to) + 1;
            Array.Resize(ref _inventory, need);
        }
        (_inventory[to], _inventory[from]) = (_inventory[from], _inventory[to]);
    }

    void EnsureInventorySize()
    {
        if (_inventory.Length == 0)
            _inventory = new UserItem?[46];
    }

    int FirstOccupiedSlot()
    {
        for (int i = 0; i < _inventory.Length; i++)
            if (_inventory[i] != null)
                return i;
        return -1;
    }

    int FirstEmptySlot(int except)
    {
        for (int i = BeltSlotCount; i < _inventory.Length; i++)
            if (i != except && _inventory[i] == null)
                return i;
        for (int i = 0; i < _inventory.Length; i++)
            if (i != except && _inventory[i] == null)
                return i;
        return -1;
    }

    void TryTradeRequest()
    {
        InputTrades++;
        WorldObject? other = NearestPlayer();
        if (other == null)
        {
            Note("trade: no ObjectPlayer yet; wait");
            Pump(2500);
            other = NearestPlayer();
        }
        if (other == null)
        {
            Note("trade: still no other player in range");
            return;
        }

        if (Chebyshev(UserLocation, other.Location) > 1)
        {
            int standX = other.Location.X - 1;
            int standY = other.Location.Y;
            Note($"trade: approach {other.Name} {other.Location.X},{other.Location.Y} via @MOVE {standX} {standY}");
            Chat($"@MOVE {standX} {standY}");
            Pump(900);
        }

        FaceToward(other.Location);
        Pump(400);

        var wait = DateTime.UtcNow - _lastTradeRequestUtc;
        if (wait.TotalMilliseconds < 2100)
            Pump(2100 - (int)wait.TotalMilliseconds);

        _lastTradeRequestUtc = DateTime.UtcNow;
        Send(new C.TradeRequest());
        Note($"input TradeRequest face={Facing} loc={UserLocation.X},{UserLocation.Y} toward {other.Name} {other.Location.X},{other.Location.Y} #{InputTrades}");
        Pump(1600);

        if (!TradeHandshakeOk)
        {
            Note("trade: no S.TradeAccept; face again and retry once");
            FaceToward(other.Location);
            Pump(2200);
            Send(new C.TradeRequest());
            Note($"input TradeRequest retry toward {other.Name}");
            Pump(1600);
        }
    }

    void TryTradeGold(uint amount)
    {
        if (!TradeHandshakeOk)
            Note("trade gold: no handshake yet; send anyway");
        if (UserGold < amount)
        {
            Note($"trade gold: low gold; @GIVEGOLD {amount + 100} (test-server)");
            Chat($"@GIVEGOLD {amount + 100}");
            Pump(600);
        }
        uint goldBefore = UserGold;
        Send(new C.TradeGold { Amount = amount });
        Note($"input TradeGold amount={amount} gold={UserGold}");
        Pump(800);
        if (UserGold < goldBefore || GoldSpent > 0)
        {
            TradeGoldOk = true;
            TradeEvidence = $"TradeGold {amount} gold {goldBefore}→{UserGold} partner={TradePartnerName ?? "-"}";
            Note("trade gold evidence: " + TradeEvidence);
        }
    }

    void TryTradeDeposit(int bagIndex)
    {
        int from = bagIndex;
        if (from < 0 || from >= _inventory.Length || _inventory[from] == null)
        {
            from = -1;
            for (int i = 0; i < _inventory.Length; i++)
            {
                if (_inventory[i] != null)
                {
                    from = i;
                    break;
                }
            }
        }
        if (from < 0)
        {
            Note("trade item: empty bag");
            return;
        }
        var item = _inventory[from];
        Send(new C.DepositTradeItem { From = from, To = 0 });
        Note($"input DepositTradeItem from={from} to=0 name={ItemName(item!)} uid={item!.UniqueID}");
        Pump(800);
    }

    void SendTradeReply(bool accept)
    {
        Send(new C.TradeReply { AcceptInvite = accept });
        Note($"input TradeReply AcceptInvite={accept} from={TradeInviteFrom ?? "-"}");
        Pump(400);
    }

    void SendTradeConfirm()
    {
        Send(new C.TradeConfirm { Locked = true });
        Note($"input TradeConfirm Locked=true partner={TradePartnerName ?? "-"}");
        Pump(800);
        if (TradeDone)
            TradeEvidence ??= $"TradeConfirm done partner={TradePartnerName} gold={UserGold} bag={BagCount}";
    }

    void FaceToward(Point target)
    {
        var dir = Functions.DirectionFromPoint(UserLocation, target);
        Facing = dir;
        Send(new C.Turn { Direction = dir });
        Note($"trade Face {dir} toward {target.X},{target.Y}");
    }

    WorldObject? NearestPlayer()
    {
        return _objects.Values
            .Where(o => o.Kind == "player" && o.ObjectID != UserObjectId
                && !string.Equals(o.Name, UserName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => Chebyshev(UserLocation, o.Location))
            .FirstOrDefault();
    }

    void MaybeAutoConfirm(string reason)
    {
        if (!AutoTradeConfirm || !TradeHandshakeOk || TradeDone)
            return;
        Send(new C.TradeConfirm { Locked = true });
        Note($"auto TradeConfirm Locked=true ({reason})");
    }

    void TryBuyKeyFromDialog(uint npcId)
    {
        string? key = null;
        foreach (string line in NpcDialogLines)
        {
            string u = line.ToUpperInvariant();
            if (u.Contains("@BUYSELL")) { key = "[@BUYSELL]"; break; }
            if (u.Contains("@BUY") && key == null) key = "[@BUY]";
        }
        if (key == null) return;
        Send(new C.CallNPC { ObjectID = npcId, Key = key });
        Note($"input Talk CallNPC buy-key {key} id={npcId}");
        Pump(800);
    }

    public void Send(Packet packet)
    {
        byte[] data = packet.GetPacketBytes().ToArray();
        _client.Client.Send(data);
        Note($"send {packet.GetType().Name} ({data.Length} bytes)");
    }

    public bool Pump(int milliseconds)
    {
        DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        bool any = false;
        while (DateTime.UtcNow < until)
        {
            while (_receive.TryDequeue(out Packet? p) && p != null)
            {
                any = true;
                Handle(p);
            }
            Thread.Sleep(15);
        }
        while (_receive.TryDequeue(out Packet? p) && p != null)
        {
            any = true;
            Handle(p);
        }
        return any;
    }

    void Upsert(uint id, string name, Point location, string kind, string library, int spriteIndex)
    {
        if (!_objects.TryGetValue(id, out var obj))
        {
            obj = new WorldObject { ObjectID = id };
            _objects[id] = obj;
        }
        obj.Name = name;
        obj.Location = location;
        obj.Kind = kind;
        obj.SpriteLibrary = library;
        obj.SpriteIndex = spriteIndex;
    }

    void Handle(Packet p)
    {
        Note($"recv {p.GetType().Name} index={p.Index}");
        switch (p)
        {
            case S.Connected:
                GotConnected = true;
                Note("handshake: Connected");
                break;
            case S.ClientVersion v:
                VersionResult = v.Result;
                VersionCheckOk = v.Result == 1;
                Note($"handshake: ClientVersion Result={v.Result} ({(v.Result == 1 ? "match" : "reject")}) VersionCheckOk={VersionCheckOk} src={VersionHashSource ?? "-"} md5={VersionHashHex ?? "-"}");
                break;
            case S.NewAccount n:
                NewAccountResult = n.Result;
                Note($"NewAccount Result={n.Result} ({DescribeNewAccount(n.Result)})");
                break;
            case S.Login login:
                LoginResult = login.Result;
                Note($"Login Result={login.Result} ({DescribeLogin(login.Result)})");
                break;
            case S.LoginBanned ban:
                Note($"LoginBanned reason={ban.Reason} expiry={ban.ExpiryDate:u}");
                break;
            case S.LoginSuccess ok:
                LoginSuccess = true;
                Characters.Clear();
                if (ok.Characters != null)
                    Characters.AddRange(ok.Characters);
                CharacterCount = Characters.Count;
                Note($"LoginSuccess characters={CharacterCount}");
                foreach (var ch in Characters)
                    Note($"  select index={ch.Index} name={ch.Name} class={ch.Class} level={ch.Level}");
                break;
            case S.NewCharacter nc:
                NewCharacterResult = nc.Result;
                Note($"NewCharacter Result={nc.Result} ({DescribeNewCharacter(nc.Result)})");
                break;
            case S.NewCharacterSuccess ncs:
                NewCharacterOk = true;
                NewCharacterResult = 10;
                if (ncs.CharInfo != null)
                {
                    Characters.Add(ncs.CharInfo);
                    CharacterCount = Characters.Count;
                    Note($"NewCharacterSuccess index={ncs.CharInfo.Index} name={ncs.CharInfo.Name} class={ncs.CharInfo.Class}");
                }
                break;
            case S.StartGame sg:
                StartGameResult = sg.Result;
                Note($"StartGame Result={sg.Result} ({DescribeStartGame(sg.Result)}) resolution={sg.Resolution}");
                if (sg.Result == 4)
                    Note("select→game: StartGame accepted (GameScene equivalent)");
                break;
            case S.StartGameBanned sgb:
                Note($"StartGameBanned reason={sgb.Reason} expiry={sgb.ExpiryDate:u}");
                break;
            case S.StartGameDelay sgd:
                Note($"StartGameDelay ms={sgd.Milliseconds}");
                break;
            case S.MapInformation map:
                MapIndex = map.MapIndex;
                MapFileName = map.FileName;
                MapTitle = map.Title;
                MiniMapIndex = map.MiniMap;
                InMap = true;
                Note($"in-map: MapInformation index={map.MapIndex} file={map.FileName} title={map.Title} lights={map.Lights} minimapLib={map.MiniMap} (MMap.Lib via --data / catalog when present)");
                break;
            case S.UserInformation user:
                UserObjectId = user.ObjectID;
                UserName = user.Name;
                UserLocation = user.Location;
                Facing = user.Direction;
                InMap = true;
                Upsert(user.ObjectID, user.Name, user.Location, "player", "CArmour/00.Lib", 0);
                UserHP = user.HP;
                UserMP = user.MP;
                UserMaxHP = Math.Max(user.HP, UserMaxHP);
                UserMaxMP = Math.Max(user.MP, UserMaxMP);
                UserLevel = user.Level;
                UserClass = user.Class;
                UserGold = user.Gold;
                IngestUserItems(user);
                Note($"in-map: UserInformation id={user.ObjectID} name={user.Name} class={user.Class} loc={user.Location.X},{user.Location.Y} hp={user.HP}/{user.MP} bag={_bag.Count} equip={EquippedCount()}");
                break;
            case S.HealthChanged hc:
                UserHP = hc.HP;
                UserMP = hc.MP;
                UserMaxHP = Math.Max(UserMaxHP, hc.HP);
                UserMaxMP = Math.Max(UserMaxMP, hc.MP);
                break;
            case S.UserLocation loc:
                UserLocation = loc.Location;
                Facing = loc.Direction;
                WalkAck = WalkSent;
                if (_objects.TryGetValue(UserObjectId, out var self))
                    self.Location = loc.Location;
                Note($"UserLocation {loc.Location.X},{loc.Location.Y} dir={loc.Direction}");
                break;
            case S.ObjectPlayer op:
                Upsert(op.ObjectID, op.Name, op.Location, "player", "CArmour/00.Lib", 0);
                Note($"ObjectPlayer id={op.ObjectID} name={op.Name} loc={op.Location.X},{op.Location.Y}");
                break;
            case S.ObjectMonster om:
                Upsert(om.ObjectID, om.Name, om.Location, "monster", "CArmour/00.Lib", 0);
                Note($"ObjectMonster id={om.ObjectID} name={om.Name} loc={om.Location.X},{om.Location.Y}");
                break;
            case S.ObjectNPC npc:
                Upsert(npc.ObjectID, npc.Name, npc.Location, "npc", "CArmour/00.Lib", 0);
                if (_objects.TryGetValue(npc.ObjectID, out var npcObj))
                {
                    npcObj.QuestIDs.Clear();
                    if (npc.QuestIDs != null)
                        npcObj.QuestIDs.AddRange(npc.QuestIDs);
                }
                Note($"ObjectNPC id={npc.ObjectID} name={npc.Name} loc={npc.Location.X},{npc.Location.Y} quests={npc.QuestIDs?.Count ?? 0}");
                break;
            case S.ObjectItem item:
                Upsert(item.ObjectID, item.Name, item.Location, "item", "CArmour/00.Lib", 0);
                _ground.Add(new GroundLoot { ObjectID = item.ObjectID, Name = item.Name, Location = item.Location, Gold = 0 });
                Note($"ObjectItem id={item.ObjectID} name={item.Name} loc={item.Location.X},{item.Location.Y}");
                break;
            case S.ObjectGold gold:
                Upsert(gold.ObjectID, $"Gold:{gold.Gold}", gold.Location, "gold", "CArmour/00.Lib", 0);
                _ground.Add(new GroundLoot { ObjectID = gold.ObjectID, Name = $"Gold:{gold.Gold}", Location = gold.Location, Gold = gold.Gold });
                Note($"ObjectGold id={gold.ObjectID} gold={gold.Gold} loc={gold.Location.X},{gold.Location.Y}");
                break;
            case S.NewItemInfo nii when nii.Info != null:
                _itemInfos[nii.Info.Index] = nii.Info;
                break;
            case S.GainedItem gained when gained.Item != null:
                PlaceInBag(gained.Item);
                Note($"GainedItem uid={gained.Item.UniqueID} index={gained.Item.ItemIndex} name={ItemName(gained.Item)} count={gained.Item.Count} bag={_bag.Count} slot={SlotOf(gained.Item.UniqueID)}");
                break;
            case S.GainedGold gg:
                _goldGained += gg.Gold;
                UserGold += gg.Gold;
                GoldEarned += gg.Gold;
                Note($"GainedGold +{gg.Gold} totalSession={_goldGained} gold={UserGold}");
                if (TradeDone)
                    TradeEvidence = $"TradeConfirm success partner={TradePartnerName ?? "-"} gold={UserGold} bag={BagCount}";
                break;
            case S.LoseGold lg:
                GoldSpent += lg.Gold;
                UserGold = UserGold > lg.Gold ? UserGold - lg.Gold : 0;
                Note($"LoseGold -{lg.Gold} gold={UserGold}");
                break;
            case S.SellItem sold:
                Note($"SellItem Success={sold.Success} uid={sold.UniqueID} count={sold.Count}");
                if (sold.Success)
                {
                    SellOk = true;
                    if (_bag.TryGetValue(sold.UniqueID, out var soldItem))
                    {
                        if (sold.Count >= soldItem.Count)
                        {
                            _bag.Remove(sold.UniqueID);
                            ClearInventorySlot(sold.UniqueID);
                        }
                        else
                            soldItem.Count = (ushort)(soldItem.Count - sold.Count);
                        SellEvidence = $"SellItem Success name={ItemName(soldItem)} uid={sold.UniqueID} gold={UserGold} bag={_bag.Count}";
                    }
                    else
                        SellEvidence = $"SellItem Success uid={sold.UniqueID} gold={UserGold} bag={_bag.Count}";
                    Note(SellEvidence);
                }
                break;
            case S.MoveItem mv:
                Note($"MoveItem Success={mv.Success} grid={mv.Grid} from={mv.From} to={mv.To}");
                if (mv.Success && mv.Grid == MirGridType.Inventory)
                {
                    string moved = mv.From >= 0 && mv.From < _inventory.Length && _inventory[mv.From] != null
                        ? ItemName(_inventory[mv.From]!)
                        : "?";
                    ApplyInventorySwap(mv.From, mv.To);
                    DragOk = true;
                    DragEvidence = $"MoveItem {moved} slot {mv.From}→{mv.To}";
                    Note(DragEvidence);
                }
                break;
            case S.MergeItem mg:
                Note($"MergeItem Success={mg.Success} fromUid={mg.IDFrom} toUid={mg.IDTo}");
                if (mg.Success)
                {
                    MergeOk = true;
                    int fromSlot = SlotOf(mg.IDFrom);
                    int toSlot = SlotOf(mg.IDTo);
                    if (fromSlot >= 0 && _inventory[fromSlot] != null && toSlot >= 0 && _inventory[toSlot] != null)
                    {
                        _inventory[toSlot]!.Count += _inventory[fromSlot]!.Count;
                        _bag.Remove(mg.IDFrom);
                        _inventory[fromSlot] = null;
                    }
                    MergeEvidence = $"MergeItem Success from={fromSlot} to={toSlot}";
                    Note(MergeEvidence);
                }
                break;
            case S.EquipItem eq:
                Note($"EquipItem Success={eq.Success} grid={eq.Grid} to={eq.To} uid={eq.UniqueID}");
                if (eq.Success)
                {
                    EquipOk = true;
                    if (_bag.TryGetValue(eq.UniqueID, out var worn))
                    {
                        _bag.Remove(eq.UniqueID);
                        ClearInventorySlot(eq.UniqueID);
                        if (eq.To >= 0)
                        {
                            if (_equipment.Length <= eq.To)
                                Array.Resize(ref _equipment, eq.To + 1);
                            _equipment[eq.To] = worn;
                        }
                        EquipEvidence = $"EquipItem Success slot={(EquipmentSlot)eq.To} name={ItemName(worn)} uid={eq.UniqueID}";
                    }
                    else
                        EquipEvidence = $"EquipItem Success slot={(EquipmentSlot)eq.To} uid={eq.UniqueID}";
                    Note(EquipEvidence);
                }
                break;
            case S.ObjectStruck struck:
                Note($"ObjectStruck id={struck.ObjectID} attacker={struck.AttackerID} loc={struck.Location.X},{struck.Location.Y}");
                if (_attacksSent > 0 && struck.AttackerID == UserObjectId && struck.ObjectID != UserObjectId)
                {
                    FightHit = true;
                    FightEvidence = $"ObjectStruck id={struck.ObjectID} by self";
                }
                break;
            case S.DamageIndicator dmg:
                Note($"DamageIndicator id={dmg.ObjectID} dmg={dmg.Damage} type={dmg.Type}");
                if (_attacksSent > 0 && dmg.ObjectID != UserObjectId && dmg.ObjectID == _fightTargetId && dmg.Damage != 0)
                {
                    FightHit = true;
                    FightEvidence ??= $"DamageIndicator id={dmg.ObjectID} dmg={dmg.Damage}";
                }
                break;
            case S.ObjectHealth hp:
                Note($"ObjectHealth id={hp.ObjectID} percent={hp.Percent}");
                if (_attacksSent > 0 && hp.ObjectID == _fightTargetId)
                {
                    FightHit = true;
                    FightEvidence ??= $"ObjectHealth id={hp.ObjectID} percent={hp.Percent}";
                }
                break;
            case S.ObjectDied died:
                Note($"ObjectDied id={died.ObjectID} loc={died.Location.X},{died.Location.Y}");
                if (_attacksSent > 0 && (died.ObjectID == _fightTargetId || FightHit))
                {
                    FightDied = true;
                    FightHit = true;
                    FightEvidence ??= $"ObjectDied id={died.ObjectID}";
                }
                if (_objects.TryGetValue(died.ObjectID, out var corpse))
                    corpse.Kind = "corpse";
                break;
            case S.Struck:
                Note($"Struck attacker={((S.Struck)p).AttackerID}");
                break;
            case S.ObjectWalk ow:
                if (_objects.TryGetValue(ow.ObjectID, out var walker))
                    walker.Location = ow.Location;
                if (ow.ObjectID == UserObjectId)
                {
                    UserLocation = ow.Location;
                    WalkAck = WalkSent;
                }
                Note($"ObjectWalk id={ow.ObjectID} loc={ow.Location.X},{ow.Location.Y} dir={ow.Direction}");
                break;
            case S.ObjectTurn ot:
                if (_objects.TryGetValue(ot.ObjectID, out var turner))
                    turner.Location = ot.Location;
                if (ot.ObjectID == UserObjectId)
                {
                    UserLocation = ot.Location;
                    Facing = ot.Direction;
                }
                Note($"ObjectTurn id={ot.ObjectID} loc={ot.Location.X},{ot.Location.Y} dir={ot.Direction}");
                break;
            case S.ObjectRemove rem:
                _objects.Remove(rem.ObjectID);
                break;
            case S.Chat chat:
                ChatRecv++;
                LastChat = chat.Message;
                _chatLines.Add(chat.Message);
                if (_chatLines.Count > 4)
                    _chatLines.RemoveAt(0);
                if (ChatSent > 0 && _lastSentChat.Length > 0
                    && chat.Message.Contains(_lastSentChat, StringComparison.OrdinalIgnoreCase))
                    ChatEcho = true;
                Note($"Chat recv [{chat.Type}] {chat.Message} echo={ChatEcho}");
                break;
            case S.NPCResponse page:
                NpcTalkOk = true;
                NpcDialogLines.Clear();
                if (page.Page != null)
                    NpcDialogLines.AddRange(page.Page.Where(l => !string.IsNullOrWhiteSpace(l)));
                Note($"NPCResponse lines={NpcDialogLines.Count} npc={NpcName} id={NpcObjectId}");
                foreach (string line in NpcDialogLines.Take(8))
                    Note($"  npc-say {line}");
                break;
            case S.NPCGoods goods:
                NpcGoods.Clear();
                _goodsItems.Clear();
                _goodsType = goods.Type;
                if (goods.List != null)
                {
                    foreach (var it in goods.List)
                    {
                        if (it == null) continue;
                        _goodsItems.Add(it);
                        NpcGoods.Add(ItemName(it));
                    }
                }
                Note($"NPCGoods count={NpcGoods.Count} rate={goods.Rate} type={goods.Type}");
                foreach (string name in NpcGoods.Take(8))
                    Note($"  npc-goods {name}");
                break;
            case S.DefaultNPC def:
                _defaultNpcId = def.ObjectID;
                Note($"DefaultNPC id={def.ObjectID}");
                break;
            case S.NPCUpdate nup:
                NpcObjectId = nup.NPCID;
                Note($"NPCUpdate id={nup.NPCID}");
                break;
            case S.TradeRequest tr:
                TradeInviteFrom = tr.Name;
                Note($"TradeRequest from {tr.Name}");
                if (AutoTradeReply)
                {
                    Send(new C.TradeReply { AcceptInvite = true });
                    Note($"auto TradeReply AcceptInvite=true from={tr.Name}");
                }
                break;
            case S.TradeAccept acc:
                TradeHandshakeOk = true;
                TradePartnerName = acc.Name;
                TradeEvidence ??= $"TradeAccept partner={acc.Name}";
                Note($"TradeAccept partner={acc.Name}");
                break;
            case S.TradeGold tg:
                TradeGoldSeen = tg.Amount;
                TradeGoldOk = true;
                Note($"TradeGold offer={tg.Amount} partner={TradePartnerName ?? "-"}");
                MaybeAutoConfirm("TradeGold");
                break;
            case S.TradeItem ti:
                int filled = ti.TradeItems?.Count(x => x != null) ?? 0;
                Note($"TradeItem slots={ti.TradeItems?.Length ?? 0} filled={filled}");
                MaybeAutoConfirm("TradeItem");
                break;
            case S.TradeConfirm:
                TradeDone = true;
                TradeEvidence = $"TradeConfirm success partner={TradePartnerName ?? "-"} gold={UserGold} bag={BagCount}";
                Note(TradeEvidence);
                break;
            case S.TradeCancel tc:
                Note($"TradeCancel Unlock={tc.Unlock} partner={TradePartnerName ?? "-"}");
                break;
            case S.DepositTradeItem dep:
                Note($"DepositTradeItem Success={dep.Success} from={dep.From} to={dep.To}");
                if (dep.Success)
                {
                    TradeDepositOk = true;
                    if (dep.From >= 0 && dep.From < _inventory.Length)
                    {
                        var moved = _inventory[dep.From];
                        if (moved != null)
                        {
                            _bag.Remove(moved.UniqueID);
                            _inventory[dep.From] = null;
                        }
                    }
                }
                break;
            case S.NewQuestInfo nq when nq.Info != null:
                _questInfo[nq.Info.Index] = nq.Info;
                if (!string.IsNullOrWhiteSpace(nq.Info.Name) && !QuestNames.Contains(nq.Info.Name))
                    QuestNames.Add(nq.Info.Name);
                Note($"NewQuestInfo {nq.Info.Index} {nq.Info.Name} npc={nq.Info.NPCIndex} finish={nq.Info.FinishNPCIndex} lv={nq.Info.MinLevelNeeded}");
                break;
            case S.ChangeQuest cq when cq.Quest != null:
            {
                var prog = cq.Quest;
                if (prog.QuestInfo == null && _questInfo.TryGetValue(prog.Id, out var bound))
                    prog.QuestInfo = bound;
                string qname = prog.QuestInfo?.Name ?? QuestLabel(prog.Id);
                switch (cq.QuestState)
                {
                    case QuestState.Add:
                    case QuestState.Update:
                        _takenQuests[prog.Id] = prog;
                        if (cq.QuestState == QuestState.Add && _pendingAccept == prog.Id)
                        {
                            QuestAcceptOk = true;
                            QuestAcceptEvidence = $"AcceptQuest id={prog.Id} name={qname} S.ChangeQuest Add taken={prog.Taken} completed={prog.Completed}";
                            Note(QuestAcceptEvidence);
                        }
                        break;
                    case QuestState.Remove:
                        _takenQuests.Remove(prog.Id);
                        if (_pendingFinish == prog.Id)
                        {
                            QuestFinishOk = true;
                            QuestFinishEvidence = $"FinishQuest id={prog.Id} name={qname} S.ChangeQuest Remove";
                            Note(QuestFinishEvidence);
                        }
                        break;
                }
                Note($"ChangeQuest {cq.QuestState} id={prog.Id} name={qname} taken={prog.Taken} completed={prog.Completed} track={cq.TrackQuest}");
                break;
            }
            case S.CompleteQuest cq:
                _completedQuestIds.Clear();
                foreach (int id in cq.CompletedQuests)
                    _completedQuestIds.Add(id);
                Note($"CompleteQuest count={_completedQuestIds.Count} ids={string.Join(',', _completedQuestIds.Take(8))}");
                break;
            case S.GainedQuestItem gq when gq.Item != null:
                Note($"GainedQuestItem uid={gq.Item.UniqueID} index={gq.Item.ItemIndex} name={ItemName(gq.Item)} x{gq.Item.Count}");
                break;
            case S.DeleteQuestItem dq:
                Note($"DeleteQuestItem uid={dq.UniqueID} count={dq.Count}");
                break;
            case S.ShareQuest sq:
                Note($"ShareQuest id={sq.QuestIndex} from={sq.SharerName}");
                break;
            case S.NewMagic nm when nm.Magic != null && !nm.Hero:
                _magics.Add(nm.Magic);
                Note($"NewMagic {nm.Magic.Name} spell={nm.Magic.Spell} key={nm.Magic.Key} magics={_magics.Count}");
                break;
            case S.Magic sm:
                MagicOk = sm.Cast || MagicOk;
                MagicEvidence = $"S.Magic spell={sm.Spell} cast={sm.Cast} target={sm.TargetID} at={sm.Target.X},{sm.Target.Y}";
                Note(MagicEvidence);
                break;
            case S.MagicCast mc:
                Note($"S.MagicCast spell={mc.Spell}");
                break;
            case S.MagicLeveled ml:
                foreach (var mag in _magics)
                {
                    if (mag.Spell == ml.Spell)
                    {
                        mag.Level = ml.Level;
                        mag.Experience = ml.Experience;
                    }
                }
                Note($"MagicLeveled {ml.Spell} lv={ml.Level}");
                break;
            case S.Disconnect d:
                Note($"Disconnect Reason={d.Reason}");
                break;
            case S.KeepAlive:
                break;
            default:
                break;
        }
    }

    void BeginReceive()
    {
        try
        {
            _client.Client.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, OnReceive, null);
        }
        catch (Exception ex)
        {
            Note($"receive begin failed: {ex.Message}");
        }
    }

    void OnReceive(IAsyncResult ar)
    {
        int read;
        try
        {
            read = _client.Client.EndReceive(ar);
        }
        catch (Exception ex)
        {
            Note($"receive ended: {ex.Message}");
            return;
        }

        if (read == 0)
        {
            Note("server closed the socket");
            return;
        }

        byte[] merged = new byte[_raw.Length + read];
        Buffer.BlockCopy(_raw, 0, merged, 0, _raw.Length);
        Buffer.BlockCopy(_buffer, 0, merged, _raw.Length, read);
        _raw = merged;

        Packet? p;
        while ((p = Packet.ReceivePacket(_raw, out _raw)) != null)
            _receive.Enqueue(p);

        BeginReceive();
    }

    public static CrystalSession Run(ConnectOptions opt)
    {
        var session = new CrystalSession();
        session.ExitCode = session.RunAttempt(opt);
        return session;
    }

    int RunAttempt(ConnectOptions opt)
    {
        Console.WriteLine($"Client.Linux protocol attempt → {opt.Host}:{opt.Port} account={opt.Account} new={opt.CreateAccount} loginOnly={opt.LoginOnly}");
        try
        {
            Connect(opt.Host, opt.Port);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Connect failed: {ex.Message}");
            return 5;
        }

        Pump(800);
        if (!GotConnected)
        {
            Console.Error.WriteLine("No S.Connected packet (server not Crystal, or not listening).");
            Dump();
            return 5;
        }

        byte[] hash = VersionHash.Resolve(opt.VersionFile, opt.VersionHashHex, out string hashSource);
        VersionHashSource = hashSource;
        VersionHashHex = VersionHash.ToHex(hash);
        Console.WriteLine($"version: src={hashSource} md5={VersionHashHex} bytes={hash.Length}");
        Send(new C.ClientVersion { VersionHash = hash });
        Pump(800);

        if (VersionResult is 0)
        {
            Console.Error.WriteLine("Server rejected client version. Point Server.Linux --version-path / Client.Linux --version-file at the same file (or matching --version-hashes), or pass --no-version-check.");
            Dump();
            return 6;
        }
        if (VersionResult == 1)
            VersionCheckOk = true;

        if (opt.CreateAccount)
        {
            Send(new C.NewAccount
            {
                AccountID = opt.Account,
                Password = opt.Password,
                BirthDate = new DateTime(2000, 1, 1),
                UserName = "Linux",
                SecretQuestion = "q",
                SecretAnswer = "a",
                EMailAddress = "linux@example.com"
            });
            Pump(800);
        }

        Send(new C.Login { AccountID = opt.Account, Password = opt.Password });
        Pump(Math.Max(800, opt.WaitMs));

        if (!LoginSuccess)
        {
            Dump();
            Console.WriteLine($"GotConnected={GotConnected} VersionResult={VersionResult?.ToString() ?? "(none)"}");
            Console.WriteLine($"NewAccountResult={NewAccountResult?.ToString() ?? "(none)"} LoginResult={LoginResult?.ToString() ?? "(none)"} LoginSuccess={LoginSuccess}");
            Console.WriteLine("Runtime parity (login→select→walk→fight→loot→equip) is a later gate.");
            if (GotConnected && VersionResult is 1 or null)
                return 0;
            return 5;
        }

        if (opt.LoginOnly)
        {
            Dump();
            Console.WriteLine($"LoginSuccess={LoginSuccess} Characters={CharacterCount}");
            Console.WriteLine("Runtime parity (login→select→walk→fight→loot→equip) is a later gate.");
            return 0;
        }

        int characterIndex = EnsureCharacter(opt.CharacterName);
        if (characterIndex < 0)
        {
            Dump();
            Console.Error.WriteLine("No character after NewCharacter; cannot StartGame.");
            return 7;
        }

        Note($"StartGame CharacterIndex={characterIndex}");
        Send(new C.StartGame { CharacterIndex = characterIndex });
        Pump(Math.Max(2000, opt.EnterWaitMs));

        if (StartGameResult == 4 || InMap)
            InMap = InMap || StartGameResult == 4;

        if (InMap && opt.Walk)
        {
            WalkSent = true;
            Send(new C.Walk { Direction = MirDirection.Right });
            Pump(800);
            Note($"walk sent Right; ack={WalkAck} loc={UserLocation.X},{UserLocation.Y}");
        }

        if (InMap && opt.PlayGate)
            PlayGate();

        Dump();
        Console.WriteLine($"VersionCheckOk={VersionCheckOk} VersionResult={VersionResult?.ToString() ?? "(none)"} src={VersionHashSource ?? "-"} md5={VersionHashHex ?? "-"}");
        Console.WriteLine($"LoginSuccess={LoginSuccess} NewCharacterOk={NewCharacterOk} NewCharacterResult={NewCharacterResult?.ToString() ?? "(none)"}");
        Console.WriteLine($"StartGameResult={StartGameResult?.ToString() ?? "(none)"} InMap={InMap} Map={MapFileName} Title={MapTitle} User={UserName} Loc={UserLocation.X},{UserLocation.Y} Objects={_objects.Count} WalkAck={WalkAck}");
        Console.WriteLine($"FightHit={FightHit} FightDied={FightDied} LootOk={LootOk} EquipOk={EquipOk}");
        if (FightEvidence != null) Console.WriteLine($"  fight : {FightEvidence}");
        if (LootEvidence != null) Console.WriteLine($"  loot  : {LootEvidence}");
        if (EquipEvidence != null) Console.WriteLine($"  equip : {EquipEvidence}");
        Console.WriteLine("Hard gate (login→select→walk→fight→loot→equip) is claimed only when all three verbs succeed in one session.");

        if (InMap && opt.PlayGate)
            return FightHit && LootOk && EquipOk ? 0 : 9;
        if (InMap && StartGameResult is 4 or null)
            return 0;
        if (StartGameResult == 4)
            return 0;
        if (StartGameResult == 0)
        {
            Console.Error.WriteLine("StartGame disabled. Start Server.Linux with --allow-start-game (and a full Jev --root, no --listen-without-world).");
            return 8;
        }
        if (StartGameResult == 3)
        {
            Console.Error.WriteLine("StartGame Result 3: no active map/start point. Need Maps + Server.MirDB (full world).");
            return 8;
        }
        return 7;
    }

    void PlayGate()
    {
        Note("Phase E: fight → loot → equip (Shared packets + existing @ commands)");
        Chat("@LEVEL 15");
        Pump(600);

        TryFight();
        TryLoot();
        TryEquip();
    }

    void TryFight()
    {
        string[] mobs = { "Chicken", "Deer", "CaveMaggot", "HookingCat", "Scarecrow", "Spider", "Wolf" };
        foreach (string mob in mobs)
        {
            int before = MonsterCount();
            Chat($"@MOB {mob}");
            Pump(700);
            var target = _objects.Values.LastOrDefault(o => o.Kind == "monster");
            if (target == null || MonsterCount() <= before && !FightHit)
            {
                Note($"no spawn visible for {mob}");
                continue;
            }

            Note($"fight target id={target.ObjectID} name={target.Name} loc={target.Location.X},{target.Location.Y}");
            _fightTargetId = target.ObjectID;
            for (int swing = 0; swing < 16 && !FightDied; swing++)
            {
                if (_objects.TryGetValue(target.ObjectID, out var live))
                    target = live;
                MirDirection dir = Functions.DirectionFromPoint(UserLocation, target.Location);
                if (UserLocation == target.Location)
                    dir = Facing;
                _attacksSent++;
                Send(new C.Attack { Direction = dir, Spell = Spell.None });
                Pump(750);
                if (FightHit)
                    break;
            }

            if (!FightHit)
            {
                Pump(1200);
            }

            if (FightHit)
            {
                Note($"fight evidence: {FightEvidence} died={FightDied}");
                return;
            }
        }

        Note("fight: no ObjectStruck/Damage/Death after scripted Attack");
    }

    void TryLoot()
    {
        int bagBefore = _bag.Count;
        uint goldBefore = _goldGained;
        Pump(400);
        var drop = _ground.LastOrDefault();
        if (drop != null && TryPickGround(drop, bagBefore, goldBefore))
            return;

        if (_bag.Count > bagBefore)
        {
            MarkLoot($"inventory grew after PickUp ({bagBefore}→{_bag.Count})");
            return;
        }

        // Seeded loot: MAKE (or use bag junk), drop at feet, PickUp — still C.PickUp + GainedItem.
        string[] seeds = { "(HP)DrugSmall", "Meat", "Dagger", "WoodenSword", "Candle" };
        UserItem? seed = _bag.Values.FirstOrDefault(i => ItemTypeOf(i) is ItemType.Potion or ItemType.Meat or ItemType.Nothing);
        if (seed == null)
        {
            int beforeMake = _bag.Count;
            foreach (string name in seeds)
            {
                Chat($"@MAKE {name}");
                Pump(500);
                seed = _bag.Values.LastOrDefault();
                if (_bag.Count > beforeMake)
                    break;
            }
        }

        if (seed == null)
        {
            Note("loot: no ground item and MAKE produced nothing");
            return;
        }

        ulong uid = seed.UniqueID;
        string seedName = ItemName(seed);
        _bag.Remove(uid);
        Send(new C.DropItem { UniqueID = uid, Count = 1 });
        Pump(800);
        var seeded = _ground.LastOrDefault(g => g.Name.Contains(seedName, StringComparison.OrdinalIgnoreCase))
                     ?? _ground.LastOrDefault();
        if (seeded != null && TryPickGround(seeded, _bag.Count, _goldGained))
            return;

        // Stand on the drop: walk every adjacent step then PickUp.
        foreach (MirDirection dir in Enum.GetValues<MirDirection>())
        {
            Send(new C.Walk { Direction = dir });
            Pump(400);
            Send(new C.PickUp());
            Pump(400);
            if (_bag.ContainsKey(uid) || _bag.Count > bagBefore || _goldGained > goldBefore)
            {
                MarkLoot($"PickUp seeded {seedName} uid={uid} after walk {dir}");
                return;
            }
        }
        Note($"loot: PickUp after drop did not restore {seedName}");
    }

    bool TryPickGround(GroundLoot drop, int bagBefore, uint goldBefore)
    {
        WalkToward(drop.Location, 6);
        Send(new C.PickUp());
        Pump(700);
        if (_bag.Count > bagBefore || _goldGained > goldBefore || _bag.Values.Any(i => ItemName(i).Contains(drop.Name.Split(':')[0], StringComparison.OrdinalIgnoreCase)))
        {
            MarkLoot(_goldGained > goldBefore
                ? $"PickUp gold +{_goldGained - goldBefore} at {drop.Location.X},{drop.Location.Y}"
                : $"PickUp ground {drop.Name} at {drop.Location.X},{drop.Location.Y} bag={_bag.Count}");
            return true;
        }
        return false;
    }

    void WalkToward(Point dest, int steps)
    {
        for (int i = 0; i < steps && UserLocation != dest; i++)
        {
            MirDirection dir = Functions.DirectionFromPoint(UserLocation, dest);
            WalkSent = true;
            Send(new C.Walk { Direction = dir });
            Pump(350);
        }
    }

    void TryEquip()
    {
        if (EquipOk)
            return;

        UserItem? wear = _bag.Values.FirstOrDefault(i => SlotFor(ItemTypeOf(i)) is >= 0);
        if (wear == null)
        {
            string[] gear = { "WoodenSword", "Dagger", "BronzeSword", "BaseDress", "Cloth", "BronzeHelmet" };
            int before = _bag.Count;
            foreach (string name in gear)
            {
                Chat($"@MAKE {name}");
                Pump(500);
                wear = _bag.Values.LastOrDefault(i => SlotFor(ItemTypeOf(i)) is >= 0);
                if (wear != null || _bag.Count > before)
                    break;
            }
            wear ??= _bag.Values.LastOrDefault();
        }

        if (wear == null)
        {
            Note("equip: no inventory item to wear");
            return;
        }

        int slot = SlotFor(ItemTypeOf(wear));
        if (slot < 0) slot = (int)EquipmentSlot.Weapon;
        Note($"equip try name={ItemName(wear)} uid={wear.UniqueID} slot={(EquipmentSlot)slot}");
        Send(new C.EquipItem { Grid = MirGridType.Inventory, UniqueID = wear.UniqueID, To = slot });
        Pump(800);
        if (!EquipOk)
            Note("equip: S.EquipItem Success=false (class/level/slot mismatch)");
    }

    void Chat(string message)
    {
        Send(new C.Chat { Message = message });
    }

    void MarkLoot(string evidence)
    {
        LootOk = true;
        LootEvidence = evidence;
        Note("loot evidence: " + evidence);
    }

    void IngestUserItems(S.UserInformation user)
    {
        _bag.Clear();
        if (user.Inventory != null)
        {
            foreach (var it in user.Inventory)
            {
                if (it == null) continue;
                _bag[it.UniqueID] = it;
                Note($"  bag uid={it.UniqueID} index={it.ItemIndex} name={ItemName(it)}");
            }
        }
        _equipment = user.Equipment ?? Array.Empty<UserItem?>();
        _inventory = user.Inventory is { Length: > 0 } inv
            ? (UserItem?[])inv.Clone()
            : new UserItem?[46];
        if (user.Equipment != null)
        {
            for (int i = 0; i < user.Equipment.Length; i++)
            {
                var it = user.Equipment[i];
                if (it == null) continue;
                Note($"  equip[{(EquipmentSlot)i}] uid={it.UniqueID} name={ItemName(it)}");
            }
        }
        _magics.Clear();
        if (user.Magics != null)
        {
            _magics.AddRange(user.Magics);
            foreach (var mag in _magics)
                Note($"  magic {mag.Name} spell={mag.Spell} lv={mag.Level} key={mag.Key}");
        }
    }

    void PlaceInBag(UserItem item)
    {
        _bag[item.UniqueID] = item;
        if (_inventory.Length == 0)
            _inventory = new UserItem?[46];
        int slot = -1;
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] == null)
            {
                slot = i;
                break;
            }
        }
        if (slot < 0)
        {
            slot = _inventory.Length;
            Array.Resize(ref _inventory, slot + 1);
        }
        _inventory[slot] = item;
    }

    void ClearInventorySlot(ulong uniqueId)
    {
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i]?.UniqueID == uniqueId)
                _inventory[i] = null;
        }
    }

    int SlotOf(ulong uniqueId)
    {
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i]?.UniqueID == uniqueId)
                return i;
        }
        return -1;
    }

    int EquippedCount() => _equipment.Count(e => e != null);

    int MonsterCount() => _objects.Values.Count(o => o.Kind == "monster");

    string ItemName(UserItem item)
    {
        if (item.Info != null && !string.IsNullOrWhiteSpace(item.Info.Name))
            return item.Info.Name;
        if (_itemInfos.TryGetValue(item.ItemIndex, out var info))
            return info.Name;
        return $"#{item.ItemIndex}";
    }

    ItemType ItemTypeOf(UserItem item)
    {
        if (item.Info != null)
            return item.Info.Type;
        return _itemInfos.TryGetValue(item.ItemIndex, out var info) ? info.Type : ItemType.Nothing;
    }

    static int SlotFor(ItemType type) => type switch
    {
        ItemType.Weapon => (int)EquipmentSlot.Weapon,
        ItemType.Armour => (int)EquipmentSlot.Armour,
        ItemType.Helmet => (int)EquipmentSlot.Helmet,
        ItemType.Necklace => (int)EquipmentSlot.Necklace,
        ItemType.Bracelet => (int)EquipmentSlot.BraceletR,
        ItemType.Ring => (int)EquipmentSlot.RingR,
        ItemType.Amulet => (int)EquipmentSlot.Amulet,
        ItemType.Belt => (int)EquipmentSlot.Belt,
        ItemType.Boots => (int)EquipmentSlot.Boots,
        ItemType.Stone => (int)EquipmentSlot.Stone,
        ItemType.Torch => (int)EquipmentSlot.Torch,
        ItemType.Mount => (int)EquipmentSlot.Mount,
        _ => -1
    };

    int EnsureCharacter(string preferredName)
    {
        var existing = Characters.FirstOrDefault(c =>
            string.Equals(c.Name, preferredName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            Note($"using existing character {existing.Name} index={existing.Index}");
            return existing.Index;
        }
        if (Characters.Count > 0)
        {
            var first = Characters[0];
            Note($"using first character {first.Name} index={first.Index}");
            return first.Index;
        }

        string name = preferredName;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (attempt > 0)
                name = (preferredName.Length > 11 ? preferredName[..11] : preferredName) + (attempt + 1);
            Note($"NewCharacter name={name} class=Warrior");
            Send(new C.NewCharacter { Name = name, Gender = MirGender.Male, Class = MirClass.Warrior });
            Pump(1200);
            if (NewCharacterOk && Characters.Count > 0)
                return Characters[^1].Index;
            if (NewCharacterResult is 5)
                continue;
            break;
        }

        return Characters.Count > 0 ? Characters[^1].Index : -1;
    }

    void Dump()
    {
        Console.WriteLine("=== Protocol log ===");
        foreach (string line in _log)
            Console.WriteLine("  " + line);
    }

    static string DescribeNewAccount(byte r) => r switch
    {
        0 => "disabled",
        1 => "bad account id",
        2 => "bad password",
        3 => "bad email",
        4 => "bad name",
        5 => "bad question",
        6 => "bad answer",
        7 => "account exists",
        8 => "success",
        _ => "unknown"
    };

    static string DescribeLogin(byte r) => r switch
    {
        0 => "disabled",
        1 => "bad account id",
        2 => "bad password",
        3 => "account not exist",
        4 => "wrong password",
        5 => "must change password",
        _ => "unknown"
    };

    static string DescribeNewCharacter(byte r) => r switch
    {
        0 => "disabled",
        1 => "bad name",
        2 => "bad gender",
        3 => "bad class",
        4 => "max characters",
        5 => "exists",
        10 => "success",
        _ => "unknown"
    };

    static string DescribeStartGame(byte r) => r switch
    {
        0 => "disabled",
        1 => "not logged in",
        2 => "character not found",
        3 => "no map/start point",
        4 => "success",
        _ => "unknown"
    };

    void Note(string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
        _log.Add(line);
        Console.WriteLine(line);
    }

    public void Dispose()
    {
        try { _client.Close(); } catch { /* ignore */ }
    }

    sealed class GroundLoot
    {
        public uint ObjectID;
        public string Name = "";
        public Point Location;
        public uint Gold;
    }
}
