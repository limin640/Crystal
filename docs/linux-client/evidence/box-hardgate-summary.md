# Operator box evidence (text only)

No Data pack, Jev tree, or bake atlases. Branch: `cursor/linux-bake-renderer-bba8` @ `e9f3c9b`.

## Hard gate — Grok Bot Linux box

Date: 2026-09-09 ~16:08 CST. Publish `Server.Linux` + `Client.Linux` Release. External Jev at operator path (not in repo).

Client session **EXIT:0**, one run:

```
NewAccount Result=8 → LoginSuccess characters=0
NewCharacterSuccess name=LinuxWar class=Warrior
StartGame Result=4 → InMap=True BichonProvince
WalkAck=True loc=289,616
FightHit=True (ObjectStruck by self)
LootOk=True PickUp (HP)DrugSmall bag=4
EquipOk=True EquipItem Success slot=Weapon WoodenSword
```

Final block:

```
LoginSuccess=True NewCharacterOk=True StartGameResult=4 InMap=True
WalkAck=True FightHit=True LootOk=True EquipOk=True
```

Deferred: WinForms GameScene UI, Silk.NET input loop, audio, WebView2. Language rewrite deferred.

## Full-corpus bake — mirfiles pack A (~7.2G)

Flags: `--atlas-size 2048 --compress bc3`  
Duration: ~33m52s (16:10–16:44 CST)

| Metric | Value |
| --- | --- |
| Libraries parsed/discovered | 1440/1440 (100%) |
| Images decoded/listed | 1869869/2143132 (87.25%) |
| Images packed/decoded | 1869867/1869869 (~100%) |
| Catalog present/expected | 162/248 (65.32%) |
| Missing catalog slots | 86 (listed, not synthesized) |
| Parse failures | 0 |
| Images blank | 273263 |
| Atlases | 6158 (png+bc3) |

Coverage is against files that exist. Missing Crystal catalog slots are reported, not invented. `ImageDecode < 100%` includes blanks/undecodable listed frames — do not invent pixels. Bake-out stays off-git.
