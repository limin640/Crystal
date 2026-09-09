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

No invented `MMap.Lib` art. Occupancy + blip from `MapReader` size / `MapCell` / packet objects.

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

## Catalog 86 / deferred

86 catalog slots remain pack-missing (listed, not synthesized). Audio (NAudio), WebView2, inventory drag-drop, and `MMap.Lib` tiles stay deferred and do not block. Quest accept/turn-in stays stubbed.
