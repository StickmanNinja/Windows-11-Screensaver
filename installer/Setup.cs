// The installer (and uninstaller) for Windows 11 Screensaver.
//
// It carries Screensavers.scr inside itself and installs it for the current user only,
// so it never needs admin rights:
//   %LOCALAPPDATA%\Programs\Windows 11 Screensaver\Screensavers.scr   the screensaver
//                                                 \Screensavers.exe   the same file, opens the picker
//                                                 \Uninstall.exe      a copy of this installer
//   Start menu: "Screensavers" (opens the picker)
//   Settings > Apps > Installed apps: "Windows 11 Screensaver" (Uninstall works from there)
//
//   Setup.exe                    shows the installer window
//   Setup.exe /silent            installs without a window
//   Uninstall.exe /uninstall     asks, then removes everything (add /silent to skip the questions)

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Windows 11 Screensaver Setup")]
[assembly: AssemblyProduct("Windows 11 Screensaver")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace ScreensaverSetup
{
    static class Installer
    {
        public const string AppName = "Windows 11 Screensaver";
        public const string Version = "1.1";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Windows11Screensaver";
        const string SettingsKey = @"Software\Windows 11 Screensaver";

        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Windows 11 Screensaver");
        public static string Scr { get { return Path.Combine(Dir, "Screensavers.scr"); } }
        public static string Exe { get { return Path.Combine(Dir, "Screensavers.exe"); } }
        public static string Uninstaller { get { return Path.Combine(Dir, "Uninstall.exe"); } }
        public static string Shortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Screensavers.lnk"); }
        }

        public static bool IsInstalled { get { return File.Exists(Scr); } }

        public static void Install()
        {
            byte[] scr;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Screensavers.scr"))
            {
                if (s == null) throw new InvalidOperationException("The screensaver is missing from this installer.");
                scr = new byte[s.Length];
                int read = 0;
                while (read < scr.Length)
                {
                    int n = s.Read(scr, read, scr.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
            }

            Directory.CreateDirectory(Dir);
            WriteFile(Scr, scr);
            WriteFile(Exe, scr);
            string me = Path.GetFullPath(Application.ExecutablePath);
            if (!string.Equals(me, Path.GetFullPath(Uninstaller), StringComparison.OrdinalIgnoreCase))
                File.Copy(me, Uninstaller, true);

            try { MakeShortcut(Shortcut, Exe, "Choose your screensaver"); } catch { } // nice to have, not essential

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", AppName);
                k.SetValue("DisplayIcon", Exe);
                k.SetValue("InstallLocation", Dir);
                k.SetValue("UninstallString", "\"" + Uninstaller + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + Uninstaller + "\" /uninstall /silent");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)((scr.Length * 2L + new FileInfo(me).Length) / 1024), RegistryValueKind.DWord);
            }
        }

        static void WriteFile(string path, byte[] data)
        {
            try { File.WriteAllBytes(path, data); }
            catch (IOException)
            {
                throw new IOException("Couldn't replace " + Path.GetFileName(path) +
                                      ". If the screensaver or its picker is open, close it and try again.");
            }
        }

        static void MakeShortcut(string path, string target, string description)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            try
            {
                object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                Type lt = link.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(target) });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { description });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { target + ",0" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                Marshal.ReleaseComObject(link);
            }
            finally { Marshal.ReleaseComObject(shell); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int GetLongPathName(string shortPath, StringBuilder longPath, int size);

        static string LongPath(string p)
        {
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                int n = GetLongPathName(p, sb, sb.Capacity);
                return Path.GetFullPath(n > 0 && n < sb.Capacity ? sb.ToString() : p);
            }
            catch { return p; }
        }

        public static void Uninstall()
        {
            // If Windows is set to use this screensaver, set it back to none (it may store a short 8.3 path)
            using (RegistryKey desk = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
            {
                if (desk != null)
                {
                    string current = desk.GetValue("SCRNSAVE.EXE") as string;
                    if (!string.IsNullOrEmpty(current))
                    {
                        string full = LongPath(current);
                        if (string.Equals(full, LongPath(Scr), StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(full, LongPath(Exe), StringComparison.OrdinalIgnoreCase))
                            desk.DeleteValue("SCRNSAVE.EXE", false);
                    }
                }
            }

            try { if (File.Exists(Shortcut)) File.Delete(Shortcut); } catch { }
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            Registry.CurrentUser.DeleteSubKeyTree(SettingsKey, false);
            try { if (File.Exists(Scr)) File.Delete(Scr); } catch { }
            try { if (File.Exists(Exe)) File.Delete(Exe); } catch { }

            // Uninstall.exe can't delete itself while it runs, so a hidden helper removes the folder a moment after we exit
            if (Directory.Exists(Dir))
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + Dir + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
        }

        // Opens Windows' Screen Saver Settings with this screensaver picked; you choose the wait time and click OK there
        public static void OpenScreenSaverSettings()
        {
            Process.Start("rundll32.exe", "desk.cpl,InstallScreenSaver " + Scr); // no quotes: same as Windows' right-click > Install
        }

        public static void OpenPicker()
        {
            ProcessStartInfo psi = new ProcessStartInfo(Exe);
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Dir;
            Process.Start(psi);
        }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length >= 3 && args[0].ToLowerInvariant() == "/shot")
            {
                using (SetupForm f = new SetupForm()) f.SaveShots(args[1], args[2]);
                return 0;
            }

            bool uninstall = false, silent = false;
            foreach (string a in args)
            {
                string s = a.Trim().TrimStart('/', '-').ToLowerInvariant();
                if (s == "uninstall") uninstall = true;
                else if (s == "silent" || s == "s" || s == "quiet") silent = true;
            }

            if (uninstall)
            {
                if (!silent && MessageBox.Show("Remove " + Installer.AppName + " from this PC?", Installer.AppName,
                                               MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 1;
                try
                {
                    Installer.Uninstall();
                    if (!silent) MessageBox.Show(Installer.AppName + " has been removed.", Installer.AppName,
                                                 MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                catch (Exception ex)
                {
                    if (!silent) MessageBox.Show("Couldn't remove everything: " + ex.Message, Installer.AppName,
                                                 MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 2;
                }
            }

            if (silent)
            {
                try { Installer.Install(); return 0; }
                catch { return 2; }
            }

            Application.Run(new SetupForm());
            return 0;
        }
    }

    // The installer window: dark, Windows 11 style, drawn by hand like the picker
    class SetupForm : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        static readonly Color Bg = Color.FromArgb(32, 32, 32), Text1 = Color.White, Text2 = Color.FromArgb(178, 178, 178);
        static readonly Color Accent = Color.FromArgb(96, 205, 255), AccentHover = Color.FromArgb(122, 214, 255), AccentText = Color.FromArgb(0, 26, 40);
        static readonly Color Button = Color.FromArgb(45, 45, 45), ButtonHover = Color.FromArgb(58, 58, 58), Border = Color.FromArgb(62, 62, 62);
        static readonly Color Tile = Color.FromArgb(43, 43, 43);

        class Btn { public string Id, Label; public bool Primary; public Rectangle R; }

        readonly float k;
        readonly Font fTitle, fBody, fSmall, fButton, fFeature;
        readonly Icon appIcon;
        Bitmap iconBmp;
        bool done;
        Btn[] buttons;
        string hover = "";

        public SetupForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = CreateGraphics()) k = g.DpiX / 96f;
            Text = Installer.AppName + " Setup";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ClientSize = new Size(Px(580), Px(400));
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Icon = appIcon; } catch { }
            try { using (Icon big = new Icon(appIcon, Px(64), Px(64))) iconBmp = big.ToBitmap(); } catch { }

            string display = PickFont("Segoe UI Variable Display"), text = PickFont("Segoe UI Variable Text");
            fTitle = new Font(display, 26 * k, FontStyle.Bold, GraphicsUnit.Pixel);
            fBody = new Font(text, 14 * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fSmall = new Font(text, 12.5f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fButton = new Font(text, 14 * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fFeature = new Font(text, 14 * k, FontStyle.Bold, GraphicsUnit.Pixel);
            ShowWelcome();
        }

        int Px(float v) { return (int)Math.Round(v * k); }
        Rectangle R(float x, float y, float w, float h) { return new Rectangle(Px(x), Px(y), Px(x + w) - Px(x), Px(y + h) - Px(y)); }

        static string PickFont(string name)
        {
            try { using (FontFamily f = new FontFamily(name)) return f.Name; }
            catch { return "Segoe UI"; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                DwmSetWindowAttribute(Handle, 20, ref on, 4);
                int caption = Bg.R | (Bg.G << 8) | (Bg.B << 16);
                DwmSetWindowAttribute(Handle, 35, ref caption, 4);
            }
            catch { }
        }

        Btn B(string id, string label, bool primary, float x, float w)
        {
            Btn b = new Btn();
            b.Id = id; b.Label = label; b.Primary = primary; b.R = R(x, 340, w, 36);
            return b;
        }

        void ShowWelcome()
        {
            done = false;
            buttons = new Btn[] { B("cancel", "Cancel", false, 342, 106), B("install", Installer.IsInstalled ? "Update" : "Install", true, 456, 106) };
            Invalidate();
        }

        void ShowDone()
        {
            done = true;
            buttons = new Btn[] { B("close", "Close", false, 24, 96), B("picker", "Open the picker", false, 248, 150), B("settings", "Set as my screensaver", true, 406, 156) };
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (iconBmp != null) g.DrawImage(iconBmp, Px(28), Px(28), Px(64), Px(64));
            using (SolidBrush b = new SolidBrush(Text1))
                g.DrawString(done ? "All set!" : Installer.AppName, fTitle, b, Px(108), Px(30));
            using (SolidBrush b = new SolidBrush(Text2))
                g.DrawString(done ? "Installed version " + Installer.Version : "Version " + Installer.Version + " · three screensavers and a picker", fSmall, b, Px(110), Px(68));

            if (!done)
            {
                Feature(g, 0, "Superhero", "A caped hero flies over the city at night.");
                Feature(g, 1, "Hacker", "Green code pours down while terminals break in.");
                Feature(g, 2, "Space", "The Earth turns, with the Moon, rockets and comets.");
                using (SolidBrush b = new SolidBrush(Text2))
                    g.DrawString("Installs just for you, in your user folder. No admin rights needed.", fSmall, b, Px(28), Px(300));
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Text1))
                using (StringFormat sf = new StringFormat())
                {
                    g.DrawString("Next, pick it in Windows' Screen Saver Settings and choose how many minutes to wait. " +
                                 "Click Settings… there (or open Screensavers from the Start menu) to choose between Superhero, Hacker and Space.",
                                 fBody, b, new RectangleF(Px(28), Px(122), Px(524), Px(90)), sf);
                }
                using (SolidBrush b = new SolidBrush(Text2))
                using (StringFormat sf = new StringFormat())
                {
                    g.DrawString("To remove it later: Settings › Apps › Installed apps › " + Installer.AppName + " › Uninstall.",
                                 fSmall, b, new RectangleF(Px(28), Px(236), Px(524), Px(40)), sf);
                }
            }

            using (Pen p = new Pen(Border)) g.DrawLine(p, 0, Px(322), ClientSize.Width, Px(322));
            foreach (Btn b in buttons) DrawButton(g, b);
        }

        void Feature(Graphics g, int i, string name, string blurb)
        {
            Rectangle r = R(28, 116 + i * 58, 524, 48);
            using (GraphicsPath p = RoundRect(r, 8 * k))
            using (SolidBrush b = new SolidBrush(Tile))
                g.FillPath(b, p);
            Color[] dots = { Color.FromArgb(214, 40, 40), Color.FromArgb(40, 230, 90), Color.FromArgb(70, 140, 255) };
            using (SolidBrush b = new SolidBrush(dots[i])) g.FillEllipse(b, r.X + Px(16), r.Y + r.Height / 2 - Px(5), Px(10), Px(10));
            using (SolidBrush b = new SolidBrush(Text1)) g.DrawString(name, fFeature, b, r.X + Px(38), r.Y + Px(14));
            using (SolidBrush b = new SolidBrush(Text2)) g.DrawString(blurb, fBody, b, r.X + Px(136), r.Y + Px(14));
        }

        void DrawButton(Graphics g, Btn btn)
        {
            bool hov = hover == btn.Id;
            using (GraphicsPath p = RoundRect(new RectangleF(btn.R.X + 0.5f, btn.R.Y + 0.5f, btn.R.Width - 1, btn.R.Height - 1), 5 * k))
            {
                using (SolidBrush b = new SolidBrush(btn.Primary ? (hov ? AccentHover : Accent) : (hov ? ButtonHover : Button)))
                    g.FillPath(b, p);
                if (!btn.Primary) using (Pen pen = new Pen(Border)) g.DrawPath(pen, p);
            }
            using (StringFormat sf = new StringFormat())
            using (SolidBrush b = new SolidBrush(btn.Primary ? AccentText : Text1))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(btn.Label, fButton, b, btn.R, sf);
            }
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Max(0.1f, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        string HitTest(Point pt)
        {
            foreach (Btn b in buttons) if (b.R.Contains(pt)) return b.Id;
            return "";
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string h = HitTest(e.Location);
            if (h != hover) { hover = h; Cursor = h.Length > 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != "") { hover = ""; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) Do(HitTest(e.Location));
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.Enter) { Do(done ? "settings" : "install"); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Do(string id)
        {
            try
            {
                switch (id)
                {
                    case "cancel":
                    case "close":
                        Close();
                        break;
                    case "install":
                        Cursor = Cursors.WaitCursor;
                        Installer.Install();
                        Cursor = Cursors.Default;
                        ShowDone();
                        break;
                    case "settings":
                        Installer.OpenScreenSaverSettings();
                        Close();
                        break;
                    case "picker":
                        Installer.OpenPicker();
                        Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show(this, ex.Message, Installer.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // For checking the look without installing anything: Setup.exe /shot welcome.png done.png
        public void SaveShots(string welcome, string finished)
        {
            CreateControl();
            using (Bitmap b = new Bitmap(Width, Height))
            {
                DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
                b.Save(welcome, System.Drawing.Imaging.ImageFormat.Png);
                ShowDone();
                DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
                b.Save(finished, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
