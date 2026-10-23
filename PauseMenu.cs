using System.Numerics;
using Raylib_cs;

// The Esc menu. Opening it pauses the box; closing it puts the pause state back how it was.
// Drawn with plain Raylib in the same panel style as the HUD.
public class PauseMenu
{
    private enum Page { Main, Settings, Controls, ConfirmQuit }
    private enum Choice { Resume, Designer, Fullscreen, Settings, FullscreenType, Controls, Quit, Back, YesQuit }

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
    private const int SliderRowHeight = 38;    // label line plus the track under it
    private const int TrackHeight = 6;

    // VOLUME SLIDERS (Settings page). Each one reads and writes a level in Sounds;
    // Func is "a bit of code that returns a value", Action is "a bit of code that takes one".
    private record VolumeSlider(string Label, Func<float> Get, Action<float> Set);
    private static readonly VolumeSlider[] sliders =
    {
        new("Master", () => Sounds.Master, v => Sounds.Master = v),
        new("Hologram hum", () => Sounds.HumLevel, v => Sounds.HumLevel = v),
        new("Birds", () => Sounds.BirdLevel, v => Sounds.BirdLevel = v),
        new("Effects", () => Sounds.EffectsLevel, v => Sounds.EffectsLevel = v),
    };
    private int draggingSlider = -1;    // which slider the mouse is holding, -1 for none

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

        if (!IsOpen) return;

        List<(Choice choice, string label)> buttons = Buttons(engine);
        Rectangle panel = PanelRect(buttons.Count, PageText().Length, SliderCount());
        UpdateSliders(panel);

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        for (int i = 0; i < buttons.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ButtonRect(panel, i, buttons.Count)))
            {
                Pick(buttons[i].choice, engine);
                return;
            }
        }
    }

    // Press on a slider to grab it, drag to set it, let go to drop it.
    // The value follows the mouse even if it slides off the track while held.
    private void UpdateSliders(Rectangle panel)
    {
        if (!Raylib.IsMouseButtonDown(MouseButton.Left)) draggingSlider = -1;

        Vector2 mouse = Raylib.GetMousePosition();
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            for (int i = 0; i < SliderCount(); i++)
            {
                if (Raylib.CheckCollisionPointRec(mouse, SliderRect(panel, i))) draggingSlider = i;
            }
        }

        if (draggingSlider >= 0)
        {
            Rectangle track = TrackRect(SliderRect(panel, draggingSlider));
            sliders[draggingSlider].Set(Math.Clamp((mouse.X - track.X) / track.Width, 0f, 1f));
        }
    }

    private int SliderCount() => page == Page.Settings ? sliders.Length : 0;

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
            case Choice.Settings: page = Page.Settings; break;
            case Choice.FullscreenType: engine.SetTrueFullscreen(!engine.TrueFullscreen); break;
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
            case Page.Settings:
                return new()
                {
                    (Choice.FullscreenType, engine.TrueFullscreen ? "Fullscreen type: True" : "Fullscreen type: Window"),
                    (Choice.Back, "Back"),
                };
            case Page.ConfirmQuit:
                return new() { (Choice.YesQuit, "Yes, quit"), (Choice.Back, "Cancel") };
            default:
                return new()
                {
                    (Choice.Resume, "Resume"),
                    (Choice.Designer, engine.Designer.IsOpen ? "Close Birb Designer" : "Birb Designer"),
                    (Choice.Fullscreen, engine.IsFullscreen ? "Windowed" : "Fullscreen"),
                    (Choice.Settings, "Settings"),
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
            case Page.Settings: return "SETTINGS";
            case Page.ConfirmQuit: return "QUIT BIRB BOX?";
            default: return "PAUSED";
        }
    }

    private string[] PageText()
    {
        switch (page)
        {
            case Page.Controls: return UIHandler.ControlLines;
            case Page.Settings: return new[] { "True fullscreen keeps Windows popups,", "like the volume bar, off the game." };
            case Page.ConfirmQuit: return new[] { "The world is not saved. The birds will be gone." };
            default: return new[] { GameEngine.Version };
        }
    }

    // ---------------- LAYOUT ----------------

    // Title, then text lines, then sliders, then buttons, centered on screen. The panel grows to fit.
    private static Rectangle PanelRect(int buttonCount, int textLines, int sliderCount)
    {
        int height = Pad + TitleSize + Pad
                   + textLines * LineHeight + Pad
                   + sliderCount * SliderRowHeight
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

    // Sliders stack down under the text lines. The row is the click area; the track sits near its bottom.
    private Rectangle SliderRect(Rectangle panel, int index)
    {
        float top = panel.Y + Pad + TitleSize + Pad + PageText().Length * LineHeight + Pad;
        return new Rectangle(panel.X + Pad, top + index * SliderRowHeight, PanelWidth - Pad * 2, SliderRowHeight);
    }

    private static Rectangle TrackRect(Rectangle row)
    {
        return new Rectangle(row.X, row.Y + row.Height - TrackHeight - 8, row.Width, TrackHeight);
    }

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(GameEngine engine, Color accent)
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Shade);

        List<(Choice choice, string label)> buttons = Buttons(engine);
        string[] lines = PageText();
        Rectangle panel = PanelRect(buttons.Count, lines.Length, SliderCount());
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

        for (int i = 0; i < SliderCount(); i++)
        {
            DrawSlider(SliderRect(panel, i), sliders[i], accent, held: i == draggingSlider);
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            DrawButton(ButtonRect(panel, i, buttons.Count), buttons[i].label, accent);
        }
    }

    // Label and percent on top, then a track filled up to the value, with a small knob at the end of the fill.
    private static void DrawSlider(Rectangle row, VolumeSlider slider, Color accent, bool held)
    {
        float value = slider.Get();
        bool hovered = held || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), row);

        UiText.Draw(slider.Label, (int)row.X, (int)row.Y + 4, TextSize, Palette.Text);
        string percent = $"{(int)MathF.Round(value * 100)}%";
        int percentWidth = UiText.Measure(percent, TextSize);
        UiText.Draw(percent, (int)(row.X + row.Width) - percentWidth, (int)row.Y + 4, TextSize, Palette.DimText);

        Rectangle track = TrackRect(row);
        Raylib.DrawRectangleRec(track, Palette.PanelDark);
        Raylib.DrawRectangleRec(new Rectangle(track.X, track.Y, track.Width * value, track.Height), Raylib.Fade(accent, hovered ? 0.9f : 0.6f));

        Vector2 knob = new Vector2(track.X + track.Width * value, track.Y + track.Height / 2f);
        Raylib.DrawCircleV(knob, hovered ? 7f : 6f, accent);
    }

    // The shared flat button plus a border, so the choices stand out over the shaded screen.
    private static void DrawButton(Rectangle r, string label, Color accent)
    {
        UiDraw.Button(r, label, accent);
        bool hovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
        Raylib.DrawRectangleLinesEx(r, 1f, Raylib.Fade(accent, hovered ? 0.9f : 0.4f));
    }
}
