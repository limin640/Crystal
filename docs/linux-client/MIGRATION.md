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
 Client.Linux: clear + batched atlas quads from catalog
 later: scenes + input + network on Linux host
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

## Next increments (not this PR)

1. Drive `MLibrary` from a bake catalog on Linux when present (fallback to `.Lib`).
2. Silk.NET input map + fold scenes into `Client.Linux`.
3. Linux audio (NAudio is Windows) and drop WebView2 patcher browser.
4. End-to-end login…equip vs Crystal server.
