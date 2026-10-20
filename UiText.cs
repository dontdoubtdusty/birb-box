using System.Numerics;
using Raylib_cs;

// All in-game text (everything except the ImGui windows) is drawn through here, in Quicksand.
// Draw and Measure work like Raylib.DrawText and Raylib.MeasureText, so the sizes used around
// the code mean the same thing they did with raylib's built-in font.
// The font is loaded once per size, the first time that size is used, so every size stays sharp.
public static class UiText
{
    private const string FontFile = "fonts/Quicksand-SemiBold.ttf";

    // Quicksand's capitals are shorter than the built-in font's at the same size,
    // so sizes are scaled up to keep text about as tall as before.
    private const float SizeScale = 1.25f;

    // Quicksand also sits a little lower in its line, so text is nudged up to keep layouts lined up.
    private const float Lift = 0.2f;

    private static readonly Dictionary<int, Font> fonts = new();

    public static void Draw(string text, int x, int y, int size, Color color)
    {
        Font font = Get(size);
        Vector2 position = new Vector2(x, y - (int)(size * Lift));
        Raylib.DrawTextEx(font, text, position, font.BaseSize, 0f, color);
    }

    public static int Measure(string text, int size)
    {
        Font font = Get(size);
        return (int)Raylib.MeasureTextEx(font, text, font.BaseSize, 0f).X;
    }

    public static void Unload()
    {
        foreach (Font font in fonts.Values) Raylib.UnloadFont(font);
        fonts.Clear();
    }

    private static Font Get(int size)
    {
        if (!fonts.TryGetValue(size, out Font font))
        {
            int pixelSize = (int)MathF.Round(size * SizeScale);
            font = Raylib.LoadFontEx(Assets.PathTo(FontFile), pixelSize, null!, 0);
            fonts[size] = font;
        }
        return font;
    }
}
