namespace Client.Linux;

/// <summary>
/// Crystal-style move/attack bindings. Windowed path uses Silk.NET keys/mouse;
/// headless CI injects the same commands via <c>--input-script</c>.
/// </summary>
internal static class InputMap
{
    /// <summary>
    /// Parse <c>Right,Right,Attack,Down</c> (also accepts WASD / numpad names).
    /// </summary>
    public static List<GameCommand> ParseScript(string script)
    {
        var list = new List<GameCommand>();
        // Comma/semicolon only so Chat:hello_world stays one token.
        foreach (string raw in script.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseToken(raw.Trim(), out var cmd))
                list.Add(cmd);
            else
                Console.Error.WriteLine($"input-script: unknown token '{raw}'");
        }
        return list;
    }

    public static bool TryParseToken(string token, out GameCommand command)
    {
        command = default;
        string t = token.Trim();
        if (t.Length == 0) return false;

        if (t.StartsWith("Chat:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Say:", StringComparison.OrdinalIgnoreCase))
        {
            int colon = t.IndexOf(':');
            string msg = colon >= 0 && colon + 1 < t.Length ? t[(colon + 1)..].Trim() : "";
            msg = msg.Replace('_', ' ');
            if (msg.Length == 0) return false;
            command = GameCommand.Chat(msg);
            return true;
        }

        if (t.Equals("Attack", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Hit", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Space", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Z", StringComparison.OrdinalIgnoreCase))
        {
            command = GameCommand.Attack(MirDirection.Right);
            return true;
        }

        if (t.Equals("Buy", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Buy:", StringComparison.OrdinalIgnoreCase))
        {
            int idx = 0;
            int colon = t.IndexOf(':');
            if (colon >= 0)
                int.TryParse(t[(colon + 1)..].Trim(), out idx);
            command = GameCommand.Buy(Math.Max(0, idx));
            return true;
        }

        if (t.Equals("Sell", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Sell:", StringComparison.OrdinalIgnoreCase))
        {
            int idx = -1;
            int colon = t.IndexOf(':');
            if (colon >= 0)
                int.TryParse(t[(colon + 1)..].Trim(), out idx);
            command = GameCommand.Sell(idx);
            return true;
        }

        if (t.Equals("Talk", StringComparison.OrdinalIgnoreCase)
            || t.Equals("NPC", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Npc", StringComparison.OrdinalIgnoreCase))
        {
            command = GameCommand.Talk();
            return true;
        }

        if (t.Equals("PickUp", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Pickup", StringComparison.OrdinalIgnoreCase)
            || t.Equals("G", StringComparison.OrdinalIgnoreCase)
            || t.Equals("F", StringComparison.OrdinalIgnoreCase))
        {
            command = GameCommand.PickUp();
            return true;
        }

        if (TryDirection(t, out var dir))
        {
            command = GameCommand.Walk(dir);
            return true;
        }

        return false;
    }

    public static bool TryDirection(string token, out MirDirection dir)
    {
        dir = MirDirection.Right;
        return token.ToUpperInvariant() switch
        {
            "UP" or "W" or "NUM8" or "NORTH" => Set(MirDirection.Up, out dir),
            "DOWN" or "S" or "NUM2" or "SOUTH" => Set(MirDirection.Down, out dir),
            "LEFT" or "A" or "NUM4" or "WEST" => Set(MirDirection.Left, out dir),
            "RIGHT" or "D" or "NUM6" or "EAST" => Set(MirDirection.Right, out dir),
            "UPLEFT" or "Q" or "NUM7" or "NW" => Set(MirDirection.UpLeft, out dir),
            "UPRIGHT" or "E" or "NUM9" or "NE" => Set(MirDirection.UpRight, out dir),
            "DOWNLEFT" or "NUM1" or "SW" => Set(MirDirection.DownLeft, out dir),
            "DOWNRIGHT" or "C" or "NUM3" or "SE" => Set(MirDirection.DownRight, out dir),
            _ => false
        };

        static bool Set(MirDirection value, out MirDirection dest)
        {
            dest = value;
            return true;
        }
    }

    /// <summary>WASD / arrows / numpad — same 8-way rose as Crystal MapControl + CMain.</summary>
    public static bool TryKeyWalk(string keyName, out MirDirection dir)
        => TryDirection(keyName, out dir);
}
