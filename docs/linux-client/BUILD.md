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

### Linux server + protocol attempt

Do **not** vendor Crystal.Database into git. Clone or copy the Jev tree to a box path:

```bash
git clone --depth 1 https://github.com/Suprcode/Crystal.Database.git /path/to/Crystal.Database
# layout: /path/to/Crystal.Database/Jev/{Configs,Envir,Maps,Server.MirDB}
```

```bash
# listen on 7000 even if maps/DB checks are incomplete (handshake + login)
dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
  --root /path/to/Crystal.Database/Jev \
  --no-version-check --listen-without-world --seconds 30

# or, bind-only (no Envir) to prove the port:
dotnet run --project Server.Linux/Server.Linux.csproj -c Release -- \
  --bind-probe --port 7000 --seconds 3

# attempt Connected → ClientVersion → NewAccount → Login
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- \
  --connect --ini Client.Linux/Mir2Test.ini --headless
```

`Client.Linux/Mir2Test.ini` is the Mir2Test.ini-style IP/port/account file. `--no-version-check` is required because Linux has no `Mir2.Exe` hash.

A complete Jev `Maps/` + `Server.MirDB` is required for StartGame / walk. Login handshake does not need baked WIL art.

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
