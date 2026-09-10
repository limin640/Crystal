using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Client.MirControls;
using Client.MirGraphics.Rendering;
using Client.MirScenes;
using SlimDX;
using SlimDX.Direct3D9;
using Blend = SlimDX.Direct3D9.Blend;

namespace Client.MirGraphics
{
    class DXManager
    {
        public static List<MImage> TextureList = new List<MImage>();
        public static List<MirControl> ControlList = new List<MirControl>();

        public static Device Device;
        public static Sprite Sprite;
        public static Line Line;

        public static Crystal.Graphics.IGpuSurface CurrentSurface;
        public static Crystal.Graphics.IGpuSurface MainSurface;
        public static PresentParameters Parameters;
        public static bool DeviceLost;
        public static float Opacity = 1F;
        public static bool Blending;
        public static float BlendingRate;
        public static BlendMode BlendingMode;


        public static Crystal.Graphics.IGpuTexture RadarTexture;
        public static List<Crystal.Graphics.IGpuTexture> Lights = new List<Crystal.Graphics.IGpuTexture>();
        public static Crystal.Graphics.IGpuTexture PoisonDotBackground;

        public static Crystal.Graphics.IGpuTexture FloorTexture, LightTexture;
        public static Crystal.Graphics.IGpuSurface FloorSurface, LightSurface;

        public static PixelShader GrayScalePixelShader;
        public static PixelShader NormalPixelShader;
        public static PixelShader MagicPixelShader;

        public static bool GrayScale;

        /// <summary>Cross-platform draw backend. SlimDX on Windows; OpenGL/Null on Linux.</summary>
        public static Crystal.Graphics.IRenderer Renderer;

        public static Point[] LightSizes =
        {
            new Point(125,95),
            new Point(205,156),
            new Point(285,217),
            new Point(365,277),
            new Point(445,338),
            new Point(525,399),
            new Point(605,460),
            new Point(685,521),
            new Point(765,581),
            new Point(845,642),
            new Point(925,703)
        };

        public static void Create()
        {
            Parameters = new PresentParameters
            {
                BackBufferFormat = Format.X8R8G8B8,
                PresentFlags = PresentFlags.LockableBackBuffer,
                BackBufferWidth = Settings.ScreenWidth,
                BackBufferHeight = Settings.ScreenHeight,
                SwapEffect = SwapEffect.Discard,
                PresentationInterval = Settings.FPSCap ? PresentInterval.One : PresentInterval.Immediate,
                Windowed = !Settings.FullScreen,
            };


            Direct3D d3d = new Direct3D();

            Capabilities devCaps = d3d.GetDeviceCaps(0, DeviceType.Hardware);
            DeviceType devType = DeviceType.Reference;
            CreateFlags devFlags = CreateFlags.HardwareVertexProcessing;

            if (devCaps.VertexShaderVersion.Major >= 2 && devCaps.PixelShaderVersion.Major >= 2)
                devType = DeviceType.Hardware;

            if ((devCaps.DeviceCaps & DeviceCaps.HWTransformAndLight) != 0)
                devFlags = CreateFlags.HardwareVertexProcessing;


            if ((devCaps.DeviceCaps & DeviceCaps.PureDevice) != 0)
                devFlags |= CreateFlags.PureDevice;


            SlimDX.Configuration.EnableObjectTracking = true;

            Device = new Device(d3d, d3d.Adapters.DefaultAdapter.Adapter, devType, Program.Form.Handle, devFlags, Parameters);

            Device.SetDialogBoxMode(true);

            LoadDeviceObjects();
            BindRenderer();
            LoadManagedTextures();
            LoadPixelsShaders();
        }

        static void BindRenderer()
        {
            if (Renderer is SlimDXRenderer existing)
            {
                existing.Rebind(Device, Sprite, Settings.ScreenWidth, Settings.ScreenHeight);
                return;
            }

            Renderer = new SlimDXRenderer(Device, Sprite, Settings.ScreenWidth, Settings.ScreenHeight);
        }

        private static unsafe void LoadPixelsShaders()
        {
            var shaderNormalPath = Settings.ShadersPath + "normal.ps";
            var shaderGrayScalePath = Settings.ShadersPath + "grayscale.ps";
            var shaderMagicPath = Settings.ShadersPath + "magic.ps";

            if (System.IO.File.Exists(shaderNormalPath))
            {
                using (var gs = ShaderBytecode.AssembleFromFile(shaderNormalPath, ShaderFlags.None))
                    NormalPixelShader = new PixelShader(Device, gs);
            }
            if (System.IO.File.Exists(shaderGrayScalePath))
            {
                using (var gs = ShaderBytecode.AssembleFromFile(shaderGrayScalePath, ShaderFlags.None))
                    GrayScalePixelShader = new PixelShader(Device, gs);
            }
            if (System.IO.File.Exists(shaderMagicPath))
            {
                using (var gs = ShaderBytecode.AssembleFromFile(shaderMagicPath, ShaderFlags.None))
                    MagicPixelShader = new PixelShader(Device, gs);
            }
        }

        private static void LoadDeviceObjects()
        {
            Sprite = new Sprite(Device);
            Line = new Line(Device) { Width = 1F };
        }

        private static void LoadManagedTextures()
        {
            MainSurface = Renderer.MainSurface;
            CurrentSurface = MainSurface;
            Renderer.SetSurface(MainSurface);

            if (RadarTexture == null || RadarTexture.Disposed)
                RadarTexture = Renderer.CreateSolidTexture(2, 2, Color.White);
            if (PoisonDotBackground == null || PoisonDotBackground.Disposed)
                PoisonDotBackground = Renderer.CreateSolidTexture(5, 5, Color.White);
            CreateLights();
        }

        private static void CreateLights()
        {
            for (int i = Lights.Count - 1; i >= 0; i--)
                Lights[i].Dispose();

            Lights.Clear();

            for (int i = 1; i < LightSizes.Length; i++)
            {
                int width = LightSizes[i].X;
                int height = LightSizes[i].Y;
                byte[] bgra = RasterizeLight(width, height);
                Lights.Add(Renderer.CreateTexture(width, height, bgra));
            }
        }

        static byte[] RasterizeLight(int width, int height)
        {
            using var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(new Rectangle(0, 0, width, height));
                using PathGradientBrush brush = new PathGradientBrush(path);
                Color[] blendColours =
                {
                    Color.White,
                    Color.FromArgb(255, 210, 210, 210),
                    Color.FromArgb(255, 160, 160, 160),
                    Color.FromArgb(255, 70, 70, 70),
                    Color.FromArgb(255, 40, 40, 40),
                    Color.FromArgb(0, 0, 0, 0)
                };
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = blendColours,
                    Positions = new[] { 0f, .20f, .40f, .60f, .80f, 1.0f }
                };
                brush.SurroundColors = blendColours;
                brush.CenterColor = Color.White;
                graphics.Clear(Color.FromArgb(0, 0, 0, 0));
                graphics.FillPath(brush, path);
            }

            var data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bgra = new byte[width * height * 4];
                int srcStride = data.Stride;
                for (int y = 0; y < height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * srcStride, bgra, y * width * 4, width * 4);
                return bgra;
            }
            finally
            {
                image.UnlockBits(data);
            }
        }

        public static void SetSurface(Crystal.Graphics.IGpuSurface surface)
        {
            if (CurrentSurface == surface)
                return;

            Flush();
            CurrentSurface = surface;
            Renderer?.SetSurface(surface);
        }

        public static void Clear(Color color) => Renderer?.Clear(color);
        public static void Flush() => Renderer?.Flush();
        public static void BeginFrame() => Renderer?.BeginFrame(Settings.ScreenWidth, Settings.ScreenHeight);
        public static void EndFrame() => Renderer?.EndFrame();
        public static void Present() => Renderer?.Present();
        public static void SetMultiplyBlend() => Renderer?.SetMultiplyBlend();
        public static Crystal.Graphics.IGpuTexture CreateRenderTarget(int width, int height) => Renderer.CreateRenderTarget(width, height);
        public static Crystal.Graphics.IGpuTexture CreateManagedTexture(int width, int height, ReadOnlySpan<byte> bgra) => Renderer.CreateTexture(width, height, bgra);
        public static void UpdateTexture(Crystal.Graphics.IGpuTexture texture, ReadOnlySpan<byte> bgra) => Renderer.UpdateTexture(texture, bgra);

        public static byte[] CopyBitmapBgra(Bitmap image)
        {
            int width = image.Width, height = image.Height;
            var data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bgra = new byte[width * height * 4];
                int stride = data.Stride;
                for (int y = 0; y < height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * stride, bgra, y * width * 4, width * 4);
                return bgra;
            }
            finally
            {
                image.UnlockBits(data);
            }
        }
        public static void SetGrayscale(bool value)
        {
            GrayScale = value;

            if (value == true)
            {
                if (Device.PixelShader == GrayScalePixelShader) return;
                Sprite.Flush();
                Device.PixelShader = GrayScalePixelShader;
            }
            else
            {
                if (Device.PixelShader == null) return;
                Sprite.Flush();
                Device.PixelShader = null;
            }
        }

        public static void Draw(Crystal.Graphics.IGpuTexture texture, Rectangle? sourceRect, float x, float y, Color color)
        {
            int w = sourceRect?.Width ?? texture.Width;
            int h = sourceRect?.Height ?? texture.Height;
            Draw(texture, sourceRect, x, y, w, h, color, 1f);
        }

        public static void DrawOpaque(Crystal.Graphics.IGpuTexture texture, Rectangle? sourceRect, float x, float y, Color color, float opacity)
        {
            int w = sourceRect?.Width ?? texture.Width;
            int h = sourceRect?.Height ?? texture.Height;
            Draw(texture, sourceRect, x, y, w, h, color, opacity);
        }

        public static void Draw(Crystal.Graphics.IGpuTexture texture, Rectangle? sourceRect, float x, float y, float w, float h, Color color, float opacity)
        {
            if (texture == null || texture.IsDisposed || Renderer == null)
                return;

            Renderer.DrawQuad(texture, sourceRect, x, y, w, h, color, opacity);
            CMain.DPSCounter++;
        }

        /// <summary>
        /// Windows-only backbuffer capture. SlimDX stays inside the adapter; CMain does not import D3D types.
        /// </summary>
        public static bool TrySaveScreenshot(string path, Action<Bitmap> overlay = null)
        {
            if (Device == null)
                return false;

            Surface backbuffer = Device.GetBackBuffer(0, 0);
            using var stream = Surface.ToStream(backbuffer, ImageFileFormat.Png);
            using var image = new Bitmap(stream);
            overlay?.Invoke(image);
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            image.Save(path, ImageFormat.Png);
            return true;
        }

        public static void AttemptReset()
        {
            try
            {
                Result result = DXManager.Device.TestCooperativeLevel();

                if (result.Code == ResultCode.DeviceLost.Code) return;

                if (result.Code == ResultCode.DeviceNotReset.Code)
                {
                    DXManager.ResetDevice();
                    return;
                }

                if (result.Code != ResultCode.Success.Code) return;

                DXManager.DeviceLost = false;
            }
            catch
            {
            }
        }

        public static void ResetDevice()
        {
            DXManager.CleanUp();
            DXManager.DeviceLost = true;

            if (DXManager.Parameters == null) return;

            Size clientSize = Program.Form.ClientSize;

            if (clientSize.Width == 0 || clientSize.Height == 0) return;

            DXManager.Parameters.Windowed = !Settings.FullScreen;
            DXManager.Parameters.BackBufferWidth = clientSize.Width;
            DXManager.Parameters.BackBufferHeight = clientSize.Height;
            DXManager.Parameters.PresentationInterval = Settings.FPSCap ? PresentInterval.Default : PresentInterval.Immediate;
            DXManager.Device.Reset(DXManager.Parameters);

            DXManager.LoadDeviceObjects();
            DXManager.BindRenderer();
            DXManager.LoadManagedTextures();
        }

        public static void AttemptRecovery()
        {
            try
            {
                Sprite.End();
            }
            catch
            {
            }

            try
            {
                Device.EndScene();
            }
            catch
            {
            }

            try
            {
                MainSurface = Device.GetBackBuffer(0, 0);
                CurrentSurface = MainSurface;
                Device.SetRenderTarget(0, MainSurface);
            }
            catch
            {
            }
        }
        public static void SetOpacity(float opacity)
        {
            if (Opacity == opacity)
                return;

            Sprite.Flush();
            Device.SetRenderState(RenderState.AlphaBlendEnable, true);
            if (opacity >= 1 || opacity < 0)
            {
                Device.SetRenderState(RenderState.SourceBlend, SlimDX.Direct3D9.Blend.SourceAlpha);
                Device.SetRenderState(RenderState.DestinationBlend, SlimDX.Direct3D9.Blend.InverseSourceAlpha);
                Device.SetRenderState(RenderState.SourceBlendAlpha, Blend.One);
                Device.SetRenderState(RenderState.BlendFactor, Color.FromArgb(255, 255, 255, 255).ToArgb());
            }
            else
            {
                Device.SetRenderState(RenderState.SourceBlend, Blend.BlendFactor);
                Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseBlendFactor);
                Device.SetRenderState(RenderState.SourceBlendAlpha, Blend.SourceAlpha);
                Device.SetRenderState(RenderState.BlendFactor, Color.FromArgb((byte)(255 * opacity), (byte)(255 * opacity), (byte)(255 * opacity), (byte)(255 * opacity)).ToArgb());
            }
            Opacity = opacity;
            Sprite.Flush();
        }
        public static void SetBlend(bool value, float rate = 1F, BlendMode mode = BlendMode.NORMAL)
        {
            if (value == Blending && BlendingRate == rate && BlendingMode == mode) return;

            Blending = value;
            BlendingRate = rate;
            BlendingMode = mode;

            Sprite.Flush();

            Sprite.End();

            if (Blending)
            {
                Sprite.Begin(SpriteFlags.DoNotSaveState);
                Device.SetRenderState(RenderState.AlphaBlendEnable, true);

                switch (BlendingMode)
                {
                    case BlendMode.INVLIGHT:
                        Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);
                        Device.SetRenderState(RenderState.SourceBlend, Blend.BlendFactor);
                        Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceColor);
                        break;
                    default:
                        Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
                        Device.SetRenderState(RenderState.DestinationBlend, Blend.One);
                        break;
                }

                Device.SetRenderState(RenderState.BlendFactor, Color.FromArgb((byte)(255 * BlendingRate), (byte)(255 * BlendingRate),
                                                                (byte)(255 * BlendingRate), (byte)(255 * BlendingRate)).ToArgb());
            }
            else
                Sprite.Begin(SpriteFlags.AlphaBlend);

            Device.SetRenderTarget(0, CurrentSurface);
        }

        public static void SetNormal(float blend, Color tintcolor)
        {
            if (Device.PixelShader == NormalPixelShader)
                return;

            Sprite.Flush();
            Device.PixelShader = NormalPixelShader;
            Device.SetPixelShaderConstant(0, new Vector4[] { new Vector4(1.0F, 1.0F, 1.0F, blend) });
            Device.SetPixelShaderConstant(1, new Vector4[] { new Vector4(tintcolor.R / 255, tintcolor.G / 255, tintcolor.B / 255, 1.0F) });
            Sprite.Flush();
        }

        public static void SetGrayscale(float blend, Color tintcolor)
        {
            if (Device.PixelShader == GrayScalePixelShader)
                return;

            Sprite.Flush();
            Device.PixelShader = GrayScalePixelShader;
            Device.SetPixelShaderConstant(0, new Vector4[] { new Vector4(1.0F, 1.0F, 1.0F, blend) });
            Device.SetPixelShaderConstant(1, new Vector4[] { new Vector4(tintcolor.R / 255, tintcolor.G / 255, tintcolor.B / 255, 1.0F) });
            Sprite.Flush();
        }

        public static void SetBlendMagic(float blend, Color tintcolor)
        {
            if (Device.PixelShader == MagicPixelShader || MagicPixelShader == null)
                return;

            Sprite.Flush();
            Device.PixelShader = MagicPixelShader;
            Device.SetPixelShaderConstant(0, new Vector4[] { new Vector4(1.0F, 1.0F, 1.0F, blend) });
            Device.SetPixelShaderConstant(1, new Vector4[] { new Vector4(tintcolor.R / 255, tintcolor.G / 255, tintcolor.B / 255, 1.0F) });
            Sprite.Flush();
        }

        public static void Clean()
        {
            for (int i = TextureList.Count - 1; i >= 0; i--)
            {
                MImage m = TextureList[i];

                if (m == null)
                {
                    TextureList.RemoveAt(i);
                    continue;
                }

                if (CMain.Time <= m.CleanTime) continue;

                m.DisposeTexture();
            }

            for (int i = ControlList.Count - 1; i >= 0; i--)
            {
                MirControl c = ControlList[i];

                if (c == null)
                {
                    ControlList.RemoveAt(i);
                    continue;
                }

                if (CMain.Time <= c.CleanTime) continue;

                c.DisposeTexture();
            }
        }


        private static void CleanUp()
        {
            if (Sprite != null)
            {
                if (!Sprite.Disposed)
                {
                    Sprite.Dispose();
                }

                Sprite = null;
            }

            if (Line != null)
            {
                if (!Line.Disposed)
                {
                    Line.Dispose();
                }

                Line = null;
            }

            if (CurrentSurface != null)
            {
                if (!CurrentSurface.Disposed)
                {
                    CurrentSurface.Dispose();
                }

                CurrentSurface = null;
            }

            if (PoisonDotBackground != null)
            {
                if (!PoisonDotBackground.Disposed)
                {
                    PoisonDotBackground.Dispose();
                }

                PoisonDotBackground = null;
            }

            if (RadarTexture != null)
            {
                if (!RadarTexture.Disposed)
                {
                    RadarTexture.Dispose();
                }

                RadarTexture = null;
            }

            if (FloorTexture != null)
            {
                if (!FloorTexture.Disposed)
                {
                    FloorTexture.Dispose();
                }

                DXManager.FloorTexture = null;

                if (DXManager.FloorSurface != null && !DXManager.FloorSurface.Disposed)
                {
                    DXManager.FloorSurface.Dispose();
                }

                DXManager.FloorSurface = null;
            }

            if (LightTexture != null)
            {
                if (!LightTexture.Disposed)
                    LightTexture.Dispose();

                DXManager.LightTexture = null;

                if (DXManager.LightSurface != null && !DXManager.LightSurface.Disposed)
                {
                    DXManager.LightSurface.Dispose();
                }

                DXManager.LightSurface = null;
            }

            if (Lights != null)
            {
                for (int i = 0; i < Lights.Count; i++)
                {
                    if (!Lights[i].Disposed)
                        Lights[i].Dispose();
                }
                Lights.Clear();
            }

            for (int i = TextureList.Count - 1; i >= 0; i--)
            {
                MImage m = TextureList[i];

                if (m == null) continue;

                m.DisposeTexture();
            }
            TextureList.Clear();


            for (int i = ControlList.Count - 1; i >= 0; i--)
            {
                MirControl c = ControlList[i];

                if (c == null) continue;

                c.DisposeTexture();
            }
            ControlList.Clear();
        }

        public static void Dispose()
        {
            CleanUp();

            Device?.Direct3D?.Dispose();
            Device?.Dispose();

            NormalPixelShader?.Dispose();
            GrayScalePixelShader?.Dispose();
            MagicPixelShader?.Dispose();

            Renderer?.Dispose();
            Renderer = null;
        }
    }
}
