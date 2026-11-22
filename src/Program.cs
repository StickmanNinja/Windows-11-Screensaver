// Screensavers for Windows 11.
//
// Windows runs a screensaver with:
//   /s          full screen (on every monitor)
//   /p <hwnd>   the little preview in Screen Saver Settings
//   /c          the Settings button: opens the picker
//   (nothing)   also the picker
// Extras for trying it out:
//   /w [scene]                                 in a normal window
//   /x <file.png> <w> <h> <seconds> [scene]    save a picture of a scene after that many seconds

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Screensavers")]
[assembly: System.Reflection.AssemblyProduct("Windows 11 Screensaver")]

namespace Screensavers
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

            Settings set = Settings.Load();

            if (a0(args) == "/where" && args.Length >= 2)
            {
                // For testing: writes down which .scr "Set as Windows screensaver" would use
                System.IO.File.WriteAllText(args[1], PickerForm.FindScr() ?? "(none)");
            }
            else if (mode == "/s")
            {
                Cursor.Hide();
                foreach (Screen s in Screen.AllScreens) new SaverForm(s.Bounds, set, Application.Exit).Show();
                Application.Run();
            }
            else if (mode == "/p")
            {
                long h;
                if (arg == null || !long.TryParse(arg, out h)) return;
                Application.Run(new SaverForm(new IntPtr(h), set));
            }
            else if (mode == "/w")
            {
                if (arg != null) { set.Scene = Scenes.Find(arg).Id; set.Shuffle = false; }
                Application.Run(new SaverForm(set));
            }
            else if (a0(args) == "/xp" && args.Length >= 2)
            {
                // A picture of the picker window, for checking how it looks
                PickerForm f = new PickerForm(set);
                f.CreateControl();
                using (Bitmap b = new Bitmap(f.Width, f.Height))
                {
                    for (int i = 0; i < 40; i++) { f.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height)); System.Threading.Thread.Sleep(30); }
                    b.Save(args[1], ImageFormat.Png);
                }
                f.Dispose();
            }
            else if (mode == "/x" && args.Length >= 5)
            {
                Shot(args[1], int.Parse(args[2]), int.Parse(args[3]),
                     double.Parse(args[4], CultureInfo.InvariantCulture), args.Length > 5 ? args[5] : set.Scene);
            }
            else
            {
                Application.Run(new PickerForm(set));
            }
        }

        static string a0(string[] args) { return args.Length > 0 ? args[0].Trim().ToLowerInvariant() : ""; }

        // Saves one frame to a PNG, plus how long a frame takes to draw (.txt next to it).
        static void Shot(string path, int w, int h, double secs, string id)
        {
            Scene sc = Scenes.Find(id).Make(w, h, 12345);
            double step = 1 / 30.0;
            for (double tt = 0; tt < secs; tt += step) sc.Update(step);
            using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                sc.Draw(g);
                bmp.Save(path, ImageFormat.Png);
            }
            // Time it on the same kind of off-screen buffer a screensaver window draws into (never shown)
            using (Graphics desk = Graphics.FromHwnd(IntPtr.Zero))
            using (BufferedGraphics bg = BufferedGraphicsManager.Current.Allocate(desk, new Rectangle(0, 0, w, h)))
            {
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < 60; i++) { sc.Update(step); sc.Draw(bg.Graphics); }
                System.IO.File.WriteAllText(path + ".txt",
                    "ms per frame: " + (sw.Elapsed.TotalMilliseconds / 60).ToString("0.0", CultureInfo.InvariantCulture));
            }
            sc.Dispose();
        }
    }
}
