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

        if (t.Equals("AllowTrade", StringComparison.OrdinalIgnoreCase))
        {
            command = GameCommand.AllowTrade();
            return true;
        }

        if (t.Equals("Trade", StringComparison.OrdinalIgnoreCase)
            || t.Equals("TradeRequest", StringComparison.OrdinalIgnoreCase))
        {
            command = GameCommand.Trade();
            return true;
        }

        if (t.Equals("TradeAccept", StringComparison.OrdinalIgnoreCase)
            || t.Equals("TradeReply", StringComparison.OrdinalIgnoreCase))
        {
            command = GameCommand.TradeAccept();
            return true;
        }

        if (t.Equals("TradeConfirm", StringComparison.OrdinalIgnoreCase))
        {
            command = GameCommand.TradeConfirm();
            return true;
        }

        if (t.Equals("TradeGold", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("TradeGold:", StringComparison.OrdinalIgnoreCase))
        {
            int amount = 50;
            int colon = t.IndexOf(':');
            if (colon >= 0)
                int.TryParse(t[(colon + 1)..].Trim(), out amount);
            command = GameCommand.TradeGold(Math.Max(1, amount));
            return true;
        }

        if (t.Equals("TradeItem", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("TradeItem:", StringComparison.OrdinalIgnoreCase))
        {
            int idx = -1;
            int colon = t.IndexOf(':');
            if (colon >= 0)
                int.TryParse(t[(colon + 1)..].Trim(), out idx);
            command = GameCommand.TradeItem(idx);
            return true;
        }

        if (t.StartsWith("Face:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Turn:", StringComparison.OrdinalIgnoreCase))
        {
            int colon = t.IndexOf(':');
            if (colon >= 0 && TryDirection(t[(colon + 1)..].Trim(), out var face))
            {
                command = GameCommand.Face(face);
                return true;
            }
            return false;
        }

        if (t.Equals("Drag", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Drag:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("InvMove:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("BagMove:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("MoveItem:", StringComparison.OrdinalIgnoreCase))
        {
            if (!t.Contains(':'))
            {
                command = GameCommand.Drag();
                return true;
            }
            if (TrySlotPair(t, out int from, out int to))
            {
                command = GameCommand.Drag(from, to);
                return true;
            }
            return false;
        }

        if (t.StartsWith("Merge:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("MergeItem:", StringComparison.OrdinalIgnoreCase))
        {
            if (TrySlotPair(t, out int from, out int to))
            {
                command = GameCommand.Merge(from, to);
                return true;
            }
            return false;
        }

        if (t.StartsWith("Wait:", StringComparison.OrdinalIgnoreCase))
        {
            int colon = t.IndexOf(':');
            int ms = 1000;
            if (colon >= 0)
                int.TryParse(t[(colon + 1)..].Trim(), out ms);
            command = GameCommand.Wait(Math.Max(0, ms));
            return true;
        }

        if (t.StartsWith("Move:", StringComparison.OrdinalIgnoreCase))
        {
            string rest = t[5..];
            string[] parts = rest.Split(new[] { ':', ',', 'x', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out int mx) && int.TryParse(parts[1], out int my))
            {
                command = GameCommand.Move(mx, my);
                return true;
            }
            return false;
        }

        if (t.Equals("QuestAccept", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("QuestAccept:", StringComparison.OrdinalIgnoreCase)
            || t.Equals("AcceptQuest", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("AcceptQuest:", StringComparison.OrdinalIgnoreCase))
        {
            int id = -1;
            int colon = t.IndexOf(':');
            if (colon >= 0)
                int.TryParse(t[(colon + 1)..].Trim(), out id);
            command = GameCommand.QuestAccept(id);
            return true;
        }

        if (t.Equals("QuestFinish", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("QuestFinish:", StringComparison.OrdinalIgnoreCase)
            || t.Equals("FinishQuest", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("FinishQuest:", StringComparison.OrdinalIgnoreCase))
        {
            int id = -1, selected = -1;
            int colon = t.IndexOf(':');
            if (colon >= 0)
            {
                string[] parts = t[(colon + 1)..].Split(new[] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                    int.TryParse(parts[0].Trim(), out id);
                if (parts.Length >= 2)
                    int.TryParse(parts[1].Trim(), out selected);
            }
            command = GameCommand.QuestFinish(id, selected);
            return true;
        }

        if (t.StartsWith("QuestAbandon:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("AbandonQuest:", StringComparison.OrdinalIgnoreCase))
        {
            int colon = t.IndexOf(':');
            if (colon >= 0 && int.TryParse(t[(colon + 1)..].Trim(), out int id))
            {
                command = GameCommand.QuestAbandon(id);
                return true;
            }
            return false;
        }

        if (t.StartsWith("QuestShare:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("ShareQuest:", StringComparison.OrdinalIgnoreCase))
        {
            int colon = t.IndexOf(':');
            if (colon >= 0 && int.TryParse(t[(colon + 1)..].Trim(), out int id))
            {
                command = GameCommand.QuestShare(id);
                return true;
            }
            return false;
        }

        if (t.Equals("Mag", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Mag:", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Magic", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Magic:", StringComparison.OrdinalIgnoreCase))
        {
            ParseMagArgs(t, out string spell, out int target);
            command = GameCommand.Mag(spell, target);
            return true;
        }

        if (t.Equals("MagTarget", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("MagTarget:", StringComparison.OrdinalIgnoreCase)
            || t.Equals("MagicTarget", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("MagicTarget:", StringComparison.OrdinalIgnoreCase))
        {
            ParseMagArgs(t, out string spell, out int target);
            command = GameCommand.MagTarget(spell, target);
            return true;
        }

        if (t.Equals("BigMap", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("BigMap:", StringComparison.OrdinalIgnoreCase)
            || t.Equals("FieldMap", StringComparison.OrdinalIgnoreCase)
            || t.Equals("B", StringComparison.OrdinalIgnoreCase))
        {
            string mode = "";
            int colon = t.IndexOf(':');
            if (colon >= 0 && colon + 1 < t.Length)
                mode = t[(colon + 1)..].Trim();
            command = GameCommand.BigMap(mode);
            return true;
        }

        if (t.Equals("WorldMap", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("WorldMap:", StringComparison.OrdinalIgnoreCase))
        {
            string mode = "";
            int colon = t.IndexOf(':');
            if (colon >= 0 && colon + 1 < t.Length)
                mode = t[(colon + 1)..].Trim();
            command = GameCommand.WorldMap(mode);
            return true;
        }

        if (t.Equals("SearchMap", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("SearchMap:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("MapSearch:", StringComparison.OrdinalIgnoreCase))
        {
            int colon = t.IndexOf(':');
            string q = colon >= 0 && colon + 1 < t.Length ? t[(colon + 1)..].Trim() : "";
            q = q.Replace('_', ' ');
            if (q.Length == 0) return false;
            command = GameCommand.SearchMap(q);
            return true;
        }

        if (t.Equals("TeleportNpc", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("TeleportNpc:", StringComparison.OrdinalIgnoreCase)
            || t.Equals("TeleportToNPC", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("TeleportToNPC:", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Teleport", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Teleport:", StringComparison.OrdinalIgnoreCase))
        {
            int id = 0;
            int colon = t.IndexOf(':');
            if (colon >= 0)
                int.TryParse(t[(colon + 1)..].Trim(), out id);
            command = GameCommand.TeleportNpc(id);
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

    /// <summary><c>Mag:Fencing</c>, <c>Mag:1</c>, <c>MagTarget:12345</c>, <c>MagTarget:Fencing,12345</c>.</summary>
    static void ParseMagArgs(string token, out string spell, out int target)
    {
        spell = "";
        target = 0;
        int colon = token.IndexOf(':');
        if (colon < 0 || colon + 1 >= token.Length)
            return;
        string[] parts = token[(colon + 1)..].Split(new[] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        string first = parts[0].Trim();
        if (Enum.TryParse<Spell>(first, true, out _)
            || (byte.TryParse(first, out byte code) && Enum.IsDefined(typeof(Spell), code)))
            spell = first;
        else if (int.TryParse(first, out int tid))
            target = tid;
        else
            spell = first;

        if (parts.Length >= 2)
            int.TryParse(parts[1].Trim(), out target);
    }

    static bool TrySlotPair(string token, out int from, out int to)
    {
        from = to = -1;
        int colon = token.IndexOf(':');
        if (colon < 0 || colon + 1 >= token.Length)
            return false;
        string[] parts = token[(colon + 1)..].Split(new[] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            && int.TryParse(parts[0].Trim(), out from)
            && int.TryParse(parts[1].Trim(), out to);
    }
}
