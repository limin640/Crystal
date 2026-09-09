using System.Net.Sockets;
using System.Reflection;
using log4net;
using log4net.Config;
using Server;
using Server.MirEnvir;

namespace Server.Linux;

/// <summary>
/// Console host for Crystal Server on Linux. The WinForms <c>Server.MirForms</c> project
/// stays Windows-only; this entry starts <see cref="Envir"/> and binds the game port.
/// </summary>
internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help"))
        {
            PrintHelp();
            return 0;
        }

        Packet.IsServer = true;

        bool probeOnly = args.Contains("--bind-probe");
        string? root = GetOption(args, "--root") ?? Environment.GetEnvironmentVariable("CRYSTAL_SERVER_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            // Never drop Configs/Envir into the git worktree.
            root = Path.Combine(Path.GetTempPath(), "crystal-server-root");
            Console.WriteLine("No --root given; using a throwaway directory (not a Crystal.Database pack).");
        }

        root = Path.GetFullPath(root);
        Directory.CreateDirectory(root);
        Directory.SetCurrentDirectory(root);
        Console.WriteLine($"Working directory (Crystal.Database / Jev layout): {root}");

        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly()!);
        FileInfo logConfig = new(Path.Combine(AppContext.BaseDirectory, "log4net.config"));
        if (logConfig.Exists)
            XmlConfigurator.Configure(logRepository, logConfig);
        else
            Console.Error.WriteLine("log4net.config not found next to the server binary; file logging disabled.");

        try
        {
            Settings.Load();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Settings.Load failed:");
            Console.Error.WriteLine(ex);
            return 2;
        }

        bool hasDb = File.Exists(Path.Combine(root, "Server.MirDB"));
        string mapsDir = Path.Combine(root, "Maps");
        int mapFileCount = 0;
        if (Directory.Exists(mapsDir))
        {
            try
            {
                mapFileCount = Directory.EnumerateFiles(mapsDir, "*.map", SearchOption.AllDirectories).Count();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Maps enumeration failed: {ex.Message}");
            }
        }

        bool hasFullWorld = hasDb && mapFileCount > 0;
        bool askedListenWithoutWorld = args.Contains("--listen-without-world");

        if (args.Contains("--no-version-check"))
            Settings.CheckVersion = false;
        if (args.Contains("--no-db-checks"))
            Settings.EnforceDBChecks = false;
        if (args.Contains("--allow-start-game"))
            Settings.AllowStartGame = true;
        if (args.Contains("--test-server"))
            Settings.TestServer = true;

        if (hasFullWorld)
        {
            // Maps + Server.MirDB: start the real Envir. Do not handshake-only.
            if (askedListenWithoutWorld)
                Console.WriteLine("Maps + Server.MirDB present; ignoring --listen-without-world (full world).");
            Settings.ListenWithoutWorld = false;
        }
        else if (askedListenWithoutWorld)
        {
            Settings.ListenWithoutWorld = true;
        }

        string? bind = GetOption(args, "--bind");
        if (!string.IsNullOrWhiteSpace(bind))
            Settings.IPAddress = bind;

        if (GetInt(args, "--port") is int port)
            Settings.Port = (ushort)port;

        int seconds = GetInt(args, "--seconds") ?? 0;

        string worldMode = hasFullWorld ? "full" : (Settings.ListenWithoutWorld ? "handshake-only" : "incomplete");
        Console.WriteLine($"WorldMode={worldMode} Server.MirDB={(hasDb ? "present" : "MISSING")} Maps={mapFileCount}");
        Console.WriteLine($"CheckVersion={Settings.CheckVersion} EnforceDBChecks={Settings.EnforceDBChecks} ListenWithoutWorld={Settings.ListenWithoutWorld} AllowStartGame={Settings.AllowStartGame} TestServer={Settings.TestServer}");
        Console.WriteLine($"Bind {Settings.IPAddress}:{Settings.Port}");
        Console.WriteLine($"Database {(File.Exists(Envir.DatabasePath) ? "present" : "MISSING (will be created empty if Start runs)")}: {Path.GetFullPath(Envir.DatabasePath)}");

        if (probeOnly)
            return RunBindProbe(Settings.IPAddress, Settings.Port, Math.Max(1, seconds == 0 ? 2 : seconds));

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Envir.Main.Start();

        // Full Jev worlds load hundreds of maps before binding 7000.
        int waitSeconds = seconds > 0 ? seconds : (hasFullWorld ? 180 : 45);
        DateTime deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
        bool listening = WaitForPort(Settings.IPAddress, Settings.Port, deadline, cts.Token);
        DrainMessages();

        if (!listening && !Envir.Main.Running)
        {
            Console.Error.WriteLine("Server failed to start. Need a Crystal.Database Jev tree with Maps + Server.MirDB, or pass --listen-without-world / --bind-probe.");
            return 3;
        }

        Console.WriteLine(listening
            ? $"Server listening on {Settings.IPAddress}:{Settings.Port} (Running={Envir.Main.Running})"
            : $"Server thread running; port {Settings.Port} not confirmed yet (Running={Envir.Main.Running})");

        if (seconds > 0)
        {
            DateTime until = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < until && !cts.IsCancellationRequested && Envir.Main.Running)
            {
                DrainMessages();
                Thread.Sleep(200);
            }
        }
        else
        {
            Console.WriteLine("Ctrl+C to stop.");
            while (!cts.IsCancellationRequested && Envir.Main.Running)
            {
                DrainMessages();
                Thread.Sleep(250);
            }
        }

        DrainMessages();
        Envir.Main.Stop();
        DrainMessages();
        Settings.Save();
        Console.WriteLine("Server.Linux stopped.");
        return listening || Envir.Main.Running ? 0 : 3;
    }

    static int RunBindProbe(string ip, int port, int seconds)
    {
        try
        {
            var listener = new TcpListener(System.Net.IPAddress.Parse(ip), port);
            listener.Start();
            Console.WriteLine($"Bind-probe listening on {ip}:{port} for {seconds}s (no Envir).");
            Thread.Sleep(seconds * 1000);
            listener.Stop();
            Console.WriteLine("Bind-probe released the port.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Bind-probe failed on {ip}:{port}: {ex.Message}");
            return 3;
        }
    }

    static bool WaitForPort(string ip, int port, DateTime deadline, CancellationToken token)
    {
        _ = ip;
        while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
        {
            DrainMessages();
            // Do not TCP-connect the game port here: accept would IP-block 127.0.0.1 for 5s.
            if (IsListening(port))
                return true;
            Thread.Sleep(200);
        }
        return IsListening(port);
    }

    static bool IsListening(int port)
    {
        string hex = port.ToString("X4");
        foreach (string path in new[] { "/proc/net/tcp", "/proc/net/tcp6" })
        {
            if (!File.Exists(path))
                continue;
            foreach (string line in File.ReadLines(path))
            {
                if (line.Contains($":{hex}", StringComparison.OrdinalIgnoreCase) && line.Contains(" 0A "))
                    return true;
            }
        }
        return false;
    }

    static void DrainMessages()
    {
        var q = MessageQueue.Instance;
        while (q.MessageLog.TryDequeue(out string? msg))
            Console.Write(msg);
        while (q.DebugLog.TryDequeue(out string? msg))
            Console.Write("DBG " + msg);
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            Crystal.Server.Linux — Envir + game-port listener (no WinForms)

            Usage (full world — Maps + Server.MirDB present, no --listen-without-world):
              dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
                --root /path/to/Crystal.Database/Jev \
                --no-version-check --allow-start-game --test-server --seconds 90

            Handshake-only (empty / incomplete root):
              dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
                --root /tmp/crystal-server-root \
                --no-version-check --listen-without-world --seconds 20

            Options:
              --root <dir>              External DB root (Configs/Envir/Maps/Server.MirDB). Not vendored.
              --bind <ip>               Listen address (default 127.0.0.1 from Setup.ini)
              --port <n>                Game port (default 7000)
              --no-version-check        Settings.CheckVersion=false (Linux client has no Mir2.Exe)
              --allow-start-game        Settings.AllowStartGame=true (StartGame Result 4; default in stock Jev Setup.ini)
              --test-server             Settings.TestServer=true — enables existing @LEVEL/@MOB/@MAKE/@MOVE (no invented commands)
              --listen-without-world    Bind 7000 even if maps/DB checks fail (handshake/login only).
                                        Ignored when Server.MirDB and *.map files exist — full Envir starts.
              --no-db-checks            Settings.EnforceDBChecks=false
              --seconds <n>             Run then exit (CI). 0 = until Ctrl+C
              --bind-probe              Bind 7000 without starting Envir (port-availability check)

            External Crystal.Database (Jev) — do not copy into git:

              git clone --depth 1 https://github.com/Suprcode/Crystal.Database.git /path/to/Crystal.Database
              # use /path/to/Crystal.Database/Jev as --root
            """);
    }

    static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }

    static int? GetInt(string[] args, string name)
    {
        string? raw = GetOption(args, name);
        return int.TryParse(raw, out int value) ? value : null;
    }
}
