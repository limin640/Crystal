# Linux client migration notes (Phase B)

## Sequencing toward the hard done gate

Hard gate (later): Linux client login → select → walk → fight → loot → equip against a Crystal-compatible server, with build + launch evidence.

This PR lands the **levers**, not that gate.

```
Data tree (WIL/WZL/WTL/Lib)
        │
        ▼
 crystal-bake ──► atlases + BC3 + bake-coverage.json  (100% of files that exist)
        │
        ▼
 IRenderer ──┬── SlimDXRenderer (Windows Client, live)
             └── OpenGLRenderer / NullRenderer (Client.Linux)
        │
        ▼
 later: scenes + CMain loop + RTs + input + network on Linux host
```

## Design choices

1. **Do not shrink the corpus.** Bake enumerates the whole Data tree. Coverage is `parsed / total` of files present. Crystal catalog slots that are absent are listed, not filled with placeholder art.
2. **Smallest unlock, not a rewrite.** `MLibrary` is the hottest draw path and is now backend-agnostic. GameScene RTs still compile against SlimDX so Windows Client stays buildable.
3. **Language rewrite waits.** Shared packets, `Client.MirNetwork`, server, and scene logic stay C# Crystal.
4. **Linux-capable Client path** is `Client.Linux` (`net8.0`). The WinForms `Client.csproj` remains `net8.0-windows7.0` + SlimDX; that target cannot compile on Linux and is not claimed as the Linux build.

## Renderer API

`Crystal.Graphics.IRenderer`:

- `CreateTexture` / `CreateRenderTarget` / `CreateSolidTexture`
- `DrawQuad` (batched on OpenGL)
- `SetBlend` / `SetOpacity` / `SetGrayscale` / `SetSurface`
- `BeginFrame` / `EndFrame` / `Flush` / `Present`

Windows: `Client/MirGraphics/Rendering/SlimDXRenderer.cs` wraps the existing Device/Sprite.

Linux: `Crystal.Graphics.Backends.OpenGLRenderer` (Silk.NET.OpenGL). Headless CI uses `NullRenderer`.

## Bake outputs

```
bake-out/
  catalog.json
  bake-coverage.json
  atlases/atlas_0000.png
  atlases/atlas_0000.bc3
```

`catalog.json` sprite keys are `{relative library}:{index}` with UV rectangles and WIL-style offsets.

## Next increments (not this PR)

1. Upload atlas sheets on `IRenderer` and draw `MLibrary` from catalog when present (fallback to `.Lib`).
2. Move control / floor / light RTs onto `CreateRenderTarget`.
3. Replace `CMain : RenderForm` with a Silk.NET window + input map.
4. Linux audio (NAudio is Windows) and drop WebView2 patcher browser.
5. End-to-end login…equip vs Crystal server.
