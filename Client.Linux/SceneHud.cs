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

    public void DrawGame(int width, int height, CrystalSession session)
    {
        Fill(8, 8, width - 16, 40, Color.FromArgb(190, 8, 10, 16));
        Text(16, 14, $"{session.MapTitle ?? "?"}  {session.UserName} Lv{session.UserLevel} {session.UserClass}  {session.UserLocation.X},{session.UserLocation.Y}  {session.Facing}  G{session.UserGold}", Color.White);

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

        if (!string.IsNullOrWhiteSpace(session.LastChat))
            Text(16, height - 72, session.LastChat, Color.Khaki);

        Text(width - 220, height - 28, $"in {session.InputWalks} atk {session.InputAttacks}", Color.Silver);
    }

    void Fill(int x, int y, int w, int h, Color color)
    {
        if (w <= 0 || h <= 0) return;
        _renderer.DrawQuad(_white, null, x, y, w, h, color);
    }

    void Text(int x, int y, string text, Color color)
    {
        int cx = x;
        foreach (char ch in text.ToUpperInvariant())
        {
            int idx = GlyphIndex(ch);
            var src = new Rectangle(idx * GlyphW, 0, GlyphW, GlyphH);
            _renderer.DrawQuad(_font, src, cx, y, GlyphW * Scale, GlyphH * Scale, color);
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
            _ => 36
        };
    }

    static IGpuTexture BuildFont(IRenderer renderer)
    {
        const int count = 45;
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
