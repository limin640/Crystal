# Linux client migration notes

## Sequencing toward the hard done gate

Hard-gate **verbs** (login → select → walk → fight → loot → equip) are evidenced on Linux vs `Server.Linux` + Shared packets. Silk.NET input + a minimum IRenderer HUD are **in progress**. Full WinForms `GameScene` and WebView2 remain deferred. Audio is `IAudio` (OpenAL / Null / NAudio). Language rewrite remains deferred.

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
 deferred: full WinForms GameScene, WebView2 (Windows-only)
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
| `--allow-start-game` / version hash | Done — `--version-path` / `--version-file` keep `CheckVersion` on; `--no-version-check` is opt-out |
| Client.Linux after LoginSuccess: character list / `NewCharacter` / `StartGame` | Done — Shared packets; GameScene-equivalent in-map state |
| Map/object draw via bake catalog **or** existing `.Lib` through `IRenderer` | Done — `Crystal.Assets.Maps.MapReader` + `MapView` on OpenGL/Null |
| Walk packet sent once in-map (evidence toward walk) | Done — one `C.Walk`; not a full input map |
| Fight / loot / equip | **Done (Phase E)** — scripted Shared packets, not GameScene UI |
| Hard gate login→select→walk→fight→loot→equip | **Evidenced** on CloudAgent VM and Grok Bot Linux box; do not regress |

Exact full-world flags (operator Jev tree **outside** git — never vendor DB/maps):

```bash
dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
  --root /path/to/Crystal.Database/Jev \
  --version-path /path/to/Crystal.Client.Linux.dll --allow-start-game --seconds 90

dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --connect --ini Client.Linux/Mir2Test.ini --headless \
  --catalog Tools/Crystal.Bake/fixtures/bake-out/catalog.json \
  --maps /path/to/Crystal.Database/Jev/Maps
```

`--listen-without-world` is ignored when `Server.MirDB` and `*.map` files exist so StartGame is not handshake-only.

### Evidence (this agent, external Jev at `/tmp/Crystal.Database/Jev` — not in git)

Server (`--root …/Jev --version-path …/Crystal.Client.Linux.dll --allow-start-game`, **no** `--listen-without-world`):

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
| Skill bar | **In progress** — 8 stubs from `ClientMagic`; `MagIcon.Lib` tiles via `--data` when present (`Icon * 2`, same as WinForms). No targeting |
| Chat log + send | **In progress** — last 4 lines; `--input-script Chat:hello` / windowed Enter compose → `C.Chat` |
| Mini-map chrome | **In progress** — IRenderer geometry + player blip; `MMap.Lib` tile via `--data` / catalog when present (`MapInformation.MiniMap` index, first decoded frame if that index is missing) |
| NPC talk (`C.CallNPC` `[@Main]` / `S.NPCResponse`) | **In progress** — IRenderer name + dialog lines |
| NPC goods (`S.NPCGoods`) | **In progress** — list after `[@BUY]`/`[@BUYSELL]` |
| NPC buy / sell (`C.BuyItem` / `C.SellItem`) | **In progress** — `--input-script Talk,Buy:0,Sell` uses existing `NPCGoods` UniqueIDs; gold/bag in logs + HUD |
| Quest panel | **In progress** — `S.NewQuestInfo` catalog + taken/done from `S.ChangeQuest` / `S.CompleteQuest`. `--input-script QuestAccept:id` / `QuestFinish:id` → `C.AcceptQuest` / `C.FinishQuest` (same as WinForms `QuestListDialog`). Approaches the Jev giver via ObjectNPC / Talk `@MOVE 289 617` |
| Player trade | **In progress** — two Client.Linux processes (`docs/linux-client/trade-two-process.sh`). `C.ChangeTrade` / `C.TradeRequest` / `C.TradeReply` / `C.TradeGold` / `C.DepositTradeItem` / `C.TradeConfirm` + matching `S.*`. Players must face each other. |
| Inventory bag move (`C.MoveItem` / `C.MergeItem`) | **In progress** — `--input-script Drag:0,8` / `Merge:from,to`; HUD slot refresh. Same packets as `MirItemCell` |
| Mouse-drag chrome (SelectedCell ghost / click-to-drop) | **In progress** — IRenderer colored-quad ghost + source/dest highlight on `Drag`. Windowed left-click pick/drop on bag/belt if Silk.NET mouse coords exist. Headless keeps `Drag:from,to` tokens. **No WIL item icons** |
| Magic targeting / skill icons (`MagIcon`) | **In progress** — `MagIcon.Lib` (fallback `MagIcon2.Lib`) through `MLibParser` + `IRenderer` when `--data` has the file. Targeting / MagIcon2 skill-book still stub |
| Mini-map WIL (`MMap.Lib`) / big map | **In progress** — `MMap.Lib` when `--data` has the file; skip when absent (no invented map art). Big-map dialog still stub |
| CMain keybind INI | Stub |
| MapControl lights / weather / doors | Stub (`MapView` floor/objects only) |
| **Audio** | **In progress** — `Crystal.Audio` `IAudio`: Null (headless), Silk.NET OpenAL on Linux, **NAudio backend** on Windows. `SoundManager` is the GameScene index API and calls through `IAudio` (`AudioFactory.CreateNAudio()`). `--play-sound` / `--sound` / `CRYSTAL_SOUND`. Fixture `Tools/Crystal.Audio/fixtures/tone.wav` (not game art). Do not vendor the Sound pack |
| **WebView2** | Windows-only, permanently deferred on Linux. `Client.Linux` never references it; a no-op browser stub is unnecessary |

```bash
# headless multi-step input after StartGame (does not replace the hard-gate --connect path)
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --connect --headless --input-script Talk,Buy:0,Sell \
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

### NPC talk (2026-09-09, same VM)

**Input-script** `--no-gate --input-script Right,Talk,Attack`: **EXIT:0**

```
input Talk CallNPC id=7 name=Merchant_Whitney key=[@Main]
NPCResponse lines=8
  npc-say Hello Traveller. What can I do for you?
  npc-say <View/@BuySell> Store.
input Talk CallNPC buy-key [@BUYSELL]
NPCGoods count=43 rate=1 type=Buy
input-script done talks=1 NpcTalkOk=True npc=Merchant_Whitney
hud npc: talkOk=True lines=3 goods=43 quests=12
  hud-npc-say Which item would you like to Buy or Sell?
  hud-npc-goods BaseDress(M) …
  hud-quest Assistant's Request
```

**Hard-gate** (no `--input-script`): **EXIT:0** — `FightHit` `LootOk` `EquipOk`, `talks=0`.

### NPC buy / sell (2026-09-09, same VM)

Same packets as Crystal `NPCDialogs`: `C.BuyItem` `{ ItemIndex = goods.UniqueID, Count = 1, Type = Buy }` and `C.SellItem` `{ UniqueID, Count = 1 }` after `S.NPCGoods`. Low gold uses existing test-server `@GIVEGOLD` (not PlayGate).

**Input-script** `--no-gate --input-script Talk,Buy:0,Sell`: **EXIT:0**

```
CallNPC Merchant_Whitney [@Main] / [@BUYSELL] → NPCGoods count=43 type=Buy
@GIVEGOLD 50000 → GainedGold +50000 gold=50000   (first run, gold was 0)
input Buy goods[0] uid=52 name=BaseDress(M) gold=49940 bag=2
LoseGold -120 gold=49820
GainedItem name=BaseDress(M) bag=3
buy evidence: BuyItem BaseDress(M) gold 49940→49820 bag 2→3
input Sell uid=8 name=BaseDress(M) gold=49820 bag=3
GainedGold +60 gold=49880
sell evidence: SellItem BaseDress(M) gold 49820→49880 bag 3→2
input-script done buys=1 sells=1 BuyOk=True SellOk=True gold=49880 bag=2
hud npc: gold=49880 bag=2 buys=1 sells=1 BuyOk=True SellOk=True
  hud-buy BuyItem BaseDress(M) gold 49940→49820 bag 2→3
  hud-sell SellItem BaseDress(M) gold 49820→49880 bag 3→2
```

**Hard-gate** (no `--input-script`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
  fight : ObjectStruck id=58297 by self
  loot  : PickUp ground (HP)DrugSmall at 306,613 bag=2
  equip : EquipItem Success slot=Weapon name=WoodenSword
  input   : talks=0 buys=0 sells=0 BuyOk=False SellOk=False
```

Player trade evidence is in the next section. Mouse-drag chrome, audio, and WebView2 stay deferred. Quest accept/turn-in UI stubbed (names only from `S.NewQuestInfo`).

### Player trade — two Client.Linux sessions (2026-09-09, same VM)

One login per account, so host is `linux2` / `LinuxWar2` and guest is `linux` / `LinuxWar`. They `@MOVE` to 300,616 and 299,616 and `C.Turn` to face each other. Script: `docs/linux-client/trade-two-process.sh`. Jev `MaxIP=5` — wait or restart Server.Linux before a later hard-gate if this IP was just used by both clients.

**Two-process** host `--auto-trade-reply --auto-trade-confirm` + guest `Trade,TradeGold:50,TradeConfirm`: **HOST_EXIT:0 GUEST_EXIT:0**

```
host ObjectPlayer LinuxWar loc=299,616
guest ObjectPlayer LinuxWar2 loc=300,616
guest C.TradeRequest face=Right loc=299,616 toward LinuxWar2 300,616
host S.TradeRequest from LinuxWar → auto C.TradeReply AcceptInvite=true
both S.TradeAccept
guest C.TradeGold 50 → LoseGold -50 gold 49880→49830
host S.TradeGold offer=50 → auto C.TradeConfirm Locked=true
guest C.TradeConfirm Locked=true
both S.TradeConfirm success
host gold=50 (GainedGold +50)  guest gold=49830
TradeHandshake=True TradeDone=True
```

**Hard-gate** (no `--input-script`, after Server.Linux restart to clear MaxIP): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
  fight : ObjectStruck id=57941 by self
  loot  : PickUp ground (HP)DrugSmall at 299,615 bag=2
  equip : EquipItem Success slot=Weapon name=WoodenSword
  input   : trades=0 TradeHandshake=False TradeDone=False
```

### Mini-map + chat send (2026-09-09, same VM)

**Input-script** `--no-gate --input-script Right,Chat:hello,Attack,Down,Attack`: **EXIT:0**

```
input Chat send 'hello' #1
input-script done walks=2 attacks=2 chats=1 ChatSent=1 ChatRecv=3 loc=306,616 WalkAck=True
map size 700x700 (MapReader / known cells — no invented MMap art)
hud minimap: 700x700 blip=306,616 blips=16 draws=817 mmapLib=101 (geometry only)
hud chat: sent=1 recv=3 echo=False lines=4
  hud-chat > hello
hud draws: … minimap=817 total=1246
```

**Hard-gate** (no `--input-script`): **EXIT:0** — `FightHit` `LootOk` `EquipOk`, `chats=0 ChatSent=0`, minimap still `700x700 draws=819`.

`MMap.Lib` was not on `--data` in that run (`mmapLib=101` catalog index only). Later increment loads it when the operator Data tree has the file. Quest accept/turn-in is evidenced. WebView2 stays deferred. SelectedCell ghost is colored quads (no WIL icons).

### Inventory / equip HUD (2026-09-09, same VM)

**Input-script** `--no-gate --input-script Right,Right,Attack,Down,Attack`: **EXIT:0**

```
input-script done walks=3 attacks=2 loc=304,616 WalkAck=True
hud inventory/equip: bag=1/46 equip=3/14 belt=1/6 skills=0 chat=3
hud draws: inv=50 equip=136 belt=21 skill=30 chat=86 total=412
  hud-bag slot=0 belt name=(HP)DrugSmall x1
  hud-equip slot=Weapon name=WoodenSword
  hud-equip slot=Armour name=BaseDress(M)
  hud-equip slot=Torch name=Candle
```

**Hard-gate** (no `--input-script`): **EXIT:0** — `FightHit` `LootOk` `EquipOk`, input counters 0.

```
hud inventory/equip: bag=2/46 equip=3/14 belt=2/6 skills=0 chat=4
hud draws: inv=50 equip=136 belt=25 skill=30 chat=130 total=462
  hud-bag slot=0 belt name=(HP)DrugSmall
  hud-bag slot=1 belt name=(HP)DrugSmall
  hud-equip Weapon WoodenSword / Armour BaseDress(M) / Torch Candle
  input   : walks=0 attacks=0 pickups=0
```

Skill stubs drew (`skill=30`) with `skills=0` — Warrior has no `ClientMagic` yet; no invented icons. Catalog **86** still pack-missing.

### Inventory bag move (2026-09-09, same VM)

Same packet as `MirItemCell`: `C.MoveItem` `{ Grid = Inventory, From, To }`. Token `Drag` / `Drag:0,8` (map teleport stays `Move:x:y`). No WIL icons; HUD slot list refreshes.

**Input-script** `--no-gate --input-script Drag`: **EXIT:0**

```
input MoveItem Grid=Inventory from=0 to=7 name=(HP)DrugSmall uid=6 dest=-
S.MoveItem Success=True grid=Inventory from=0 to=7
drag evidence: MoveItem (HP)DrugSmall slot 0→7
hud drag: ok=True moves=1 MoveItem (HP)DrugSmall slot 0→7
  hud-bag slot=6 name=WoodenSword x1
  hud-bag slot=7 name=(HP)DrugSmall x1
  hud-drag MoveItem (HP)DrugSmall slot 0→7
```

**Hard-gate** (no `--input-script`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
  fight : ObjectStruck id=60089 by self
  loot  : PickUp ground (HP)DrugSmall
  equip : EquipItem Success slot=Weapon name=WoodenSword
  input   : drags=0 DragOk=False
```

`C.MergeItem` is wired (`Merge:from,to`) for stackables. SelectedCell ghost (next section) is colored quads only — no invented WIL icons.

### Inventory SelectedCell ghost (2026-09-09, same VM)

IRenderer overlay on bag/belt: gold source, cyan dest, floating quad. `hud-drag ghost=N` is the extra fill count (not WIL sprites). Windowed left-click pick/drop uses the same slot hit-test; headless keeps `Drag` / `Drag:from,to`.

**Input-script** `--no-gate --input-script Drag`: **EXIT:0**

```
input MoveItem Grid=Inventory from=0 to=7 name=(HP)DrugSmall uid=6 dest=-
S.MoveItem Success=True grid=Inventory from=0 to=7
drag evidence: MoveItem (HP)DrugSmall slot 0→7
hud-drag ghost=3 from=0 to=7
  hud-bag slot=7 name=(HP)DrugSmall x1
  hud-drag MoveItem (HP)DrugSmall slot 0→7
  input   : drags=1 DragOk=True
```

Three extra IRenderer fills: gold source, cyan dest, floating quad. No WIL icons.

**Hard-gate** (no `--input-script`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
  fight : ObjectStruck id=57941 by self
  loot  : PickUp ground (HP)DrugSmall at 302,612 bag=2
  equip : EquipItem Success slot=Weapon name=WoodenSword uid=5
hud-drag ghost=0 from=-1 to=-1
  input   : drags=0 DragOk=False
```

### Linux audio — IAudio / OpenAL (2026-09-09, same VM)

`Crystal.Audio` mirrors `IRenderer`: Null on `--headless` (no device), Silk.NET OpenAL on `--play-sound`, NAudio `WaveOutEvent` on Windows (`SoundManager` → `IAudio`). No Sound pack on this VM; fixture `Tools/Crystal.Audio/fixtures/tone.wav` (generated sine, not game art). Operator packs: `--sound` / `CRYSTAL_SOUND`. OpenAL Soft uses the `null` output when no card (`alsoft-headless.conf` / `ALSOFT_DRIVERS=null`).

**Play** `--headless --play-sound`: **EXIT:0**

```
SoundPlayOk=True backend=Silk.NET OpenAL file=Tools/Crystal.Audio/fixtures/tone.wav err=-
```

**Hard-gate** `--connect --headless` (no `--play-sound`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
  fight : ObjectStruck id=60089 by self
  loot  : PickUp ground (HP)DrugSmall
  equip : EquipItem Success slot=Weapon name=WoodenSword
sound: backend=Null (headless) SoundPlayOk=False skipped=headless
```

WebView2 stays Windows-only (no Linux stub). WIL item icons stay pack-missing (catalog 86).

### Windows SoundManager → IAudio (2026-09-10)

`Client/MirSounds/SoundManager.cs` is a GameScene-facing index facade (`PlaySound(int)`, `SoundList`, `Settings.SoundPath`). Device I/O is `Crystal.Audio.IAudio` via `AudioFactory.CreateNAudio()` → `NAudioAudio` (WaveOutEvent + mixer, wav/mp3). Linux PlayGate still uses `AudioFactory.Create(headless, play)` (Null / OpenAL) and never constructs NAudio.

**Windows build** (operator box; SlimDX + WinForms):

```bat
dotnet build Client\Client.csproj -c Release
```

This Linux VM: `NETSDK1100` (`net8.0-windows7.0` needs `EnableWindowsTargeting` + a Windows pack). `Crystal.Audio` (includes NAudio backend) and `Client.Linux` **do** build here.

**Linux** `--headless --play-sound`: **EXIT:0**

```
SoundPlayOk=True backend=Silk.NET OpenAL file=/workspace/Tools/Crystal.Audio/fixtures/tone.wav err=-
```

**Hard-gate** `--connect --headless` (no `--play-sound`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
  fight : ObjectStruck id=66116 by self
  loot  : PickUp ground (HP)DrugSmall at 303,611 bag=2
  equip : EquipItem Success slot=Weapon name=WoodenSword uid=1
sound: backend=Null (headless) SoundPlayOk=False skipped=headless
```

### Quest accept / turn-in (2026-09-10, same VM)

Same packets as WinForms `QuestListDialog`: `C.AcceptQuest` `{ NPCIndex, QuestIndex }`, `C.FinishQuest` `{ QuestIndex, SelectedItemIndex }`. Catalog from `S.NewQuestInfo`; taken/done from `S.ChangeQuest` / `S.CompleteQuest`. Headless tokens `QuestAccept` / `QuestFinish` (optional `:id`). Approaches the giver already in view or via Talk `@MOVE 289 617`. No invented quest files.

**Input-script** `--no-gate --input-script QuestAccept,QuestFinish`: **EXIT:0**

```
input AcceptQuest npc=3 id=1 name=Assistant's Request
S.ChangeQuest Add id=1 name=Assistant's Request taken=True completed=True
input FinishQuest id=1 selected=-1 name=Assistant's Request
S.ChangeQuest Remove id=1 name=Assistant's Request
QuestAcceptOk=True QuestFinishOk=True
  hud-quest-accept AcceptQuest id=1 name=Assistant's Request S.ChangeQuest Add taken=True completed=True
  hud-quest-finish FinishQuest id=1 name=Assistant's Request S.ChangeQuest Remove
```

Starter Jev quest 1 has no kill/item tasks, so it completes on accept and turn-in is CraftLady (in DataRange after the Jane approach).

**Hard-gate** (no `--input-script`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
  fight : ObjectStruck id=68606 by self
  loot  : PickUp ground (HP)DrugSmall at 288,615 bag=2
  equip : EquipItem Success slot=Weapon name=WoodenSword uid=5
hud quest: info=154 taken=0 done=1 accepts=0 AcceptOk=False FinishOk=False
  input   : quests=0 QuestAcceptOk=False QuestFinishOk=False
```

### MMap.Lib / MagIcon tiles (2026-09-10, same VM)

WinForms `Libraries.MiniMap` / `Libraries.MagIcon` are `Settings.DataPath + "MMap"` / `"MagIcon"` (`.Lib`). Client.Linux uses the same files through `Crystal.Assets` `LibraryParser` / `MLibParser` + `IRenderer` when `--data` / `CRYSTAL_DATA` points at an external Data tree (or a bake catalog sprite exists). Missing files skip — no invented texels. Do not vendor the pack. On Linux, `DataPath` tries the exact name then a case-fold match in the directory so an operator `mmap.Lib` opens as `MMap.Lib` (no symlink). The same resolver is used for other Data `.Lib` paths in `MapView`.

Bake fixture `Tools/Crystal.Bake/fixtures/bake-out` has no MMap/MagIcon sprites (they stay in the listed **86** missing catalog slots). Operator path: download the mirfiles Crystal `Data` folder (the directory that contains `MMap.Lib` and `MagIcon.Lib`) and pass `--data /path/to/Data`. Smoke without a pack: `crystal-bake init-sample /tmp/crystal-mmap-sample` writes synthetic checkers (not game art) including those two files.

**Hard-gate** `--connect --headless` (no `--data`): **EXIT:0**

```
hud-lib skip MMap.Lib: missing (no --data / catalog sprite)
hud-lib skip MagIcon.Lib: missing (no --data / catalog sprite)
hud-lib skip MagIcon2.Lib: missing (no --data / catalog sprite)
hud-lib ready MMapOk=False MagIconOk=False images=0/0
FightHit=True FightDied=False LootOk=True EquipOk=True
hud mmap: MMapOk=False MagIconOk=False mmapDraws=0 magDraws=0 mmapIndex=-1 magIndex=-1 mmapSrc=- magSrc=- images=0/0
```

**Hard-gate** `--connect --headless --data /tmp/crystal-mmap-sample` (`crystal-bake init-sample`, not a vendored pack): **EXIT:0**

```
hud-lib MMap file=MMap.Lib ok=True images=4 src=/tmp/crystal-mmap-sample/MMap.Lib
hud-lib MagIcon file=MagIcon.Lib ok=True images=4 src=/tmp/crystal-mmap-sample/MagIcon.Lib
hud-lib ready MMapOk=True MagIconOk=True images=4/4
FightHit=True FightDied=False LootOk=True EquipOk=True
  fight : ObjectStruck id=58654 by self
  loot  : PickUp ground (HP)DrugSmall at 288,613 bag=2
  equip : EquipItem Success slot=Weapon name=WoodenSword uid=5
hud minimap: 700x700 blip=288,613 blips=39 draws=55 mmapLib=101
hud mmap: MMapOk=True MagIconOk=True mmapDraws=1 magDraws=1 mmapIndex=0 magIndex=0 mmapSrc=/tmp/crystal-mmap-sample/MMap.Lib magSrc=/tmp/crystal-mmap-sample/MagIcon.Lib images=4/4
```

Jev `mmapLib=101` is past the 4-image fixture, so draw falls back to the first decoded frame (`mmapIndex=0`). Warrior has no `ClientMagic`; skill slot 0 probes `MagIcon[0]` (`magDraws=1`). Operator `--data` with a real pack uses the same parse+draw path. A pack that ships `mmap.Lib` (not `MMap.Lib`) opens through `DataPath` case-fold — no symlink.

### Version hash (2026-09-10, same VM)

WinForms `LoginScene.SendVersion` MD5s `Application.ExecutablePath`. Server `Settings.LoadVersion` MD5s each `VersionPath` file (default `.\Mir2.Exe`) and `MirConnection.ClientVersion` compares `C.ClientVersion.VersionHash` when `CheckVersion` is true.

Linux uses the same MD5-of-file (or a `.md5` / `.hashes` hex list). Do **not** vendor `Mir2.Exe`.

| Side | Flag / env | Default |
| --- | --- | --- |
| Server.Linux | `--version-path` / `CRYSTAL_VERSION_PATH` | Setup.ini `VersionPath` (often missing `Mir2.Exe`) |
| Server.Linux | `--version-hashes` / `CRYSTAL_VERSION_HASHES` | none |
| Client.Linux | `--version-file` / `CRYSTAL_VERSION_FILE` | `Crystal.Client.Linux.dll` (this host) |
| Client.Linux | `--version-hash` / `CRYSTAL_VERSION_HASH` | none |

When hashes load, Server.Linux sets `CheckVersion=true` unless `--no-version-check`. CLI overrides are not written back to operator `Setup.ini`.

Local pair (no Windows exe on the box): both sides hash `Client.Linux/bin/Release/net8.0/Crystal.Client.Linux.dll`. Operator with a real `Mir2.Exe`: point both flags at that file (hash only).

**Hard-gate** Server **without** `--no-version-check` (`--version-path …/Crystal.Client.Linux.dll --allow-start-game --test-server`): **EXIT:0**

```
CheckVersion=True VersionPath=…/Crystal.Client.Linux.dll hashes=1
version-hash[0]=be802de59906a44ee66f52d365ff91a9
version: src=…/Crystal.Client.Linux.dll md5=be802de59906a44ee66f52d365ff91a9 bytes=16
handshake: ClientVersion Result=1 (match) VersionCheckOk=True
VersionCheckOk=True VersionResult=1
FightHit=True FightDied=False LootOk=True EquipOk=True
  fight : ObjectStruck id=58296 by self
  loot  : PickUp ground (HP)DrugSmall at 288,612 bag=2
  equip : EquipItem Success slot=Weapon name=WoodenSword uid=1
```

`--no-version-check` was not passed. Stock Jev `Setup.ini` `CheckVersion=False` was overridden for the process only (not written back).

## Remaining residuals (checklist)

These do **not** block the hard-gate (login→select→walk→fight→loot→equip **EXIT:0**). Do not invent WIL/game art to close them.

- [ ] **Full WinForms `GameScene`** — Client.Linux is Shared packets + `MapView` + IRenderer HUD, not a language rewrite of the scene graph / dialogs.
- [ ] **WebView2** — Windows-only (WinForms Evergreen; no Linux runtime). Permanently deferred on Linux. `Client.Linux` must never reference it.
- [ ] **WIL item icons** — `Items` / `StateItem` / `DNItems` catalog sheets. Colored-quad SelectedCell ghost is the Linux stand-in. Catalog **86** slots stay pack-missing (listed, not synthesized).
- [x] **`MMap.Lib` / MagIcon tiles** — `HudLibSheet` parses optional `--data` `.Lib` via `MLibParser` and draws through `IRenderer`. Linux open is case-insensitive (`mmap.Lib` matches `MMap.Lib`; no symlink). Skip when absent (`MMapOk=False`). Leftover: no big-map dialog, no MagIcon2 skill-book / targeting, no invented tiles. Operator Data stays outside git.
- [x] **Quest accept / turn-in** — `C.AcceptQuest` / `C.FinishQuest` / `C.AbandonQuest` / `C.ShareQuest` + `S.ChangeQuest` / `S.CompleteQuest`. HUD lists available/taken. Leftover: no WinForms quest diary chrome / select-reward picker UI (script uses `QuestFinish:id,selected`).
- [x] **Windows `SoundManager` → `IAudio` fold** — `SoundManager` calls `IAudio` (NAudio backend). Leftover: GameScene still uses the index API (`PlaySound(int)` / `SoundList.lst`), not raw paths; `WaveOutEvent` is Windows-runtime; `Client.csproj` still does not build on Linux (SlimDX / WinForms / WebView2).
- [x] **Version hash** — same MD5-of-file as WinForms `LoginScene.SendVersion` / `Settings.LoadVersion`. Server `--version-path` / `CRYSTAL_VERSION_PATH` (file or `.md5` / `.hashes` list) + Client `--version-file` / `CRYSTAL_VERSION_FILE` (default: this host's `Crystal.Client.Linux.dll`). `--no-version-check` remains an opt-out. Do not vendor `Mir2.Exe`. Leftover: a Windows server that only lists `Mir2.Exe` needs the operator to add the Linux client hash or point Linux `--version-file` at that exe.
- [ ] **Operator art / Data / Jev / Sound packs** — stay outside git. `--data` / `--maps` / `--sound` / `--root` point at external trees. Do not vendor bake atlases.

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
