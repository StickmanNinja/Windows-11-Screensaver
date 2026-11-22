// Space: the Earth turns slowly in front of the Milky Way, with drifting clouds and city lights
// on the night side. The Moon and satellites orbit it, a space station drifts past, comets
// streak by, rockets launch from the surface, and now and then an astronaut floats across.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Screensavers
{
    class SpaceScene : Scene
    {
        // ---------- the Earth's map, made once from noise and shared by every SpaceScene ----------

        const int MapW = 1536, MapH = 768;
        static int[] dayMap;     // colour; alpha 254 marks ocean (for the sun glint)
        static byte[] cityMap, cloudMap;
        static readonly object mapLock = new object();

        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        static float Noise(float x, float y, float z, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y), zi = (int)Math.Floor(z);
            float xf = x - xi, yf = y - yi, zf = z - zi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf), w = zf * zf * (3 - 2 * zf);
            float a = Hash(xi, yi, zi, seed), b = Hash(xi + 1, yi, zi, seed);
            float c = Hash(xi, yi + 1, zi, seed), d = Hash(xi + 1, yi + 1, zi, seed);
            float e = Hash(xi, yi, zi + 1, seed), f = Hash(xi + 1, yi, zi + 1, seed);
            float gg = Hash(xi, yi + 1, zi + 1, seed), hh = Hash(xi + 1, yi + 1, zi + 1, seed);
            float x1 = a + (b - a) * u, x2 = c + (d - c) * u, x3 = e + (f - e) * u, x4 = gg + (hh - gg) * u;
            float y1 = x1 + (x2 - x1) * v, y2 = x3 + (x4 - x3) * v;
            return y1 + (y2 - y1) * w;
        }

        static float Fbm(float x, float y, float z, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, freq = 1, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Noise(x * freq, y * freq, z * freq, seed + i * 17);
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / norm;
        }

        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        static void BuildMaps()
        {
            lock (mapLock)
            {
                if (dayMap != null) return;
                int n = MapW * MapH;
                float[] hs = new float[n], ms = new float[n], cs = new float[n], cis = new float[n], lats = new float[n];
                System.Threading.Tasks.Parallel.For(0, MapH, delegate(int y) // every CPU core takes some rows
                {
                    double lat = (0.5 - (y + 0.5) / MapH) * Math.PI;
                    float cl = (float)Math.Cos(lat), sl = (float)Math.Sin(lat);
                    for (int x = 0; x < MapW; x++)
                    {
                        double lon = ((x + 0.5) / MapW - 0.5) * 2 * Math.PI;
                        float px = cl * (float)Math.Sin(lon), py = sl, pz = cl * (float)Math.Cos(lon);
                        int i = y * MapW + x;
                        hs[i] = Fbm(px * 1.7f + 3, py * 1.7f, pz * 1.7f, 7, 11);
                        cs[i] = Fbm(px * 2.6f, py * 5.0f, pz * 2.6f + 5, 5, 37);
                        if (hs[i] > 0.5f) // only land needs these
                        {
                            ms[i] = Fbm(px * 2.3f, py * 2.3f + 7, pz * 2.3f, 3, 23);
                            cis[i] = Fbm(px * 14, py * 14, pz * 14, 2, 51);
                        }
                        else ms[i] = 0.5f;
                        lats[i] = (float)(Math.Abs(lat) * 180 / Math.PI);
                    }
                });
                float[] sample = new float[n / 7];
                for (int i = 0; i < sample.Length; i++) sample[i] = hs[i * 7];
                Array.Sort(sample);
                float sea = sample[(int)(sample.Length * 0.68)], top = sample[sample.Length - 1];

                int[] day = new int[n];
                byte[] city = new byte[n], cloud = new byte[n];
                for (int i = 0; i < n; i++)
                {
                    float h = hs[i], m = ms[i];

                    // Sea: lighter near the coast, deep blue far out
                    float d = Shapes.Clamp01((sea - h) / 0.12f);
                    float r = Lerp(38, 6, d), g = Lerp(104, 30, d), b = Lerp(172, 82, d);

                    // Land: beach, then dry or green depending on rain, then mountains with snowy peaks
                    float e = Math.Max(0, (h - sea) / (top - sea));
                    float wet = Shapes.Clamp01((m - 0.45f) * 5);
                    float lr = Lerp(172, 52, wet), lg = Lerp(146, 110, wet), lb = Lerp(92, 48, wet);
                    float beach = 1 - Shapes.Clamp01((e - 0.015f) / 0.025f);
                    lr = Lerp(lr, 196, beach); lg = Lerp(lg, 180, beach); lb = Lerp(lb, 130, beach);
                    float mt = Shapes.Clamp01((e - 0.42f) / 0.3f);
                    lr = Lerp(lr, 112, mt); lg = Lerp(lg, 98, mt); lb = Lerp(lb, 84, mt);
                    float snow = Shapes.Clamp01((e - 0.86f) / 0.1f) * 0.85f;
                    lr = Lerp(lr, 232, snow); lg = Lerp(lg, 236, snow); lb = Lerp(lb, 240, snow);

                    // Blend across the coastline so it isn't jagged
                    float land = Shapes.Clamp01((h - sea) / 0.006f + 0.5f);
                    r = Lerp(r, lr, land); g = Lerp(g, lg, land); b = Lerp(b, lb, land);

                    // Polar ice with a ragged, soft edge
                    float ice = Shapes.Clamp01((lats[i] - (70 + (cs[i] - 0.5f) * 30)) / 2.5f);
                    r = Lerp(r, 232, ice); g = Lerp(g, 239, ice); b = Lerp(b, 247, ice);

                    int alpha = land < 0.5f && ice < 0.5f ? 254 : 255;
                    if (land >= 1 && ice < 0.2f && e < 0.5f && cis[i] > 0.57f)
                    {
                        float k = Shapes.Clamp01((cis[i] - 0.57f) / 0.12f);
                        if (Hash(i, 3, 9, 77) < 0.55f) k *= 0.35f;
                        city[i] = (byte)(k * 255);
                    }
                    day[i] = (alpha << 24) | (Shapes.Clamp255(r) << 16) | (Shapes.Clamp255(g) << 8) | Shapes.Clamp255(b);
                    cloud[i] = (byte)(Shapes.Clamp01((cs[i] - 0.5f) * 3.4f) * 0.92f * 255);
                }
                cityMap = city;
                cloudMap = cloud;
                dayMap = day;
            }
        }

        // ---------- the scene ----------

        class Orbiter { public float Rx, Ry, Tilt, Speed, Phase; public double Ang; }
        class Twinkle { public float X, Y, Size, Phase, Speed; }

        readonly Random rng;
        double t;
        readonly Surface bg;
        readonly float cx, cy, r;
        readonly float sx, sy, sz, hx, hy, hz; // sun direction, and the half-way vector for the glint

        readonly int gr, gsize;
        readonly Bitmap globe;
        readonly int[] gbuf;
        readonly int count;
        readonly int[] dIdx, dV;
        readonly float[] dU, dLight, dRim, dSpec;
        readonly byte[] dCov;
        double rot, rotC;

        readonly Twinkle[] twinkles;
        readonly Bitmap moonImg;
        readonly Orbiter moon;
        readonly Orbiter[] sats;

        bool stationOn; float stationX, stationY; double nextStation;
        bool cometOn; float cometX, cometY, cometVx, cometVy; double nextComet;
        bool rocketOn; float rocketX, rocketY, rocketHeading, rocketSpeed, rocketTurn; double rocketStart, nextRocket, nextPuff;
        readonly List<PointF> puffs = new List<PointF>();
        readonly List<double> puffTimes = new List<double>();
        bool astroOn; float astroX, astroY, astroVx, astroVy, astroRot, astroSpin; double nextAstro;

        readonly SolidBrush brush = new SolidBrush(Color.White);
        readonly Pen pen = new Pen(Color.White);

        float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        public SpaceScene(int w, int h, int seed) : base(w, h)
        {
            BuildMaps();
            rng = new Random(seed);
            cx = W * 0.5f; cy = H * 0.56f; r = H * 0.32f;

            float l = (float)Math.Sqrt(0.62 * 0.62 + 0.40 * 0.40 + 0.67 * 0.67);
            sx = -0.62f / l; sy = -0.40f / l; sz = 0.67f / l;
            float hl = (float)Math.Sqrt(sx * sx + sy * sy + (sz + 1) * (sz + 1));
            hx = sx / hl; hy = sy / hl; hz = (sz + 1) / hl;

            // Work out once, for every pixel of the globe, which spot on the map it shows and how it's lit
            gr = (int)Math.Max(4, Math.Min(Math.Ceiling(r), 760)); // stretching a smaller globe is much slower than drawing it full size
            gsize = gr * 2 + 2;
            float gc = gr + 1;
            globe = new Bitmap(gsize, gsize, PixelFormat.Format32bppPArgb);
            gbuf = new int[gsize * gsize];
            List<int> idx = new List<int>(), vs = new List<int>();
            List<float> us = new List<float>(), lights = new List<float>(), rims = new List<float>(), specs = new List<float>();
            List<byte> covs = new List<byte>();
            float roll = -0.40f, pitch = 0.30f;
            float cr = (float)Math.Cos(roll), srl = (float)Math.Sin(roll), cp = (float)Math.Cos(pitch), sp = (float)Math.Sin(pitch);
            float edge = 1 + 1.5f / gr;
            for (int py = 0; py < gsize; py++)
            {
                for (int px = 0; px < gsize; px++)
                {
                    float dx = (px + 0.5f - gc) / gr, dy = (py + 0.5f - gc) / gr;
                    float d2 = dx * dx + dy * dy;
                    if (d2 > edge * edge) continue;
                    float d = (float)Math.Sqrt(d2);
                    float cov = Shapes.Clamp01((1 - d) * gr + 0.5f);
                    if (cov <= 0) continue;
                    if (d > 0.999f) { dx *= 0.999f / d; dy *= 0.999f / d; }
                    float nz = (float)Math.Sqrt(Math.Max(0, 1 - dx * dx - dy * dy));
                    float x1 = dx * cr - dy * srl, y1 = dx * srl + dy * cr, z1 = nz;
                    float y2 = y1 * cp - z1 * sp, z2 = y1 * sp + z1 * cp;
                    double lat = Math.Asin(Math.Max(-1, Math.Min(1, -y2))), lon = Math.Atan2(x1, z2);
                    idx.Add(py * gsize + px);
                    us.Add((float)((lon / (2 * Math.PI) + 0.5) * MapW) % MapW);
                    vs.Add(Math.Max(0, Math.Min(MapH - 1, (int)((0.5 - lat / Math.PI) * MapH))));
                    lights.Add(dx * sx + dy * sy + nz * sz);
                    rims.Add((float)Math.Pow(1 - nz, 2.2));
                    specs.Add((float)Math.Pow(Math.Max(0, dx * hx + dy * hy + nz * hz), 40));
                    covs.Add((byte)(cov * 255));
                }
            }
            count = idx.Count;
            dIdx = idx.ToArray(); dV = vs.ToArray(); dU = us.ToArray();
            dLight = lights.ToArray(); dRim = rims.ToArray(); dSpec = specs.ToArray(); dCov = covs.ToArray();

            bg = new Surface(W, H);
            PaintBackground(bg.G);

            twinkles = new Twinkle[(int)Math.Min(160, 40 + W * H / 15000)];
            for (int i = 0; i < twinkles.Length; i++)
            {
                Twinkle s = new Twinkle();
                do { s.X = R(0, W); s.Y = R(0, H); }
                while ((s.X - cx) * (s.X - cx) + (s.Y - cy) * (s.Y - cy) < r * r * 1.5f);
                s.Size = Math.Max(1f, R(1.2f, 2.6f) * S);
                s.Phase = R(0, 6.28f); s.Speed = R(0.5f, 2f);
                twinkles[i] = s;
            }

            moon = new Orbiter();
            moon.Rx = r * 2.25f; moon.Ry = r * 0.42f; moon.Tilt = -0.18f; moon.Speed = (float)(2 * Math.PI / 70); moon.Ang = R(0, 6.28f);
            moonImg = MakeMoon((int)Math.Max(4, r * 0.25f));

            sats = new Orbiter[3];
            for (int i = 0; i < sats.Length; i++)
            {
                Orbiter o = new Orbiter();
                o.Rx = r * R(1.2f, 1.55f); o.Ry = r * R(0.25f, 0.55f); o.Tilt = R(-0.35f, 0.35f);
                o.Speed = (float)(2 * Math.PI / R(18, 34)) * (i == 1 ? -1 : 1); o.Ang = R(0, 6.28f); o.Phase = R(0, 1);
                sats[i] = o;
            }

            nextStation = R(5, 12);
            nextComet = R(8, 16);
            nextRocket = R(3, 8);
            nextAstro = R(14, 24);
        }

        void PaintBackground(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle all = new Rectangle(-1, -1, W + 2, H + 2);
            using (LinearGradientBrush lg = new LinearGradientBrush(all, Color.FromArgb(2, 3, 11), Color.FromArgb(8, 10, 28), 90f))
                g.FillRectangle(lg, all);

            // The Sun, just off the top-left corner
            float sunX = -0.08f * W, sunY = -0.12f * H, sunR = 0.8f * H;
            Glow(g, sunX, sunY, sunR, Color.FromArgb(110, 255, 238, 205));
            Glow(g, sunX, sunY, sunR * 0.35f, Color.FromArgb(120, 255, 248, 230));

            // The Milky Way: a hazy band of faint stars across the sky
            float ax = -0.1f * W, ay = 0.95f * H, bx = 1.1f * W, by = 0.08f * H;
            float len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            float nx = -(by - ay) / len, ny = (bx - ax) / len;
            for (int i = 0; i < 7; i++)
            {
                float k = R(0.05f, 0.95f), off = R(-0.05f, 0.05f) * H;
                Glow(g, ax + (bx - ax) * k + nx * off, ay + (by - ay) * k + ny * off, R(0.18f, 0.32f) * H,
                     i % 2 == 0 ? Color.FromArgb(26, 150, 140, 215) : Color.FromArgb(20, 120, 160, 230));
            }
            int band = W * H / 650;
            for (int i = 0; i < band; i++)
            {
                float k = (float)rng.NextDouble();
                double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
                float gauss = (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
                float off = gauss * 0.07f * H;
                brush.Color = Color.FromArgb(rng.Next(25, 110), 220, 225, 255);
                float s = Math.Max(1, R(0.6f, 1.4f) * S);
                g.FillRectangle(brush, ax + (bx - ax) * k + nx * off, ay + (by - ay) * k + ny * off, s, s);
            }
            int stars = W * H / 1100;
            for (int i = 0; i < stars; i++)
            {
                double c = rng.NextDouble();
                Color tint = c < 0.12 ? Color.FromArgb(190, 210, 255) : c < 0.2 ? Color.FromArgb(255, 225, 190) : Color.White;
                brush.Color = Color.FromArgb(rng.Next(50, 235), tint);
                float s = Math.Max(1, R(0.6f, 1.8f) * S);
                g.FillRectangle(brush, R(0, W), R(0, H), s, s);
            }

            // A faint lens flare from the Sun across the picture
            float[] at = { 0.38f, 0.55f, 0.7f, 0.92f };
            float[] rs = { 0.05f, 0.025f, 0.07f, 0.035f };
            Color[] cs = { Color.FromArgb(8, 255, 200, 140), Color.FromArgb(11, 160, 220, 255), Color.FromArgb(6, 200, 160, 255), Color.FromArgb(9, 140, 255, 200) };
            for (int i = 0; i < at.Length; i++)
            {
                float fx = sunX + (W * 0.75f - sunX) * at[i], fy = sunY + (H * 0.8f - sunY) * at[i], fr = rs[i] * H;
                brush.Color = cs[i];
                g.FillEllipse(brush, fx - fr, fy - fr, fr * 2, fr * 2);
            }

            // The atmosphere's blue glow around the Earth
            float ar = r * 1.2f;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(cx - ar, cy - ar, ar * 2, ar * 2);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Colors = new Color[] { Color.FromArgb(0, 90, 150, 255), Color.FromArgb(120, 110, 175, 255), Color.FromArgb(40, 80, 140, 255), Color.FromArgb(0, 80, 140, 255) };
                    cb.Positions = new float[] { 0f, 1 - 1 / 1.2f, 0.3f, 1f };
                    pg.InterpolationColors = cb;
                    g.FillPath(pg, p);
                }
            }
        }

        void Glow(Graphics g, float x, float y, float rad, Color c)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(x - rad, y - rad, rad * 2, rad * 2);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterColor = c;
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillPath(pg, p);
                }
            }
        }

        Bitmap MakeMoon(int mr)
        {
            int size = mr * 2 + 2;
            Bitmap b = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            int[] px = new int[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - (mr + 1)) / mr, dy = (y + 0.5f - (mr + 1)) / mr;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float cov = Shapes.Clamp01((1 - d) * mr + 0.5f);
                    if (cov <= 0) continue;
                    if (d > 0.999f) { dx *= 0.999f / d; dy *= 0.999f / d; }
                    float nz = (float)Math.Sqrt(Math.Max(0, 1 - dx * dx - dy * dy));
                    float grey = 150 + 90 * (Fbm(dx * 3 + 9, dy * 3, nz * 3, 4, 91) - 0.5f) * 2;
                    if (Fbm(dx * 1.3f, dy * 1.3f + 4, nz * 1.3f, 3, 93) > 0.55f) grey *= 0.68f;
                    float day = Shapes.Clamp01((dx * sx + dy * sy + nz * sz) * 3 + 0.1f);
                    float v = grey * (0.03f + 0.97f * day);
                    int a = (int)(cov * 255);
                    int c = Shapes.Clamp255(v * cov);
                    px[y * size + x] = (a << 24) | (c << 16) | (c << 8) | Shapes.Clamp255(v * 0.97f * cov);
                }
            BitmapData bd = b.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            Marshal.Copy(px, 0, bd.Scan0, px.Length);
            b.UnlockBits(bd);
            return b;
        }

        public override void Update(double dt)
        {
            t += dt;
            float fdt = (float)dt;
            rot = (rot + dt * MapW / 120.0) % MapW;
            rotC = (rotC + dt * MapW / 100.0) % MapW;
            moon.Ang += moon.Speed * dt;
            foreach (Orbiter o in sats) o.Ang += o.Speed * dt;

            if (!stationOn && t > nextStation) { stationOn = true; stationX = W + 0.12f * W; stationY = R(0.1f, 0.2f) * H; }
            if (stationOn)
            {
                stationX -= W * 1.3f / 55 * fdt;
                if (stationX < -0.15f * W) { stationOn = false; nextStation = t + R(50, 80); }
            }

            if (!cometOn && t > nextComet)
            {
                cometOn = true;
                cometX = W * 1.1f; cometY = R(0.04f, 0.28f) * H;
                cometVx = -W / R(16, 24); cometVy = H * R(0.01f, 0.04f);
            }
            if (cometOn)
            {
                cometX += cometVx * fdt; cometY += cometVy * fdt;
                if (cometX < -0.4f * W) { cometOn = false; nextComet = t + R(35, 60); }
            }

            if (!rocketOn && t > nextRocket)
            {
                rocketOn = true;
                rocketStart = t;
                float a = R(-150, -30) * (float)Math.PI / 180;
                rocketX = cx + (float)Math.Cos(a) * r * 0.9f;
                rocketY = cy + (float)Math.Sin(a) * r * 0.9f;
                rocketHeading = a;
                rocketSpeed = 15 * S;
                rocketTurn = R(-0.18f, 0.18f);
                puffs.Clear(); puffTimes.Clear();
                nextPuff = t;
            }
            if (rocketOn)
            {
                rocketSpeed += 55 * S * fdt;
                rocketHeading += rocketTurn * fdt;
                rocketX += (float)Math.Cos(rocketHeading) * rocketSpeed * fdt;
                rocketY += (float)Math.Sin(rocketHeading) * rocketSpeed * fdt;
                if (t >= nextPuff) { puffs.Add(new PointF(rocketX, rocketY)); puffTimes.Add(t); nextPuff = t + 0.025; }
                bool off = rocketX < -50 * S || rocketX > W + 50 * S || rocketY < -50 * S || rocketY > H + 50 * S;
                if ((off || t - rocketStart > 14) && (puffTimes.Count == 0 || t - puffTimes[puffTimes.Count - 1] > 2.5))
                {
                    rocketOn = false; nextRocket = t + R(18, 32);
                }
            }
            while (puffTimes.Count > 0 && t - puffTimes[0] > 2.5) { puffTimes.RemoveAt(0); puffs.RemoveAt(0); }

            if (!astroOn && t > nextAstro)
            {
                astroOn = true;
                bool fromLeft = rng.NextDouble() < 0.5;
                astroX = fromLeft ? -0.08f * W : 1.08f * W;
                astroY = R(0.15f, 0.8f) * H;
                astroVx = (fromLeft ? 1 : -1) * W * 1.16f / R(60, 80);
                astroVy = R(-0.004f, 0.004f) * H;
                astroRot = R(0, 360); astroSpin = R(-14, 14);
            }
            if (astroOn)
            {
                astroX += astroVx * fdt; astroY += astroVy * fdt; astroRot += astroSpin * fdt;
                if (astroX < -0.1f * W || astroX > 1.1f * W) { astroOn = false; nextAstro = t + R(60, 100); }
            }
        }

        void Orbit(Orbiter o, out float x, out float y, out float depth)
        {
            float ex = (float)Math.Cos(o.Ang) * o.Rx, ey = (float)Math.Sin(o.Ang) * o.Ry;
            float ct = (float)Math.Cos(o.Tilt), st = (float)Math.Sin(o.Tilt);
            x = cx + ex * ct - ey * st;
            y = cy + ex * st + ey * ct;
            depth = (float)Math.Sin(o.Ang); // positive: on the near side, in front of the Earth
        }

        public override void Draw(Graphics g)
        {
            bg.DrawTo(g, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (Twinkle s in twinkles)
            {
                int a = (int)(150 + 105 * Math.Sin(t * s.Speed + s.Phase));
                brush.Color = Color.FromArgb(Math.Max(0, Math.Min(255, a)), 235, 240, 255);
                g.FillEllipse(brush, s.X - s.Size / 2, s.Y - s.Size / 2, s.Size, s.Size);
            }

            if (cometOn) DrawComet(g);

            float mx, my, md;
            Orbit(moon, out mx, out my, out md);
            if (md < 0) DrawMoon(g, mx, my, md);
            foreach (Orbiter o in sats) DrawSatIf(g, o, false);

            RenderGlobe();
            if (gr >= r - 0.5f) g.DrawImageUnscaled(globe, (int)Math.Round(cx - gr - 1), (int)Math.Round(cy - gr - 1));
            else
            {
                float k = r / gr;
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(globe, new RectangleF(cx - (gr + 1) * k, cy - (gr + 1) * k, gsize * k, gsize * k));
            }

            if (rocketOn || puffs.Count > 0) DrawRocket(g);
            foreach (Orbiter o in sats) DrawSatIf(g, o, true);
            if (md >= 0) DrawMoon(g, mx, my, md);
            if (stationOn) DrawStation(g);
            if (astroOn) DrawAstronaut(g);
            g.SmoothingMode = SmoothingMode.None;
        }

        void RenderGlobe()
        {
            float rotI = (float)rot, rotCI = (float)rotC;
            for (int i = 0; i < count; i++)
            {
                float uu = dU[i] - rotI; if (uu < 0) uu += MapW;
                int u = (int)uu; if (u >= MapW) u -= MapW;
                int row = dV[i] * MapW;
                int c = dayMap[row + u];
                float rr = (c >> 16) & 255, gg = (c >> 8) & 255, bb = c & 255;
                float day = dLight[i] * 2.4f + 0.2f;
                if (day < 0) day = 0; else if (day > 1) day = 1;
                float lit = 0.05f + 0.95f * day;
                rr *= lit; gg *= lit; bb *= lit;

                if (day < 0.6f)
                {
                    int cty = cityMap[row + u];
                    if (cty > 0)
                    {
                        float k = cty * (0.6f - day) * (1 / 0.6f) / 255f;
                        rr += 255 * k; gg += 190 * k; bb += 105 * k;
                    }
                }
                if (((c >> 24) & 255) == 254 && day > 0)
                {
                    float s = dSpec[i] * day * 170;
                    rr += s; gg += s; bb += s * 0.9f;
                }

                float cu = dU[i] - rotCI; if (cu < 0) cu += MapW;
                int cui = (int)cu; if (cui >= MapW) cui -= MapW;
                float ca = cloudMap[row + cui] * (1 / 255f);
                if (ca > 0)
                {
                    float cl = 250 * (0.03f + 0.97f * day);
                    rr += (cl - rr) * ca; gg += (cl - gg) * ca; bb += (cl * 1.02f - bb) * ca;
                }

                float rim = dRim[i] * (0.15f + 0.85f * day);
                rr += 60 * rim; gg += 120 * rim; bb += 255 * rim;

                int cov = dCov[i];
                float f = cov / 255f;
                int ir = rr > 255 ? 255 : (int)rr, ig = gg > 255 ? 255 : (int)gg, ib = bb > 255 ? 255 : (int)bb;
                if (cov < 255) { ir = (int)(ir * f); ig = (int)(ig * f); ib = (int)(ib * f); }
                gbuf[dIdx[i]] = (cov << 24) | (ir << 16) | (ig << 8) | ib;
            }
            BitmapData bd = globe.LockBits(new Rectangle(0, 0, gsize, gsize), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            Marshal.Copy(gbuf, 0, bd.Scan0, gbuf.Length);
            globe.UnlockBits(bd);
        }

        void DrawMoon(Graphics g, float x, float y, float depth)
        {
            float rad = r * 0.2f * (0.85f + 0.25f * depth);
            g.InterpolationMode = InterpolationMode.Bilinear;
            float k = rad / ((moonImg.Width - 2) / 2f);
            g.DrawImage(moonImg, new RectangleF(x - moonImg.Width / 2f * k, y - moonImg.Height / 2f * k, moonImg.Width * k, moonImg.Height * k));
        }

        void DrawSatIf(Graphics g, Orbiter o, bool front)
        {
            float x, y, d;
            Orbit(o, out x, out y, out d);
            if ((d >= 0) != front) return;
            if (!front && (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r) return; // hidden behind the Earth
            float s = S * (0.8f + 0.25f * d);
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform((float)(o.Ang * 30 % 360) * 0.1f - 15);
            g.ScaleTransform(s, s);
            brush.Color = Color.FromArgb(58, 90, 175);
            g.FillRectangle(brush, -17, -3.5f, 10, 7);
            g.FillRectangle(brush, 7, -3.5f, 10, 7);
            pen.Color = Color.FromArgb(140, 170, 230); pen.Width = 0.8f;
            g.DrawRectangle(pen, -17, -3.5f, 10, 7);
            g.DrawRectangle(pen, 7, -3.5f, 10, 7);
            g.DrawLine(pen, -7, 0, 7, 0);
            brush.Color = Color.FromArgb(214, 170, 78);
            g.FillRectangle(brush, -4, -4.5f, 8, 9);
            pen.Color = Color.FromArgb(200, 200, 210); pen.Width = 1;
            g.DrawLine(pen, 0, -4.5f, 0, -10);
            if ((t * 1.3 + o.Phase) % 1.0 < 0.15)
            {
                brush.Color = Color.FromArgb(70, 255, 60, 60); g.FillEllipse(brush, -4, -14, 8, 8);
                brush.Color = Color.FromArgb(255, 80, 80); g.FillEllipse(brush, -1.5f, -11.5f, 3, 3);
            }
            g.Restore(st);
        }

        void DrawComet(Graphics g)
        {
            float sunX = -0.08f * W, sunY = -0.12f * H;
            float ux = cometX - sunX, uy = cometY - sunY;
            float ul = (float)Math.Sqrt(ux * ux + uy * uy);
            ux /= ul; uy /= ul;
            float len = H * 0.38f, px = -uy, py = ux;
            float ex = cometX + ux * len, ey = cometY + uy * len;
            float[] widths = { H * 0.035f, H * 0.012f };
            int[] alphas = { 90, 150 };
            for (int i = 0; i < 2; i++)
            {
                float w0 = 3 * S, w1 = widths[i];
                PointF[] pts = { new PointF(cometX + px * w0, cometY + py * w0), new PointF(ex + px * w1, ey + py * w1),
                                 new PointF(ex - px * w1, ey - py * w1), new PointF(cometX - px * w0, cometY - py * w0) };
                using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(cometX, cometY), new PointF(ex, ey),
                           Color.FromArgb(alphas[i], 200, 230, 255), Color.FromArgb(0, 200, 230, 255)))
                {
                    lg.WrapMode = WrapMode.TileFlipXY;
                    g.FillPolygon(lg, pts);
                }
            }
            float hr = 9 * S;
            brush.Color = Color.FromArgb(60, 200, 230, 255); g.FillEllipse(brush, cometX - hr * 2, cometY - hr * 2, hr * 4, hr * 4);
            brush.Color = Color.FromArgb(160, 230, 245, 255); g.FillEllipse(brush, cometX - hr, cometY - hr, hr * 2, hr * 2);
            brush.Color = Color.White; g.FillEllipse(brush, cometX - hr * 0.4f, cometY - hr * 0.4f, hr * 0.8f, hr * 0.8f);
        }

        void DrawRocket(Graphics g)
        {
            for (int i = 0; i < puffs.Count; i++)
            {
                float age = (float)(t - puffTimes[i]);
                float rad = (1.5f + age * 9) * S;
                int a = (int)(38 * (1 - age / 2.5f));
                if (a <= 0) continue;
                brush.Color = Color.FromArgb(a, 210, 210, 220);
                g.FillEllipse(brush, puffs[i].X - rad, puffs[i].Y - rad, rad * 2, rad * 2);
            }
            if (!rocketOn) return;
            float x = rocketX, y = rocketY;
            if (x < -60 * S || x > W + 60 * S || y < -60 * S || y > H + 60 * S) return;
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(rocketHeading * 180 / (float)Math.PI);
            g.ScaleTransform(S * 1.3f, S * 1.3f);
            float flick = (float)(0.75 + 0.25 * Math.Sin(t * 40));
            brush.Color = Color.FromArgb(200, 255, 150, 40);
            g.FillPolygon(brush, new PointF[] { new PointF(-9, -3), new PointF(-9 - 14 * flick, 0), new PointF(-9, 3) });
            brush.Color = Color.FromArgb(240, 255, 235, 140);
            g.FillPolygon(brush, new PointF[] { new PointF(-9, -1.6f), new PointF(-9 - 7 * flick, 0), new PointF(-9, 1.6f) });
            brush.Color = Color.FromArgb(200, 40, 40);
            g.FillPolygon(brush, new PointF[] { new PointF(-9, -3), new PointF(-13, -7), new PointF(-5, -3) });
            g.FillPolygon(brush, new PointF[] { new PointF(-9, 3), new PointF(-13, 7), new PointF(-5, 3) });
            brush.Color = Color.FromArgb(236, 238, 242);
            g.FillRectangle(brush, -9, -3, 15, 6);
            brush.Color = Color.FromArgb(200, 40, 40);
            g.FillPolygon(brush, new PointF[] { new PointF(6, -3), new PointF(12, 0), new PointF(6, 3) });
            g.Restore(st);
        }

        void DrawStation(Graphics g)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(stationX, stationY + (float)Math.Sin(t * 0.4) * 6 * S);
            g.RotateTransform(-6);
            float s = S * 1.5f;
            g.ScaleTransform(s, s);
            pen.Color = Color.FromArgb(170, 175, 185); pen.Width = 3;
            g.DrawLine(pen, -78, 0, 78, 0);
            float[] xs = { -66, -42, 42, 66 };
            foreach (float x in xs)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    RectangleF p = side < 0 ? new RectangleF(x - 9, -40, 18, 32) : new RectangleF(x - 9, 8, 18, 32);
                    using (LinearGradientBrush lg = new LinearGradientBrush(p, Color.FromArgb(40, 60, 130), Color.FromArgb(18, 28, 70), 90f))
                        g.FillRectangle(lg, p);
                    pen.Color = Color.FromArgb(190, 160, 90); pen.Width = 0.8f;
                    g.DrawRectangle(pen, p.X, p.Y, p.Width, p.Height);
                    for (int k = 1; k < 4; k++) g.DrawLine(pen, p.X, p.Y + p.Height * k / 4, p.Right, p.Y + p.Height * k / 4);
                }
                pen.Color = Color.FromArgb(160, 165, 175); pen.Width = 1.5f;
                g.DrawLine(pen, x, -8, x, 8);
            }
            using (GraphicsPath m = Shapes.RoundRect(new RectangleF(-26, -7, 52, 14), 6))
            using (LinearGradientBrush lg = new LinearGradientBrush(new RectangleF(-26, -7, 52, 14), Color.FromArgb(240, 242, 246), Color.FromArgb(150, 155, 165), 90f))
                g.FillPath(lg, m);
            using (GraphicsPath m = Shapes.RoundRect(new RectangleF(-7, -20, 14, 14), 5))
            using (LinearGradientBrush lg = new LinearGradientBrush(new RectangleF(-7, -20, 14, 14), Color.FromArgb(235, 238, 242), Color.FromArgb(140, 145, 155), 0f))
                g.FillPath(lg, m);
            brush.Color = Color.FromArgb(225, 228, 235);
            g.FillRectangle(brush, -14, 7, 6, 14);
            g.FillRectangle(brush, 8, 7, 6, 14);
            if ((t * 0.9) % 1.0 < 0.08)
            {
                brush.Color = Color.FromArgb(80, 255, 255, 255); g.FillEllipse(brush, 72, -6, 12, 12);
                brush.Color = Color.White; g.FillEllipse(brush, 76, -2, 4, 4);
            }
            g.Restore(st);
        }

        void DrawAstronaut(Graphics g)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(astroX, astroY);
            g.RotateTransform(astroRot);
            float s = S * 1.4f;
            g.ScaleTransform(s, s);
            Color suit = Color.FromArgb(236, 237, 241), shade = Color.FromArgb(185, 188, 196);

            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(-14, -18, 28, 32), 5)) { brush.Color = shade; g.FillPath(brush, p); }
            using (Pen p = new Pen(suit, 9)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, -6, 14, -10, 33); g.DrawLine(p, 6, 14, 11, 32); }
            brush.Color = Color.FromArgb(120, 124, 132);
            g.FillEllipse(brush, -15, 30, 10, 7); g.FillEllipse(brush, 6, 29, 10, 7);
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(-12, -16, 24, 33), 7))
            using (LinearGradientBrush lg = new LinearGradientBrush(new RectangleF(-12, -16, 24, 33), suit, shade, 0f))
                g.FillPath(lg, p);
            brush.Color = Color.FromArgb(60, 70, 90); g.FillRectangle(brush, -6, -6, 12, 8);
            brush.Color = Color.FromArgb(255, 70, 70); g.FillEllipse(brush, -4, -4, 3, 3);
            brush.Color = Color.FromArgb(80, 200, 255); g.FillEllipse(brush, 1, -4, 3, 3);
            using (Pen p = new Pen(suit, 8)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, -11, -10, -24, 4); g.DrawLine(p, 11, -10, 24, -20); }
            brush.Color = Color.FromArgb(120, 124, 132);
            g.FillEllipse(brush, -28, 1, 8, 8); g.FillEllipse(brush, 21, -25, 8, 8);
            brush.Color = suit; g.FillEllipse(brush, -12, -38, 24, 24);
            using (LinearGradientBrush lg = new LinearGradientBrush(new RectangleF(-8, -33, 16, 13), Color.FromArgb(250, 205, 90), Color.FromArgb(120, 70, 15), 60f))
                g.FillEllipse(lg, -8, -33, 16, 13);
            brush.Color = Color.FromArgb(150, 255, 255, 255); g.FillEllipse(brush, -5, -31, 5, 3);
            g.Restore(st);
        }

        public override void Dispose()
        {
            bg.Dispose(); globe.Dispose(); moonImg.Dispose();
            brush.Dispose(); pen.Dispose();
        }
    }
}
