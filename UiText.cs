using System.Globalization;
using System.Numerics;
using Raylib_cs;

// All in-game text (everything except the ImGui windows) is drawn through here. Larger text is in
// Arkhane, the game's own font; small text is in Chakra Petch (see SmallMaxSize). Arkhane is not a .ttf: it is a glyph atlas (resources/fonts/arkhane.png) plus a
// list of where each character sits in it (arkhane.txt), both made from claude/arkhane.svg by
// tools/FontAtlas. Draw and Measure work like Raylib.DrawText and Raylib.MeasureText, so the sizes
// used around the code mean the same thing they did with raylib's built-in font.
// One texture serves every size: the atlas is drawn scaled, with mipmaps so small text stays smooth.
public static class UiText
{
    private const string AtlasFile = "fonts/arkhane.png";
    private const string MetricsFile = "fonts/arkhane.txt";

    // How tall a capital is on screen, as a share of the size. Arkhane is bold and wide, so it is
    // kept a little smaller than the old font to leave layouts about as roomy as before.
    private const float CapRatio = 0.78f;

    // Where the capitals' top sits below the given y, as a share of the size (keeps layouts lined up).
    private const float CapTop = 0.15f;

    // Space between letters and the width of a space, in atlas pixels (the atlas capital is ~96 tall).
    private const float LetterGap = 12f;
    private const float SpaceWidth = 40f;

    private readonly struct Glyph
    {
        public readonly Rectangle Source;   // The cell in the atlas (includes a 1 px soft border).
        public readonly float TopOffset;    // The cell's top against the baseline, in atlas pixels.
        public Glyph(Rectangle source, float topOffset) { Source = source; TopOffset = topOffset; }
        public float Advance => Source.Width - 2f + LetterGap;
    }

    private static readonly Dictionary<char, Glyph> glyphs = new();
    private static Texture2D atlas;
    private static float capPixels = 96f;
    private static bool loaded;

    // Small text is hard to read in Arkhane, so sizes up to this use Chakra Petch, a squared-off sans
    // whose cut corners match Arkhane's. Bigger sizes use Arkhane.
    private const int SmallMaxSize = 18;
    private const string SmallFontFile = "fonts/ChakraPetch-Medium.ttf";
    private const float SmallSizeScale = 1.2f;   // Chakra Petch's capitals are a bit short for the size.
    private const float SmallLift = 0.2f;        // Nudges small text up to sit where the old text did.

    // Chakra Petch loaded once per size, the first time that size is used, so every size stays sharp.
    private static readonly Dictionary<int, Font> smallFonts = new();

    // Draws one line of text with its top-left corner at x, y.
    public static void Draw(string text, int x, int y, int size, Color color)
    {
        if (size <= SmallMaxSize)
        {
            Font font = SmallFont(size);
            Raylib.DrawTextEx(font, text, new Vector2(x, y - (int)(size * SmallLift)), font.BaseSize, 0f, color);
            return;
        }
        Load();
        float k = size * CapRatio / capPixels;
        float pen = x;
        float baseline = y + size * CapTop + capPixels * k;
        foreach (char c in text)
        {
            if (c == ' ') { pen += SpaceWidth * k; continue; }
            Glyph g = Find(c);
            Rectangle dest = new Rectangle(
                MathF.Round(pen - k), MathF.Round(baseline + g.TopOffset * k),
                g.Source.Width * k, g.Source.Height * k);
            Raylib.DrawTexturePro(atlas, g.Source, dest, Vector2.Zero, 0f, color);
            pen += g.Advance * k;
        }
    }

    // Width of a line of text in pixels, as Draw would lay it out.
    public static int Measure(string text, int size)
    {
        if (size <= SmallMaxSize)
        {
            Font font = SmallFont(size);
            return (int)Raylib.MeasureTextEx(font, text, font.BaseSize, 0f).X;
        }
        Load();
        float k = size * CapRatio / capPixels;
        float width = 0f;
        foreach (char c in text)
            width += (c == ' ' ? SpaceWidth : Find(c).Advance) * k;
        return (int)width;
    }

    private static Font SmallFont(int size)
    {
        if (!smallFonts.TryGetValue(size, out Font font))
        {
            int pixelSize = (int)MathF.Round(size * SmallSizeScale);
            font = Raylib.LoadFontEx(Assets.PathTo(SmallFontFile), pixelSize, null!, 0);
            smallFonts[size] = font;
        }
        return font;
    }

    public static void Unload()
    {
        foreach (Font font in smallFonts.Values) Raylib.UnloadFont(font);
        smallFonts.Clear();
        if (!loaded) return;
        Raylib.UnloadTexture(atlas);
        glyphs.Clear();
        loaded = false;
    }

    // A character's glyph; anything the font does not have shows as a question mark.
    private static Glyph Find(char c) => glyphs.TryGetValue(c, out Glyph g) ? g : glyphs['?'];

    // Reads the atlas and its glyph list the first time text is used.
    private static void Load()
    {
        if (loaded) return;
        atlas = Raylib.LoadTexture(Assets.PathTo(AtlasFile));
        Raylib.GenTextureMipmaps(ref atlas);
        Raylib.SetTextureFilter(atlas, TextureFilter.Trilinear);

        var inv = CultureInfo.InvariantCulture;
        foreach (string line in File.ReadAllLines(Assets.PathTo(MetricsFile)))
        {
            string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 2 && p[0] == "cap") { capPixels = float.Parse(p[1], inv); continue; }
            if (p.Length != 6) continue;
            char c = (char)int.Parse(p[0], inv);
            var source = new Rectangle(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv), float.Parse(p[4], inv));
            glyphs[c] = new Glyph(source, float.Parse(p[5], inv));
        }
        loaded = true;
    }
}
