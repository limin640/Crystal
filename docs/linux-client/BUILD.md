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
dotnet build Crystal.Audio/Crystal.Audio.csproj -c Release
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
  --version-path /path/to/Client.Linux/bin/Release/net8.0/Crystal.Client.Linux.dll \
  --allow-start-game --seconds 90
```

| Flag | Effect |
| --- | --- |
| `--root <Jev>` | `chdir` to external `Configs/Envir/Maps/Server.MirDB`. Never a repo path. Env: `CRYSTAL_SERVER_ROOT`. Default if omitted: `/tmp/crystal-server-root` (throwaway). |
| `--version-path <files>` | `Settings.VersionPath` (comma-separated). MD5 each existing file, or parse `.md5` / `.hashes` hex lists. Env: `CRYSTAL_VERSION_PATH`. When at least one hash loads, `CheckVersion=true` unless `--no-version-check`. Do not vendor `Mir2.Exe`. |
| `--version-hashes <hex,>` | Extra 32-char MD5 hex entries. Env: `CRYSTAL_VERSION_HASHES`. |
| `--check-version` | Force `Settings.CheckVersion=true` (stock Jev `Setup.ini` has `False`). |
| `--no-version-check` | Opt-out: `Settings.CheckVersion=false`. Not required when client and server hash the same file. |
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
  --listen-without-world --seconds 20

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
| `--data <dir>` | Optional client Data tree (`CRYSTAL_DATA`). `MapView` floor `.Lib` plus `MMap.Lib` / `MagIcon.Lib` HUD tiles via `MLibParser`. Missing files skip. Do not vendor the pack. Operator: the folder that contains `MMap.Lib` (same as WinForms `Settings.DataPath`). Smoke: `crystal-bake init-sample /tmp/crystal-mmap-sample` |
| `--catalog` | Bake atlas catalog (fixture or operator bake-out) |
| `--character` | Name for `C.NewCharacter` if the account has no chars (default `LinuxWar`) |
| `--no-walk` | Do not send the scripted `C.Walk` after enter |
| `--no-gate` | Stop after walk (skip scripted Attack / PickUp / EquipItem) |
| `--input-script` | After StartGame, inject Crystal keys. `QuestAccept` / `QuestAccept:id` → `C.AcceptQuest`; `QuestFinish` / `QuestFinish:id` → `C.FinishQuest` (same packets as `QuestListDialog`). `Drag` / `Drag:0,8` → `C.MoveItem`; `Merge:0,1` → `C.MergeItem`. Map teleport stays `Move:x:y` (`@MOVE`). |
| `--auto-trade-reply` | On `S.TradeRequest`, send `C.TradeReply` `AcceptInvite=true` |
| `--auto-trade-confirm` | On `S.TradeGold` / `S.TradeItem`, send `C.TradeConfirm` `Locked=true` |
| `--keep-alive <ms>` | Pump after the input script so a second client can finish the trade |
| `--play-sound` | Play one WAV through Silk.NET OpenAL (even when `--headless`). Hard-gate omits this and uses Null |
| `--sound <path>` | Operator Sound file or directory (`CRYSTAL_SOUND`). Not vendored. Fixture: `Tools/Crystal.Audio/fixtures/tone.wav` |
| `--input-step-ms` | Delay between injected commands (default 400) |
| `--window` | Silk.NET OpenGL + keyboard/mouse after `--connect` (inventory/equip HUD on IRenderer) |
| `--enter-wait-ms` | How long to wait for `MapInformation` / `UserInformation` |
| `--version-file <path>` | File to MD5 for `C.ClientVersion` (`CRYSTAL_VERSION_FILE`). Same as WinForms hashing `Application.ExecutablePath`. Default: `Crystal.Client.Linux.dll`. Operator `Mir2.Exe` is hashed, not executed, and is not vendored. |
| `--version-hash <hex>` | Send this 32-char MD5 instead of hashing a file (`CRYSTAL_VERSION_HASH`). Matches server `--version-hashes` / `.md5` lists. |

`Client.Linux/Mir2Test.ini` is the Mir2Test.ini-style IP/port/account file.

This is StartGame / in-map evidence, **not** fight/loot/equip parity. Login handshake does not need baked WIL art; floor draw uses the catalog or an existing `.Lib`.

## Windows Client (unchanged TFM)

```bat
dotnet build Client\Client.csproj -c Release
```

Needs Windows + SlimDX (`Components\SlimDX.dll`). Will **not** compile on Linux (`net8.0-windows7.0`, WinForms, SlimDX, WebView2) — this VM reports `NETSDK1100` without a Windows targeting pack. SlimDX remains the Windows `IRenderer` backend. `SoundManager` calls `Crystal.Audio.IAudio` (`NAudioAudio`); NAudio is referenced from `Crystal.Audio`, not from `Client.csproj`. GameScene floor/light RTs, control textures, CMain present, and particles go through `IRenderer` / `System.Numerics.Vector2`.

## Solution

```bash
# Linux: build only the portable projects (the .sln also contains WinExe projects)
dotnet build Crystal.Assets/Crystal.Assets.csproj Crystal.Graphics/Crystal.Graphics.csproj \
  Crystal.Audio/Crystal.Audio.csproj \
  Tools/Crystal.Bake/Crystal.Bake.csproj Server/Server.Library.csproj \
  Server.Linux/Server.Linux.csproj Client.Linux/Client.Linux.csproj
```
