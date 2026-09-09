using System.Collections.Concurrent;
using System.Net.Sockets;
using C = ClientPackets;
using S = ServerPackets;

namespace Client.Linux;

/// <summary>
/// Shared-protocol TCP session (same framing as <c>Client.MirNetwork.Network</c>).
/// Enough to attempt Connected → ClientVersion → NewAccount/Login against a Crystal server.
/// </summary>
internal sealed class CrystalSession : IDisposable
{
    readonly TcpClient _client = new() { NoDelay = true };
    readonly ConcurrentQueue<Packet> _receive = new();
    readonly byte[] _buffer = new byte[8 * 1024];
    byte[] _raw = Array.Empty<byte>();
    readonly List<string> _log = new();

    public bool SocketConnected => _client.Connected;
    public bool GotConnected { get; private set; }
    public byte? VersionResult { get; private set; }
    public byte? NewAccountResult { get; private set; }
    public byte? LoginResult { get; private set; }
    public bool LoginSuccess { get; private set; }
    public int CharacterCount { get; private set; }
    public IReadOnlyList<string> Log => _log;

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
                CharacterCount = ok.Characters?.Count ?? 0;
                Note($"LoginSuccess characters={CharacterCount}");
                break;
            case S.Disconnect d:
                Note($"Disconnect Reason={d.Reason}");
                break;
            case S.KeepAlive:
                break;
            default:
                Note($"recv (unhandled for this host) {p.GetType().Name}");
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

    public static int RunAttempt(string host, int port, string account, string password, bool createAccount, int waitMs)
    {
        Console.WriteLine($"Client.Linux protocol attempt → {host}:{port} account={account} new={createAccount}");
        using var session = new CrystalSession();
        try
        {
            session.Connect(host, port);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Connect failed: {ex.Message}");
            return 5;
        }

        session.Pump(800);
        if (!session.GotConnected)
        {
            Console.Error.WriteLine("No S.Connected packet (server not Crystal, or not listening).");
            foreach (string line in session.Log) Console.WriteLine("  " + line);
            return 5;
        }

        session.Send(new C.ClientVersion { VersionHash = new byte[16] });
        session.Pump(800);

        if (session.VersionResult is 0)
        {
            Console.Error.WriteLine("Server rejected client version. Start Server.Linux with --no-version-check.");
            return 6;
        }

        if (createAccount)
        {
            session.Send(new C.NewAccount
            {
                AccountID = account,
                Password = password,
                BirthDate = new DateTime(2000, 1, 1),
                UserName = "Linux",
                SecretQuestion = "q",
                SecretAnswer = "a",
                EMailAddress = "linux@example.com"
            });
            session.Pump(800);
        }

        session.Send(new C.Login { AccountID = account, Password = password });
        session.Pump(Math.Max(800, waitMs));

        Console.WriteLine("=== Protocol log ===");
        foreach (string line in session.Log)
            Console.WriteLine("  " + line);

        Console.WriteLine($"GotConnected={session.GotConnected} VersionResult={session.VersionResult?.ToString() ?? "(none)"}");
        Console.WriteLine($"NewAccountResult={session.NewAccountResult?.ToString() ?? "(none)"} LoginResult={session.LoginResult?.ToString() ?? "(none)"} LoginSuccess={session.LoginSuccess} Characters={session.CharacterCount}");
        Console.WriteLine("Runtime parity (login→select→walk→fight→loot→equip) is a later gate.");

        if (session.LoginSuccess)
            return 0;
        if (session.GotConnected && session.VersionResult is 1 or null)
            return 0; // handshake reached login; Result 3/4 is still a real attempt
        return 5;
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
