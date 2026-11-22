// Superhero screensaver for Windows.
// The hero from the game flies over Big City at night: stars, the Moon, searchlights,
// a plane going by, windows lighting up, cars on the street, and now and then a loop-the-loop.
//
// Build it with build.ps1 (it makes Superhero.scr). Windows runs a screensaver with:
//   /s          full screen (on every monitor)
//   /p <hwnd>   the little preview in Screen Saver Settings
//   /c          settings (there aren't any yet)
// Extras for trying it out:
//   /w                     in a normal window
//   /x <file.png> <w> <h> <seconds>   save a picture of the scene after that many seconds

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Superhero")]
[assembly: System.Reflection.AssemblyProduct("Superhero screensaver")]

namespace SuperheroSaver
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string mode = "/c";
            string arg = null;
            if (args.Length > 0)
            {
                string a = args[0].Trim().ToLowerInvariant();
                if (a.Length >= 2) mode = a.Substring(0, 2);
                if (a.Length > 3 && a[2] == ':') arg = a.Substring(3);
                else if (args.Length > 1) arg = args[1];
            }

            if (mode == "/s")
            {
                foreach (Screen s in Screen.AllScreens) new SaverForm(s.Bounds).Show();
                Application.Run();
            }
            else if (mode == "/p")
            {
                long h;
                if (arg == null || !long.TryParse(arg, out h)) return;
                Application.Run(new SaverForm(new IntPtr(h)));
            }
            else if (mode == "/w")
            {
                Application.Run(new SaverForm());
            }
            else if (mode == "/x" && args.Length >= 5)
            {
                Shot(args[1], int.Parse(args[2]), int.Parse(args[3]), double.Parse(args[4], CultureInfo.InvariantCulture));
            }
            else
            {
                MessageBox.Show(
                    "Superhero screensaver\n\nThe hero flies over Big City at night.\nThere are no settings yet: just sit back and watch.\n\nMove the mouse or press a key to stop it.",
                    "Superhero", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Saves one frame to a PNG, and how long a frame takes to draw next to it (.txt).
        static void Shot(string path, int w, int h, double secs)
        {
            Scene sc = new Scene(w, h, 12345);
            sc.LoopAt(secs - 1.3); // so the picture catches the hero partway around a loop-the-loop
            double step = 1 / 30.0;
            for (double tt = 0; tt < secs; tt += step) sc.Update(step);
            using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                sc.Draw(g);
                bmp.Save(path, ImageFormat.Png);
            }
            // Time it on the same kind of off-screen buffer the screensaver window draws into (never shown)
            using (Graphics desk = Graphics.FromHwnd(IntPtr.Zero))
            using (BufferedGraphics bg = BufferedGraphicsManager.Current.Allocate(desk, new Rectangle(0, 0, w, h)))
            {
                Graphics g = bg.Graphics;
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < 60; i++) { sc.Update(step); sc.Draw(g); }
                System.IO.File.WriteAllText(path + ".txt",
                    "ms per frame: " + (sw.Elapsed.TotalMilliseconds / 60).ToString("0.0", CultureInfo.InvariantCulture) + "\n" + sc.Profile(g));
            }
        }
    }

    class SaverForm : Form
    {
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

        Scene scene;
        readonly Stopwatch clock = Stopwatch.StartNew();
        double last;
        readonly Timer timer = new Timer();
        readonly IntPtr parentHandle = IntPtr.Zero;
        readonly bool closeOnInput;
        Point mouseStart = new Point(int.MinValue, 0);

        // Full screen on one monitor
        public SaverForm(Rectangle bounds)
        {
            Setup();
            closeOnInput = true;
            Bounds = bounds;
            TopMost = true;
            Cursor.Hide();
        }

        // Inside the preview box in Screen Saver Settings
        public SaverForm(IntPtr parent)
        {
            parentHandle = parent;
            Setup();
        }

        // In a normal window, to try it out
        public SaverForm()
        {
            Setup();
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 720);
        }

        void Setup()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            Text = "Superhero";
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            timer.Interval = 15;
            timer.Tick += delegate { Invalidate(); };
            timer.Start();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (parentHandle != IntPtr.Zero)
                {
                    RECT r;
                    GetClientRect(parentHandle, out r);
                    cp.Parent = parentHandle;
                    cp.Style = 0x40000000 | 0x10000000; // WS_CHILD | WS_VISIBLE
                    cp.ExStyle = 0;
                    cp.X = 0; cp.Y = 0;
                    cp.Width = Math.Max(1, r.Right - r.Left);
                    cp.Height = Math.Max(1, r.Bottom - r.Top);
                }
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
            if (scene == null || scene.W != w || scene.H != h)
            {
                if (scene != null) scene.Dispose();
                scene = new Scene(w, h, Environment.TickCount);
                last = clock.Elapsed.TotalSeconds;
            }
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, Math.Max(0, now - last));
            last = now;
            scene.Update(dt);
            scene.Draw(e.Graphics);
        }

        void Quit()
        {
            if (closeOnInput) Application.Exit();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!closeOnInput) return;
            if (mouseStart.X == int.MinValue) { mouseStart = e.Location; return; }
            if (Math.Abs(e.X - mouseStart.X) > 8 || Math.Abs(e.Y - mouseStart.Y) > 8) Quit();
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Quit(); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); Quit(); }
    }

    class Star { public float X, Y, R, Phase, Speed; public Color Tint; }
    class Cloud { public float X, Y, W, Speed; public Bitmap Img; }
    class Car { public float X, Speed; public int Lane; public Color Body; }
    class Streak { public float Y, Len, Speed, Phase; }

    class Building
    {
        public float X, W, H, TopW, TopH, BlinkPhase, AntennaH;
        public int Cols, Rows, Roof; // Roof: 0 flat, 1 antenna, 2 water tower, 3 spire
        public byte[] Win;           // 0 dark, 1 warm light, 2 TV blue
        public Color Body;
    }

    class Layer
    {
        public float Speed, MinH, MaxH, MinW, MaxW, Gap, CellW, CellH, Edge, LitMin, LitMax, ToggleRate, ToggleAcc, NextX;
        public double Offset;
        public int Depth;
        public Color Body;
        public SolidBrush Warm, Cool, Unlit;
        public Brush BodyFill; // far-off layers fade into the city haze toward the ground
        public readonly List<Building> B = new List<Building>();
    }

    class Scene : IDisposable
    {
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr hdc, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        IntPtr skyDC = IntPtr.Zero, skyHbm = IntPtr.Zero, skyOld = IntPtr.Zero;

        public readonly int W, H;
        readonly float S;      // 1 at 1080 pixels tall
        readonly float V;      // how fast the city goes by, pixels a second
        readonly Random rng;
        double t;

        readonly Bitmap sky;
        readonly Star[] stars;
        readonly Layer[] layers;
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<Car> cars = new List<Car>();
        readonly Streak[] streaks;
        float roadOffset;

        bool planeOn; float planeX, planeY; double nextPlane;
        bool shootOn; float shX, shY, shVx, shVy; double shStart, nextShoot;
        double loopStart = -100, nextLoop;
        const double LoopTime = 3.4;

        readonly SolidBrush brush = new SolidBrush(Color.White);
        readonly List<RectangleF> warmRects = new List<RectangleF>(), coolRects = new List<RectangleF>(), unlitRects = new List<RectangleF>();

        // The hero's Classic costume from the game
        static readonly Color Suit = C("#1f4fd1"), SuitDark = C("#173b9e"), SuitLight = C("#3a6cf0");
        static readonly Color Cape = C("#d62828"), CapeDark = C("#8c1212"), Gold = C("#ffd23f");
        static readonly Color Skin = C("#e8b48a"), Hair = C("#2b1b12"), Mask = C("#132f7d");

        static Color C(string hex)
        {
            int v = int.Parse(hex.Substring(1), NumberStyles.HexNumber);
            return Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
        }

        float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static int Clamp255(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }
        static Color Shade(Color c, int d) { return Color.FromArgb(c.A, Clamp255(c.R + d), Clamp255(c.G + d), Clamp255(c.B + d)); }

        public Scene(int w, int h, int seed)
        {
            W = w; H = h;
            S = h / 1080f;
            V = 240 * S;
            rng = new Random(seed);
            nextPlane = R(4, 10);
            nextShoot = R(3, 8);
            nextLoop = R(7, 12);

            sky = MakeSky();
            try
            {
                skyHbm = sky.GetHbitmap();
                skyDC = CreateCompatibleDC(IntPtr.Zero);
                skyOld = SelectObject(skyDC, skyHbm);
            }
            catch { skyDC = IntPtr.Zero; }

            float mx = W * 0.78f, my = H * 0.2f, mr = H * 0.055f;
            stars = new Star[(int)Math.Min(600, 120 + W * H / 9000)];
            for (int i = 0; i < stars.Length; i++)
            {
                Star s = new Star();
                do { s.X = R(0, W); s.Y = (float)Math.Pow(rng.NextDouble(), 1.4) * H * 0.7f; }
                while ((s.X - mx) * (s.X - mx) + (s.Y - my) * (s.Y - my) < mr * mr * 2.2f);
                s.R = Math.Max(1f, R(0.6f, 2.2f) * S * 1.2f);
                s.Phase = R(0, 6.28f);
                s.Speed = R(0.6f, 2.5f);
                double k = rng.NextDouble();
                s.Tint = k < 0.15 ? Color.FromArgb(190, 210, 255) : k < 0.25 ? Color.FromArgb(255, 235, 200) : Color.White;
                stars[i] = s;
            }

            for (int i = 0; i < 5; i++) clouds.Add(MakeCloud(R(-W * 0.2f, W)));

            layers = new Layer[]
            {
                MakeLayer(0, 0.12f, 0.16f, 0.46f, 50, 130, 0, 5.5f, 7.5f, C("#171b3a"), 0,
                          Color.FromArgb(150, 255, 214, 140), Color.FromArgb(130, 170, 200, 255), Color.Empty, 0.10f, 0.30f, 8),
                MakeLayer(1, 0.35f, 0.12f, 0.40f, 60, 160, 24, 8.5f, 11.5f, C("#0f1229"), 6,
                          Color.FromArgb(215, 255, 214, 140), Color.FromArgb(190, 165, 200, 255), Color.Empty, 0.12f, 0.40f, 6),
                MakeLayer(2, 1.0f, 0.08f, 0.355f, 90, 230, 50, 14, 19, C("#080a18"), 10,
                          Color.FromArgb(255, 255, 220, 150), Color.FromArgb(240, 168, 205, 255), Color.FromArgb(255, 18, 21, 40), 0.15f, 0.45f, 3),
            };
            layers[0].BodyFill = new LinearGradientBrush(new PointF(0, H * 0.40f), new PointF(0, H * 0.97f + 1), C("#171b3a"), C("#2d2648"));
            layers[1].BodyFill = new LinearGradientBrush(new PointF(0, H * 0.60f), new PointF(0, H * 0.97f + 1), C("#0f1229"), C("#1c1835"));
            foreach (Layer L in layers) FillLayer(L);

            for (int lane = 0; lane < 2; lane++)
                for (int i = 0; i < 6; i++) cars.Add(MakeCar(lane, R(0, W * 1.6f)));

            streaks = new Streak[12];
            for (int i = 0; i < streaks.Length; i++)
            {
                Streak s = new Streak();
                s.Y = R(-24, 24); s.Len = R(30, 90); s.Speed = R(0.8f, 1.6f); s.Phase = R(0, 1);
                streaks[i] = s;
            }
        }

        public void Dispose()
        {
            if (skyDC != IntPtr.Zero) { SelectObject(skyDC, skyOld); DeleteDC(skyDC); }
            if (skyHbm != IntPtr.Zero) DeleteObject(skyHbm);
            sky.Dispose();
            brush.Dispose();
            foreach (Cloud c in clouds) c.Img.Dispose();
            foreach (Layer L in layers) { L.Warm.Dispose(); L.Cool.Dispose(); L.Unlit.Dispose(); if (L.BodyFill != null) L.BodyFill.Dispose(); }
        }

        // ---------- making things ----------

        Bitmap MakeSky()
        {
            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle all = new Rectangle(-1, -1, W + 2, H + 2);
                using (LinearGradientBrush lg = new LinearGradientBrush(all, Color.Black, Color.Black, 90f))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Colors = new Color[] { C("#03050f"), C("#091030"), C("#1a1c4b"), C("#3b2a5c"), C("#6b3c5f") };
                    cb.Positions = new float[] { 0f, 0.35f, 0.62f, 0.86f, 1f };
                    lg.InterpolationColors = cb;
                    g.FillRectangle(lg, all);
                }

                float mx = W * 0.78f, my = H * 0.2f, mr = H * 0.055f;
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(mx - mr * 4.5f, my - mr * 4.5f, mr * 9, mr * 9);
                    using (PathGradientBrush pg = new PathGradientBrush(gp))
                    {
                        pg.CenterColor = Color.FromArgb(70, 205, 215, 255);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 205, 215, 255) };
                        g.FillPath(pg, gp);
                    }
                }
                RectangleF moon = new RectangleF(mx - mr, my - mr, mr * 2, mr * 2);
                using (LinearGradientBrush lg = new LinearGradientBrush(moon, C("#fffbea"), C("#d6d1bc"), 135f))
                    g.FillEllipse(lg, moon);
                using (SolidBrush crater = new SolidBrush(Color.FromArgb(38, 110, 105, 90)))
                {
                    float[][] cr = { new float[] { -0.35f, -0.2f, 0.28f }, new float[] { 0.25f, 0.3f, 0.2f },
                                     new float[] { 0.1f, -0.45f, 0.14f }, new float[] { -0.15f, 0.45f, 0.12f },
                                     new float[] { 0.45f, -0.1f, 0.1f } };
                    foreach (float[] c in cr)
                        g.FillEllipse(crater, mx + c[0] * mr - c[2] * mr, my + c[1] * mr - c[2] * mr, c[2] * mr * 2, c[2] * mr * 2);
                }
            }
            return bmp;
        }

        Cloud MakeCloud(float x)
        {
            Cloud c = new Cloud();
            c.X = x;
            c.Y = R(0.08f, 0.45f) * H;
            c.W = R(0.18f, 0.4f) * W;
            c.Speed = R(6, 14) * S;
            int n = rng.Next(6, 11);
            RectangleF[] puffs = new RectangleF[n];
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float pw = c.W * R(0.22f, 0.5f), ph = pw * R(0.2f, 0.36f);
                puffs[i] = new RectangleF(R(0, c.W - pw), R(-ph * 0.5f, ph * 0.3f), pw, ph);
                minY = Math.Min(minY, puffs[i].Top - ph * 0.15f);
                maxY = Math.Max(maxY, puffs[i].Bottom);
            }
            // Baked once into a picture with soft edges: each puff is a few rings, faint outside, thicker inside
            c.Img = new Bitmap((int)c.W + 2, (int)(maxY - minY) + 2, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(c.Img))
            using (SolidBrush b = new SolidBrush(Color.White))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float[] rings = { 1f, 0.8f, 0.6f, 0.4f };
                foreach (float k in rings)
                {
                    foreach (RectangleF p in puffs)
                    {
                        float w = p.Width * k, h = p.Height * k;
                        float px = p.X + (p.Width - w) / 2, py = p.Y - minY + (p.Height - h) / 2;
                        b.Color = Color.FromArgb(9, 165, 170, 215);   // moonlit tops
                        g.FillEllipse(b, px, py - h * 0.14f, w, h);
                        b.Color = Color.FromArgb(10, 60, 62, 105);
                        g.FillEllipse(b, px, py, w, h);
                    }
                }
            }
            c.Y += minY;
            return c;
        }

        Car MakeCar(int lane, float x)
        {
            Car c = new Car();
            c.Lane = lane;
            c.X = x;
            c.Speed = (lane == 0 ? -1 : 1) * R(70, 150) * S;
            int k = rng.Next(4);
            c.Body = k == 0 ? C("#2a1418") : k == 1 ? C("#141c2c") : k == 2 ? C("#26261c") : C("#1a1a22");
            return c;
        }

        Layer MakeLayer(int depth, float speed, float minH, float maxH, float minW, float maxW, float gap,
                        float cellW, float cellH, Color body, float edge, Color warm, Color cool, Color unlit,
                        float litMin, float litMax, float toggleRate)
        {
            Layer L = new Layer();
            L.Depth = depth; L.Speed = speed;
            L.MinH = minH * H; L.MaxH = maxH * H;
            L.MinW = minW * S; L.MaxW = maxW * S; L.Gap = gap * S;
            L.CellW = Math.Max(2.5f, cellW * S); L.CellH = Math.Max(3f, cellH * S);
            L.Body = body; L.Edge = edge;
            L.Warm = new SolidBrush(warm); L.Cool = new SolidBrush(cool);
            L.Unlit = new SolidBrush(unlit.IsEmpty ? Color.Transparent : unlit);
            L.LitMin = litMin; L.LitMax = litMax;
            L.ToggleRate = toggleRate;
            L.NextX = -100 * S;
            return L;
        }

        void FillLayer(Layer L)
        {
            float off = (float)L.Offset;
            while (L.B.Count > 0 && L.B[0].X + L.B[0].W - off < -300 * S) L.B.RemoveAt(0);
            while (L.NextX - off < W + 300 * S) L.B.Add(NewBuilding(L));
        }

        Building NewBuilding(Layer L)
        {
            Building b = new Building();
            b.W = R(L.MinW, L.MaxW);
            b.H = L.MinH + (float)Math.Pow(rng.NextDouble(), 1.3) * (L.MaxH - L.MinH);
            bool tower = rng.NextDouble() < 0.07;
            if (tower) { b.H = L.MaxH * R(1.1f, 1.25f); b.W = Math.Max(b.W, (L.MinW + L.MaxW) * 0.5f); }
            b.X = L.NextX + (L.Gap > 0 ? R(L.Gap * 0.2f, L.Gap) : 0);
            L.NextX = b.X + b.W;

            if (tower || rng.NextDouble() < 0.35)
            {
                b.TopW = b.W * R(0.45f, 0.75f);
                b.TopH = b.H * R(0.1f, 0.22f);
            }
            double k = rng.NextDouble();
            b.Roof = tower ? 3 : k < 0.3 ? 1 : (k < 0.45 && L.Depth == 2 && b.TopW == 0) ? 2 : 0;
            b.AntennaH = R(0.06f, 0.14f) * H * (L.Depth == 0 ? 0.6f : 1f);
            b.BlinkPhase = R(0, 6.28f);
            int d = rng.Next(-6, 7);
            b.Body = Shade(L.Body, d);

            float pad = L.CellW * 0.7f;
            b.Cols = Math.Max(1, (int)((b.W - pad * 2) / L.CellW));
            b.Rows = Math.Max(1, (int)((b.H - pad) / L.CellH));
            b.Win = new byte[b.Cols * b.Rows];
            float lit = R(L.LitMin, L.LitMax);
            for (int r = 0; r < b.Rows; r++)
            {
                bool darkFloor = rng.NextDouble() < 0.18;
                for (int c = 0; c < b.Cols; c++)
                {
                    if (darkFloor || rng.NextDouble() > lit) continue;
                    b.Win[r * b.Cols + c] = (byte)(rng.NextDouble() < 0.12 ? 2 : 1);
                }
            }
            return b;
        }

        // ---------- moving things ----------

        public void Update(double dt)
        {
            t += dt;
            float fdt = (float)dt;

            foreach (Layer L in layers)
            {
                L.Offset += L.Speed * V * dt;
                FillLayer(L);
                L.ToggleAcc += L.ToggleRate * fdt;
                while (L.ToggleAcc >= 1)
                {
                    L.ToggleAcc -= 1;
                    Building b = L.B[rng.Next(L.B.Count)];
                    int i = rng.Next(b.Win.Length);
                    b.Win[i] = b.Win[i] != 0 ? (byte)0 : (byte)(rng.NextDouble() < 0.15 ? 2 : 1);
                }
            }

            for (int i = 0; i < clouds.Count; i++)
            {
                Cloud c = clouds[i];
                c.X -= c.Speed * fdt;
                if (c.X + c.W < 0) { c.Img.Dispose(); clouds[i] = MakeCloud(W + R(0, W * 0.3f)); }
            }

            roadOffset += V * fdt;
            for (int i = 0; i < cars.Count; i++)
            {
                Car c = cars[i];
                c.X += (c.Speed - V) * fdt;
                if (c.X < -60 * S) cars[i] = MakeCar(c.Lane, W + R(20, W * 0.8f) * 1f);
            }

            if (!planeOn && t > nextPlane) { planeOn = true; planeX = W + 40 * S; planeY = R(0.07f, 0.17f) * H; }
            if (planeOn)
            {
                planeX -= 70 * S * fdt;
                if (planeX < -60 * S) { planeOn = false; nextPlane = t + R(18, 35); }
            }

            if (!shootOn && t > nextShoot)
            {
                shootOn = true; shStart = t;
                shX = R(0.2f, 1f) * W; shY = R(0.02f, 0.3f) * H;
                float sp = R(700, 1000) * S, a = R(150, 165) * (float)Math.PI / 180;
                shVx = (float)Math.Cos(a) * sp; shVy = (float)Math.Sin(a) * sp;
            }
            if (shootOn)
            {
                shX += shVx * fdt; shY += shVy * fdt;
                if (t - shStart > 0.9) { shootOn = false; nextShoot = t + R(7, 18); }
            }

            if (t >= nextLoop && t > loopStart + LoopTime)
            {
                loopStart = t;
                nextLoop = t + LoopTime + R(16, 32);
            }
        }

        public void LoopAt(double when) { nextLoop = when; }

        void HeroAt(double tt, out float x, out float y)
        {
            x = W * (float)(0.40 + 0.10 * Math.Sin(tt * 0.11) + 0.03 * Math.Sin(tt * 0.37 + 2));
            y = H * (float)(0.40 + 0.08 * Math.Sin(tt * 0.19 + 1) + 0.025 * Math.Sin(tt * 0.61));
            double p = (tt - loopStart) / LoopTime;
            if (p > 0 && p < 1)
            {
                double a = 2 * Math.PI * p * p * (3 - 2 * p);
                float rad = 0.1f * H;
                x += rad * (float)Math.Sin(a);
                y -= rad * (float)(1 - Math.Cos(a));
            }
        }

        // ---------- drawing ----------

        public void Draw(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            DrawSky(g);
            DrawStars(g);
            DrawShootingStar(g);
            DrawClouds(g);
            DrawPlane(g);
            DrawSearchlights(g);

            DrawLayer(g, layers[0]);
            DrawLayer(g, layers[1]);
            DrawLayer(g, layers[2]);
            DrawRoad(g);

            float x, y, x2, y2;
            HeroAt(t, out x, out y);
            HeroAt(t + 0.02, out x2, out y2);
            float dx = (x2 - x) / 0.02f, dy = (y2 - y) / 0.02f;
            float ang = (float)(Math.Atan2(dy, 110 * S + dx) * 180 / Math.PI);
            ang += (float)Math.Sin(t * 1.3) * 2;
            DrawHero(g, x, y, ang, H / 1000f);
        }

        public string Profile(Graphics g)
        {
            string[] names = { "sky", "stars", "clouds", "searchlights", "far city", "mid city", "near city", "road", "hero" };
            double[] ms = new double[names.Length];
            Stopwatch sw = new Stopwatch();
            for (int n = 0; n < 30; n++)
            {
                Update(1 / 30.0);
                for (int i = 0; i < names.Length; i++)
                {
                    sw.Restart();
                    switch (i)
                    {
                        case 0: DrawSky(g); break;
                        case 1: DrawStars(g); break;
                        case 2: DrawClouds(g); break;
                        case 3: DrawSearchlights(g); break;
                        case 4: DrawLayer(g, layers[0]); break;
                        case 5: DrawLayer(g, layers[1]); break;
                        case 6: DrawLayer(g, layers[2]); break;
                        case 7: DrawRoad(g); break;
                        case 8: DrawHero(g, W / 2, H / 2, 0, H / 1000f); break;
                    }
                    ms[i] += sw.Elapsed.TotalMilliseconds / 30;
                }
            }
            string s = "";
            for (int i = 0; i < names.Length; i++) s += names[i] + " " + ms[i].ToString("0.00", CultureInfo.InvariantCulture) + "\n";
            return s;
        }

        void DrawStars(Graphics g)
        {
            foreach (Star s in stars)
            {
                int a = (int)(150 + 105 * Math.Sin(t * s.Speed + s.Phase));
                brush.Color = Color.FromArgb(Clamp255(a), s.Tint);
                if (s.R < 1.6f) g.FillRectangle(brush, s.X, s.Y, s.R, s.R);
                else g.FillEllipse(brush, s.X - s.R / 2, s.Y - s.R / 2, s.R, s.R);
            }
        }

        void DrawShootingStar(Graphics g)
        {
            if (!shootOn) return;
            float age = (float)(t - shStart);
            float fade = age < 0.15f ? age / 0.15f : Math.Max(0, 1 - (age - 0.15f) / 0.75f);
            float tailX = shX - shVx * 0.18f, tailY = shY - shVy * 0.18f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(shX, shY), new PointF(tailX, tailY),
                       Color.FromArgb((int)(230 * fade), 255, 255, 255), Color.FromArgb(0, 180, 200, 255)))
            using (Pen p = new Pen(lg, Math.Max(1, 2.2f * S)))
                g.DrawLine(p, shX, shY, tailX, tailY);
            g.SmoothingMode = SmoothingMode.None;
        }

        void DrawSky(Graphics g)
        {
            if (skyDC != IntPtr.Zero)
            {
                IntPtr hdc = g.GetHdc();
                try { BitBlt(hdc, 0, 0, W, H, skyDC, 0, 0, 0x00CC0020); } // SRCCOPY
                finally { g.ReleaseHdc(hdc); }
            }
            else g.DrawImageUnscaled(sky, 0, 0);
        }

        void DrawClouds(Graphics g)
        {
            foreach (Cloud c in clouds)
                if (c.X < W) g.DrawImageUnscaled(c.Img, (int)c.X, (int)c.Y);
        }

        void DrawPlane(Graphics g)
        {
            if (!planeOn) return;
            float x = planeX, y = planeY, s = S;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            brush.Color = Color.FromArgb(160, 20, 22, 40);
            g.FillEllipse(brush, x - 22 * s, y - 2.5f * s, 44 * s, 5 * s);
            g.FillPolygon(brush, new PointF[] { new PointF(x - 4 * s, y), new PointF(x + 6 * s, y - 1), new PointF(x + 14 * s, y + 8 * s), new PointF(x + 9 * s, y + 8 * s) });
            g.FillPolygon(brush, new PointF[] { new PointF(x + 16 * s, y - 1), new PointF(x + 22 * s, y - 9 * s), new PointF(x + 24 * s, y - 9 * s), new PointF(x + 22 * s, y) });
            bool blink = (t * 1.1) % 1.0 < 0.5;
            bool strobe = (t * 0.8) % 1.0 < 0.06;
            if (blink) Glow(g, x + 12 * s, y + 8 * s, 2 * s, Color.FromArgb(255, 60, 60));
            Glow(g, x + 2 * s, y - 2 * s, 1.5f * s, Color.FromArgb(60, 255, 120));
            if (strobe) Glow(g, x + 23 * s, y - 9 * s, 3 * s, Color.White);
            g.SmoothingMode = SmoothingMode.None;
        }

        void Glow(Graphics g, float x, float y, float r, Color c)
        {
            r = Math.Max(r, 1.2f);
            brush.Color = Color.FromArgb(55, c);
            g.FillEllipse(brush, x - r * 3, y - r * 3, r * 6, r * 6);
            brush.Color = c;
            g.FillEllipse(brush, x - r, y - r, r * 2, r * 2);
        }

        void DrawSearchlights(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float[] xs = { 0.22f, 0.66f };
            for (int i = 0; i < xs.Length; i++)
            {
                double a = (-90 + 30 * Math.Sin(t * 0.27 + i * 2.3) + (i == 0 ? -8 : 10)) * Math.PI / 180;
                float ox = W * xs[i], oy = H * 0.9f, len = H * 1.15f;
                float ex = ox + (float)Math.Cos(a) * len, ey = oy + (float)Math.Sin(a) * len;
                float px = -(float)Math.Sin(a), py = (float)Math.Cos(a);
                float w0 = 5 * S, w1 = H * 0.055f;
                PointF[] pts = { new PointF(ox + px * w0, oy + py * w0), new PointF(ex + px * w1, ey + py * w1),
                                 new PointF(ex - px * w1, ey - py * w1), new PointF(ox - px * w0, oy - py * w0) };
                using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(ox, oy), new PointF(ex, ey),
                           Color.FromArgb(48, 215, 225, 255), Color.FromArgb(0, 215, 225, 255)))
                {
                    lg.WrapMode = WrapMode.TileFlipXY;
                    g.FillPolygon(lg, pts);
                }
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        void DrawLayer(Graphics g, Layer L)
        {
            float off = (float)Math.Floor(L.Offset);
            float baseY = H * 0.965f;
            float pad = L.CellW * 0.7f;
            float ww = (float)Math.Max(1.5, L.CellW * 0.55), wh = (float)Math.Max(1.5, L.CellH * 0.5);
            warmRects.Clear(); coolRects.Clear(); unlitRects.Clear();
            bool drawUnlit = L.Unlit.Color.A > 0;

            foreach (Building b in L.B)
            {
                float sx = b.X - off;
                if (sx > W + 20 || sx + b.W < -20) continue;
                float mainTop = baseY - (b.H - b.TopH);
                float roofY = baseY - b.H;
                float topX = sx + (b.W - b.TopW) / 2;

                brush.Color = b.Body;
                Brush fill = L.BodyFill != null ? L.BodyFill : brush;
                g.FillRectangle(fill, sx, mainTop, b.W, baseY - mainTop + 1);
                if (b.TopW > 0) g.FillRectangle(fill, topX, roofY, b.TopW, b.TopH + 1);

                if (L.Edge > 0)
                {
                    // Moonlight on the right-hand edges (the Moon is on the right)
                    brush.Color = Shade(b.Body, (int)L.Edge);
                    float ew = Math.Max(1, 2.5f * S);
                    g.FillRectangle(brush, sx + b.W - ew, mainTop, ew, baseY - mainTop);
                    if (b.TopW > 0) g.FillRectangle(brush, topX + b.TopW - ew, roofY, ew, b.TopH);
                    g.FillRectangle(brush, sx, mainTop, b.W, ew * 0.6f);
                }

                DrawRoof(g, L, b, sx, roofY, topX);

                float gridX = sx + (b.W - b.Cols * L.CellW) / 2 + (L.CellW - ww) / 2;
                for (int r = 0; r < b.Rows; r++)
                {
                    float wy = roofY + pad + r * L.CellH;
                    if (wy + wh > baseY - 2) break;
                    bool inTop = wy < mainTop + 1;
                    if (inTop && b.TopW <= 0) continue;
                    for (int c = 0; c < b.Cols; c++)
                    {
                        float wx = gridX + c * L.CellW;
                        if (inTop && (wx < topX + 1 || wx + ww > topX + b.TopW - 1)) continue;
                        byte s = b.Win[r * b.Cols + c];
                        if (s == 1) warmRects.Add(new RectangleF(wx, wy, ww, wh));
                        else if (s == 2) coolRects.Add(new RectangleF(wx, wy, ww, wh));
                        else if (drawUnlit) unlitRects.Add(new RectangleF(wx, wy, ww, wh));
                    }
                }
            }
            if (unlitRects.Count > 0) g.FillRectangles(L.Unlit, unlitRects.ToArray());
            if (warmRects.Count > 0) g.FillRectangles(L.Warm, warmRects.ToArray());
            if (coolRects.Count > 0) g.FillRectangles(L.Cool, coolRects.ToArray());
        }

        void DrawRoof(Graphics g, Layer L, Building b, float sx, float roofY, float topX)
        {
            float cx = b.TopW > 0 ? topX + b.TopW / 2 : sx + b.W / 2;
            float lw = Math.Max(1, 2 * S * (L.Depth == 0 ? 0.7f : 1));
            brush.Color = b.Body;
            if (b.Roof == 1)
            {
                g.FillRectangle(brush, cx - lw / 2, roofY - b.AntennaH, lw, b.AntennaH);
                double blink = Math.Sin(t * 2.2 + b.BlinkPhase);
                if (blink > 0.2 && L.Depth > 0)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Glow(g, cx, roofY - b.AntennaH, 1.6f * S * (L.Depth == 2 ? 1.3f : 1f), Color.FromArgb(255, 50, 50));
                    g.SmoothingMode = SmoothingMode.None;
                }
            }
            else if (b.Roof == 2)
            {
                float tx = sx + b.W * 0.28f, tw = 20 * S, th = 18 * S, legs = 9 * S;
                g.FillRectangle(brush, tx + 2 * S, roofY - legs, lw, legs);
                g.FillRectangle(brush, tx + tw - 2 * S - lw, roofY - legs, lw, legs);
                g.FillRectangle(brush, tx, roofY - legs - th, tw, th);
                g.FillPolygon(brush, new PointF[] { new PointF(tx - 1 * S, roofY - legs - th), new PointF(tx + tw / 2, roofY - legs - th - 8 * S), new PointF(tx + tw + 1 * S, roofY - legs - th) });
            }
            else if (b.Roof == 3)
            {
                float bw = (b.TopW > 0 ? b.TopW : b.W) * 0.35f, sh = b.H * 0.13f;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPolygon(brush, new PointF[] { new PointF(cx - bw / 2, roofY + 1), new PointF(cx, roofY - sh), new PointF(cx + bw / 2, roofY + 1) });
                g.FillRectangle(brush, cx - lw / 2, roofY - sh - b.AntennaH * 0.6f, lw, b.AntennaH * 0.6f);
                if (Math.Sin(t * 1.8 + b.BlinkPhase) > 0) Glow(g, cx, roofY - sh - b.AntennaH * 0.6f, 1.8f * S, Color.FromArgb(255, 50, 50));
                g.SmoothingMode = SmoothingMode.None;
            }
        }

        void DrawRoad(Graphics g)
        {
            float top = H * 0.965f;
            brush.Color = C("#05060d");
            g.FillRectangle(brush, 0, top, W, H - top + 1);
            brush.Color = C("#1a1c33");
            g.FillRectangle(brush, 0, top, W, Math.Max(1, 2.5f * S));

            float dash = 46 * S, mid = H * 0.982f;
            brush.Color = Color.FromArgb(90, 200, 170, 80);
            float start = -(roadOffset % dash);
            for (float x = start; x < W; x += dash) g.FillRectangle(brush, x, mid, dash * 0.45f, Math.Max(1, 1.5f * S));

            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (Car c in cars)
            {
                float y = c.Lane == 0 ? H * 0.972f : H * 0.988f;
                float cw = 28 * S, ch = 7 * S;
                brush.Color = c.Body;
                g.FillRectangle(brush, c.X, y - ch, cw, ch);
                g.FillRectangle(brush, c.X + cw * 0.22f, y - ch * 1.7f, cw * 0.5f, ch * 0.8f);
                bool right = c.Speed > 0;
                float front = right ? c.X + cw : c.X, back = right ? c.X : c.X + cw;
                float dir = right ? 1 : -1;
                using (LinearGradientBrush beam = new LinearGradientBrush(new PointF(front, 0), new PointF(front + dir * 40 * S, 0),
                           Color.FromArgb(70, 255, 240, 190), Color.FromArgb(0, 255, 240, 190)))
                    g.FillPolygon(beam, new PointF[] { new PointF(front, y - ch * 0.7f), new PointF(front + dir * 40 * S, y - ch * 1.4f),
                                                       new PointF(front + dir * 40 * S, y + ch * 0.5f), new PointF(front, y - ch * 0.2f) });
                brush.Color = Color.FromArgb(255, 248, 210);
                g.FillRectangle(brush, right ? front - 2 * S : front, y - ch * 0.75f, 2 * S, ch * 0.4f);
                brush.Color = Color.FromArgb(255, 40, 40);
                g.FillRectangle(brush, right ? back : back - 2 * S, y - ch * 0.75f, 2 * S, ch * 0.4f);
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        static Pen RoundPen(Color c, float w)
        {
            Pen p = new Pen(c, w);
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            return p;
        }

        // The hero, facing right, flying flat. Drawn in his own little coordinates (about 170 long).
        void DrawHero(Graphics g, float x, float y, float ang, float sc)
        {
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(x, y);
            g.RotateTransform(ang);
            g.ScaleTransform(sc, sc);
            float tt = (float)t;

            // Wind rushing past
            using (Pen p = new Pen(Color.White, 1.6f))
            {
                foreach (Streak s in streaks)
                {
                    float k = (float)((t * s.Speed + s.Phase) % 1.0);
                    float x0 = -70 - k * 170, len = s.Len * (1 - k * 0.5f);
                    int a = (int)(120 * (1 - k) * (k < 0.1f ? k / 0.1f : 1));
                    p.Color = Color.FromArgb(Clamp255(a), 215, 228, 255);
                    g.DrawLine(p, x0, s.Y, x0 - len, s.Y);
                }
            }

            float kick = (float)Math.Sin(tt * 2.3) * 3, kick2 = (float)Math.Sin(tt * 2.3 + 1.4) * 3;

            // Back leg and back arm (in shadow)
            using (Pen p = RoundPen(Shade(SuitDark, -10), 12)) g.DrawLine(p, -30, 4, -88, 6 + kick);
            using (Pen p = RoundPen(Shade(CapeDark, 10), 13)) g.DrawLine(p, -86, 6 + kick, -101, 6.5f + kick);
            using (Pen p = RoundPen(Shade(SuitDark, -10), 9)) g.DrawLine(p, 12, 4, -8, 10);
            using (SolidBrush b = new SolidBrush(CapeDark)) g.FillEllipse(b, -15, 4.5f, 11, 11);

            // Cape, rippling in the wind, with a gold trim and the emblem
            const int N = 18;
            PointF[] cape = new PointF[N * 2];
            float ph = tt * 6.5f;
            for (int i = 0; i < N; i++)
            {
                float u = i / (float)(N - 1);
                float cx = 16 - 134 * u;
                cape[i] = new PointF(cx, -12 - 5 * u + (float)Math.Sin(ph - u * 7) * 9 * u);
                cape[N * 2 - 1 - i] = new PointF(cx - 5 * u, 3 + 20 * u + (float)Math.Sin(ph - u * 7 + 0.7) * 11 * u);
            }
            using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(17, 0), new PointF(-125, 0), Shade(Cape, 12), CapeDark))
                g.FillPolygon(lg, cape);
            using (Pen p = new Pen(Gold, 1.8f)) { p.LineJoin = LineJoin.Round; g.DrawPolygon(p, cape); }
            {
                int i = (int)(N * 0.42f);
                float ex = (cape[i].X + cape[N * 2 - 1 - i].X) / 2, ey = (cape[i].Y + cape[N * 2 - 1 - i].Y) / 2;
                using (SolidBrush b = new SolidBrush(Gold)) g.FillEllipse(b, ex - 7, ey - 7, 14, 14);
                using (SolidBrush b = new SolidBrush(Suit)) g.FillEllipse(b, ex - 5, ey - 5, 10, 10);
            }

            // Body
            using (GraphicsPath torso = new GraphicsPath())
            {
                torso.AddEllipse(-40, -14, 70, 27);
                using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(0, -15), new PointF(0, 14), SuitLight, SuitDark))
                    g.FillPath(lg, torso);
                Region old = g.Clip;
                g.SetClip(torso, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Gold)) g.FillRectangle(b, -36, -16, 7, 32);
                g.Clip = old;
                using (Pen p = new Pen(Color.FromArgb(90, 190, 210, 255), 2)) g.DrawArc(p, -40, -14, 70, 27, 200, 140);
            }
            using (SolidBrush b = new SolidBrush(Gold))
                g.FillPolygon(b, new PointF[] { new PointF(8, -1), new PointF(14, 4), new PointF(8, 9), new PointF(2, 4) });
            using (Pen p = new Pen(Cape, 1.2f))
                g.DrawPolygon(p, new PointF[] { new PointF(8, -1), new PointF(14, 4), new PointF(8, 9), new PointF(2, 4) });

            // Near leg with a red boot
            using (Pen p = RoundPen(Suit, 12)) g.DrawLine(p, -30, -1, -90, -1 + kick2);
            using (Pen p = RoundPen(Cape, 13)) g.DrawLine(p, -88, -1 + kick2, -104, -0.5f + kick2);

            // Fist out in front, the classic way
            using (Pen p = RoundPen(Suit, 10)) g.DrawLine(p, 16, -7, 60, -11);
            using (SolidBrush b = new SolidBrush(Cape)) g.FillEllipse(b, 57.5f, -17.5f, 13, 13);

            // Head
            using (Pen p = RoundPen(Skin, 8)) g.DrawLine(p, 25, -8, 32, -10);
            using (SolidBrush b = new SolidBrush(Skin)) g.FillEllipse(b, 26.5f, -23.5f, 21, 21);
            using (SolidBrush b = new SolidBrush(Hair))
            {
                g.FillEllipse(b, 25, -25.5f, 21, 13);
                g.FillPolygon(b, new PointF[] { new PointF(42, -21), new PointF(50, -17), new PointF(44, -16) }); // the curl
            }
            using (Pen p = RoundPen(Mask, 4.5f)) g.DrawLine(p, 37, -15.5f, 46.5f, -14.5f);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, 42.5f, -16.5f, 3, 2.2f);

            g.Restore(st);
        }
    }
}
