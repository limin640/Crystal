# Linux client migration notes

## Sequencing toward the hard done gate

Hard gate (later): Linux client login → select → walk → fight → loot → equip against a Crystal-compatible server, with build + launch evidence.

This PR lands the **levers**, not that gate.

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
 Client.Linux: catalog DrawQuad + Shared protocol connect/login/select/StartGame
 Server.Linux: full Jev --root (Maps + Server.MirDB) without --listen-without-world
 later: walk input + fight → loot → equip
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
| Fight / loot / equip | **Not claimed** |
| Hard gate login→select→walk→fight→loot→equip | **Not claimed** |

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

That is NewCharacter → StartGame → in-map (+ one walk ack). It is **not** fight/loot/equip and **not** the hard gate.

## Still blocking fight / loot / equip (and a real walk loop)

1. **Input** — one scripted `C.Walk` is evidence, not a Silk.NET keymap or GameScene movement loop.
2. **Combat / loot / equip packets** — `C.Attack`, pickup, inventory/equip are not sent or drawn as GameScene dialogs.
3. **WinForms GameScene** — still the Windows client. Client.Linux is a packet + `MapView` equivalent, not a language rewrite of the scene graph.
4. **Operator art** — floor/objects draw from bake catalog or a real `--data` `.Lib` tree. Missing catalog slots stay missing (low-fi remap of existing texels only). Do not invent WIL art.
5. **Audio / WebView2** — Windows-only, later.
6. **Version hash** — Linux server still needs `--no-version-check` unless a real `Mir2.Exe` hash list is supplied.

Hard gate remains: login→select→walk→fight→loot→equip on Linux vs Crystal server.
