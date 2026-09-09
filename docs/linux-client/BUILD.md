# Build and run (Phase B)

Requires .NET SDK 8.x (`global.json` pins 8.0.425 with latestFeature roll-forward).

```bash
# optional if the SDK is not on PATH
export PATH="$HOME/.dotnet:$PATH"
```

## Linux (this environment)

These three projects are the Linux-capable path:

```bash
dotnet build Crystal.Assets/Crystal.Assets.csproj -c Release
dotnet build Crystal.Graphics/Crystal.Graphics.csproj -c Release
dotnet build Tools/Crystal.Bake/Crystal.Bake.csproj -c Release
dotnet build Client.Linux/Client.Linux.csproj -c Release
```

### Bake + coverage against the sample Data tree

The sample tree is **synthetic fixture bytes**, not ripped game art. Point `--data` at a real client `Data/` folder to measure a full corpus.

```bash
dotnet run --project Tools/Crystal.Bake/Crystal.Bake.csproj -c Release -- \
  init-sample Tools/Crystal.Bake/fixtures/Data

dotnet run --project Tools/Crystal.Bake/Crystal.Bake.csproj -c Release -- \
  bake --data Tools/Crystal.Bake/fixtures/Data --out Tools/Crystal.Bake/fixtures/bake-out --atlas-size 256
```

Coverage is printed and written to `Tools/Crystal.Bake/fixtures/bake-out/bake-coverage.json`.

Primary lever: **images decoded / images listed (%)** and **libraries parsed / libraries discovered (%)**.

### Linux client host

```bash
# no display / CI
dotnet run --project Client.Linux/Client.Linux.csproj -c Release -- --headless \
  --catalog Tools/Crystal.Bake/fixtures/bake-out/catalog.json

# machine with a GL display
dotnet run --project Client.Linux/Client.Linux.csproj -c Release
```

`--headless` initializes `NullRenderer`, issues draw calls, and exits 0. That is compile + launch evidence for this phase, **not** login→equip parity.

## Windows Client (unchanged TFM)

```bat
dotnet build Client\Client.csproj -c Release
```

Needs Windows + SlimDX (`Components\SlimDX.dll`). Will **not** compile on Linux (`net8.0-windows7.0`, WinForms, SlimDX).

## Solution

```bash
# Linux: build only the portable projects (the .sln also contains WinExe projects)
dotnet build Crystal.Assets/Crystal.Assets.csproj Crystal.Graphics/Crystal.Graphics.csproj \
  Tools/Crystal.Bake/Crystal.Bake.csproj Client.Linux/Client.Linux.csproj
```
