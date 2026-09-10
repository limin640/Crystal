# Crystal graphics / library audit (Phase B)

See also `docs/linux-client/MIGRATION.md`, `docs/linux-client/BUILD.md`, `docs/linux-client/decisions.tsv`.

Inventory of the Windows SlimDX/D3D9 draw path and every WIL / library load path.
Scope is the **full Client corpus**, not a map slice.

## Runtime library load (Client)

Client never opens `.Wil` / `.Wzl` / `.Wtl` at runtime. All in-game art goes through `MLibrary` (`.Lib`, version 2/3, gzip BGRA).

| Path | Role |
| --- | --- |
| `Client/MirGraphics/MLibrary.cs` → `Libraries` | Constructs every `MLibrary` the client can draw |
| `Client/Settings.cs` | Data-tree roots (`Data\`, `Data\Monster\`, `Data\CArmour\`, map packs, …) |
| `Libraries` static ctor | Numbered folders sized from the last `NN.Lib` on disk; map libs 0–399 reserved |

### Catalog roots (`Settings.DataPath` + name + `.Lib`)

ChrSel, Prguse, Prguse2, Prguse3, UI_32bit, BuffIcon, Help, MMap, MapLinkIcon, Title, MagIcon, MagIcon2, Magic, Magic2, Magic3, Effect, MagicC, GuildSkill, Weather, Background, Dragon, Items, StateItem, DNItems, Items_Tooltip_32bit, Deco.

### Numbered folders (array length = last numeric file + 1)

CArmour, CHair, CWeapon, CWeaponEffect, CHumEffect, AArmour, AHair, AWeapon (` L`/` R`), AHumEffect, ARArmour, ARHair, ARWeapon (` S`), ARHumEffect, Monster, Gate, Flag, Siege, NPC, Mount, Fishing, Pet, Transform, TransformRide2, TransformEffect, TransformWeaponEffect.

### Map libs (`Libraries.MapLibs[400]`)

- 0–99 Wemade Mir2 (`Map/WemadeMir2/Tiles`, Smtiles, Objects, Objects2–27, Objects_32bit)
- 100–199 Shanda Mir2 (Tiles1–10, SmTiles1–10, Objects1–31, AniTiles1)
- 200–299 Wemade Mir3 (5 states × Tilesc…Object2c)
- 300–399 Shanda Mir3 (same names, suffix states)

`crystal-bake` encodes this catalog in `Crystal.Assets/CrystalCatalog.cs` and reports **present / expected**. Missing slots are listed; art is never synthesized.

## Source library formats (LibraryEditor → `.Lib`)

| Format | Loader | Index | Notes |
| --- | --- | --- | --- |
| `.Lib` v2/v3 | `MLibrary` / `MLibraryV2` | in-file | Client runtime |
| `.Lib` v0 | `MLibraryV0` | in-file | DXT1, editor convert only |
| `.Wil` + `.Wix` | `WeMadeLibrary` | Wix | Types 0/2/3/5 (classic / new Wemade / Mir3 / 32-bit) |
| `.Wzl` + `.Wzx` | `WeMadeLibrary` `_nType=1` | Wzx | zlib |
| `.Miz` + `.Mix` | `WeMadeLibrary` `_nType=4` | Mix | Shanda Mir3 |
| `.Wtl` | `WTLLibrary` | in-file | ILIB / DXT-style |

Bake parsers live in `Crystal.Assets/Parsers/` and are Linux-safe (no GDI+, no SlimDX).

## SlimDX / Direct3D9 call sites

| File | Usage |
| --- | --- |
| `Client/MirGraphics/DXManager.cs` | Device, Sprite, Line, surfaces, shaders, lights, `Draw` |
| `Client/MirGraphics/MLibrary.cs` | **Migrated this PR** to `IGpuTexture` / `IRenderer.DrawQuad` |
| `Client/Forms/CMain.cs` | **Migrated** to `Form` + `DXManager` frame facade; screenshot via adapter |
| `Client/MirControls/MirControl.cs` | **Migrated** control RTs + `DrawLine` borders |
| `Client/MirControls/MirLabel.cs` | **Migrated** GDI → `UpdateTexture` (no LockRectangle) |
| `Client/MirControls/MirTextBox.cs` | **Migrated** GDI → `UpdateTexture` |
| `Client/MirControls/MirScene.cs` | **Migrated** control RT |
| `Client/MirControls/MirGoodsCell.cs` | **Migrated** `PointF[]` borders |
| `Client/MirScenes/GameScene.cs` | **Migrated** floor/light RTs, light quads, multiply compose |
| `Client/MirScenes/Dialogs/MainDialogs.cs` | **Migrated** radar `IGpuTexture` draws |
| `Client/MirScenes/Dialogs/BigMapDialog.cs` | **Migrated** radar dots |
| `Client/MirScenes/Dialogs/QuestDialogs.cs` | unused SlimDX using removed |
| `Client/MirObjects/MapObject.cs` | **Migrated** poison dots |
| `Client/MirGraphics/ParticleEngine.cs` | **Migrated** to `System.Numerics.Vector2` |
| `Client/MirGraphics/Particles/*.cs` | **Migrated** to `System.Numerics.Vector2` |

Windows-only binaries: `Components/SlimDX.dll`, `Client.csproj` `net8.0-windows7.0`, NAudio, WebView2.

## What this PR migrated vs left

**Behind `IRenderer` now**

- `DXManager.Renderer` (SlimDX adapter on Windows; OpenGL/Null on Linux)
- All `MLibrary` / `MImage` texture create + sprite draws
- GameScene floor/light RTs, control/scene RTs, GDI labels, radar/poison, particles
- `CMain` present loop (`Form`, not `RenderForm`)

**Still SlimDX (Windows adapter only, not a content cut)**

- `Device` / `Sprite` / `Line` / D3D reset inside `DXManager` + `SlimDXRenderer`
- Pixel shaders (`normal.ps`, `grayscale.ps`, `magic.ps`)
- Screenshot backbuffer read (`DXManager.TrySaveScreenshot`)

Protocol, scenes, and `Libraries` catalog are unchanged. Linux does not require SlimDX.
