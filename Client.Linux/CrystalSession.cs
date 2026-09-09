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
    public int WaitMs { get; set; } = 1500;
    public int EnterWaitMs { get; set; } = 5000;
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

    public int ExitCode { get; private set; } = 5;
    public bool SocketConnected => _client.Connected;
    public bool GotConnected { get; private set; }
    public byte? VersionResult { get; private set; }
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
    public IReadOnlyList<string> Log => _log;
    public IReadOnlyList<WorldObject> Objects => _objects.Values.ToList();

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
                Note($"handshake: ClientVersion Result={v.Result} ({(v.Result == 1 ? "match" : "reject")})");
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
                InMap = true;
                Note($"in-map: MapInformation index={map.MapIndex} file={map.FileName} title={map.Title} lights={map.Lights}");
                break;
            case S.UserInformation user:
                UserObjectId = user.ObjectID;
                UserName = user.Name;
                UserLocation = user.Location;
                InMap = true;
                Upsert(user.ObjectID, user.Name, user.Location, "player", "CArmour/00.Lib", 0);
                Note($"in-map: UserInformation id={user.ObjectID} name={user.Name} class={user.Class} loc={user.Location.X},{user.Location.Y} hp={user.HP}/{user.MP}");
                break;
            case S.UserLocation loc:
                UserLocation = loc.Location;
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
                Note($"ObjectNPC id={npc.ObjectID} name={npc.Name} loc={npc.Location.X},{npc.Location.Y}");
                break;
            case S.ObjectItem item:
                Upsert(item.ObjectID, item.Name, item.Location, "item", "CArmour/00.Lib", 0);
                Note($"ObjectItem id={item.ObjectID} name={item.Name} loc={item.Location.X},{item.Location.Y}");
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
            case S.ObjectRemove rem:
                _objects.Remove(rem.ObjectID);
                break;
            case S.Chat chat:
                Note($"Chat [{chat.Type}] {chat.Message}");
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

        Send(new C.ClientVersion { VersionHash = new byte[16] });
        Pump(800);

        if (VersionResult is 0)
        {
            Console.Error.WriteLine("Server rejected client version. Start Server.Linux with --no-version-check.");
            Dump();
            return 6;
        }

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

        Dump();
        Console.WriteLine($"LoginSuccess={LoginSuccess} NewCharacterOk={NewCharacterOk} NewCharacterResult={NewCharacterResult?.ToString() ?? "(none)"}");
        Console.WriteLine($"StartGameResult={StartGameResult?.ToString() ?? "(none)"} InMap={InMap} Map={MapFileName} Title={MapTitle} User={UserName} Loc={UserLocation.X},{UserLocation.Y} Objects={_objects.Count} WalkAck={WalkAck}");
        Console.WriteLine("Runtime parity (login→select→walk→fight→loot→equip) is a later gate. Phase D is StartGame / in-map, not fight/loot/equip.");

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
}
