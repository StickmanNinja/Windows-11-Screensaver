// The window a screensaver plays in: full screen on a monitor, inside the little preview box
// in Screen Saver Settings, or in a normal window for trying it out.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Screensavers
{
    class SaverForm : Form
    {
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

        readonly SceneHost host;
        readonly Stopwatch clock = Stopwatch.StartNew();
        double last;
        readonly Timer timer = new Timer();
        readonly IntPtr parentHandle = IntPtr.Zero;
        readonly Action quit;
        Point mouseStart = new Point(int.MinValue, 0);

        // Full screen on one monitor. Any key, click or real mouse move calls quit.
        public SaverForm(Rectangle bounds, Settings set, Action quit)
        {
            host = new SceneHost(set, 1.0);
            this.quit = quit;
            Setup();
            Bounds = bounds;
            TopMost = true;
        }

        // Inside the preview box in Screen Saver Settings
        public SaverForm(IntPtr parent, Settings set)
        {
            parentHandle = parent;
            host = new SceneHost(set, 0.5);
            Setup();
        }

        // In a normal window
        public SaverForm(Settings set)
        {
            host = new SceneHost(set, 0.6);
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
            Text = "Screensavers";
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
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
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, Math.Max(0, now - last));
            last = now;
            host.Frame(e.Graphics, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), dt);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            host.Dispose();
            base.OnFormClosed(e);
        }

        void Quit()
        {
            if (quit != null) quit();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (quit == null) return;
            if (mouseStart.X == int.MinValue) { mouseStart = e.Location; return; }
            if (Math.Abs(e.X - mouseStart.X) > 8 || Math.Abs(e.Y - mouseStart.Y) > 8) Quit();
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Quit(); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); Quit(); }
    }
}
