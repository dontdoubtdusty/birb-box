using System.Numerics;
using Raylib_cs;

// Small shared pieces that several files used to define for themselves.

// Colors used in more than one file.
public static class Palette
{
    public static readonly Color Brass = new Color(122, 96, 64, 255);
    public static readonly Color BrassDark = new Color(58, 44, 30, 255);
    public static readonly Color BrassLight = new Color(196, 158, 92, 255);
    public static readonly Color BrassPlinth = new Color(74, 56, 38, 255);    // the base both pedestals stand on

    public static readonly Color PanelFill = new Color(10, 14, 20, 200);
    public static readonly Color PanelDark = new Color(30, 38, 46, 255);     // behind bars, bags and text boxes
    public static readonly Color Text = new Color(220, 232, 236, 255);
    public static readonly Color DimText = new Color(130, 150, 158, 255);

    // The hologram color is kept as 0..1 numbers (for the shader and ImGui); this turns it into a Raylib Color.
    public static Color ToColor(Vector3 rgb, int alpha = 255)
    {
        return new Color(
            (int)Math.Clamp(rgb.X * 255f, 0, 255),
            (int)Math.Clamp(rgb.Y * 255f, 0, 255),
            (int)Math.Clamp(rgb.Z * 255f, 0, 255),
            alpha);
    }

    // Blends from color a (t = 0) to color b (t = 1).
    public static Color Mix(Color a, Color b, float t)
    {
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t),
            (int)(a.A + (b.A - a.A) * t));
    }

    // Moves a color toward white by 'amount' (0 to 1), keeping its see-through-ness.
    public static Color Lighten(Color c, float amount)
    {
        return Mix(c, new Color(255, 255, 255, (int)c.A), amount);
    }

    // Each channel times the other's, as if seen through tinted glass.
    public static Color Multiply(Color a, Color b)
    {
        return new Color(a.R * b.R / 255, a.G * b.G / 255, a.B * b.B / 255, a.A * b.A / 255);
    }

    // A glow's alpha scaled by a brightness (1 = normal), capped at fully opaque.
    public static int GlowAlpha(int alpha, float brightness) => (int)MathF.Min(255f, alpha * brightness);
}

// One shared random number source.
public static class Rng
{
    public static float Range(float min, float max) => min + Random.Shared.NextSingle() * (max - min);
    public static bool Chance(double probability) => Random.Shared.NextDouble() < probability;
    public static int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);

    // A random-looking number from 0 to 1 that is always the same for the same inputs.
    // Used for looks that must not jump around between frames, like where each seed grain lies.
    // Pass whatever numbers identify the thing (an entity ID, an index) plus a different last number
    // for each value needed (one for an angle, one for a distance, and so on).
    public static float Fixed(int a, int b = 0, int c = 0, int d = 0)
    {
        uint h = (uint)a * 73856093u ^ (uint)b * 19349663u ^ (uint)c * 83492791u ^ (uint)d * 2654435761u;
        h ^= h >> 13;
        h *= 0x5bd1e995u;
        h ^= h >> 15;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }
}

// Seconds as a clock: 125 -> "02:05".
public static class Clock
{
    public static string Format(float seconds) => $"{(int)(seconds / 60):00}:{(int)(seconds % 60):00}";
}

// Files in the resources folder, found next to the .exe so the game starts from any folder or shortcut.
public static class Assets
{
    public static string PathTo(string file) => Path.Combine(AppContext.BaseDirectory, "resources", file);

    // The project folder (the one holding BirdsApp.csproj), or null when there isn't one (a copy given to a friend).
    // The game runs either from bin\ inside the project or from the "Birb Cube" folder next to it, so every folder
    // above the game is checked, both itself and a BirdsApp folder inside it.
    public static string? ProjectFolder { get; } = FindProjectFolder();

    private static string? FindProjectFolder()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            foreach (string folder in new[] { dir.FullName, Path.Combine(dir.FullName, "BirdsApp") })
            {
                if (File.Exists(Path.Combine(folder, "BirdsApp.csproj"))) return folder;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
