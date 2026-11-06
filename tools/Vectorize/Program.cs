// Vectorize: turns a two-tone raster (for example a font specimen) into a scalable SVG.
//
// Pipeline: grayscale -> light blur (kills paper grain) -> Otsu threshold -> marching squares
// with sub-pixel interpolation (smooth contours) -> drop tiny specks -> Douglas-Peucker
// simplification -> corner-aware cubic Bezier fitting -> SVG path (even-odd fill so holes work).
//
// Usage: dotnet run --project tools/Vectorize -- <input> <output.svg> [scale] [minArea] [tolerance]
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: Vectorize <input> <output.svg> [scale=4] [minArea=14] [tolerance=0.7]");
            return 1;
        }
        string input = args[0], output = args[1];
        double scale = args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 4;
        double minArea = args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : 14;
        double tol = args.Length > 4 ? double.Parse(args[4], CultureInfo.InvariantCulture) : 0.7;

        LoadGray(input, out int w, out int h, out float[] gray, out Color lightCol, out Color darkCol, out bool lightIsInk, out double thr);
        Console.WriteLine($"{w}x{h}, threshold {thr:F1}, ink is {(lightIsInk ? "light" : "dark")}");

        // Pad by one pixel of background so every contour closes inside the grid.
        int pw = w + 2, ph = h + 2;
        float[] field = new float[pw * ph];
        Array.Fill(field, lightIsInk ? 0f : 255f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                field[(y + 1) * pw + x + 1] = gray[y * w + x];

        // Make "inside" always mean "ink" by flipping dark-on-light images.
        float level = (float)thr;
        if (!lightIsInk)
        {
            for (int i = 0; i < field.Length; i++) field[i] = 255 - field[i];
            level = 255 - level;
        }

        var loops = Trace(field, pw, ph, level);
        Console.WriteLine($"raw contours: {loops.Count}");

        var sb = new StringBuilder();
        int kept = 0;
        foreach (var loop in loops)
        {
            if (Math.Abs(Area(loop)) < minArea) continue;
            var simp = SimplifyClosed(loop, tol);
            if (simp.Count < 3) continue;
            AppendCurve(sb, simp, -1.0, -1.0); // undo the 1px padding
            kept++;
        }
        Console.WriteLine($"kept contours: {kept}");

        var inv = CultureInfo.InvariantCulture;
        string bgHex = Hex(lightIsInk ? darkCol : lightCol);
        string inkHex = Hex(lightIsInk ? lightCol : darkCol);
        var svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{(w * scale).ToString("0.##", inv)}\" height=\"{(h * scale).ToString("0.##", inv)}\" viewBox=\"0 0 {w} {h}\">\n");
        svg.Append($"<rect width=\"{w}\" height=\"{h}\" fill=\"{bgHex}\"/>\n");
        svg.Append($"<path fill=\"{inkHex}\" fill-rule=\"evenodd\" d=\"{sb}\"/>\n");
        svg.Append("</svg>\n");
        File.WriteAllText(output, svg.ToString());
        Console.WriteLine($"wrote {output} ({new FileInfo(output).Length / 1024} KB)");
        return 0;
    }

    // ---- loading and thresholding ----

    // Reads the image, blurs it lightly, and works out the ink/background colors and threshold.
    private static void LoadGray(string path, out int w, out int h, out float[] gray,
        out Color lightCol, out Color darkCol, out bool lightIsInk, out double threshold)
    {
        using var bmp = new Bitmap(path);
        w = bmp.Width; h = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        int stride = data.Stride;
        byte[] raw = new byte[stride * h];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, raw, 0, raw.Length);
        bmp.UnlockBits(data);

        float[] g = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 3; // BGR
                g[y * w + x] = 0.299f * raw[i + 2] + 0.587f * raw[i + 1] + 0.114f * raw[i];
            }
        gray = Blur(Blur(g, w, h), w, h);

        // Otsu threshold on the blurred image.
        long[] hist = new long[256];
        foreach (float v in gray) hist[Math.Clamp((int)v, 0, 255)]++;
        double total = gray.Length, sumAll = 0;
        for (int i = 0; i < 256; i++) sumAll += i * (double)hist[i];
        double sumB = 0, wB = 0, best = -1; int t = 128;
        for (int i = 0; i < 256; i++)
        {
            wB += hist[i]; if (wB == 0) continue;
            double wF = total - wB; if (wF == 0) break;
            sumB += i * (double)hist[i];
            double mB = sumB / wB, mF = (sumAll - sumB) / wF;
            double between = wB * wF * (mB - mF) * (mB - mF);
            if (between > best) { best = between; t = i; }
        }
        threshold = t;

        // Average colors on each side of the threshold; the minority side is the ink.
        long rL = 0, gL = 0, bL = 0, nL = 0, rD = 0, gD = 0, bD = 0, nD = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 3;
                if (gray[y * w + x] > t) { rL += raw[i + 2]; gL += raw[i + 1]; bL += raw[i]; nL++; }
                else { rD += raw[i + 2]; gD += raw[i + 1]; bD += raw[i]; nD++; }
            }
        lightCol = nL > 0 ? Color.FromArgb((int)(rL / nL), (int)(gL / nL), (int)(bL / nL)) : Color.White;
        darkCol = nD > 0 ? Color.FromArgb((int)(rD / nD), (int)(gD / nD), (int)(bD / nD)) : Color.Black;
        lightIsInk = nL < nD;
    }

    // 3x3 box blur with clamped edges.
    private static float[] Blur(float[] src, int w, int h)
    {
        float[] dst = new float[src.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        s += src[Math.Clamp(y + dy, 0, h - 1) * w + Math.Clamp(x + dx, 0, w - 1)];
                dst[y * w + x] = s / 9f;
            }
        return dst;
    }

    // ---- marching squares ----

    // Finds every closed contour where the field crosses the level, with sub-pixel positions.
    private static List<List<(double X, double Y)>> Trace(float[] f, int w, int h, float level)
    {
        // Node id: horizontal edge (x,y)-(x+1,y) = 2*(y*w+x), vertical edge (x,y)-(x,y+1) = 2*(y*w+x)+1.
        // Each node touches exactly two segments, so it keeps two neighbor slots.
        var adj = new Dictionary<int, int[]>();
        void Add(int k, int v)
        {
            if (!adj.TryGetValue(k, out var s)) { s = new[] { -1, -1 }; adj[k] = s; }
            if (s[0] == -1) s[0] = v; else s[1] = v;
        }
        void Link(int a, int b) { Add(a, b); Add(b, a); }
        int Hn(int x, int y) => 2 * (y * w + x);
        int Vn(int x, int y) => 2 * (y * w + x) + 1;

        for (int y = 0; y < h - 1; y++)
            for (int x = 0; x < w - 1; x++)
            {
                float tl = f[y * w + x], tr = f[y * w + x + 1], br = f[(y + 1) * w + x + 1], bl = f[(y + 1) * w + x];
                int c = (tl > level ? 8 : 0) | (tr > level ? 4 : 0) | (br > level ? 2 : 0) | (bl > level ? 1 : 0);
                if (c == 0 || c == 15) continue;
                int T = Hn(x, y), B = Hn(x, y + 1), L = Vn(x, y), R = Vn(x + 1, y);
                bool center = (tl + tr + br + bl) * 0.25f > level;
                switch (c)
                {
                    case 1: case 14: Link(L, B); break;
                    case 2: case 13: Link(B, R); break;
                    case 3: case 12: Link(L, R); break;
                    case 4: case 11: Link(T, R); break;
                    case 6: case 9: Link(T, B); break;
                    case 7: case 8: Link(T, L); break;
                    case 5: if (center) { Link(T, L); Link(B, R); } else { Link(T, R); Link(L, B); } break;
                    case 10: if (center) { Link(T, R); Link(L, B); } else { Link(T, L); Link(B, R); } break;
                }
            }

        // Sub-pixel position of the crossing on an edge.
        (double X, double Y) Pos(int id)
        {
            bool vertical = (id & 1) == 1;
            int cell = id >> 1, x = cell % w, y = cell / w;
            if (!vertical)
            {
                float a = f[y * w + x], b = f[y * w + x + 1];
                return (x + (level - a) / (b - a), y);
            }
            float c0 = f[y * w + x], c1 = f[(y + 1) * w + x];
            return (x, y + (level - c0) / (c1 - c0));
        }

        var loops = new List<List<(double X, double Y)>>();
        var seen = new HashSet<int>();
        foreach (int start in adj.Keys.ToList())
        {
            if (seen.Contains(start)) continue;
            var loop = new List<(double X, double Y)>();
            int prev = -1, cur = start;
            while (true)
            {
                seen.Add(cur);
                loop.Add(Pos(cur));
                var s = adj[cur];
                int next = (s[0] != prev) ? s[0] : s[1];
                if (prev == -1) next = s[0];
                if (next == -1 || seen.Contains(next)) break;
                prev = cur; cur = next;
            }
            if (loop.Count >= 3) loops.Add(loop);
        }
        return loops;
    }

    // ---- geometry ----

    private static double Area(List<(double X, double Y)> p)
    {
        double s = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return s * 0.5;
    }

    // Douglas-Peucker on a closed loop: split at the point farthest from the start, simplify each half.
    private static List<(double X, double Y)> SimplifyClosed(List<(double X, double Y)> p, double tol)
    {
        int n = p.Count, i1 = 0; double far = -1;
        for (int i = 1; i < n; i++)
        {
            double d = Dist2(p[0], p[i]);
            if (d > far) { far = d; i1 = i; }
        }
        var a = p.GetRange(0, i1 + 1);
        var b = p.GetRange(i1, n - i1); b.Add(p[0]);
        var sa = DP(a, tol); var sb = DP(b, tol);
        var res = new List<(double X, double Y)>();
        res.AddRange(sa.Take(sa.Count - 1));
        res.AddRange(sb.Take(sb.Count - 1));
        return res;
    }

    private static List<(double X, double Y)> DP(List<(double X, double Y)> p, double tol)
    {
        var keep = new bool[p.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int, int)>();
        stack.Push((0, p.Count - 1));
        while (stack.Count > 0)
        {
            var (s, e) = stack.Pop();
            double maxD = -1; int idx = -1;
            for (int i = s + 1; i < e; i++)
            {
                double d = SegDist(p[i], p[s], p[e]);
                if (d > maxD) { maxD = d; idx = i; }
            }
            if (maxD > tol) { keep[idx] = true; stack.Push((s, idx)); stack.Push((idx, e)); }
        }
        var r = new List<(double X, double Y)>();
        for (int i = 0; i < p.Count; i++) if (keep[i]) r.Add(p[i]);
        return r;
    }

    private static double Dist2((double X, double Y) a, (double X, double Y) b)
        => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    private static double SegDist((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        if (l2 < 1e-12) return Math.Sqrt(Dist2(p, a));
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return Math.Sqrt(Dist2(p, (a.X + t * dx, a.Y + t * dy)));
    }

    // ---- SVG output ----

    // Writes a closed path. Gentle vertices get smooth cubic tangents; sharp corners stay sharp.
    private static void AppendCurve(StringBuilder sb, List<(double X, double Y)> pts, double ox, double oy)
    {
        var inv = CultureInfo.InvariantCulture;
        int n = pts.Count;
        string F(double v) => v.ToString("0.##", inv);
        (double X, double Y) P(int i) { var q = pts[((i % n) + n) % n]; return (q.X + ox, q.Y + oy); }

        // Tangent at vertex i; zero at a corner (turn sharper than about 40 degrees).
        (double X, double Y) Tan(int i)
        {
            var a = P(i - 1); var b = P(i); var c = P(i + 1);
            double ux = b.X - a.X, uy = b.Y - a.Y, vx = c.X - b.X, vy = c.Y - b.Y;
            double lu = Math.Sqrt(ux * ux + uy * uy), lv = Math.Sqrt(vx * vx + vy * vy);
            if (lu < 1e-9 || lv < 1e-9) return (0, 0);
            double cos = (ux * vx + uy * vy) / (lu * lv);
            if (cos < 0.766) return (0, 0);
            return ((c.X - a.X) * 0.5, (c.Y - a.Y) * 0.5);
        }

        var p0 = P(0);
        sb.Append($"M{F(p0.X)} {F(p0.Y)}");
        for (int i = 0; i < n; i++)
        {
            var a = P(i); var b = P(i + 1);
            var ta = Tan(i); var tb = Tan(i + 1);
            if (ta == (0, 0) && tb == (0, 0))
            {
                sb.Append($"L{F(b.X)} {F(b.Y)}");
                continue;
            }
            // Cap handle length at the chord so curves never overshoot.
            double chord = Math.Sqrt(Dist2(a, b));
            (double X, double Y) Clip((double X, double Y) t)
            {
                double l = Math.Sqrt(t.X * t.X + t.Y * t.Y);
                return l > chord ? (t.X / l * chord, t.Y / l * chord) : t;
            }
            ta = Clip(ta); tb = Clip(tb);
            sb.Append($"C{F(a.X + ta.X / 3)} {F(a.Y + ta.Y / 3)} {F(b.X - tb.X / 3)} {F(b.Y - tb.Y / 3)} {F(b.X)} {F(b.Y)}");
        }
        sb.Append('Z');
    }

    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
}
