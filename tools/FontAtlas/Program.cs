// FontAtlas: cuts the Arkhane specimen SVG (claude/arkhane.svg, made by tools/Vectorize) into single
// glyphs and writes a glyph atlas for the game: arkhane.png (white glyphs, alpha coverage) and
// arkhane.txt (one line per character: where it is in the atlas and where it sits against the baseline).
//
// Steps: read the path's closed loops -> group loops into rows (gaps in height) -> inside a row, group
// loops whose widths overlap into one glyph (holes and dots join their letter) -> name each glyph from
// the specimen's known order -> rasterize each glyph with even-odd fill, 4x4 supersampling -> pack.
//
// Usage: dotnet run --project tools/FontAtlas -- <input.svg> <out folder> [capPixels=96]
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal static class Program
{
    private class Loop
    {
        public List<(double X, double Y)> P;
        public double MinX, MaxX, MinY, MaxY;
    }

    private class Glyph
    {
        public List<Loop> Loops = new();
        public double MinX = 1e9, MaxX = -1e9, MinY = 1e9, MaxY = -1e9;
        public char Ch;
        public int Row;
        public void Add(Loop l)
        {
            Loops.Add(l);
            MinX = Math.Min(MinX, l.MinX); MaxX = Math.Max(MaxX, l.MaxX);
            MinY = Math.Min(MinY, l.MinY); MaxY = Math.Max(MaxY, l.MaxY);
        }
    }

    // Characters of each specimen row, left to right (row 0 is the big title, skipped).
    private static readonly string[] RowChars =
    {
        null,
        "ABCDEFGHIJKLM",
        "NOPQRSTUVWXYZ",
        "abcdefghijklm",
        "nopqrstuvwxyz",
        "0123456789",
        "(!\"#$%&'()*+,-./:;",
        "<=>?@[\\]^_`{|}~",
    };

    private static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("Usage: FontAtlas <input.svg> <out folder> [capPixels=96]"); return 1; }
        string svg = File.ReadAllText(args[0]);
        string outDir = args[1];
        int capPx = args.Length > 2 ? int.Parse(args[2]) : 96;
        Directory.CreateDirectory(outDir);

        var loops = ParseLoops(svg);
        Console.WriteLine($"loops: {loops.Count}");

        // Rows: merge loops whose height ranges overlap.
        var byTop = loops.OrderBy(l => l.MinY).ToList();
        var rows = new List<List<Loop>>();
        double rowBottom = -1e9;
        foreach (var l in byTop)
        {
            if (rows.Count == 0 || l.MinY > rowBottom) { rows.Add(new List<Loop>()); rowBottom = l.MaxY; }
            rows[^1].Add(l);
            rowBottom = Math.Max(rowBottom, l.MaxY);
        }
        Console.WriteLine($"rows found: {rows.Count}");
        for (int r = 0; r < rows.Count; r++)
            Console.WriteLine($"  row {r}: y {rows[r].Min(l => l.MinY):F0}-{rows[r].Max(l => l.MaxY):F0}, {rows[r].Count} loops");

        var glyphs = new List<Glyph>();
        for (int r = 1; r < rows.Count && r < RowChars.Length; r++)
        {
            // Group by overlapping x ranges.
            var sorted = rows[r].OrderBy(l => l.MinX).ToList();
            var row = new List<Glyph>();
            foreach (var l in sorted)
            {
                var g = row.Count > 0 ? row[^1] : null;
                if (g != null && l.MinX <= g.MaxX + 1.0) g.Add(l);
                else { var n = new Glyph { Row = r }; n.Add(l); row.Add(n); }
            }
            string chars = RowChars[r];
            // The double quote is two separate ticks; join them into one glyph.
            if (r == 6 && row.Count == chars.Length + 1)
            {
                foreach (var l in row[3].Loops) row[2].Add(l);
                row.RemoveAt(3);
            }
            Console.WriteLine($"row {r}: {row.Count} glyphs, expected {chars.Length}");
            if (row.Count != chars.Length)
            {
                Console.WriteLine("  x ranges: " + string.Join(" ", row.Select(g => $"{g.MinX:F0}-{g.MaxX:F0}")));
                continue;
            }
            for (int i = 0; i < row.Count; i++) { row[i].Ch = chars[i]; glyphs.Add(row[i]); }
        }
        if (glyphs.Count == 0) return 2;

        // The first glyph of the punctuation row is a stray "(" in the specimen; keep the real one.
        var stray = glyphs.FirstOrDefault(g => g.Row == 6);
        if (stray != null) glyphs.Remove(stray);

        // Cap height from the capital row; baseline per row from glyphs that sit on it.
        double capH = Median(glyphs.Where(g => g.Row == 1 && "BCDEHKLMN".Contains(g.Ch)).Select(g => g.MaxY - g.MinY));
        double scale = capPx / capH;
        Console.WriteLine($"cap height {capH:F1} -> scale {scale:F3}");
        var baseline = new Dictionary<int, double>();
        for (int r = 1; r <= 7; r++)
        {
            var rowG = glyphs.Where(g => g.Row == r).ToList();
            if (rowG.Count == 0) continue;
            if (r == 6) baseline[r] = rowG.First(g => g.Ch == '.').MaxY;
            else if (r == 7) baseline[r] = rowG.First(g => g.Ch == '?').MaxY;
            else if (r == 3) baseline[r] = Median(rowG.Where(g => g.Ch != 'g' && g.Ch != 'j').Select(g => g.MaxY));
            else baseline[r] = Median(rowG.Select(g => g.MaxY));
        }
        // Row 4 (n-z) copies the capitals' look; give it the same baseline logic as row 2.
        if (baseline.ContainsKey(4)) baseline[4] = Median(glyphs.Where(g => g.Row == 4 && "nortuvwxz".Contains(g.Ch)).Select(g => g.MaxY));

        // Rasterize and pack.
        const int Pad = 3, SheetW = 1024;
        var cells = new List<(Glyph G, int W, int H, byte[] A, int X, int Y)>();
        foreach (var g in glyphs)
        {
            int w = (int)Math.Ceiling((g.MaxX - g.MinX) * scale) + 2, h = (int)Math.Ceiling((g.MaxY - g.MinY) * scale) + 2;
            cells.Add((g, w, h, Raster(g, scale, w, h), 0, 0));
        }
        int cx = Pad, cy = Pad, shelf = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (cx + c.W + Pad > SheetW) { cx = Pad; cy += shelf + Pad; shelf = 0; }
            cells[i] = (c.G, c.W, c.H, c.A, cx, cy);
            cx += c.W + Pad; shelf = Math.Max(shelf, c.H);
        }
        int sheetH = 1; while (sheetH < cy + shelf + Pad) sheetH *= 2;

        using var bmp = new Bitmap(SheetW, sheetH, PixelFormat.Format32bppArgb);
        var inv = CultureInfo.InvariantCulture;
        var meta = new StringBuilder();
        meta.AppendLine($"cap {capPx}");
        foreach (var c in cells)
        {
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                    bmp.SetPixel(c.X + x, c.Y + y, Color.FromArgb(c.A[y * c.W + x], 255, 255, 255));
            // yOff: the cell's top against the baseline, in atlas pixels (negative = above).
            double yOff = (c.G.MinY - baseline[c.G.Row]) * scale - 1;
            meta.AppendLine($"{(int)c.G.Ch} {c.X} {c.Y} {c.W} {c.H} {yOff.ToString("F2", inv)}");
        }
        bmp.Save(Path.Combine(outDir, "arkhane.png"), ImageFormat.Png);
        // A dark-background copy for eyeballing the result.
        using (var preview = new Bitmap(SheetW, sheetH))
        using (var pg = Graphics.FromImage(preview))
        {
            pg.Clear(Color.FromArgb(37, 37, 39));
            pg.DrawImage(bmp, 0, 0);
            preview.Save(Path.Combine(outDir, "arkhane-preview.png"), ImageFormat.Png);
        }
        File.WriteAllText(Path.Combine(outDir, "arkhane.txt"), meta.ToString());
        Console.WriteLine($"wrote {glyphs.Count} glyphs to {outDir} ({SheetW}x{sheetH})");
        return 0;
    }

    private static double Median(IEnumerable<double> v)
    {
        var a = v.OrderBy(x => x).ToList();
        return a[a.Count / 2];
    }

    // Reads M / L / C / Z commands (absolute, as Vectorize writes them) into flattened closed loops.
    private static List<Loop> ParseLoops(string svg)
    {
        var d = Regex.Match(svg, "<path[^>]* d=\"([^\"]+)\"").Groups[1].Value;
        var loops = new List<Loop>();
        List<(double X, double Y)> cur = null;
        (double X, double Y) last = (0, 0);
        foreach (Match m in Regex.Matches(d, "([MLCZ])([^MLCZ]*)"))
        {
            char cmd = m.Groups[1].Value[0];
            var n = m.Groups[2].Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            switch (cmd)
            {
                case 'M': cur = new(); last = (n[0], n[1]); cur.Add(last); break;
                case 'L': last = (n[0], n[1]); cur.Add(last); break;
                case 'C':
                    var p0 = last; var p1 = (n[0], n[1]); var p2 = (n[2], n[3]); var p3 = (n[4], n[5]);
                    for (int i = 1; i <= 8; i++)
                    {
                        double t = i / 8.0, u = 1 - t;
                        cur.Add((u * u * u * p0.X + 3 * u * u * t * p1.Item1 + 3 * u * t * t * p2.Item1 + t * t * t * p3.Item1,
                                 u * u * u * p0.Y + 3 * u * u * t * p1.Item2 + 3 * u * t * t * p2.Item2 + t * t * t * p3.Item2));
                    }
                    last = p3; break;
                case 'Z':
                    loops.Add(new Loop
                    {
                        P = cur,
                        MinX = cur.Min(p => p.X), MaxX = cur.Max(p => p.X), MinY = cur.Min(p => p.Y), MaxY = cur.Max(p => p.Y)
                    });
                    break;
            }
        }
        return loops;
    }

    // Even-odd fill of a glyph's loops, 4x4 supersampled, into coverage bytes.
    private static byte[] Raster(Glyph g, double scale, int w, int h)
    {
        const int S = 4;
        var cover = new int[w * h];
        var xs = new List<double>();
        for (int sy = 0; sy < h * S; sy++)
        {
            // Specimen y of this sub-scanline (the cell has a 1px border).
            double y = g.MinY + ((sy + 0.5) / S - 1) / scale;
            xs.Clear();
            foreach (var l in g.Loops)
            {
                var p = l.P;
                for (int i = 0; i < p.Count; i++)
                {
                    var a = p[i]; var b = p[(i + 1) % p.Count];
                    if ((a.Y <= y) == (b.Y <= y)) continue;
                    xs.Add(a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }
            }
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
            {
                int x0 = (int)Math.Ceiling(((xs[k] - g.MinX) * scale + 1) * S - 0.5);
                int x1 = (int)Math.Ceiling(((xs[k + 1] - g.MinX) * scale + 1) * S - 0.5);
                x0 = Math.Max(0, x0); x1 = Math.Min(w * S, x1);
                for (int sx = x0; sx < x1; sx++) cover[(sy / S) * w + sx / S]++;
            }
        }
        var bytes = new byte[w * h];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)Math.Min(255, cover[i] * 255 / (S * S));
        return bytes;
    }
}
