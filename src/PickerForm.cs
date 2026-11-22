// The picker: a dark, Windows 11-style window for choosing the screensaver.
// A big live preview, a card per screensaver (each with its own little live preview),
// Shuffle, speed, and buttons to watch it full screen or set it as the Windows screensaver.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Screensavers
{
    class PickerForm : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        static readonly Color Bg = Color.FromArgb(32, 32, 32), Card = Color.FromArgb(43, 43, 43), CardHover = Color.FromArgb(50, 50, 50);
        static readonly Color Border = Color.FromArgb(62, 62, 62), Text1 = Color.White, Text2 = Color.FromArgb(178, 178, 178);
        static readonly Color Accent = Color.FromArgb(96, 205, 255), AccentHover = Color.FromArgb(122, 214, 255), AccentText = Color.FromArgb(0, 26, 40);
        static readonly Color Button = Color.FromArgb(45, 45, 45), ButtonHover = Color.FromArgb(58, 58, 58), Well = Color.FromArgb(30, 30, 30);

        readonly Settings set;          // what you're choosing (saved on Save)
        readonly Settings previewSet;   // the big preview: the selected scene, never shuffled
        readonly float k;               // DPI scale
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        double last;

        SceneHost bigHost;
        readonly Bitmap bigBmp;
        readonly Scene[] thumbs;
        readonly Bitmap[] thumbBmps;

        readonly Font fTitle, fSub, fName, fBig, fBody, fSmall, fButton, fSection;
        readonly Rectangle rBig, rShuffle, rToggle, rSpeedRow, rSpeed, rPreview, rInstall, rCancel, rSave;
        readonly Rectangle[] rCards, rThumbs, rSeg;
        string hover = "";
        readonly List<SaverForm> fullForms = new List<SaverForm>();
        readonly SolidBrush brush = new SolidBrush(Color.White);
        readonly Pen pen = new Pen(Color.White);
        readonly Icon icon;

        static readonly string[] SpeedNames = { "Slow", "Normal", "Fast" };

        public PickerForm(Settings saved)
        {
            set = saved.Clone();
            previewSet = saved.Clone();
            previewSet.Shuffle = false;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = CreateGraphics()) k = g.DpiX / 96f;
            Text = "Screensavers";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            KeyPreview = true;
            ClientSize = new Size(Px(1000), Px(660));
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { icon = null; }
            if (icon == null) icon = MakeIcon();
            Icon = icon;

            string display = PickFont("Segoe UI Variable Display"), text = PickFont("Segoe UI Variable Text");
            fTitle = new Font(display, 28 * k, FontStyle.Bold, GraphicsUnit.Pixel);
            fSub = new Font(text, 14 * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fBig = new Font(display, 20 * k, FontStyle.Bold, GraphicsUnit.Pixel);
            fName = new Font(text, 15 * k, FontStyle.Bold, GraphicsUnit.Pixel);
            fBody = new Font(text, 13.5f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fSmall = new Font(text, 12.5f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fButton = new Font(text, 14 * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fSection = new Font(text, 14 * k, FontStyle.Bold, GraphicsUnit.Pixel);

            rBig = R(28, 92, 600, 338);
            int n = Scenes.All.Length;
            rCards = new Rectangle[n];
            rThumbs = new Rectangle[n];
            for (int i = 0; i < n; i++)
            {
                rCards[i] = R(652, 118 + i * 106, 320, 96);
                rThumbs[i] = R(664, 130 + i * 106, 128, 72);
            }
            rShuffle = R(652, 446, 320, 56);
            rToggle = R(652 + 320 - 16 - 44, 446 + 17, 44, 22);
            rSpeedRow = R(652, 512, 320, 56);
            rSpeed = R(652 + 320 - 16 - 186, 512 + 12, 186, 32);
            rSeg = new Rectangle[3];
            for (int j = 0; j < 3; j++) rSeg[j] = R(652 + 320 - 16 - 186 + j * 62, 512 + 12, 62, 32);
            rPreview = R(28, 606, 176, 36);
            rInstall = R(214, 606, 236, 36);
            rCancel = R(742, 606, 110, 36);
            rSave = R(862, 606, 110, 36);

            bigBmp = new Bitmap(rBig.Width, rBig.Height, PixelFormat.Format32bppRgb);
            bigHost = new SceneHost(previewSet, 0.35);
            thumbs = new Scene[n];
            thumbBmps = new Bitmap[n];
            for (int i = 0; i < n; i++)
            {
                thumbBmps[i] = new Bitmap(rThumbs[i].Width, rThumbs[i].Height, PixelFormat.Format32bppRgb);
                thumbs[i] = Scenes.All[i].Make(rThumbs[i].Width, rThumbs[i].Height, 100 + i);
                thumbs[i].Update(1.5); // so each little preview starts with something going on
            }

            timer.Interval = 30;
            timer.Tick += delegate { Invalidate(); };
            timer.Start();
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
                DwmSetWindowAttribute(Handle, 20, ref on, 4);            // dark title bar
                int caption = Bg.R | (Bg.G << 8) | (Bg.B << 16);
                DwmSetWindowAttribute(Handle, 35, ref caption, 4);       // title bar the same grey as the window
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        int Selected { get { return Scenes.IndexOf(set.Scene); } }

        // ---------- drawing ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, Math.Max(0, now - last));
            last = now;

            using (Graphics bg = Graphics.FromImage(bigBmp)) bigHost.Frame(bg, bigBmp.Width, bigBmp.Height, dt);
            for (int i = 0; i < thumbs.Length; i++)
            {
                thumbs[i].Update(dt * set.SpeedFactor);
                using (Graphics tg = Graphics.FromImage(thumbBmps[i])) thumbs[i].Draw(tg);
            }

            Graphics g = e.Graphics;
            g.Clear(Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Label(g, "Screensavers", fTitle, Text1, 28, 20);
            Label(g, "Pick what plays when your PC is idle. Everything here is moving: these are live previews.", fSub, Text2, 28, 60);

            Image(g, bigBmp, rBig, 10, Bg);
            SceneInfo info = Scenes.All[Selected];
            Label(g, info.Name, fBig, Text1, 28, 444);
            using (StringFormat sf = new StringFormat())
            {
                brush.Color = Text2;
                g.DrawString(info.Blurb, fBody, brush, new RectangleF(Px(28), Px(474), Px(600), Px(40)), sf);
            }
            if (set.Shuffle)
                Label(g, "Shuffle is on: they all take turns, switching every 3 minutes.", fBody, Accent, 28, 520);

            Label(g, "Choose a screensaver", fSection, Text1, 652, 90);
            for (int i = 0; i < rCards.Length; i++) DrawCard(g, i);

            Panel(g, rShuffle, hover == "shuffle" ? CardHover : Card);
            Label(g, "Shuffle", fName, Text1, 668, 454);
            Label(g, "Take turns, switching every 3 minutes", fSmall, Text2, 668, 476);
            DrawToggle(g, rToggle, set.Shuffle);

            Panel(g, rSpeedRow, Card);
            Label(g, "Speed", fName, Text1, 668, 530);
            DrawSegments(g);

            pen.Color = Border; pen.Width = 1;
            g.DrawLine(pen, 0, Px(588), ClientSize.Width, Px(588));
            DrawButton(g, rPreview, "Preview full screen", false, hover == "preview");
            DrawButton(g, rInstall, "Set as Windows screensaver", false, hover == "install");
            DrawButton(g, rCancel, "Cancel", false, hover == "cancel");
            DrawButton(g, rSave, "Save", true, hover == "save");
        }

        void Label(Graphics g, string s, Font f, Color c, float x, float y)
        {
            brush.Color = c;
            g.DrawString(s, f, brush, Px(x), Px(y));
        }

        void Panel(Graphics g, Rectangle r, Color fill)
        {
            using (GraphicsPath p = Shapes.RoundRect(r, 8 * k))
            {
                brush.Color = fill;
                g.FillPath(brush, p);
            }
        }

        // A picture with rounded corners: draw it, then paint the corners in the colour behind it
        void Image(Graphics g, Bitmap img, Rectangle r, float rad, Color behind)
        {
            g.DrawImageUnscaled(img, r.X, r.Y);
            using (GraphicsPath mask = new GraphicsPath())
            {
                mask.FillMode = FillMode.Alternate;
                Rectangle outer = r;
                outer.Inflate(1, 1);
                mask.AddRectangle(outer);
                mask.AddPath(Shapes.RoundRect(r, rad * k), false);
                brush.Color = behind;
                g.FillPath(brush, mask);
            }
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad * k))
            {
                pen.Color = Color.FromArgb(40, 255, 255, 255); pen.Width = 1;
                g.DrawPath(pen, p);
            }
        }

        void DrawCard(Graphics g, int i)
        {
            Rectangle r = rCards[i];
            bool sel = i == Selected, hov = hover == "card" + i;
            Color fill = hov ? CardHover : Card;
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), 8 * k))
            {
                brush.Color = fill;
                g.FillPath(brush, p);
                pen.Color = sel ? Accent : Border;
                pen.Width = sel ? 2 * k : 1;
                g.DrawPath(pen, p);
            }
            Image(g, thumbBmps[i], rThumbs[i], 6, fill);
            SceneInfo info = Scenes.All[i];
            brush.Color = Text1;
            g.DrawString(info.Name, fName, brush, r.X + Px(140), r.Y + Px(17));
            using (StringFormat sf = new StringFormat())
            {
                sf.Trimming = StringTrimming.EllipsisWord;
                brush.Color = Text2;
                g.DrawString(info.Tagline, fSmall, brush, new RectangleF(r.X + Px(140), r.Y + Px(40), Px(168), Px(40)), sf);
            }
            if (sel)
            {
                float cr = 9 * k, ccx = r.Right - Px(16), ccy = r.Y + Px(19);
                brush.Color = Accent;
                g.FillEllipse(brush, ccx - cr, ccy - cr, cr * 2, cr * 2);
                using (Pen check = new Pen(AccentText, 1.8f * k))
                {
                    check.StartCap = check.EndCap = LineCap.Round;
                    g.DrawLines(check, new PointF[] { new PointF(ccx - 4 * k, ccy), new PointF(ccx - 1 * k, ccy + 3 * k), new PointF(ccx + 4.5f * k, ccy - 3 * k) });
                }
            }
        }

        void DrawToggle(Graphics g, Rectangle r, bool on)
        {
            float rad = r.Height / 2f;
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad))
            {
                if (on) { brush.Color = Accent; g.FillPath(brush, p); }
                else { pen.Color = Text2; pen.Width = 1.2f * k; g.DrawPath(pen, p); }
            }
            float kr = on ? rad * 0.6f : rad * 0.5f;
            float kx = on ? r.Right - rad : r.X + rad;
            brush.Color = on ? AccentText : Text2;
            g.FillEllipse(brush, kx - kr, r.Y + rad - kr, kr * 2, kr * 2);
        }

        void DrawSegments(Graphics g)
        {
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(rSpeed.X + 0.5f, rSpeed.Y + 0.5f, rSpeed.Width - 1, rSpeed.Height - 1), 6 * k))
            {
                brush.Color = Well; g.FillPath(brush, p);
                pen.Color = Border; pen.Width = 1; g.DrawPath(pen, p);
            }
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                for (int j = 0; j < 3; j++)
                {
                    RectangleF s = rSeg[j];
                    s.Inflate(-3 * k, -3 * k);
                    bool on = set.Speed == j;
                    if (on || hover == "speed" + j)
                    {
                        using (GraphicsPath p = Shapes.RoundRect(s, 4 * k))
                        {
                            brush.Color = on ? Accent : ButtonHover;
                            g.FillPath(brush, p);
                        }
                    }
                    brush.Color = on ? AccentText : Text1;
                    g.DrawString(SpeedNames[j], fSmall, brush, rSeg[j], sf);
                }
            }
        }

        void DrawButton(Graphics g, Rectangle r, string label, bool primary, bool hov)
        {
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), 5 * k))
            {
                brush.Color = primary ? (hov ? AccentHover : Accent) : (hov ? ButtonHover : Button);
                g.FillPath(brush, p);
                if (!primary) { pen.Color = Border; pen.Width = 1; g.DrawPath(pen, p); }
            }
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                brush.Color = primary ? AccentText : Text1;
                g.DrawString(label, fButton, brush, r, sf);
            }
        }

        // ---------- clicking and keys ----------

        string HitTest(Point pt)
        {
            for (int i = 0; i < rCards.Length; i++) if (rCards[i].Contains(pt)) return "card" + i;
            if (rShuffle.Contains(pt)) return "shuffle";
            for (int j = 0; j < 3; j++) if (rSeg[j].Contains(pt)) return "speed" + j;
            if (rPreview.Contains(pt)) return "preview";
            if (rInstall.Contains(pt)) return "install";
            if (rCancel.Contains(pt)) return "cancel";
            if (rSave.Contains(pt)) return "save";
            return "";
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            hover = HitTest(e.Location);
            Cursor = hover.Length > 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = "";
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            string hit = HitTest(e.Location);
            if (hit.StartsWith("card")) Select(int.Parse(hit.Substring(4)));
            else if (hit.StartsWith("speed")) { set.Speed = int.Parse(hit.Substring(5)); previewSet.Speed = set.Speed; }
            else if (hit == "shuffle") set.Shuffle = !set.Shuffle;
            else if (hit == "preview") StartFullPreview();
            else if (hit == "install") Install();
            else if (hit == "cancel") Close();
            else if (hit == "save") { if (Save()) Close(); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            int n = Scenes.All.Length;
            switch (keyData)
            {
                case Keys.Escape: Close(); return true;
                case Keys.Enter: if (Save()) Close(); return true;
                case Keys.Up: case Keys.Left: Select((Selected + n - 1) % n); return true;
                case Keys.Down: case Keys.Right: Select((Selected + 1) % n); return true;
                case Keys.Space: set.Shuffle = !set.Shuffle; return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Select(int i)
        {
            if (Scenes.All[i].Id == set.Scene) return;
            set.Scene = Scenes.All[i].Id;
            previewSet.Scene = set.Scene;
            bigHost.Dispose();
            bigHost = new SceneHost(previewSet, 0.35);
        }

        bool Save()
        {
            try { set.Save(); return true; }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't save your choice: " + ex.Message, "Screensavers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        void StartFullPreview()
        {
            if (fullForms.Count > 0) return;
            timer.Stop();
            hover = "";
            Settings s = set.Clone();
            Cursor.Hide();
            foreach (Screen sc in Screen.AllScreens)
            {
                SaverForm f = new SaverForm(sc.Bounds, s, delegate { BeginInvoke((MethodInvoker)EndFullPreview); });
                fullForms.Add(f);
                f.Show();
            }
        }

        void EndFullPreview()
        {
            if (fullForms.Count == 0) return;
            List<SaverForm> list = new List<SaverForm>(fullForms);
            fullForms.Clear();
            foreach (SaverForm f in list) f.Close();
            Cursor.Show();
            last = clock.Elapsed.TotalSeconds;
            timer.Start();
            Activate();
        }

        // Saves the choice, then opens Windows' own Screen Saver Settings with this screensaver picked,
        // where you choose the wait time and click OK.
        void Install()
        {
            if (!Save()) return;
            string scr = FindScr();
            if (scr == null)
            {
                MessageBox.Show(this, "Couldn't find Screensavers.scr. Run the installer again, or right-click Screensavers.scr and choose Install.",
                                "Screensavers", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { OpenScreenSaverSettings(scr); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't open Screen Saver Settings: " + ex.Message, "Screensavers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // The picker may be running from Screensavers.exe (the Start menu shortcut), so look for the .scr:
        // this file itself, the .scr next to it, or where the installer puts it. As a last resort, make one next to us.
        public static string FindScr()
        {
            string me = Application.ExecutablePath;
            if (me.EndsWith(".scr", StringComparison.OrdinalIgnoreCase)) return me;
            string dir = System.IO.Path.GetDirectoryName(me);
            string[] candidates =
            {
                System.IO.Path.Combine(dir, "Screensavers.scr"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       @"Programs\Windows 11 Screensaver\Screensavers.scr"),
            };
            foreach (string c in candidates) if (System.IO.File.Exists(c)) return c;
            try
            {
                string copy = candidates[0];
                System.IO.File.Copy(me, copy, true);
                return copy;
            }
            catch { return null; }
        }

        // Same command Windows uses for right-click > Install (the path goes in without quotes)
        public static void OpenScreenSaverSettings(string scr)
        {
            Process.Start("rundll32.exe", "desk.cpl,InstallScreenSaver " + scr);
        }

        static Icon MakeIcon()
        {
            using (Bitmap b = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath p = Shapes.RoundRect(new RectangleF(1, 1, 30, 30), 7))
                    using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(30, 48, 120), Color.FromArgb(10, 14, 40), 90f))
                        g.FillPath(lg, p);
                    using (SolidBrush moon = new SolidBrush(Color.FromArgb(255, 226, 140))) g.FillEllipse(moon, 7, 6, 17, 17);
                    using (SolidBrush cut = new SolidBrush(Color.FromArgb(24, 38, 98))) g.FillEllipse(cut, 12, 3, 16, 16);
                    using (SolidBrush star = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(star, 22, 22, 3, 3);
                        g.FillEllipse(star, 9, 25, 2, 2);
                        g.FillEllipse(star, 25, 9, 2, 2);
                    }
                }
                return System.Drawing.Icon.FromHandle(b.GetHicon());
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            bigHost.Dispose();
            bigBmp.Dispose();
            foreach (Scene s in thumbs) s.Dispose();
            foreach (Bitmap b in thumbBmps) b.Dispose();
            brush.Dispose(); pen.Dispose();
            base.OnFormClosed(e);
        }
    }
}
