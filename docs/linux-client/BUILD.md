# Build and run (Phase B+)

Requires .NET SDK 8.x (`global.json` pins 8.0.425 with latestFeature roll-forward).

```bash
# optional if the SDK is not on PATH
export PATH="$HOME/.dotnet:$PATH"
```

## Linux (this environment)

These projects are the Linux-capable path:

```bash
dotnet build Crystal.Assets/Crystal.Assets.csproj -c Release
dotnet build Crystal.Graphics/Crystal.Graphics.csproj -c Release
dotnet build Tools/Crystal.Bake/Crystal.Bake.csproj -c Release
dotnet build Server/Server.Library.csproj -c Release
dotnet build Server.Linux/Server.Linux.csproj -c Release
dotnet build Client.Linux/Client.Linux.csproj -c Release
```

`Server.MirForms` (`net8.0-windows7.0` WinExe) does **not** build on Linux. Use `Server.Linux`.

### Bake + coverage against the sample Data tree

The sample tree is **synthetic fixture bytes**, not ripped game art.

```bash
dotnet run --project Tools/Crystal.Bake/Crystal.Bake.csproj -c Release -- \
  init-sample Tools/Crystal.Bake/fixtures/Data

dotnet run --project Tools/Crystal.Bake/Crystal.Bake.csproj -c Release -- \
  bake --data Tools/Crystal.Bake/fixtures/Data --out Tools/Crystal.Bake/fixtures/bake-out --atlas-size 256
```

Coverage is printed and written to `Tools/Crystal.Bake/fixtures/bake-out/bake-coverage.json`
(the filename must stay `bake-coverage.json`; `.gitignore` ignores `coverage*.json`).

Primary levers: **images decoded / images listed (%)** and **libraries parsed / libraries discovered (%)**.
`MissingCatalogSlots` lists every expected Crystal catalog path that is absent.

Bake is streaming: each library is parsed, pixels are blitted onto the current atlas sheet, then discarded. Finished sheets are flushed to PNG+BC3 before the next library. That is the resilience path for a real ~7GB+ Data tree.

### External operator Data pack (do not vendor into git)

The operator downloads mirfiles crystal/patch `Data` to a box path. Point `--data` at that folder. Do **not** copy the pack into this repository.

```bash
dotnet run --project Tools/Crystal.Bake/Crystal.Bake.csproj -c Release -- \
  bake --data /path/to/Crystal/Data --out /path/to/bake-out \
  --atlas-size 2048 --compress bc3
```

Replace `/path/to/Crystal/Data` with the downloaded pack root (the directory that contains `Prguse.Lib`, `Map/`, `Monster/`, …).

### Synthetic stress tree (no copyrighted binaries)

Writes every `CrystalCatalog` slot plus a few numbered-folder files as 4×4 `.Lib` files. Use a throwaway directory:

```bash
dotnet run --project Tools/Crystal.Bake/Crystal.Bake.csproj -c Release -- \
  init-stress /tmp/crystal-bake-stress/Data

dotnet run --project Tools/Crystal.Bake/Crystal.Bake.csproj -c Release -- \
  bake --data /tmp/crystal-bake-stress/Data --out /tmp/crystal-bake-stress/out --atlas-size 256
```

### Linux client host

```bash
# no display / CI — uploads catalog atlases into NullRenderer and draws every sprite
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- --headless \
  --catalog Tools/Crystal.Bake/fixtures/bake-out/catalog.json

# machine with a GL display — Silk.NET window, batched atlas quads, optional auto-exit
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --catalog Tools/Crystal.Bake/fixtures/bake-out/catalog.json --frames 3
```

`--headless` initializes `NullRenderer`, uploads atlas PNGs, issues `DrawQuad` for each catalog sprite, and exits 0. That is compile + launch + catalog-draw evidence, **not** login→equip parity.

`--frames N` closes a windowed session after N presents (useful on a box with a display). Windowed OpenGL needs a working display **and** GLFW (`sudo apt-get install libglfw3 libgl1` on Debian/Ubuntu). Without a usable window platform the process exits 4 and tells you to use `--headless`. CI should stay on `--headless`.

### Linux server — full Jev world (Phase D)

Do **not** vendor Crystal.Database / maps / `Server.MirDB` into git. The operator keeps a Jev tree outside the repo:

```bash
git clone --depth 1 https://github.com/Suprcode/Crystal.Database.git /path/to/Crystal.Database
# layout: /path/to/Crystal.Database/Jev/{Configs,Envir,Maps,Server.MirDB}
```

When `Server.MirDB` exists **and** `Maps/*.map` files exist, Server.Linux starts the full Envir (start points, maps). **Do not pass `--listen-without-world`.** That flag is handshake-only and is ignored if a full world is present.

```bash
# EXACT flags for StartGame / in-map (full external Jev root)
dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
  --root /path/to/Crystal.Database/Jev \
  --no-version-check --allow-start-game --seconds 90
```

| Flag | Effect |
| --- | --- |
| `--root <Jev>` | `chdir` to external `Configs/Envir/Maps/Server.MirDB`. Never a repo path. Env: `CRYSTAL_SERVER_ROOT`. Default if omitted: `/tmp/crystal-server-root` (throwaway). |
| `--no-version-check` | `Settings.CheckVersion=false` (Linux has no `Mir2.Exe` hash). Stock Jev `Setup.ini` already has `CheckVersion=False`. |
| `--allow-start-game` | `Settings.AllowStartGame=true`. Without this (and without `AdminAccount`), `S.StartGame.Result=0`. Stock Jev `Setup.ini` already has `AllowStartGame=True`; pass the flag anyway so a custom root cannot silently disable StartGame. |
| `--test-server` | `Settings.TestServer=true`. Enables existing `@LEVEL` / `@MOB` / `@MAKE` / `@MOVE` so Client.Linux can script fight/loot/equip. |
| `--listen-without-world` | Bind 7000 when maps/DB fail `CanStartEnvir` (login handshake only). **Ignored** when `Server.MirDB` + `*.map` exist. |
| `--no-db-checks` | `Settings.EnforceDBChecks=false` |
| `--seconds N` | Run then exit (CI). Full world load can take a minute before port 7000 is bound. |
| `--bind` / `--port` | Listen address (default `127.0.0.1:7000`) |

```bash
# handshake-only (empty / incomplete root — Phase C)
dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
  --root /tmp/crystal-server-root \
  --no-version-check --listen-without-world --seconds 20

# bind-only (no Envir) to prove the port:
dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
  --bind-probe --port 7000 --seconds 3
```

### Linux client — select → StartGame → in-map draw

```bash
# NewAccount/Login → NewCharacter (if empty) → StartGame → MapInformation
# then draw floor/objects from bake catalog (or --data .Lib) via IRenderer
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --connect --ini Client.Linux/Mir2Test.ini --headless \
  --catalog Tools/Crystal.Bake/fixtures/bake-out/catalog.json \
  --maps /path/to/Crystal.Database/Jev/Maps

# login handshake only (Phase C):
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --connect --login-only --ini Client.Linux/Mir2Test.ini --headless
```

| Flag | Effect |
| --- | --- |
| `--connect` | Shared `Packet` session (Connected → version → account → select → StartGame) |
| `--login-only` | Stop after `LoginSuccess` (no NewCharacter / StartGame) |
| `--maps <dir>` | External Jev `Maps/` for `.map` load (`CRYSTAL_MAPS`). Not vendored. |
| `--data <dir>` | Optional client Data tree for runtime `.Lib` via `MLibParser` (`CRYSTAL_DATA`) |
| `--catalog` | Bake atlas catalog (fixture or operator bake-out) |
| `--character` | Name for `C.NewCharacter` if the account has no chars (default `LinuxWar`) |
| `--no-walk` | Do not send the scripted `C.Walk` after enter |
| `--no-gate` | Stop after walk (skip scripted Attack / PickUp / EquipItem) |
| `--input-script` | After StartGame, inject Crystal keys (`Right,Talk,Buy:0,Sell,Chat:hello,Attack`). `Talk` → `C.CallNPC` `[@Main]` then `[@BUYSELL]`; `Buy`/`Buy:N` → `C.BuyItem` from `S.NPCGoods`; `Sell`/`Sell:N` → `C.SellItem` |
| `--input-step-ms` | Delay between injected commands (default 400) |
| `--window` | Silk.NET OpenGL + keyboard/mouse after `--connect` (inventory/equip HUD on IRenderer) |
| `--enter-wait-ms` | How long to wait for `MapInformation` / `UserInformation` |

`Client.Linux/Mir2Test.ini` is the Mir2Test.ini-style IP/port/account file.

This is StartGame / in-map evidence, **not** fight/loot/equip parity. Login handshake does not need baked WIL art; floor draw uses the catalog or an existing `.Lib`.

## Windows Client (unchanged TFM)

```bat
dotnet build Client\Client.csproj -c Release
```

Needs Windows + SlimDX (`Components\SlimDX.dll`). Will **not** compile on Linux (`net8.0-windows7.0`, WinForms, SlimDX). SlimDX remains the Windows `IRenderer` backend. GameScene floor/light RTs, control textures, CMain present, and particles go through `IRenderer` / `System.Numerics.Vector2`.

## Solution

```bash
# Linux: build only the portable projects (the .sln also contains WinExe projects)
dotnet build Crystal.Assets/Crystal.Assets.csproj Crystal.Graphics/Crystal.Graphics.csproj \
  Tools/Crystal.Bake/Crystal.Bake.csproj Server/Server.Library.csproj \
  Server.Linux/Server.Linux.csproj Client.Linux/Client.Linux.csproj
```
