using Raylib_cs;

// Panel and button drawing shared by the HUD, the minimap, the bucket and the Esc menu.
public static class UiDraw
{
    private const int ButtonTextSize = 20;

    // Dark see-through box with a faint border and brighter corner ticks.
    public static void Panel(Rectangle r, Color accent)
    {
        Raylib.DrawRectangleRec(r, Palette.PanelFill);
        Raylib.DrawRectangleLinesEx(r, 1f, Raylib.Fade(accent, 0.3f));

        int x = (int)r.X, y = (int)r.Y, w = (int)r.Width, h = (int)r.Height;
        const int tick = 10;
        Raylib.DrawRectangle(x, y, tick, 2, accent);
        Raylib.DrawRectangle(x, y, 2, tick, accent);
        Raylib.DrawRectangle(x + w - tick, y, tick, 2, accent);
        Raylib.DrawRectangle(x + w - 2, y, 2, tick, accent);
        Raylib.DrawRectangle(x, y + h - 2, tick, 2, accent);
        Raylib.DrawRectangle(x, y + h - tick, 2, tick, accent);
        Raylib.DrawRectangle(x + w - tick, y + h - 2, tick, 2, accent);
        Raylib.DrawRectangle(x + w - 2, y + h - tick, 2, tick, accent);
    }

    // A flat button with its label in the middle. Lit means switched on (like Follow while following).
    public static void Button(Rectangle r, string label, Color accent, bool lit = false)
    {
        bool hovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
        Color fill = lit ? Raylib.Fade(accent, 0.8f) : Raylib.Fade(accent, hovered ? 0.35f : 0.18f);
        Raylib.DrawRectangleRec(r, fill);

        int textWidth = UiText.Measure(label, ButtonTextSize);
        int x = (int)(r.X + (r.Width - textWidth) / 2);
        int y = (int)(r.Y + (r.Height - ButtonTextSize) / 2);
        UiText.Draw(label, x, y, ButtonTextSize, lit ? Palette.BrassDark : Palette.Text);
    }
}
