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

## Catalog 86 / deferred

86 catalog slots remain pack-missing (listed, not synthesized). Audio (NAudio) and WebView2 stay deferred and do not block.
