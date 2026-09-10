# Client.Linux input + HUD evidence

CloudAgent VM, 2026-09-09. Branch `cursor/linux-bake-renderer-bba8`. External Jev at `/tmp/Crystal.Database/Jev` (**not** in git). Fixture catalog only.

Linux Release builds green: `Crystal.Assets`, `Crystal.Graphics`, `Crystal.Bake`, `Server.Library`, `Server.Linux`, `Client.Linux`.

## Hard-gate unchanged (do not regress)

`--connect --headless` with no `--input-script`. **EXIT:0**

```
StartGame Result=4 (success)
WalkAck=True loc=300,615
FightHit=True LootOk=True EquipOk=True
  fight : ObjectStruck id=58653 by self
  loot  : PickUp ground (HP)DrugSmall
  equip : EquipItem Success slot=Torch name=Candle
  input   : walks=0 attacks=0 pickups=0
```

PlayGate verbs are still the scripted one-shot path. Input counters stay at 0 when `--input-script` is omitted.

## Headless input-injected walk+attack after StartGame

`--connect --headless --no-gate --input-script Right,Right,Attack,Down,Attack`. **EXIT:0**

Same `Drive()` as Silk.NET WASD/Space. Not the Phase E one-shot `TryFight`.

```
StartGame Result=4 → BichonProvince 298,615
input Walk Right #1 loc=298,615 → 299,615
input Walk Right #2 loc=299,615 → 300,615
input Attack Right #1
input Walk Down #3 loc=300,615 → 300,616
input Attack Down #2
input-script done walks=3 attacks=2 loc=300,616 WalkAck=True
```

IRenderer HUD + MapView: `draws=272 floor=143 objectDraws=24` (Null backend).

## Windowed Silk.NET OpenGL

`--connect --window --frames 12 --no-gate --input-script Right,Attack`. **EXIT:0**

```
input Walk Right #1 → 301,615
input Attack Right #1 → ObjectStruck / DamageIndicator dmg=-6
input-script done walks=1 attacks=1 FightHit=True
Client.Linux windowed OK frames=12 backend=Silk.NET OpenGL
```

## Inventory / equip / belt / skill HUD (same VM, later)

IRenderer panels from `UserInformation` / `GainedItem` / `EquipItem`. No WIL item icons.

**Input-script** `--no-gate --input-script Right,Right,Attack,Down,Attack`: **EXIT:0**

```
input-script done walks=3 attacks=2 loc=304,616 WalkAck=True
hud inventory/equip: bag=1/46 equip=3/14 belt=1/6 skills=0 chat=3
hud draws: inv=50 equip=136 belt=21 skill=30 chat=86 total=412
  hud-bag slot=0 belt name=(HP)DrugSmall x1
  hud-equip slot=Weapon name=WoodenSword
  hud-equip slot=Armour name=BaseDress(M)
  hud-equip slot=Torch name=Candle
  hud-chat Welcome to the Legend of Mir 2 Server.
```

**Hard-gate** `--connect --headless` (no `--input-script`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
hud inventory/equip: bag=2/46 equip=3/14 belt=2/6 skills=0 chat=4
hud draws: inv=50 equip=136 belt=25 skill=30 chat=130 total=462
  hud-bag slot=0 belt (HP)DrugSmall
  hud-bag slot=1 belt (HP)DrugSmall
  hud-equip Weapon / Armour / Torch
  input   : walks=0 attacks=0 pickups=0
```

Skill bar draws 8 empty stubs when `Magics` is empty (Warrior, no invented spells).

## Mini-map geometry + Shared chat send (same VM, later)

Occupancy + blip from `MapReader` size / `MapCell` / packet objects. `MMap.Lib` tiles draw only when `--data` has the file (see later MMap section). No invented map art.

**Input-script** `--no-gate --input-script Right,Chat:hello,Attack,Down,Attack`: **EXIT:0**

```
input Chat send 'hello' #1
input-script done walks=2 attacks=2 chats=1 ChatSent=1 ChatRecv=3 loc=306,616 WalkAck=True
map size 700x700 (MapReader / known cells — no invented MMap art)
hud minimap: 700x700 blip=306,616 blips=16 draws=817 mmapLib=101 (geometry only)
hud chat: sent=1 recv=3 lines=4
  hud-chat > hello
hud draws: minimap=817 total=1246
```

Windowed path: Enter starts compose, type, Enter sends the same `C.Chat`.

**Hard-gate** `--connect --headless` (no `--input-script`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
hud minimap: 700x700 blip=306,615 blips=18 draws=819
  input   : walks=0 attacks=0 pickups=0 chats=0 ChatSent=0
```

## NPC talk — CallNPC / NPCResponse (same VM, later)

Same packets as GameScene left-click: `C.CallNPC` `[@Main]` then `S.NPCResponse`. Optional `[@BUYSELL]` → `S.NPCGoods`. Quest names from `S.NewQuestInfo` at enter.

**Input-script** `--no-gate --input-script Right,Talk,Attack`: **EXIT:0**

```
input Talk CallNPC id=7 name=Merchant_Whitney key=[@Main]
NPCResponse lines=8
  npc-say Hello Traveller. What can I do for you?
  npc-say <View/@BuySell> Store.
CallNPC [@BUYSELL] → NPCGoods count=43 type=Buy
input-script done talks=1 NpcTalkOk=True
hud npc: talkOk=True name=Merchant_Whitney goods=43 quests=12
  hud-npc-say Which item would you like to Buy or Sell?
  hud-npc-goods BaseDress(M)
  hud-quest Assistant's Request
```

**Hard-gate** `--connect --headless` (no `--input-script`): **EXIT:0**

```
FightHit=True LootOk=True EquipOk=True
  input   : talks=0
```

## NPC buy / sell — C.BuyItem / C.SellItem (same VM, later)

`--no-gate --input-script Talk,Buy:0,Sell`. Uses `S.NPCGoods` UniqueIDs. **EXIT:0**

```
NPCGoods count=43 type=Buy
input Buy goods[0] BaseDress(M) gold=49940 bag=2
LoseGold -120 → GainedItem BaseDress(M) bag=3
buy evidence: BuyItem BaseDress(M) gold 49940→49820 bag 2→3
input Sell BaseDress(M) gold=49820 bag=3
GainedGold +60
sell evidence: SellItem BaseDress(M) gold 49820→49880 bag 3→2
hud npc: gold=49880 bag=2 buys=1 sells=1 BuyOk=True SellOk=True
  hud-buy BuyItem BaseDress(M) gold 49940→49820 bag 2→3
  hud-sell SellItem BaseDress(M) gold 49820→49880 bag 3→2
```

**Hard-gate** `--connect --headless` (no `--input-script`): **EXIT:0** — `FightHit` `LootOk` `EquipOk`, `buys=0` `sells=0`.

## Player trade — two Client.Linux processes (same VM, later)

`docs/linux-client/trade-two-process.sh`. Host `linux2`/`LinuxWar2` `--auto-trade-reply --auto-trade-confirm`. Guest `linux`/`LinuxWar` `Trade,TradeGold:50,TradeConfirm`. Face each other at 299,616 / 300,616. **HOST_EXIT:0 GUEST_EXIT:0**

```
guest C.TradeRequest face=Right loc=299,616 toward LinuxWar2 300,616
host S.TradeRequest from LinuxWar → C.TradeReply AcceptInvite=true
both S.TradeAccept
guest C.TradeGold 50 → LoseGold -50 gold 49880→49830
host S.TradeGold offer=50 → C.TradeConfirm Locked=true
guest C.TradeConfirm → both S.TradeConfirm success
host GainedGold +50 gold=50
TradeHandshake=True TradeDone=True
```

**Hard-gate** `--connect --headless` (no `--input-script`): **EXIT:0** — `FightHit` `LootOk` `EquipOk`, `trades=0 TradeDone=False`. Restart Server.Linux first if Jev `MaxIP=5` just counted the pair.

## Inventory bag move — C.MoveItem (same VM, later)

`--no-gate --input-script Drag` auto-picks first bag item → first empty non-belt slot. Same `C.MoveItem` as `MirItemCell`. **EXIT:0**

```
input MoveItem Grid=Inventory from=0 to=7 name=(HP)DrugSmall
S.MoveItem Success=True from=0 to=7
drag evidence: MoveItem (HP)DrugSmall slot 0→7
hud-bag slot=7 name=(HP)DrugSmall x1
  hud-drag MoveItem (HP)DrugSmall slot 0→7
```

**Hard-gate** `--connect --headless` (no `--input-script`): **EXIT:0** — `FightHit` `LootOk` `EquipOk`, `drags=0`.

`C.MergeItem` is wired (`Merge:from,to`). SelectedCell ghost is colored quads (next section) — no WIL icons.

## Inventory SelectedCell ghost (same VM, later)

`--no-gate --input-script Drag` draws IRenderer gold/cyan overlays + a floating quad. Headless tokens unchanged. Windowed left-click pick/drop when Silk.NET mouse coords exist. **EXIT:0**

```
input MoveItem Grid=Inventory from=0 to=7 name=(HP)DrugSmall
S.MoveItem Success=True from=0 to=7
hud-drag ghost=3 from=0 to=7
  hud-bag slot=7 name=(HP)DrugSmall x1
  input   : drags=1 DragOk=True
```

**Hard-gate** `--connect --headless` (no `--input-script`): **EXIT:0** — `FightHit` `LootOk` `EquipOk`, `drags=0`, `hud-drag ghost=0`.

```
FightHit=True LootOk=True EquipOk=True
hud-drag ghost=0 from=-1 to=-1
  input   : drags=0 DragOk=False
```

## Linux audio — IAudio / Silk.NET OpenAL (same VM, later)

`--headless --play-sound` (fixture wav; no operator Sound pack on this VM). **EXIT:0**

```
SoundPlayOk=True backend=Silk.NET OpenAL file=Tools/Crystal.Audio/fixtures/tone.wav
```

**Hard-gate** `--connect --headless` (no `--play-sound`): **EXIT:0** — Null backend, no device.

```
sound: backend=Null (headless) SoundPlayOk=False skipped=headless
FightHit=True LootOk=True EquipOk=True
```

Windows `SoundManager` calls through `IAudio` (`NAudioAudio`). WebView2 is Windows-only (permanently deferred on Linux).

## Windows SoundManager → IAudio (later)

`SoundManager.Create()` → `AudioFactory.CreateNAudio()`. Linux PlayGate unchanged (`AudioFactory.Create`). Windows `Client.csproj` build command: `dotnet build Client\Client.csproj -c Release` (this VM: `NETSDK1100`).

**`--headless --play-sound`**: **EXIT:0** — `SoundPlayOk=True backend=Silk.NET OpenAL`

**`--connect --headless`**: **EXIT:0** — `FightHit` `LootOk` `EquipOk`, `sound: backend=Null (headless)`

## Quest accept / turn-in (later)

`--no-gate --input-script QuestAccept,QuestFinish` → `C.AcceptQuest` / `C.FinishQuest`. **EXIT:0**

```
AcceptQuest id=1 name=Assistant's Request S.ChangeQuest Add taken=True completed=True
FinishQuest id=1 name=Assistant's Request S.ChangeQuest Remove
QuestAcceptOk=True QuestFinishOk=True
```

**Hard-gate** `--connect --headless`: **EXIT:0** — `quests=0` `QuestAcceptOk=False`.

## MMap.Lib / MagIcon tiles (later)

Optional `--data` / `CRYSTAL_DATA`. Same files as WinForms `Libraries.MiniMap` / `MagIcon`. Parse via `MLibParser`; draw via `IRenderer`. Skip when absent.

**Hard-gate** `--connect --headless` (no `--data`): **EXIT:0**

```
hud-lib skip MMap.Lib: missing (no --data / catalog sprite)
hud-lib ready MMapOk=False MagIconOk=False images=0/0
FightHit=True LootOk=True EquipOk=True
hud mmap: MMapOk=False MagIconOk=False mmapDraws=0 magDraws=0
```

**Hard-gate** `--connect --headless --data /tmp/crystal-mmap-sample` (`crystal-bake init-sample`, synthetic checkers, not game art): **EXIT:0**

```
hud-lib ready MMapOk=True MagIconOk=True images=4/4
FightHit=True LootOk=True EquipOk=True
hud mmap: MMapOk=True MagIconOk=True mmapDraws=1 magDraws=1 mmapIndex=0 magIndex=0
```

Jev `mmapLib=101` is past the 4-image fixture, so draw uses the first decoded frame.

Operator pack: `--data /path/to/Crystal/Data` (do not vendor). Linux `DataPath` opens `mmap.Lib` as `MMap.Lib` (exact path, then directory case-fold). Bake fixture catalog still lists MMap/MagIcon among the **86** missing slots.

**Case-fold smoke** `--data /tmp/crystal-mmap-case` (`mmap.Lib` / `magicon.Lib` only): **EXIT:0**

```
hud-lib MMap file=MMap.Lib ok=True src=/tmp/crystal-mmap-case/mmap.Lib
hud-lib MagIcon file=MagIcon.Lib ok=True src=/tmp/crystal-mmap-case/magicon.Lib
hud mmap: MMapOk=True MagIconOk=True mmapDraws=1 magDraws=1
```

## MagIcon2 skill-book / C.Magic (later)

Independent `MagIcon2.Lib` bind (WinForms skill-book `MagicButton` / `AssignKeyPanel`, `Icon * 2`). `--input-script Mag` / `MagTarget` → Shared `C.Magic` (`SpellTargetLock` on MagTarget). Starter Warrior often has no learned spell; `MagicOk` stays false. Linux `DataPath` opens `magicon2.Lib` as `MagIcon2.Lib`.

**Hard-gate** `--connect --headless` (no `--data`): **EXIT:0**

```
hud-lib skip MagIcon2.Lib: missing (no --data / catalog sprite)
hud-lib ready MMapOk=False MagIconOk=False MagIcon2Ok=False images=0/0/0
FightHit=True LootOk=True EquipOk=True
hud mmap: MagIcon2Ok=False mag2Draws=0 mag2Index=-1
```

**Smoke** `--data /tmp/crystal-mmap-sample --input-script Mag,MagTarget` (`crystal-bake init-sample`): **EXIT:0**

```
hud-lib MagIcon2 file=MagIcon2.Lib ok=True images=4 src=/tmp/crystal-mmap-sample/MagIcon2.Lib
hud-lib ready MagIcon2Ok=True images=4/4/4
FightHit=True LootOk=True EquipOk=True
input Mag C.Magic spell=Fencing target=3302 lock=True known=0
hud mmap: MagIcon2Ok=True mag2Draws=1 mag2Index=0 mag2Src=/tmp/crystal-mmap-sample/MagIcon2.Lib
```

**Case-fold** `--data /tmp/crystal-mmap-case` (`magicon2.Lib` only): **EXIT:0** — `src=/tmp/crystal-mmap-case/magicon2.Lib` mag2Draws=1.

## Big-map dialog (later)

IRenderer chrome (WinForms `BigMapDialog` / B). MapReader size + `MMap.Lib` when `--data` has the file. `--input-script BigMap`. No invented map art.

**Hard-gate** `--connect --headless` (closed / no `--data`): **EXIT:0**

```
hud bigmap: open=False BigMapOk=False draws=0 blips=0 mmap=False index=101
FightHit=True LootOk=True EquipOk=True
```

**Smoke** `--data /tmp/crystal-mmap-sample --input-script BigMap` (`crystal-bake init-sample`): **EXIT:0**

```
send RequestMapInfo
NewMapInfo index=1 title=BichonProvince size=700x700 big=101 npcs=39
hud bigmap: open=True BigMapOk=True draws=69 blips=34 mmap=True src=/tmp/crystal-mmap-sample/MMap.Lib
FightHit=True LootOk=True EquipOk=True
```

**Case-fold** `--data /tmp/crystal-mmap-case` (`mmap.Lib`): **EXIT:0** — `BigMapOk=True` draws=70 `src=/tmp/crystal-mmap-case/mmap.Lib`.

## World overlay / SearchMap / TeleportToNPC (later)

`MapLinkIcon.Lib` via `DataPath`. Quad chrome if Prguse2/Title missing. `WorldMap` / `SearchMap:text` / `TeleportNpc`.

**Hard-gate** `--connect --headless` (closed / no `--data`): **EXIT:0**

```
hud-lib skip MapLinkIcon.Lib: missing (no --data / catalog sprite)
hud world: open=False WorldMapOk=False MapLinkIconOk=False
FightHit=True LootOk=True EquipOk=True
```

**Smoke** `--data /tmp/crystal-mmap-sample --input-script WorldMap,SearchMap:Bic,TeleportNpc`: **EXIT:0**

```
hud-lib MapLinkIcon file=MapLinkIcon.Lib ok=True src=/tmp/crystal-mmap-sample/MapLinkIcon.Lib
S.SearchMapResult q=Bic map=1 npc=0
hud world: WorldMapOk=True draws=64 MapLinkIconOk=True
hud search: SearchMapOk=True q=Bic map=1
hud teleport: TeleportOk=False can=0 (Bichon NPCs not CanTeleportTo)
FightHit=True LootOk=True EquipOk=True
```

**Case-fold** `maplinkicon.Lib`: **EXIT:0** — `src=/tmp/crystal-mmap-case/maplinkicon.Lib`.

## Title / Prguse2 chrome (later)

WinForms Title[820] / Prguse2[1360,1365,1366] via `--data`. Quad fallback. HUD probes without opening WorldMap.

**Hard-gate** `--connect --headless` (no `--data`, WorldMap closed): **EXIT:0**

```
hud-lib skip Prguse2.Lib: missing (no --data / catalog sprite)
hud chrome: TitleOk=True Prguse2Ok=False titleDraws=1 prg2Draws=0 titleSrc=catalog
FightHit=True LootOk=True EquipOk=True
```

Fixture catalog already lists Title (2 sprites). Prguse2 skips. WorldMap stayed closed.

**Smoke** `--data /tmp/crystal-mmap-sample` (WorldMap closed): **EXIT:0**

```
hud-lib Prguse2 file=Prguse2.Lib ok=True src=/tmp/crystal-mmap-sample/Prguse2.Lib
hud chrome: TitleOk=True Prguse2Ok=True titleDraws=1 prg2Draws=1
FightHit=True LootOk=True EquipOk=True
```

**Case-fold** `title.Lib` / `prguse2.Lib`: **EXIT:0** — src=`/tmp/crystal-mmap-case/title.Lib` / `prguse2.Lib`.

## Version hash (later)

Same MD5-of-file as WinForms. Server `--version-path` / Client `--version-file` (default `Crystal.Client.Linux.dll`). `--no-version-check` is opt-out. Do not vendor `Mir2.Exe`.

**Hard-gate** Server `--version-path …/Crystal.Client.Linux.dll` (no `--no-version-check`): **EXIT:0**

```
CheckVersion=True hashes=1
version: src=…/Crystal.Client.Linux.dll md5=be802de59906a44ee66f52d365ff91a9
handshake: ClientVersion Result=1 (match) VersionCheckOk=True
FightHit=True LootOk=True EquipOk=True
```

## Catalog 86 / deferred

86 catalog slots remain pack-missing (listed, not synthesized). WebView2 (WinForms Evergreen — no Linux runtime) stays permanently deferred. WIL item icons stay pack-missing. Title / Prguse2 / MapLinkIcon load only when the operator `--data` tree has those files. Quad fallback when frames are missing — no invented art. Do not vendor Sound packs; fixture wav is not game art. Version hash uses an operator file or this host's Linux client DLL. See `MIGRATION.md` residuals checklist.
