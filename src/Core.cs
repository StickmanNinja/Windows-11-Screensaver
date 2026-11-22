// The pieces every screensaver shares: the Scene base class, the list of scenes,
// settings (saved in the registry), the host that handles shuffle and speed,
// and a fast off-screen drawing surface.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Screensavers
{
    // One screensaver. It's told how much time went by, then asked to draw a frame.
    abstract class Scene : IDisposable
    {
        public readonly int W, H;
        protected readonly float S; // 1 at 1080 pixels tall, so things keep their size on any screen

        protected Scene(int w, int h) { W = w; H = h; S = h / 1080f; }

        public abstract void Update(double dt);
        public abstract void Draw(Graphics g);
        public virtual void Dispose() { }
    }

    class SceneInfo
    {
        public readonly string Id, Name, Tagline, Blurb;
        public readonly Func<int, int, int, Scene> Make;
        public SceneInfo(string id, string name, string tagline, string blurb, Func<int, int, int, Scene> make)
        {
            Id = id; Name = name; Tagline = tagline; Blurb = blurb; Make = make;
        }
    }

    static class Scenes
    {
        public static readonly SceneInfo[] All =
        {
            new SceneInfo("superhero", "Superhero", "A night flight over the city",
                          "A caped hero flies over the city at night, with searchlights, planes and loop-the-loops.",
                          delegate(int w, int h, int seed) { return new CityScene(w, h, seed); }),
            new SceneInfo("hacker", "Hacker", "Code rain and break-ins",
                          "Green code pours down the screen while terminals break into the mainframe.",
                          delegate(int w, int h, int seed) { return new CodeScene(w, h, seed); }),
            new SceneInfo("space", "Space", "The Earth, the Moon and rockets",
                          "The Earth turns while the Moon, satellites, rockets, comets and an astronaut go by.",
                          delegate(int w, int h, int seed) { return new SpaceScene(w, h, seed); }),
        };

        public static SceneInfo Find(string id)
        {
            foreach (SceneInfo s in All) if (s.Id == id) return s;
            return All[0];
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i;
            return 0;
        }
    }

    class Settings
    {
        public string Scene = "superhero";
        public bool Shuffle;
        public int Speed = 1; // 0 slow, 1 normal, 2 fast

        const string Key = @"Software\Windows 11 Screensaver";
        public const double ShuffleSeconds = 180;

        public float SpeedFactor { get { return Speed == 0 ? 0.55f : Speed == 2 ? 1.7f : 1f; } }

        public Settings Clone() { return (Settings)MemberwiseClone(); }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key))
                {
                    if (k != null)
                    {
                        string id = k.GetValue("Scene") as string;
                        if (id != null) s.Scene = Scenes.Find(id).Id;
                        s.Shuffle = Convert.ToInt32(k.GetValue("Shuffle", 0)) != 0;
                        s.Speed = Math.Max(0, Math.Min(2, Convert.ToInt32(k.GetValue("Speed", 1))));
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Key))
            {
                k.SetValue("Scene", Scene);
                k.SetValue("Shuffle", Shuffle ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("Speed", Speed, RegistryValueKind.DWord);
            }
        }
    }

    // Runs the chosen scene at the chosen speed. With Shuffle on it fades to black every few
    // minutes and switches to another one.
    class SceneHost : IDisposable
    {
        readonly Settings set;
        readonly double fadeTime;
        readonly Random rng = new Random();
        Scene scene;
        string current;
        double sinceSwitch, fadeOut = -1, fadeIn;
        readonly SolidBrush black = new SolidBrush(Color.Black);

        public SceneHost(Settings settings, double fadeSeconds)
        {
            set = settings;
            fadeTime = fadeSeconds;
        }

        public void Frame(Graphics g, int w, int h, double dt)
        {
            if (scene == null || scene.W != w || scene.H != h)
            {
                Start(current ?? (set.Shuffle ? Scenes.All[rng.Next(Scenes.All.Length)].Id : set.Scene), w, h);
            }

            if (set.Shuffle)
            {
                sinceSwitch += dt;
                if (fadeOut < 0 && sinceSwitch > Settings.ShuffleSeconds) fadeOut = 0;
            }
            if (fadeOut >= 0)
            {
                fadeOut += dt;
                if (fadeOut >= fadeTime)
                {
                    string next = current;
                    while (next == current && Scenes.All.Length > 1) next = Scenes.All[rng.Next(Scenes.All.Length)].Id;
                    Start(next, w, h);
                }
            }
            else if (fadeIn < fadeTime) fadeIn += dt;

            scene.Update(dt * set.SpeedFactor);
            scene.Draw(g);

            double dark = fadeOut >= 0 ? fadeOut / fadeTime : fadeTime > 0 ? 1 - fadeIn / fadeTime : 0;
            if (dark > 0.001)
            {
                black.Color = Color.FromArgb((int)(255 * Math.Min(1, dark)), 0, 0, 0);
                g.FillRectangle(black, 0, 0, w, h);
            }
        }

        void Start(string id, int w, int h)
        {
            if (scene != null) scene.Dispose();
            current = id;
            scene = Scenes.Find(id).Make(w, h, rng.Next());
            sinceSwitch = 0;
            fadeOut = -1;
            fadeIn = 0;
        }

        public void Dispose()
        {
            if (scene != null) scene.Dispose();
            black.Dispose();
        }
    }

    static class Shapes
    {
        public static System.Drawing.Drawing2D.GraphicsPath RoundRect(RectangleF r, float rad)
        {
            System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = Math.Max(0.1f, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        public static int Clamp255(float v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }
    }

    static class Gdi
    {
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr hdc, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        public const int SRCCOPY = 0x00CC0020;

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }
    }

    // An off-screen picture you can draw on with G, and copy to the screen very quickly.
    // (Windows copies these much faster than GDI+ can draw a big bitmap.)
    class Surface : IDisposable
    {
        public readonly int W, H;
        public readonly Graphics G;
        readonly Bitmap bmp;
        readonly IntPtr dc, hbm, old;

        public Surface(int w, int h)
        {
            W = Math.Max(1, w); H = Math.Max(1, h);
            Gdi.BITMAPINFOHEADER bi = new Gdi.BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = W; bi.biHeight = -H; bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr bits;
            hbm = Gdi.CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);
            if (hbm == IntPtr.Zero) throw new OutOfMemoryException("Couldn't make a drawing surface");
            dc = Gdi.CreateCompatibleDC(IntPtr.Zero);
            old = Gdi.SelectObject(dc, hbm);
            bmp = new Bitmap(W, H, W * 4, PixelFormat.Format32bppRgb, bits);
            G = Graphics.FromImage(bmp);
        }

        public void DrawTo(Graphics target, int x, int y)
        {
            G.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
            IntPtr hdc = target.GetHdc();
            try { Gdi.BitBlt(hdc, x, y, W, H, dc, 0, 0, Gdi.SRCCOPY); }
            finally { target.ReleaseHdc(hdc); }
        }

        public void Dispose()
        {
            G.Dispose();
            bmp.Dispose();
            Gdi.SelectObject(dc, old);
            Gdi.DeleteDC(dc);
            Gdi.DeleteObject(hbm);
        }
    }
}
