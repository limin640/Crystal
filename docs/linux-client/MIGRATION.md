# Linux client migration notes

## Sequencing toward the hard done gate

Hard-gate **verbs** (login → select → walk → fight → loot → equip) are evidenced on Linux vs `Server.Linux` + Shared packets. Silk.NET input + a minimum IRenderer HUD are **in progress**. Full WinForms `GameScene`, audio, and WebView2 remain deferred. Language rewrite remains deferred.

This PR lands the bake/renderer levers **and** the Linux verb path. It does not vendor Data, Jev, or bake atlases.

```
Data tree (WIL/WZL/WTL/Lib)
        │
        ▼
 crystal-bake ──► atlases + BC3 + bake-coverage.json  (100% of files that exist)
        │         streaming pack + WTL DXT decode
        ▼
 IRenderer ──┬── SlimDXRenderer (Windows Client, live)
             └── OpenGLRenderer / NullRenderer (Client.Linux)
        │
        ▼
 Client.Linux: Shared packets + Silk.NET input/HUD + MapView (IRenderer)
 Server.Linux: full Jev --root (Maps + Server.MirDB) without --listen-without-world
 deferred: full WinForms GameScene, audio, WebView2
```

## Design choices

1. **Do not shrink the corpus.** Bake enumerates the whole Data tree. Coverage is `parsed / total` of files present. Crystal catalog slots that are absent are listed, not filled with placeholder art.
2. **Smallest unlock, not a rewrite.** `MLibrary`, GameScene floor/light RTs, control textures, CMain present, and particles are backend-agnostic. SlimDX stays as the Windows adapter only.
3. **Language rewrite waits.** Shared packets, `Client.MirNetwork`, server, and scene logic stay C# Crystal.
4. **Linux-capable Client path** is `Client.Linux` (`net8.0`). The WinForms `Client.csproj` remains `net8.0-windows7.0` + SlimDX; that target cannot compile on Linux and is not claimed as the Linux build.
5. **Do not vendor a client pack.** Operators point `--data` at an external Data tree. `init-stress` synthesizes tiny `.Lib` files for catalog-shaped load tests.

## Renderer API

`Crystal.Graphics.IRenderer`:

- `CreateTexture` / `CreateRenderTarget` / `CreateSolidTexture` / `UpdateTexture` / `GetSurface`
- `DrawQuad` (batched on OpenGL)
- `SetBlend` / `SetOpacity` / `SetGrayscale` / `SetSurface` / `SetMultiplyBlend`
- `BeginFrame` / `EndFrame` / `Flush` / `Present` / `Clear`

Windows: `Client/MirGraphics/Rendering/SlimDXRenderer.cs` wraps the existing Device/Sprite.

Linux: `Crystal.Graphics.Backends.OpenGLRenderer` (Silk.NET.OpenGL). Headless CI uses `NullRenderer`.

`IGpuSurface` from `GetSurface()` is owned by the texture — callers must not dispose it.

## What moved off SlimDX types this increment

| Surface | Path |
| --- | --- |
| Floor / light RTs + light compose | `GameScene` → `IRenderer.CreateRenderTarget` / `SetMultiplyBlend` |
| Control / scene RTs, borders, GDI labels | `MirControl` / `MirScene` / `MirLabel` / `MirTextBox` |
| CMain present loop | `CMain : Form` (not `RenderForm`); `DXManager.BeginFrame/EndFrame/Present` |
| Screenshot | `DXManager.TrySaveScreenshot` (SlimDX stays inside the adapter) |
| Particles | `System.Numerics.Vector2` |
| Linux catalog draw | `Client.Linux` uploads atlas PNGs and `DrawQuad`s every sprite |

Still Windows-only inside the SlimDX adapter: Device/Sprite/Line, pixel shaders (`normal.ps` / `grayscale.ps` / `magic.ps`), D3D reset.

## Bake outputs

```
bake-out/
  catalog.json
  bake-coverage.json
  atlases/atlas_0000.png
  atlases/atlas_0000.bc3
```

`catalog.json` sprite keys are `{relative library}:{index}` with UV rectangles and WIL-style offsets.

WTL: v1 RLE + DXT-like 8-byte blocks and v2 zlib+DXT1/3/5 are decoded in software. Unknown texture types stay undecoded and are counted as listed-not-decoded.

## Phase C checklist (accepted)

| Unit | Status |
| --- | --- |
| `Server.Library` (`net8.0`) builds on Linux | Done |
| `Server.Linux` console host (no WinForms) | Done |
| Listen on 7000 (`--listen-without-world` or full Jev world) | Done |
| External Crystal.Database Jev path, not vendored | Done |
| Client.Linux `Mir2Test.ini` IP/port + `--connect` | Done |
| Shared `Packet` handshake: Connected → ClientVersion → NewAccount/Login | Done (`LoginSuccess`, empty chars, no world) |
| Bake / IRenderer / no SlimDX on Linux | Unchanged |

## Phase D checklist (this increment)

| Unit | Status |
| --- | --- |
| Server.Linux loads full external Jev root (`Configs/Envir/Maps/Server.MirDB`) | Done — `--root` + maps present ⇒ **no** `--listen-without-world` |
| `--allow-start-game` / `--no-version-check` documented | Done — see BUILD.md |
| Client.Linux after LoginSuccess: character list / `NewCharacter` / `StartGame` | Done — Shared packets; GameScene-equivalent in-map state |
| Map/object draw via bake catalog **or** existing `.Lib` through `IRenderer` | Done — `Crystal.Assets.Maps.MapReader` + `MapView` on OpenGL/Null |
| Walk packet sent once in-map (evidence toward walk) | Done — one `C.Walk`; not a full input map |
| Fight / loot / equip | **Done (Phase E)** — scripted Shared packets, not GameScene UI |
| Hard gate login→select→walk→fight→loot→equip | **Evidenced** on CloudAgent VM and Grok Bot Linux box; do not regress |

Exact full-world flags (operator Jev tree **outside** git — never vendor DB/maps):

```bash
dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
  --root /path/to/Crystal.Database/Jev \
  --no-version-check --allow-start-game --seconds 90

dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --connect --ini Client.Linux/Mir2Test.ini --headless \
  --catalog Tools/Crystal.Bake/fixtures/bake-out/catalog.json \
  --maps /path/to/Crystal.Database/Jev/Maps
```

`--listen-without-world` is ignored when `Server.MirDB` and `*.map` files exist so StartGame is not handshake-only.

### Evidence (this agent, external Jev at `/tmp/Crystal.Database/Jev` — not in git)

Server (`--root …/Jev --no-version-check --allow-start-game`, **no** `--listen-without-world`):

```
WorldMode=full Server.MirDB=present Maps=1698
CheckVersion=False EnforceDBChecks=False ListenWithoutWorld=False AllowStartGame=True
463 Maps Loaded.
Network Started.
Server listening on 127.0.0.1:7000 (Running=True)
User logged in.
LinuxWar has connected.
```

Client (`--connect --headless --catalog fixtures/bake-out/catalog.json --maps …/Jev/Maps`):

```
LoginSuccess characters=0
NewCharacterSuccess index=1 name=LinuxWar class=Warrior
StartGame Result=4 (success)
in-map: MapInformation index=1 file=0 title=BichonProvince
in-map: UserInformation id=57940 name=LinuxWar loc=288,616
send Walk → UserLocation 289,616 dir=Right  WalkAck=True
Map loaded …/Maps/0.map 700x700
headless Null: draws=167 floor=143 objectDraws=24
```

Linux Release builds green: `Crystal.Assets`, `Crystal.Graphics`, `Crystal.Bake`, `Server.Library`, `Server.Linux`, `Client.Linux`.

That is NewCharacter → StartGame → in-map (+ one walk ack).

## Phase E checklist (this increment)

| Unit | Status |
| --- | --- |
| Scripted `C.Attack` vs nearby / `@MOB` spawn | Done — `ObjectStruck` + `DamageIndicator` + `ObjectHealth` |
| `C.PickUp` ground item (kill drop or seeded) | Done — `GainedItem` / bag count |
| `C.EquipItem` from inventory | Done — `S.EquipItem.Success` + equipment slot |
| Same session after walk | Done |
| WinForms GameScene / audio / WebView2 | Deferred (do not block) |
| Silk.NET input + IRenderer select/game HUD | **In progress** — Shared C.Walk/C.Attack, not WinForms |

`--test-server` on Server.Linux sets `Settings.TestServer=true` so the **existing** `@LEVEL` / `@MOB` / `@MAKE` / `@MOVE` commands work. No invented packets.

```bash
dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
  --root /path/to/Crystal.Database/Jev \
  --no-version-check --allow-start-game --test-server --seconds 90
```

### Evidence (same Jev root, one Client.Linux session)

```
StartGame Result=4  WalkAck=True  User=LinuxWar loc=298,616
@MOB Deer → ObjectMonster id=58654
C.Attack → ObjectStruck id=58654 attacker=58298 (self)
           DamageIndicator dmg=-6  ObjectHealth percent=76
C.PickUp → GainedItem uid=3 name=(HP)DrugSmall bag=3
C.EquipItem → Success=True slot=Armour name=BaseDress(M) uid=2
FightHit=True LootOk=True EquipOk=True
```

Linux Release builds still green. Jev / Data stay outside git.

## Client.Linux interactivity (in progress)

Silk.NET windowed keyboard/mouse (WASD / arrows / numpad, Space/Ctrl attack, G pickup, left-click walk, right-click attack) drives **the same** `C.Walk` / `C.Attack` / `C.PickUp` packets as Crystal. Headless CI injects the same commands with `--input-script Right,Right,Attack`.

Minimum **select** and **game HUD** draw through `IRenderer` — not WinForms: map/loc/level/class/gold, HP/MP, last 4 `S.Chat` lines, **inventory bag grid**, **equip slots**, **belt** (inventory 0–5, same as Crystal `BeltDialog`), and an 8-slot **skill stub** filled from `UserInformation.Magics` / `S.NewMagic`. Procedural 3×5 HUD glyphs are UI chrome, not WIL art.

Catalog **86** missing slots stay pack-missing (listed, not synthesized). Do not invent art.

### WinForms-only (stubbed on Linux — listed, not blocking)

| Piece | Status |
| --- | --- |
| `SelectScene` / `GameScene` dialog graph | Stub — HUD + packets only |
| Inventory bag + equip slots | **In progress** — IRenderer panel from `UserInformation` / `GainedItem` / `EquipItem` |
| Belt (inv 0–5) | **In progress** — IRenderer stub, Crystal `BeltDialog` slot map |
| Skill bar | **In progress** — 8 stubs from `ClientMagic` (no MagIcon WIL, no targeting) |
| Chat log | **In progress** — last 4 `S.Chat` lines (no input box) |
| `MirMessageBox`, NPC/quest/trade windows | Stub |
| Magic targeting / skill icons (`MagIcon`) | Stub |
| Inventory drag-drop / use-item clicks | Stub (`C.EquipItem` still works) |
| Mini-map (`MMap.Lib`) / big map | Stub |
| Chat input box / CMain keybind INI | Stub |
| MapControl lights / weather / doors | Stub (`MapView` floor/objects only) |
| **Audio (NAudio)** | Deferred — do not block |
| **WebView2** | Deferred — do not block |

```bash
# headless multi-step input after StartGame (does not replace the hard-gate --connect path)
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --connect --headless --input-script Right,Right,Attack,Down,Attack \
  --catalog Tools/Crystal.Bake/fixtures/bake-out/catalog.json \
  --maps /path/to/Crystal.Database/Jev/Maps

# windowed (needs DISPLAY + libglfw3)
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --connect --window --catalog … --maps …
```

### CloudAgent evidence (2026-09-09)

Linux Release builds green: `Crystal.Assets`, `Crystal.Graphics`, `Crystal.Bake`, `Server.Library`, `Server.Linux`, `Client.Linux`.

**Hard-gate unchanged** (`--connect --headless`, no `--input-script`): **EXIT:0**

```
StartGame Result=4 (success)
WalkAck=True loc=300,615
FightHit=True FightDied=False LootOk=True EquipOk=True
  fight : ObjectStruck id=58653 by self
  loot  : PickUp ground (HP)DrugSmall at 300,615 bag=2
  equip : EquipItem Success slot=Torch name=Candle uid=4
  input   : walks=0 attacks=0 pickups=0
```

**Input-injected walk+attack after StartGame** (`--no-gate --input-script Right,Right,Attack,Down,Attack`): **EXIT:0**

```
StartGame Result=4 → InMap BichonProvince loc=298,615
input-script 5 commands
input Walk Right #1 loc=298,615 → UserLocation 299,615
input Walk Right #2 loc=299,615 → UserLocation 300,615
input Attack Right #1
input Walk Down #3 loc=300,615 → UserLocation 300,616
input Attack Down #2
input-script done walks=3 attacks=2 pickups=0 loc=300,616 WalkAck=True
  input   : walks=3 attacks=2 pickups=0
```

**Windowed OpenGL** (`--connect --window --frames 12 --no-gate --input-script Right,Attack`): **EXIT:0**

```
input Walk Right #1 → UserLocation 301,615
input Attack Right #1 → ObjectStruck / DamageIndicator / ObjectHealth
input-script done walks=1 attacks=1 WalkAck=True FightHit=True
Client.Linux windowed OK frames=12 backend=Silk.NET OpenGL
```

See [evidence/input-hud.md](evidence/input-hud.md). Catalog **86** slots remain pack-missing.

## Remaining gaps (OK to defer)

1. **Full GameScene** — Client.Linux is Shared packets + `MapView` + HUD, not a language rewrite of the WinForms scene graph.
2. **Audio / WebView2** — Windows-only; documented, not a Linux verb blocker.
3. **Operator art** — floor/objects still catalog or `--data` `.Lib`; 86 catalog slots remain missing-on-disk. No invented WIL.
4. **Version hash** — `--no-version-check` unless a real `Mir2.Exe` hash list is supplied.

The login→select→walk→fight→loot→equip **verbs** stay evidenced (do not regress). Input/HUD is the next fold toward GameScene, not a replacement of that proof.

## Operator box hard gate

Reproduced on the **Grok Bot Linux box** (not only the CloudAgent VM). Date: 2026-09-09 ~16:08 CST. Branch zipball `cursor/linux-bake-renderer-bba8` at `e9f3c9b`. Publish `Server.Linux` + `Client.Linux` Release. External Jev at an operator path (**not** in the repo).

Client session **EXIT:0** proved in one run:

| Verb | Evidence |
| --- | --- |
| Account | `NewAccount Result=8` → `LoginSuccess characters=0` |
| Select | `NewCharacterSuccess name=LinuxWar class=Warrior` |
| StartGame | `StartGame Result=4` → `InMap=True` BichonProvince |
| Walk | `WalkAck=True` loc=289,616 |
| Fight | `FightHit=True` (`ObjectStruck` by self) |
| Loot | `LootOk=True` `PickUp` `(HP)DrugSmall` bag=4 |
| Equip | `EquipOk=True` `EquipItem Success` slot=Weapon `WoodenSword` |

Final block:

```
LoginSuccess=True NewCharacterOk=True StartGameResult=4 InMap=True
WalkAck=True FightHit=True LootOk=True EquipOk=True
```

Do **not** vendor bake atlases, the Data pack, or DB/maps into git. Metrics and log excerpts only.

## Full-corpus bake (mirfiles pack A)

Real mirfiles crystal/patch Data (~7.2G), operator box, **not** in git.

Flags: `--atlas-size 2048 --compress bc3`  
Duration: ~33m52s (16:10–16:44 CST)

| Metric | Value |
| --- | --- |
| Libraries parsed/discovered | **1440/1440 (100%)** |
| **Non-blank image decode** | **1869869/1869869 (100%)** |
| **Listed-slot decode** | **1869869/2143132 (87.25%)** — includes empty Mir library slots |
| Images blank | 273263 (`listed − decoded` equals Blank exactly) |
| Images packed/decoded | 1869867/1869869 (2 decoded-not-packed; optional packer edge) |
| Catalog present/expected | **162/248 (65.32%)** — missing-on-disk only |
| Missing catalog slots | **86** (listed, not synthesized) |
| Parse failures | **0** |
| Atlases | 6158 (png+bc3) |

**Dual metrics.** Operator-box gap analysis: `listed − decoded == Blank` (273263). The 12.75% listed-slot gap is empty Mir library slots in the denominator, **not** a decoder bug. Non-blank decode is **100%**. Do **not** invent pixels for blanks; no decoder fix is required for that gap. Two frames decoded but not packed is an optional packer edge, not a decode failure. Catalog 162/248 is files absent from this Data tree, not invented art. Bake-out stays on the operator box.
