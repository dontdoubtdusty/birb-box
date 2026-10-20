using System.Numerics;
using Raylib_cs;

// The Esc menu. Opening it pauses the box; closing it puts the pause state back how it was.
// Drawn with plain Raylib in the same panel style as the HUD.
public class PauseMenu
{
    private enum Page { Main, Controls, ConfirmQuit }
    private enum Choice { Resume, Designer, Fullscreen, Controls, Quit, Back, YesQuit }

    public bool IsOpen { get; private set; }
    public bool QuitRequested { get; private set; }

    private Page page = Page.Main;
    private bool wasPaused;    // the pause state from before the menu opened

    // LAYOUT
    private const int PanelWidth = 340;
    private const int Pad = 20;
    private const int TitleSize = 20;
    private const int TextSize = 12;
    private const int LineHeight = 18;
    private const int ButtonHeight = 36;
    private const int ButtonGap = 10;
    private static readonly Color Shade = new Color(0, 0, 0, 140);

    // ---------------- UPDATE (no drawing) ----------------

    // Esc opens the menu, steps back from a sub-page, or closes it from the main page.
    public void Update(GameEngine engine)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (!IsOpen) Open(engine);
            else if (page != Page.Main) page = Page.Main;
            else Close(engine);
            return;
        }

        if (!IsOpen || !Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        List<(Choice choice, string label)> buttons = Buttons(engine);
        Rectangle panel = PanelRect(buttons.Count, PageText().Length);
        for (int i = 0; i < buttons.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ButtonRect(panel, i, buttons.Count)))
            {
                Pick(buttons[i].choice, engine);
                return;
            }
        }
    }

    private void Open(GameEngine engine)
    {
        IsOpen = true;
        page = Page.Main;
        wasPaused = engine.Paused;
        engine.Paused = true;
        engine.HoveredBird = null;
    }

    private void Close(GameEngine engine)
    {
        IsOpen = false;
        engine.Paused = wasPaused;
    }

    private void Pick(Choice choice, GameEngine engine)
    {
        switch (choice)
        {
            case Choice.Resume: Close(engine); break;
            case Choice.Designer:
                Close(engine);    // first, so the Designer remembers the pause state from before the menu
                engine.Designer.Toggle(engine);
                break;
            case Choice.Fullscreen: engine.ToggleFullscreen(); break;
            case Choice.Controls: page = Page.Controls; break;
            case Choice.Quit: page = Page.ConfirmQuit; break;
            case Choice.Back: page = Page.Main; break;
            case Choice.YesQuit: QuitRequested = true; break;
        }
    }

    // ---------------- PAGE CONTENTS ----------------

    private List<(Choice, string)> Buttons(GameEngine engine)
    {
        switch (page)
        {
            case Page.Controls:
                return new() { (Choice.Back, "Back") };
            case Page.ConfirmQuit:
                return new() { (Choice.YesQuit, "Yes, quit"), (Choice.Back, "Cancel") };
            default:
                return new()
                {
                    (Choice.Resume, "Resume"),
                    (Choice.Designer, engine.Designer.IsOpen ? "Close Birb Designer" : "Birb Designer"),
                    (Choice.Fullscreen, engine.IsFullscreen ? "Windowed" : "Fullscreen"),
                    (Choice.Controls, "Controls"),
                    (Choice.Quit, "Quit"),
                };
        }
    }

    private string PageTitle()
    {
        switch (page)
        {
            case Page.Controls: return "CONTROLS";
            case Page.ConfirmQuit: return "QUIT BIRB BOX?";
            default: return "PAUSED";
        }
    }

    private string[] PageText()
    {
        switch (page)
        {
            case Page.Controls: return UIHandler.ControlLines;
            case Page.ConfirmQuit: return new[] { "The world is not saved. The birds will be gone." };
            default: return new[] { GameEngine.Version };
        }
    }

    // ---------------- LAYOUT ----------------

    // Title, then text lines, then buttons, centered on screen. The panel grows to fit.
    private static Rectangle PanelRect(int buttonCount, int textLines)
    {
        int height = Pad + TitleSize + Pad
                   + textLines * LineHeight + Pad
                   + buttonCount * (ButtonHeight + ButtonGap) - ButtonGap + Pad;
        float x = (Raylib.GetScreenWidth() - PanelWidth) / 2f;
        float y = (Raylib.GetScreenHeight() - height) / 2f;
        return new Rectangle(x, y, PanelWidth, height);
    }

    // Buttons stack up from the bottom of the panel.
    private static Rectangle ButtonRect(Rectangle panel, int index, int buttonCount)
    {
        float top = panel.Y + panel.Height - Pad - buttonCount * (ButtonHeight + ButtonGap) + ButtonGap;
        return new Rectangle(panel.X + Pad, top + index * (ButtonHeight + ButtonGap), PanelWidth - Pad * 2, ButtonHeight);
    }

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(GameEngine engine, Color accent)
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Shade);

        List<(Choice choice, string label)> buttons = Buttons(engine);
        string[] lines = PageText();
        Rectangle panel = PanelRect(buttons.Count, lines.Length);
        UiDraw.Panel(panel, accent);

        int x = (int)panel.X + Pad;
        int y = (int)panel.Y + Pad;
        UiText.Draw(PageTitle(), x, y, TitleSize, accent);
        y += TitleSize + Pad;

        foreach (string line in lines)
        {
            UiText.Draw(line, x, y, TextSize, Palette.DimText);
            y += LineHeight;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            DrawButton(ButtonRect(panel, i, buttons.Count), buttons[i].label, accent);
        }
    }

    // The shared flat button plus a border, so the choices stand out over the shaded screen.
    private static void DrawButton(Rectangle r, string label, Color accent)
    {
        UiDraw.Button(r, label, accent);
        bool hovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
        Raylib.DrawRectangleLinesEx(r, 1f, Raylib.Fade(accent, hovered ? 0.9f : 0.4f));
    }
}
