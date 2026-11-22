// Hacker: green code pours down the screen. Terminal windows pop up and type out a break-in,
// and every so often a progress bar decrypts the mainframe and ACCESS GRANTED flashes up.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;

namespace Screensavers
{
    class CodeScene : Scene
    {
        const double StepTime = 1 / 30.0;
        const string Glyphs = "0123456789ABCDEF<>{}[]()/\\|=+-*&%$#@!?;:~^";

        class Term
        {
            public RectangleF R;
            public double Start, Life;
            public string Title, Text;
            public bool Hack;
        }

        readonly Random rng;
        readonly Surface rain;      // the falling code, kept between frames and slowly faded
        readonly int cw, ch, cols, rows;
        readonly Bitmap[] gHead, gBody, gDim;
        readonly float[] colY, colSpeed;
        readonly int[] colRow, colEnd, colGlyph;
        double acc, t;

        readonly SolidBrush fade = new SolidBrush(Color.FromArgb(22, 0, 0, 0));
        readonly SolidBrush black = new SolidBrush(Color.Black);
        readonly SolidBrush brush = new SolidBrush(Color.White);
        readonly Pen pen = new Pen(Color.White);
        readonly Font mono, monoBold, big;
        readonly float lineH, charW;

        readonly List<Term> terms = new List<Term>();
        double nextTerm, nextHack, grantedAt = -100;
        bool hackOn;

        static readonly string[] Script =
        {
            "$ ssh root@{ip}",
            "Connecting to {ip}:22 ...",
            "Authenticating .......... OK",
            "$ ./scan --deep --ports 1-65535",
            "PORT      STATE   SERVICE",
            "22/tcp    open    ssh",
            "443/tcp   open    https",
            "8080/tcp  open    http-proxy",
            "Bypassing firewall ...... done",
            "for (int i = 0; i < nodes.Length; i++)",
            "{",
            "    var key = Crack(nodes[i], 0x{hex});",
            "    if (key != null) Upload(key);",
            "}",
            "Injecting payload 0x{hex}",
            "Tracing route: {n} hops",
            "Rerouting through {ip}",
            "Uplink established",
            "Cipher: AES-256-GCM",
            "Handshake complete",
            "Compiling exploit.c ...... OK",
            "Downloading /vault/blueprints  {p}%",
            "0x{hex}  {dump}",
            "0x{hex}  {dump}",
            "0x{hex}  {dump}",
            "Covering tracks .......... done",
            "$ whoami",
            "root",
        };

        static readonly string[] Titles = { "root@mainframe:~", "uplink.sh", "nmap", "sat-link", "tracer", "kernel.log", "proxy-chain" };

        float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        public CodeScene(int w, int h, int seed) : base(w, h)
        {
            rng = new Random(seed);
            ch = (int)Math.Round(Math.Max(8f, 22 * S));
            cw = (int)Math.Round(ch * 0.62f);
            cols = (W + cw - 1) / cw;
            rows = (H + ch - 1) / ch;

            rain = new Surface(W, H);
            rain.G.Clear(Color.Black);
            gHead = MakeGlyphs(Color.FromArgb(225, 255, 230));
            gBody = MakeGlyphs(Color.FromArgb(40, 235, 90));
            gDim = MakeGlyphs(Color.FromArgb(22, 150, 55));

            colY = new float[cols]; colSpeed = new float[cols];
            colRow = new int[cols]; colEnd = new int[cols]; colGlyph = new int[cols];
            for (int c = 0; c < cols; c++)
            {
                Restart(c);
                colRow[c] = rng.Next(-rows / 2, rows);
                colY[c] = colRow[c];
            }

            mono = new Font("Consolas", Math.Max(7f, 15 * S), FontStyle.Regular, GraphicsUnit.Pixel);
            monoBold = new Font("Consolas", Math.Max(7f, 15 * S), FontStyle.Bold, GraphicsUnit.Pixel);
            big = new Font("Consolas", Math.Max(10f, 70 * S), FontStyle.Bold, GraphicsUnit.Pixel);
            lineH = mono.Size * 1.32f;
            charW = mono.Size * 0.55f;

            for (int i = 0; i < 45; i++) Tick(); // start with the screen already full of code
            nextTerm = 1.2;
            nextHack = R(14, 22);
        }

        Bitmap[] MakeGlyphs(Color c)
        {
            Bitmap[] set = new Bitmap[Glyphs.Length];
            using (Font f = new Font("Consolas", ch * 0.8f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush b = new SolidBrush(c))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < Glyphs.Length; i++)
                {
                    set[i] = new Bitmap(cw, ch, PixelFormat.Format32bppPArgb);
                    using (Graphics g = Graphics.FromImage(set[i]))
                    {
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        g.DrawString(Glyphs[i].ToString(), f, b, new RectangleF(0, 0, cw, ch), sf);
                    }
                }
            }
            return set;
        }

        void Restart(int c)
        {
            colRow[c] = -rng.Next(0, rows / 2 + 1);
            colY[c] = colRow[c];
            colSpeed[c] = R(0.3f, 1f);
            colEnd[c] = rows + rng.Next(0, 8);
        }

        void Cell(Bitmap[] set, int c, int r, int glyph)
        {
            int x = c * cw, y = r * ch;
            rain.G.FillRectangle(black, x, y, cw, ch);
            rain.G.DrawImageUnscaled(set[glyph], x, y);
        }

        // One step of the rain: everything fades a little, and each column's bright head moves down.
        void Tick()
        {
            rain.G.FillRectangle(fade, 0, 0, W, H);
            for (int c = 0; c < cols; c++)
            {
                colY[c] += colSpeed[c];
                int target = (int)Math.Floor(colY[c]);
                while (colRow[c] < target)
                {
                    int prev = colRow[c];
                    colRow[c]++;
                    if (prev >= 0 && prev < rows) Cell(gBody, c, prev, colGlyph[c]);
                    colGlyph[c] = rng.Next(Glyphs.Length);
                    if (colRow[c] >= 0 && colRow[c] < rows) Cell(gHead, c, colRow[c], colGlyph[c]);
                }
                if (colRow[c] > colEnd[c]) Restart(c);
            }
            // A few characters just behind the heads change as you watch
            for (int k = 0; k < cols / 5; k++)
            {
                int c = rng.Next(cols);
                int back = rng.Next(2, 7);
                int r = colRow[c] - back;
                if (r >= 0 && r < rows) Cell(back >= 4 ? gDim : gBody, c, r, rng.Next(Glyphs.Length));
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            acc += dt;
            int n = 0;
            while (acc >= StepTime && n < 4) { acc -= StepTime; Tick(); n++; }
            if (acc > StepTime) acc = 0;

            for (int i = terms.Count - 1; i >= 0; i--)
            {
                Term m = terms[i];
                if (t - m.Start > m.Life)
                {
                    terms.RemoveAt(i);
                    if (m.Hack) { hackOn = false; grantedAt = t; nextHack = t + R(30, 45); }
                }
            }

            int normal = 0;
            foreach (Term m in terms) if (!m.Hack) normal++;
            if (t > nextTerm && normal < 2 && !InGranted())
            {
                float tw = Math.Min(W * 0.9f, Math.Max(W * R(0.28f, 0.36f), 42 * charW));
                float th = Math.Min(H * 0.8f, Math.Max(H * R(0.26f, 0.34f), 12 * lineH));
                RectangleF spot = FindSpot(tw, th);
                if (!spot.IsEmpty)
                {
                    Term m = new Term();
                    m.R = spot; m.Start = t; m.Life = R(9, 14);
                    m.Title = Titles[rng.Next(Titles.Length)];
                    m.Text = MakeSession();
                    terms.Add(m);
                }
                nextTerm = t + R(2.5f, 6f);
            }

            if (!hackOn && t > nextHack && !InGranted())
            {
                float tw = Math.Min(W * 0.9f, Math.Max(W * 0.36f, 44 * charW)), th = Math.Min(H * 0.6f, Math.Max(H * 0.22f, 9 * lineH));
                Term m = new Term();
                m.R = new RectangleF((W - tw) / 2, (H - th) / 2, tw, th);
                m.Start = t; m.Life = 7.5; m.Hack = true;
                m.Title = "breach.exe";
                m.Text = "Target: MAINFRAME " + Ip();
                terms.Add(m);
                hackOn = true;
            }
        }

        bool InGranted() { return t - grantedAt < 3; }

        RectangleF FindSpot(float tw, float th)
        {
            float pad = 16 * S;
            for (int tries = 0; tries < 12; tries++)
            {
                RectangleF r = new RectangleF(R(pad, Math.Max(pad, W - tw - pad)), R(pad, Math.Max(pad, H - th - pad)), tw, th);
                bool ok = true;
                foreach (Term m in terms)
                {
                    RectangleF o = m.R;
                    o.Inflate(pad, pad);
                    if (o.IntersectsWith(r)) { ok = false; break; }
                }
                if (ok) return r;
            }
            return RectangleF.Empty;
        }

        string Ip() { return rng.Next(10, 223) + "." + rng.Next(0, 255) + "." + rng.Next(0, 255) + "." + rng.Next(1, 254); }
        string Hex(int n) { StringBuilder sb = new StringBuilder(); for (int i = 0; i < n; i++) sb.Append("0123456789ABCDEF"[rng.Next(16)]); return sb.ToString(); }

        string MakeSession()
        {
            StringBuilder sb = new StringBuilder();
            int n = rng.Next(14, 24);
            int start = rng.Next(Script.Length);
            for (int k = 0; k < n; k++)
            {
                string line = Script[(start + k) % Script.Length];
                line = line.Replace("{ip}", Ip()).Replace("{hex}", Hex(4)).Replace("{n}", rng.Next(5, 19).ToString())
                           .Replace("{p}", rng.Next(12, 100).ToString());
                if (line.Contains("{dump}"))
                {
                    StringBuilder d = new StringBuilder();
                    for (int i = 0; i < 8; i++) d.Append(Hex(2)).Append(' ');
                    line = line.Replace("{dump}", d.ToString().TrimEnd());
                }
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        public override void Draw(Graphics g)
        {
            rain.DrawTo(g, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            foreach (Term m in terms) DrawTerm(g, m);
            if (InGranted()) DrawGranted(g);
            g.SmoothingMode = SmoothingMode.None;
        }

        static int A(int a, float f) { return Math.Max(0, Math.Min(255, (int)(a * f))); }

        void DrawTerm(Graphics g, Term m)
        {
            float age = (float)(t - m.Start);
            float f = Shapes.Clamp01(Math.Min(age / 0.35f, (float)(m.Life - age) / 0.5f));
            if (f <= 0) return;
            RectangleF r = m.R;
            float bar = Math.Max(10, 26 * S), pad = 12 * S;

            using (GraphicsPath p = Shapes.RoundRect(r, 8 * S))
            {
                brush.Color = Color.FromArgb(A(228, f), 2, 12, 5);
                g.FillPath(brush, p);
                brush.Color = Color.FromArgb(A(255, f), 8, 36, 16);
                g.SetClip(p);
                g.FillRectangle(brush, r.X, r.Y, r.Width, bar);
                g.ResetClip();
                pen.Color = Color.FromArgb(A(200, f), 40, 220, 90);
                pen.Width = Math.Max(1, 1.5f * S);
                g.DrawPath(pen, p);
            }
            Color[] dots = { Color.FromArgb(255, 95, 86), Color.FromArgb(255, 189, 46), Color.FromArgb(39, 201, 63) };
            float dr = 4.5f * S;
            for (int i = 0; i < 3; i++)
            {
                brush.Color = Color.FromArgb(A(210, f), dots[i]);
                g.FillEllipse(brush, r.X + pad + i * 15 * S, r.Y + bar / 2 - dr, dr * 2, dr * 2);
            }
            brush.Color = Color.FromArgb(A(230, f), 130, 255, 160);
            g.DrawString(m.Title, monoBold, brush, r.X + pad + 52 * S, r.Y + (bar - monoBold.Size * 1.2f) / 2);

            float x0 = r.X + pad, y0 = r.Y + bar + 8 * S;
            int maxLines = Math.Max(1, (int)((r.Height - bar - 14 * S) / lineH));
            Color text = Color.FromArgb(A(235, f), 70, 245, 115);
            g.SetClip(r);

            if (!m.Hack)
            {
                int chars = Math.Min(m.Text.Length, (int)Math.Max(0, (age - 0.4f) * 55));
                string[] lines = m.Text.Substring(0, chars).Split('\n');
                int first = Math.Max(0, lines.Length - maxLines);
                brush.Color = text;
                for (int i = first; i < lines.Length; i++)
                    g.DrawString(lines[i], mono, brush, x0, y0 + (i - first) * lineH);
                if ((int)(t * 2.5) % 2 == 0)
                {
                    string lastLine = lines[lines.Length - 1];
                    g.FillRectangle(brush, x0 + lastLine.Length * charW + 2 * S, y0 + (lines.Length - 1 - first) * lineH + 2 * S, charW, mono.Size);
                }
            }
            else
            {
                float prog = Shapes.Clamp01((age - 0.5f) / (float)(m.Life - 2.0));
                brush.Color = text;
                g.DrawString(m.Text, mono, brush, x0, y0);
                g.DrawString(prog < 1 ? "DECRYPTING " + new string('.', 1 + (int)(t * 3) % 3) : "DECRYPTED", monoBold, brush, x0, y0 + lineH * 1.3f);

                RectangleF pb = new RectangleF(x0, y0 + lineH * 2.8f, r.Width - pad * 2, lineH * 1.1f);
                pen.Color = text;
                g.DrawRectangle(pen, pb.X, pb.Y, pb.Width, pb.Height);
                brush.Color = Color.FromArgb(A(230, f), 40, 230, 90);
                g.FillRectangle(brush, pb.X + 3 * S, pb.Y + 3 * S, (pb.Width - 6 * S) * prog, pb.Height - 6 * S);
                brush.Color = Color.FromArgb(A(255, f), 220, 255, 225);
                string pct = ((int)(prog * 100)).ToString() + "%";
                g.DrawString(pct, monoBold, brush, pb.Right - charW * (pct.Length + 1), pb.Y + (pb.Height - monoBold.Size * 1.2f) / 2);

                Random fake = new Random((int)(t * 12));
                brush.Color = Color.FromArgb(A(150, f), 40, 200, 80);
                for (int i = 0; i < maxLines - 4; i++)
                {
                    StringBuilder d = new StringBuilder("0x");
                    for (int j = 0; j < 4; j++) d.Append("0123456789ABCDEF"[fake.Next(16)]);
                    d.Append(' ');
                    for (int j = 0; j < 12; j++) d.Append(' ').Append("0123456789ABCDEF"[fake.Next(16)]).Append("0123456789ABCDEF"[fake.Next(16)]);
                    g.DrawString(d.ToString(), mono, brush, x0, pb.Bottom + 8 * S + i * lineH);
                }
            }
            g.ResetClip();
        }

        void DrawGranted(Graphics g)
        {
            float age = (float)(t - grantedAt);
            float f = Shapes.Clamp01(Math.Min(age / 0.15f, (3 - age) / 0.4f));
            if (age < 0.7f && (int)(age * 10) % 2 == 1) f *= 0.35f; // flickers on
            const string msg = "ACCESS GRANTED";
            SizeF sz = g.MeasureString(msg, big);
            RectangleF box = new RectangleF((W - sz.Width) / 2 - 40 * S, (H - sz.Height) / 2 - 22 * S, sz.Width + 80 * S, sz.Height + 44 * S);
            using (GraphicsPath p = Shapes.RoundRect(box, 10 * S))
            {
                brush.Color = Color.FromArgb(A(235, f), 0, 18, 7);
                g.FillPath(brush, p);
                pen.Color = Color.FromArgb(A(255, f), 60, 255, 120);
                pen.Width = Math.Max(1.5f, 3 * S);
                g.DrawPath(pen, p);
            }
            float tx = (W - sz.Width) / 2, ty = (H - sz.Height) / 2;
            brush.Color = Color.FromArgb(A(55, f), 60, 255, 120);
            float o = 3 * S;
            g.DrawString(msg, big, brush, tx - o, ty);
            g.DrawString(msg, big, brush, tx + o, ty);
            g.DrawString(msg, big, brush, tx, ty - o);
            g.DrawString(msg, big, brush, tx, ty + o);
            brush.Color = Color.FromArgb(A(255, f), 150, 255, 175);
            g.DrawString(msg, big, brush, tx, ty);
        }

        public override void Dispose()
        {
            rain.Dispose();
            foreach (Bitmap b in gHead) b.Dispose();
            foreach (Bitmap b in gBody) b.Dispose();
            foreach (Bitmap b in gDim) b.Dispose();
            fade.Dispose(); black.Dispose(); brush.Dispose(); pen.Dispose();
            mono.Dispose(); monoBold.Dispose(); big.Dispose();
        }
    }
}
