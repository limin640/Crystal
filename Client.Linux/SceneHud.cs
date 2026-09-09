using System.Drawing;
using Crystal.Graphics;

namespace Client.Linux;

/// <summary>
/// Minimum select + in-game HUD through <see cref="IRenderer"/> (not WinForms).
/// Bars and a 3×5 procedural glyph atlas — UI chrome, not WIL/game art.
/// </summary>
internal sealed class SceneHud : IDisposable
{
    readonly IRenderer _renderer;
    readonly IGpuTexture _white;
    readonly IGpuTexture _font;
    const int GlyphW = 4;
    const int GlyphH = 6;
    const int Scale = 2;

    public int HudDraws { get; private set; }
    public int InventoryDraws { get; private set; }
    public int EquipDraws { get; private set; }
    public int BeltDraws { get; private set; }
    public int SkillDraws { get; private set; }
    public int ChatDraws { get; private set; }
    public int MiniMapDraws { get; private set; }
    public int MiniMapBlips { get; private set; }
    public int NpcDraws { get; private set; }
    public int BagFilled { get; private set; }
    public int EquipFilled { get; private set; }
    public int BeltFilled { get; private set; }
    public int SkillsFilled { get; private set; }
    /// <summary>Extra IRenderer fills for SelectedCell / drop highlight / floating ghost (no WIL icons).</summary>
    public int DragGhostDraws { get; private set; }

    const int InvPanelInsetX = 292;
    const int InvPanelY = 188;
    const int InvCols = 8;
    const int InvCellW = 34;
    const int InvCellH = 16;
    const int InvVisibleRows = 8;
    const int BeltInsetX = 140;
    const int BeltInsetY = 118;

    public SceneHud(IRenderer renderer)
    {
        _renderer = renderer;
        _white = renderer.CreateSolidTexture(2, 2, Color.White);
        _font = BuildFont(renderer);
    }

    public void DrawSelect(int width, int height, CrystalSession session)
    {
        Fill(8, 8, width - 16, 72, Color.FromArgb(200, 16, 20, 32));
        Text(16, 16, "SELECT (Client.Linux / IRenderer)", Color.LightGoldenrodYellow);
        Text(16, 36, $"account chars={session.CharacterCount}  StartGame={session.StartGameResult?.ToString() ?? "-"}", Color.Gainsboro);
        int y = 88;
        foreach (var ch in session.Characters)
        {
            Fill(16, y, 280, 22, Color.FromArgb(180, 32, 40, 56));
            Text(20, y + 4, $"{ch.Index} {ch.Name} {ch.Class} Lv{ch.Level}", Color.White);
            y += 26;
        }
        if (session.Characters.Count == 0)
            Text(16, y, "(empty — NewCharacter)", Color.Gray);
        Text(16, height - 28, "WinForms dialogs stubbed — see MIGRATION.md", Color.DimGray);
    }

    public void DrawGame(int width, int height, CrystalSession session, MapView? mapView = null)
    {
        ResetCounts();
        Fill(8, 8, width - 16, 40, Color.FromArgb(190, 8, 10, 16));
        Text(16, 14, $"{session.MapTitle ?? "?"}  {session.UserName} Lv{session.UserLevel} {session.UserClass}  {session.UserLocation.X},{session.UserLocation.Y}  {session.Facing}  G{session.UserGold}", Color.White);

        DrawEquipPanel(16, 56, session);
        DrawInventoryPanel(width - 292, 188, session);
        DrawMiniMap(width - 140, 52, 128, session, mapView);
        DrawBeltBar(width / 2 - 140, height - 118, session);
        DrawSkillBar(16, height - 118, session);
        DrawChatLog(16, height - 176, session);
        DrawNpcPanel(270, 56, session);

        int barW = Math.Min(220, width / 3);
        int hp = session.UserHP;
        int mp = session.UserMP;
        int hpMax = Math.Max(hp, session.UserMaxHP);
        int mpMax = Math.Max(mp, session.UserMaxMP);
        Fill(16, height - 52, barW, 12, Color.FromArgb(255, 40, 16, 16));
        if (hpMax > 0)
            Fill(16, height - 52, (int)(barW * (hp / (float)hpMax)), 12, Color.FromArgb(255, 180, 40, 40));
        Text(16 + barW + 8, height - 54, $"HP {hp}/{hpMax}", Color.MistyRose);

        Fill(16, height - 32, barW, 12, Color.FromArgb(255, 16, 24, 48));
        if (mpMax > 0)
            Fill(16, height - 32, (int)(barW * (mp / (float)mpMax)), 12, Color.FromArgb(255, 48, 96, 200));
        Text(16 + barW + 8, height - 34, $"MP {mp}/{mpMax}", Color.LightSteelBlue);

        Text(width - 280, height - 28, $"in {session.InputWalks} atk {session.InputAttacks} buy {session.InputBuys} sell {session.InputSells} G{session.UserGold}", Color.Silver);
        if (session.ChatComposing)
            Text(16, height - 72, $"> {session.ChatDraft}_", Color.Yellow);
        DrawDragGhost(width, height, session);
    }

    public void WriteEvidence(CrystalSession session)
    {
        Console.WriteLine($"hud inventory/equip: bag={BagFilled}/{session.InventorySlots.Count} gold={session.UserGold} equip={EquipFilled}/{session.EquipmentSlots.Count} belt={BeltFilled}/{CrystalSession.BeltSlotCount} skills={SkillsFilled} chat={session.ChatLines.Count}");
        Console.WriteLine($"hud minimap: {session.MapWidth}x{session.MapHeight} blip={session.UserLocation.X},{session.UserLocation.Y} blips={MiniMapBlips} draws={MiniMapDraws} mmapLib={session.MiniMapIndex} (geometry only)");
        Console.WriteLine($"hud chat: sent={session.ChatSent} recv={session.ChatRecv} echo={session.ChatEcho} lines={session.ChatLines.Count}");
        Console.WriteLine($"hud npc: talkOk={session.NpcTalkOk} name={session.NpcName ?? "-"} id={session.NpcObjectId} calls={session.NpcCallSent} lines={session.NpcDialogLines.Count} goods={session.NpcGoods.Count} quests={session.QuestNames.Count} gold={session.UserGold} bag={session.BagCount} buys={session.InputBuys} sells={session.InputSells} BuyOk={session.BuyOk} SellOk={session.SellOk}");
        Console.WriteLine($"hud trade: handshake={session.TradeHandshakeOk} done={session.TradeDone} partner={session.TradePartnerName ?? "-"} invite={session.TradeInviteFrom ?? "-"} goldSeen={session.TradeGoldSeen} deposit={session.TradeDepositOk}");
        Console.WriteLine($"hud drag: ok={session.DragOk} moves={session.InputDrags} merge={session.MergeOk} {session.DragEvidence ?? "-"}");
        Console.WriteLine($"hud-drag ghost={DragGhostDraws} from={session.SelectedSlot} to={session.DragHoverSlot}");
        Console.WriteLine($"hud draws: inv={InventoryDraws} equip={EquipDraws} belt={BeltDraws} skill={SkillDraws} chat={ChatDraws} minimap={MiniMapDraws} npc={NpcDraws} total={HudDraws}");
        for (int i = 0; i < session.InventorySlots.Count; i++)
        {
            var it = session.InventorySlots[i];
            if (it == null) continue;
            string belt = i < CrystalSession.BeltSlotCount ? " belt" : "";
            Console.WriteLine($"  hud-bag slot={i}{belt} name={session.DisplayName(it)} x{it.Count}");
        }
        for (int i = 0; i < session.EquipmentSlots.Count; i++)
        {
            var it = session.EquipmentSlots[i];
            if (it == null) continue;
            Console.WriteLine($"  hud-equip slot={(EquipmentSlot)i} name={session.DisplayName(it)}");
        }
        foreach (var mag in session.Magics)
            Console.WriteLine($"  hud-skill {mag.Name} spell={mag.Spell} lv={mag.Level} key={mag.Key}");
        foreach (string line in session.ChatLines)
            Console.WriteLine($"  hud-chat {line}");
        if (!string.IsNullOrWhiteSpace(session.NpcName))
            Console.WriteLine($"  hud-npc name={session.NpcName}");
        foreach (string line in session.NpcDialogLines.Take(8))
            Console.WriteLine($"  hud-npc-say {line}");
        foreach (string g in session.NpcGoods.Take(8))
            Console.WriteLine($"  hud-npc-goods {g}");
        if (session.BuyEvidence != null)
            Console.WriteLine($"  hud-buy {session.BuyEvidence}");
        if (session.SellEvidence != null)
            Console.WriteLine($"  hud-sell {session.SellEvidence}");
        if (session.TradeEvidence != null)
            Console.WriteLine($"  hud-trade {session.TradeEvidence}");
        if (session.DragEvidence != null)
            Console.WriteLine($"  hud-drag {session.DragEvidence}");
        if (session.MergeEvidence != null)
            Console.WriteLine($"  hud-merge {session.MergeEvidence}");
        foreach (string q in session.QuestNames.Take(8))
            Console.WriteLine($"  hud-quest {q}");
    }

    void ResetCounts()
    {
        HudDraws = InventoryDraws = EquipDraws = BeltDraws = SkillDraws = ChatDraws = MiniMapDraws = NpcDraws = 0;
        MiniMapBlips = BagFilled = EquipFilled = BeltFilled = SkillsFilled = DragGhostDraws = 0;
    }

    /// <summary>
    /// Hit-test belt (0–5) then visible bag cells. Windowed click-to-drop; headless keeps Drag tokens.
    /// </summary>
    public bool TryHitBagSlot(int width, int height, int mx, int my, out int slot)
    {
        int beltEnd = CrystalSession.BeltSlotCount;
        for (int i = 0; i < beltEnd; i++)
        {
            if (TrySlotCell(width, height, i, out int x, out int y, out int w, out int h)
                && mx >= x && mx < x + w && my >= y && my < y + h)
            {
                slot = i;
                return true;
            }
        }
        int bagEnd = beltEnd + InvCols * InvVisibleRows;
        for (int i = beltEnd; i < bagEnd; i++)
        {
            if (TrySlotCell(width, height, i, out int x, out int y, out int w, out int h)
                && mx >= x && mx < x + w && my >= y && my < y + h)
            {
                slot = i;
                return true;
            }
        }
        slot = -1;
        return false;
    }

    static bool TrySlotCell(int width, int height, int slot, out int x, out int y, out int w, out int h)
    {
        x = y = w = h = 0;
        if (slot < 0) return false;
        if (slot < CrystalSession.BeltSlotCount)
        {
            int bx = width / 2 - BeltInsetX;
            int by = height - BeltInsetY;
            x = bx + 6 + slot * 44;
            y = by + 16;
            w = 40;
            h = 16;
            return true;
        }
        int i = slot - CrystalSession.BeltSlotCount;
        if (i >= InvCols * InvVisibleRows) return false;
        int px = width - InvPanelInsetX;
        int py = InvPanelY;
        x = px + 6 + (i % InvCols) * InvCellW;
        y = py + 20 + (i / InvCols) * InvCellH;
        w = InvCellW - 2;
        h = InvCellH - 1;
        return true;
    }

    void DrawDragGhost(int width, int height, CrystalSession session)
    {
        if (!session.ShowDragGhost && session.SelectedSlot < 0) return;
        int from = session.SelectedSlot;
        int to = session.DragHoverSlot;
        if (from >= 0 && TrySlotCell(width, height, from, out int fx, out int fy, out int fw, out int fh))
            GhostFill(fx, fy, fw, fh, Color.FromArgb(150, 220, 176, 40));
        if (to >= 0 && TrySlotCell(width, height, to, out int tx, out int ty, out int tw, out int th))
            GhostFill(tx, ty, tw, th, Color.FromArgb(150, 40, 200, 176));
        int floatSlot = to >= 0 ? to : from;
        if (floatSlot >= 0 && TrySlotCell(width, height, floatSlot, out int gx, out int gy, out _, out _))
            GhostFill(gx + 6, gy - 8, 18, 10, Color.FromArgb(210, 240, 196, 56));
    }

    void GhostFill(int x, int y, int w, int h, Color color)
    {
        Fill(x, y, w, h, color);
        DragGhostDraws++;
    }

    void DrawNpcPanel(int x, int y, CrystalSession session)
    {
        int before = HudDraws;
        Fill(x, y, 260, 200, Color.FromArgb(190, 16, 14, 20));
        Text(x + 6, y + 2, "NPC", Color.Plum);
        if (!string.IsNullOrWhiteSpace(session.NpcName))
            Text(x + 40, y + 2, Clip(session.NpcName, 18), Color.White);
        else
            Text(x + 40, y + 2, session.NpcTalkOk ? "TALK" : "NONE", Color.Gray);
        int row = y + 18;
        foreach (string line in session.NpcDialogLines.Take(6))
        {
            Text(x + 6, row, Clip(line, 30), Color.Thistle);
            row += 14;
        }
        Text(x + 6, row, $"GOLD {session.UserGold} BAG {session.BagCount}", Color.Khaki);
        row += 14;
        if (session.NpcGoods.Count > 0)
        {
            Text(x + 6, row, "GOODS", Color.Khaki);
            row += 14;
            foreach (string g in session.NpcGoods.Take(3))
            {
                Text(x + 6, row, Clip(g, 28), Color.Wheat);
                row += 14;
            }
        }
        if (session.QuestNames.Count > 0)
        {
            Text(x + 6, Math.Min(row, y + 170), $"QUEST {session.QuestNames.Count}", Color.LightGreen);
        }
        NpcDraws = HudDraws - before;
    }

    void DrawMiniMap(int x, int y, int size, CrystalSession session, MapView? mapView)
    {
        int before = HudDraws;
        int mw = mapView is { MapLoaded: true } ? mapView.MapWidth : session.MapWidth;
        int mh = mapView is { MapLoaded: true } ? mapView.MapHeight : session.MapHeight;
        Fill(x, y, size, size, Color.FromArgb(220, 8, 12, 10));
        Fill(x + 2, y + 14, size - 4, size - 16, Color.FromArgb(255, 12, 20, 14));
        Text(x + 4, y + 2, "MAP", Color.PaleGreen);

        if (mw > 0 && mh > 0)
        {
            int inner = size - 20;
            int ox = x + 4;
            int oy = y + 16;
            var cells = mapView is { MapLoaded: true } ? mapView.Cells : null;
            const int samples = 32;
            float cw = inner / (float)samples;
            float ch = inner / (float)samples;
            for (int sy = 0; sy < samples; sy++)
            {
                int my = Math.Clamp(sy * mh / samples, 0, Math.Max(0, mh - 1));
                for (int sx = 0; sx < samples; sx++)
                {
                    int mx = Math.Clamp(sx * mw / samples, 0, Math.Max(0, mw - 1));
                    bool filled = false;
                    if (cells != null && mx < cells.GetLength(0) && my < cells.GetLength(1))
                    {
                        var cell = cells[mx, my];
                        filled = cell.BackImage != 0 && cell.BackIndex != -1;
                    }
                    if (filled)
                        Fill(ox + (int)(sx * cw), oy + (int)(sy * ch), Math.Max(1, (int)cw), Math.Max(1, (int)ch), Color.FromArgb(180, 28, 48, 32));
                }
            }

            void Blip(int cellX, int cellY, Color color, int w = 3)
            {
                int px = ox + (int)(cellX / (float)mw * inner) - w / 2;
                int py = oy + (int)(cellY / (float)mh * inner) - w / 2;
                Fill(px, py, w, w, color);
                MiniMapBlips++;
            }

            foreach (var obj in session.Objects)
            {
                if (obj.ObjectID == session.UserObjectId) continue;
                var color = obj.Kind switch
                {
                    "monster" => Color.FromArgb(255, 180, 48, 48),
                    "npc" => Color.FromArgb(255, 80, 160, 220),
                    "item" or "gold" => Color.Gold,
                    _ => Color.Gray
                };
                Blip(obj.Location.X, obj.Location.Y, color, 2);
            }
            Blip(session.UserLocation.X, session.UserLocation.Y, Color.Yellow, 4);
            Text(x + 4, y + size - 12, $"{mw}x{mh}", Color.DarkSeaGreen);
        }
        else
            Text(x + 8, y + size / 2, "NO SIZE", Color.Gray);

        MiniMapDraws = HudDraws - before;
    }

    void DrawEquipPanel(int x, int y, CrystalSession session)
    {
        int before = HudDraws;
        Fill(x, y, 248, 18 + 14 * 16, Color.FromArgb(180, 20, 16, 12));
        Text(x + 6, y + 2, "EQUIP", Color.Wheat);
        var slots = session.EquipmentSlots;
        int n = Math.Max(slots.Count, 14);
        for (int i = 0; i < n && i < 14; i++)
        {
            var it = i < slots.Count ? slots[i] : null;
            int rowY = y + 18 + i * 16;
            Fill(x + 4, rowY, 240, 15, it == null ? Color.FromArgb(160, 28, 24, 20) : Color.FromArgb(200, 56, 40, 28));
            string label = it == null
                ? $"{(EquipmentSlot)i}"
                : $"{(EquipmentSlot)i} {Clip(session.DisplayName(it), 16)}";
            Text(x + 8, rowY + 1, label, it == null ? Color.DimGray : Color.White);
            if (it != null) EquipFilled++;
        }
        EquipDraws = HudDraws - before;
    }

    void DrawInventoryPanel(int x, int y, CrystalSession session)
    {
        int before = HudDraws;
        const int cols = InvCols;
        const int cellW = InvCellW;
        const int cellH = InvCellH;
        var bag = session.InventorySlots;
        int bagStart = CrystalSession.BeltSlotCount;
        int bagSlots = Math.Max(0, bag.Count - bagStart);
        int rows = Math.Max(5, (int)Math.Ceiling(Math.Max(bagSlots, 40) / (double)cols));
        rows = Math.Min(rows, InvVisibleRows);
        Fill(x, y, cols * cellW + 12, 22 + rows * cellH, Color.FromArgb(180, 12, 16, 24));
        Text(x + 6, y + 2, "INVENTORY", Color.PowderBlue);
        for (int i = 0; i < rows * cols; i++)
        {
            int slot = bagStart + i;
            var it = slot < bag.Count ? bag[slot] : null;
            int cx = x + 6 + (i % cols) * cellW;
            int cy = y + 20 + (i / cols) * cellH;
            Fill(cx, cy, cellW - 2, cellH - 1, it == null ? Color.FromArgb(150, 24, 28, 36) : Color.FromArgb(210, 48, 64, 80));
            if (it != null)
            {
                Text(cx + 1, cy + 1, Clip(session.DisplayName(it), 4), Color.White);
                BagFilled++;
            }
        }
        InventoryDraws = HudDraws - before;
    }

    void DrawBeltBar(int x, int y, CrystalSession session)
    {
        int before = HudDraws;
        const int n = CrystalSession.BeltSlotCount;
        Fill(x, y, n * 44 + 8, 36, Color.FromArgb(190, 16, 20, 16));
        Text(x + 4, y + 2, "BELT", Color.DarkSeaGreen);
        var bag = session.InventorySlots;
        for (int i = 0; i < n; i++)
        {
            var it = i < bag.Count ? bag[i] : null;
            int cx = x + 6 + i * 44;
            Fill(cx, y + 16, 40, 16, it == null ? Color.FromArgb(150, 28, 32, 28) : Color.FromArgb(210, 48, 72, 48));
            string label = it == null ? $"{i + 1}" : $"{i + 1} {Clip(session.DisplayName(it), 3)}";
            Text(cx + 2, y + 17, label, it == null ? Color.Gray : Color.White);
            if (it != null)
            {
                BeltFilled++;
                BagFilled++;
            }
        }
        BeltDraws = HudDraws - before;
    }

    void DrawSkillBar(int x, int y, CrystalSession session)
    {
        int before = HudDraws;
        const int n = 8;
        Fill(x, y, n * 36 + 8, 36, Color.FromArgb(190, 16, 16, 28));
        Text(x + 4, y + 2, "SKILL", Color.Plum);
        for (int i = 0; i < n; i++)
        {
            ClientMagic? mag = i < session.Magics.Count ? session.Magics[i] : null;
            int cx = x + 6 + i * 36;
            Fill(cx, y + 16, 32, 16, mag == null ? Color.FromArgb(150, 28, 28, 40) : Color.FromArgb(210, 64, 48, 96));
            string label = mag == null ? $"F{i + 1}" : Clip(string.IsNullOrWhiteSpace(mag.Name) ? mag.Spell.ToString() : mag.Name, 4);
            Text(cx + 1, y + 17, label, mag == null ? Color.Gray : Color.White);
            if (mag != null) SkillsFilled++;
        }
        SkillDraws = HudDraws - before;
    }

    void DrawChatLog(int x, int y, CrystalSession session)
    {
        int before = HudDraws;
        var lines = session.ChatLines;
        if (lines.Count == 0 && string.IsNullOrWhiteSpace(session.LastChat))
        {
            ChatDraws = 0;
            return;
        }
        Fill(x, y, 420, 52, Color.FromArgb(160, 8, 8, 12));
        var show = lines.Count > 0 ? lines : new[] { session.LastChat! };
        int row = y + 2;
        foreach (string line in show)
        {
            Text(x + 4, row, Clip(line, 50), Color.Khaki);
            row += 12;
        }
        ChatDraws = HudDraws - before;
    }

    static string Clip(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text[..max];
    }

    void Fill(int x, int y, int w, int h, Color color)
    {
        if (w <= 0 || h <= 0) return;
        _renderer.DrawQuad(_white, null, x, y, w, h, color);
        HudDraws++;
    }

    void Text(int x, int y, string text, Color color)
    {
        int cx = x;
        foreach (char ch in text.ToUpperInvariant())
        {
            int idx = GlyphIndex(ch);
            var src = new Rectangle(idx * GlyphW, 0, GlyphW, GlyphH);
            _renderer.DrawQuad(_font, src, cx, y, GlyphW * Scale, GlyphH * Scale, color);
            HudDraws++;
            cx += GlyphW * Scale;
        }
    }

    static int GlyphIndex(char ch)
    {
        if (ch is >= '0' and <= '9') return ch - '0';
        if (ch is >= 'A' and <= 'Z') return 10 + (ch - 'A');
        return ch switch
        {
            ' ' => 36,
            ':' => 37,
            '/' => 38,
            '-' => 39,
            ',' => 40,
            '.' => 41,
            '(' => 42,
            ')' => 43,
            '?' => 44,
            '#' => 45,
            '+' => 46,
            _ => 36
        };
    }

    static IGpuTexture BuildFont(IRenderer renderer)
    {
        const int count = 47;
        int w = GlyphW * count;
        var bgra = new byte[w * GlyphH * 4];
        void Plot(int gi, int px, int py)
        {
            if (px < 0 || px >= 3 || py < 0 || py >= 5) return;
            int x = gi * GlyphW + px;
            int i = (py * w + x) * 4;
            bgra[i] = 255;
            bgra[i + 1] = 255;
            bgra[i + 2] = 255;
            bgra[i + 3] = 255;
        }

        ReadOnlySpan<string> glyphs =
        [
            "### # #### # ### # ### ### ### ", // 0-9 packed 3-wide rows later — use bits instead
        ];
        _ = glyphs;

        // 3×5 bitmasks, bit0 = top-left, row-major.
        uint[] bits =
        [
            0b111_101_101_101_111, // 0
            0b010_110_010_010_111, // 1
            0b111_001_111_100_111, // 2
            0b111_001_111_001_111, // 3
            0b101_101_111_001_001, // 4
            0b111_100_111_001_111, // 5
            0b111_100_111_101_111, // 6
            0b111_001_001_001_001, // 7
            0b111_101_111_101_111, // 8
            0b111_101_111_001_111, // 9
            0b010_101_111_101_101, // A
            0b110_101_110_101_110, // B
            0b011_100_100_100_011, // C
            0b110_101_101_101_110, // D
            0b111_100_110_100_111, // E
            0b111_100_110_100_100, // F
            0b011_100_101_101_011, // G
            0b101_101_111_101_101, // H
            0b111_010_010_010_111, // I
            0b001_001_001_101_010, // J
            0b101_101_110_101_101, // K
            0b100_100_100_100_111, // L
            0b101_111_111_101_101, // M
            0b101_111_111_111_101, // N
            0b010_101_101_101_010, // O
            0b110_101_110_100_100, // P
            0b010_101_101_111_011, // Q
            0b110_101_110_101_101, // R
            0b011_100_010_001_110, // S
            0b111_010_010_010_010, // T
            0b101_101_101_101_111, // U
            0b101_101_101_101_010, // V
            0b101_101_111_111_101, // W
            0b101_101_010_101_101, // X
            0b101_101_010_010_010, // Y
            0b111_001_010_100_111, // Z
            0,                     // space
            0b000_010_000_010_000, // :
            0b001_001_010_100_100, // /
            0b000_000_111_000_000, // -
            0b000_000_000_010_100, // ,
            0b000_000_000_000_010, // .
            0b010_100_100_100_010, // (
            0b010_001_001_001_010, // )
            0b010_101_001_000_010, // ?
            0b101_101_010_101_101, // #
            0b010_010_111_010_010, // +
        ];

        for (int g = 0; g < bits.Length; g++)
        {
            uint m = bits[g];
            for (int row = 0; row < 5; row++)
            for (int col = 0; col < 3; col++)
            {
                int bit = 14 - (row * 3 + col);
                if (((m >> bit) & 1) != 0)
                    Plot(g, col, row);
            }
        }

        return renderer.CreateTexture(w, GlyphH, bgra);
    }

    public void Dispose()
    {
        _white.Dispose();
        _font.Dispose();
    }
}
