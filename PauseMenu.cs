using System.Numerics;
using Raylib_cs;

// The Esc menu. Opening it pauses the box; closing it puts the pause state back how it was.
// Drawn with plain Raylib in the same panel style as the HUD.
public class PauseMenu
{
    private enum Page { Main, Settings, Controls, ConfirmQuit, ConfirmNewWorld }
    private enum Choice { Resume, Designer, Fullscreen, Settings, FullscreenType, NameTags, HandYellow, Quality, Controls, NewWorld, Quit, Back, YesQuit, YesNewWorld }

    public bool IsOpen { get; private set; }
    public bool QuitRequested { get; private set; }
    public bool ShowingSettings => IsOpen && page == Page.Settings;    // the right hand is held up to show its color

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

    // SLIDERS (Settings page). The volume ones read and write a level in Sounds; the last sets the hands' shade.
    // Func is "a bit of code that returns a value", Action is "a bit of code that takes one".
    // IsHandColor: drawn as a strip of the hand shades instead of a percent bar.
    private record Slider(string Label, Func<float> Get, Action<float> Set, bool IsHandColor = false);
    private static readonly Slider[] sliders =
    {
        new("Master", () => Sounds.Master, v => Sounds.Master = v),
        new("Hologram hum", () => Sounds.HumLevel, v => Sounds.HumLevel = v),
        new("Birds", () => Sounds.BirdLevel, v => Sounds.BirdLevel = v),
        new("Effects", () => Sounds.EffectsLevel, v => Sounds.EffectsLevel = v),
        new("Hand color", () => GameEngine.Instance.Hands.Tone, SetHandTone, IsHandColor: true),
    };

    // Moving the shade slider also leaves Groening yellow.
    private static void SetHandTone(float tone)
    {
        GameEngine.Instance.Hands.Tone = tone;
        GameEngine.Instance.Hands.Yellow = false;
    }
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

    // Settings are only changed in here (or by the F key and the radio knob, caught on quit), so they're saved on the way out.
    private void Close(GameEngine engine)
    {
        IsOpen = false;
        engine.Paused = wasPaused;
        Settings.Save(engine);
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
            case Choice.Fullscreen: engine.Window.ToggleFullscreen(); break;
            case Choice.Settings: page = Page.Settings; break;
            case Choice.FullscreenType: engine.Window.SetTrueFullscreen(!engine.Window.TrueFullscreen); break;
            case Choice.NameTags: CycleNameTags(engine.Renderer.ChatMessages); break;
            case Choice.HandYellow: engine.Hands.Yellow = !engine.Hands.Yellow; break;
            case Choice.Quality: CycleQuality(engine.Renderer); break;
            case Choice.Controls: page = Page.Controls; break;
            case Choice.NewWorld: page = Page.ConfirmNewWorld; break;
            case Choice.YesNewWorld:
                engine.Saves.StartNewWorld(engine);
                engine.Saves.Save(engine);
                EventLog.Add("The old patch fades into the fog. A new one flickers into view.");
                Close(engine);
                break;
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
                    (Choice.FullscreenType, engine.Window.TrueFullscreen ? "Fullscreen type: True" : "Fullscreen type: Window"),
                    (Choice.NameTags, NameTagsLabel(engine.Renderer.ChatMessages.NameLabels)),
                    (Choice.HandYellow, engine.Hands.Yellow ? "Hands: Groening yellow (on)" : "Hands: Groening yellow"),
                    (Choice.Quality, QualityLabel(engine.Renderer.Quality)),
                    (Choice.Back, "Back"),
                };
            case Page.ConfirmQuit:
                return new() { (Choice.YesQuit, "Yes, quit"), (Choice.Back, "Cancel") };
            case Page.ConfirmNewWorld:
                return new() { (Choice.YesNewWorld, "Yes, start over"), (Choice.Back, "Cancel") };
            default:
                return new()
                {
                    (Choice.Resume, "Resume"),
                    (Choice.Designer, engine.Designer.IsOpen ? "Close Birb Designer" : "Birb Designer"),
                    (Choice.Fullscreen, engine.Window.IsFullscreen ? "Windowed" : "Fullscreen"),
                    (Choice.Settings, "Settings"),
                    (Choice.Controls, "Controls"),
                    (Choice.NewWorld, "New world"),
                    (Choice.Quit, "Quit"),
                };
        }
    }

    // Named birds, then all birds, then off, then back around.
    private static void CycleNameTags(ChatMessages chat)
    {
        chat.NameLabels = chat.NameLabels switch
        {
            NameLabelMode.Named => NameLabelMode.All,
            NameLabelMode.All => NameLabelMode.Off,
            _ => NameLabelMode.Named,
        };
    }

    // High, then medium, then low, then back around.
    private static void CycleQuality(HologramRenderer renderer)
    {
        renderer.Quality = renderer.Quality >= 2f ? 1.5f : renderer.Quality >= 1.5f ? 1f : 2f;
    }

    private static string QualityLabel(float quality) => quality switch
    {
        >= 2f => "Hologram quality: High",
        >= 1.5f => "Hologram quality: Medium",
        _ => "Hologram quality: Low",
    };

    private static string NameTagsLabel(NameLabelMode mode) => mode switch
    {
        NameLabelMode.Named => "Name tags: Birds you named",
        NameLabelMode.All => "Name tags: All birds",
        _ => "Name tags: Off",
    };

    private string PageTitle()
    {
        switch (page)
        {
            case Page.Controls: return "CONTROLS";
            case Page.Settings: return "SETTINGS";
            case Page.ConfirmQuit: return "QUIT BIRB CUBE?";
            case Page.ConfirmNewWorld: return "START A NEW WORLD?";
            default: return "PAUSED";
        }
    }

    private string[] PageText()
    {
        switch (page)
        {
            case Page.Controls: return UIHandler.ControlLines;
            case Page.Settings: return new[] { "True fullscreen keeps Windows popups,", "like the volume bar, off the game.", "Settings are kept between runs." };
            case Page.ConfirmQuit: return new[] { "Your terrarium is saved and will be here next time." };
            case Page.ConfirmNewWorld: return new[] { "The birds, seed, feeders, coins and bags here", "will be gone for good." };
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
    private static void DrawSlider(Rectangle row, Slider slider, Color accent, bool held)
    {
        if (slider.IsHandColor)
        {
            DrawHandColorSlider(row, slider, accent, held);
            return;
        }

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

    // The track is a strip of every shade the slider can pick; the knob is filled with the hands' color right now.
    // While Groening yellow is on, the strip dims and the label says so.
    private static void DrawHandColorSlider(Rectangle row, Slider slider, Color accent, bool held)
    {
        Hands hands = GameEngine.Instance.Hands;
        bool hovered = held || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), row);

        UiText.Draw(slider.Label, (int)row.X, (int)row.Y + 4, TextSize, Palette.Text);
        if (hands.Yellow)
        {
            const string Note = "Groening yellow";
            UiText.Draw(Note, (int)(row.X + row.Width) - UiText.Measure(Note, TextSize), (int)row.Y + 4, TextSize, Palette.DimText);
        }

        Rectangle track = TrackRect(row);
        const int Steps = 32;
        float stepWidth = track.Width / Steps;
        float strength = hands.Yellow ? 0.35f : 1f;
        for (int i = 0; i < Steps; i++)
        {
            Color shade = Raylib.Fade(Hands.ToneColor((i + 0.5f) / Steps), strength);
            Raylib.DrawRectangleRec(new Rectangle(track.X + i * stepWidth, track.Y, stepWidth + 1f, track.Height), shade);
        }

        Vector2 knob = new Vector2(track.X + track.Width * slider.Get(), track.Y + track.Height / 2f);
        float radius = hovered ? 8f : 7f;
        Raylib.DrawCircleV(knob, radius + 1.5f, accent);
        Raylib.DrawCircleV(knob, radius, hands.SkinColor);
    }

    // The shared flat button plus a border, so the choices stand out over the shaded screen.
    private static void DrawButton(Rectangle r, string label, Color accent)
    {
        UiDraw.Button(r, label, accent);
        bool hovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
        Raylib.DrawRectangleLinesEx(r, 1f, Raylib.Fade(accent, hovered ? 0.9f : 0.4f));
    }
}
