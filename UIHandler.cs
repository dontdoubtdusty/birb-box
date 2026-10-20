using System.Numerics;
using ImGuiNET;
using Raylib_cs;

// The ImGui tool panels: settings, bird tracker and the log.
// Hidden by default; Tab shows and hides them. The always-visible HUD is in Hud.cs and MiniMap.cs.
public class UIHandler
{
    public bool ShowTools = false;

    // Shown here under "Controls" and on the Esc menu's Controls page.
    public static readonly string[] ControlLines =
    {
        "Drag sideways: turn the box   Drag up/down: tilt",
        "Wheel on hologram or map: zoom view",
        "Wheel elsewhere: move closer / farther",
        "WASD: pan (Shift = faster)",
        "Hold right mouse: pour bucket",
        "Click bird: select   Space: pause",
        "Click name on bird card: rename",
        "Click minimap: jump view   +/-: zoom levels",
        "Click gunk: scrape (needs Muck Scraper)",
        "Click feeder: pick up   Click dirt: set down",
        "Store: click Zoo Cam to go back",
        "Tab: show/hide tools   F: fullscreen",
        "B: Birb Designer",
        "F9: screenshot   Esc: menu",
    };

    private int scatterCount = 10;
    private bool onlyBirdsInView = true;
    private int lastLogVersion = -1;

    public void Draw(GameEngine engine)
    {
        engine.HoveredBird = null;
        if (!ShowTools) return;

        DrawControlPanel(engine);
        DrawBirdTracker(engine);
        DrawEventLog();
    }

    // ---------------- CONTROL PANEL ----------------

    private void DrawControlPanel(GameEngine engine)
    {
        ImGui.SetNextWindowPos(new Vector2(12, 150), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(290, 0), ImGuiCond.FirstUseEver);
        ImGui.Begin("Birb Box");

        ImGui.Text($"Window center: {engine.View.Center.X:0.0}, {engine.View.Center.Y:0.0}");
        ImGui.Text($"FPS {Raylib.GetFPS()}   frame {Raylib.GetFrameTime() * 1000f:0.0} ms   update {engine.UpdateMs:0.00} ms");
        ImGui.Text($"Birds {World.Instance.GetAllEntities<Bird>().Count}   food {World.Instance.GetAllEntities<Food>().Count}");

        if (ImGui.Button(engine.Paused ? "Play" : "Pause", new Vector2(80, 0)))
        {
            engine.Paused = !engine.Paused;
        }
        ImGui.SameLine();
        if (ImGui.Button("Fullscreen (F)"))
        {
            engine.ToggleFullscreen();
        }
        ImGui.SliderFloat("Sim speed", ref engine.SimSpeed, 0.25f, 4f, "%.2fx");

        ImGui.SeparatorText("Birds");
        BirdSpawner birds = engine.BirdSpawner;
        ImGui.SliderFloat("Hunger rate", ref birds.HungerTickdownRate, 0.5f, 20f, "%.1f s per 5%%");
        ImGui.SliderFloat("Arrivals", ref birds.ArrivalsPerFoodPerMinute, 0f, 3f, "%.2f per food/min");
        ImGui.SliderInt("Max birds", ref birds.MaxBirds, 5, 150);
        ImGui.SliderFloat("Gunk penalty", ref birds.GunkPenalty, 0f, 0.5f, "%.2f per pile");
        if (ImGui.Button("Call a bird"))
        {
            birds.SpawnArrival();
        }

        ImGui.SeparatorText("Food");
        FoodSpawner food = engine.FoodSpawner;
        ImGui.Checkbox("Natural seed drops", ref food.NaturalDrops);
        ImGui.SliderFloat("Drops", ref food.DropsPerMinute, 0f, 60f, "%.0f per min");
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("Amount", ref scatterCount);
        scatterCount = Math.Clamp(scatterCount, 1, 100);
        if (ImGui.Button("Scatter in view")) food.ScatterInView(scatterCount, engine.View);
        ImGui.SameLine();
        if (ImGui.Button("Scatter across world")) food.ScatterAcrossWorld(scatterCount);
        if (ImGui.Button("Cheat: +10 coins")) engine.Coins += 10;

        if (ImGui.CollapsingHeader("Hologram"))
        {
            HologramRenderer r = engine.Renderer;
            ImGui.ColorEdit3("Color", ref r.HoloColor);
            ImGui.SliderFloat("Glow", ref r.GlowStrength, 0f, 3f);
            ImGui.SliderFloat("Scanlines", ref r.ScanlineStrength, 0f, 0.5f);
            ImGui.Checkbox("Smooth edges", ref r.SmoothEdges);
            ImGui.SliderFloat("Width", ref engine.View.RadiusScale, 0.6f, ViewWindow.MaxRadiusScale, "%.2f");
            ImGui.SliderFloat("Fog start", ref engine.View.FogStart, 0.3f, 0.95f, "%.2f");
            ImGui.Checkbox("Projector base", ref r.ShowProjector);
        }

        if (ImGui.CollapsingHeader("Controls"))
        {
            foreach (string line in ControlLines) ImGui.TextDisabled(line);
        }

        ImGui.End();
    }

    // ---------------- BIRD TRACKER ----------------

    private void DrawBirdTracker(GameEngine engine)
    {
        ImGui.SetNextWindowPos(new Vector2(ImGui.GetIO().DisplaySize.X - 262, 270), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(250, 230), ImGuiCond.FirstUseEver);
        ImGui.Begin("Birbs Tracker");

        ImGui.Checkbox("Only birds in view", ref onlyBirdsInView);
        ImGui.Separator();

        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (onlyBirdsInView && !engine.View.Contains(bird.Position)) continue;

            ImGui.PushID(bird.EntityID);
            if (ImGui.Selectable($"{bird.Name}  ({bird.State})", bird == engine.SelectedBird))
            {
                engine.SelectedBird = bird == engine.SelectedBird ? null : bird;
            }
            if (ImGui.IsItemHovered())
            {
                engine.HoveredBird = bird;
                ImGui.SetTooltip($"HUNGER: {bird.Hunger:0.00}\nSPEED: {bird.Speed * 100:0}\nAGE: {bird.FormatAge()}");
            }
            ImGui.PopID();
        }

        ImGui.End();
    }

    // ---------------- EVENT LOG ----------------

    private void DrawEventLog()
    {
        Vector2 display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(new Vector2(display.X - 412, display.Y - 380), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(400, 130), ImGuiCond.FirstUseEver);
        ImGui.Begin("Bird Log");

        foreach (string entry in EventLog.Entries)
        {
            ImGui.TextWrapped(entry);
        }

        // Scroll to the bottom when something new is logged
        if (EventLog.Version != lastLogVersion)
        {
            ImGui.SetScrollHereY(1f);
            lastLogVersion = EventLog.Version;
        }

        ImGui.End();
    }

    // ---------------- STYLE ----------------

    // The panels use Chakra Petch, a squared-off sci-fi font. The in-box text stays Quicksand (see UiText).
    public static void LoadFont()
    {
        ImGui.GetIO().Fonts.AddFontFromFileTTF(Assets.PathTo("fonts/ChakraPetch-Medium.ttf"), 16f);
    }

    // Dark panels with teal accents to match the hologram.
    public void ApplyStyle()
    {
        ImGuiStylePtr style = ImGui.GetStyle();
        style.WindowRounding = 6f;
        style.FrameRounding = 4f;
        style.GrabRounding = 4f;
        style.WindowBorderSize = 1f;

        Vector4 teal = new Vector4(0.36f, 0.86f, 0.82f, 1f);
        Vector4 tealDim = new Vector4(0.36f, 0.86f, 0.82f, 0.35f);
        Vector4 tealFaint = new Vector4(0.36f, 0.86f, 0.82f, 0.18f);

        var colors = style.Colors;
        colors[(int)ImGuiCol.WindowBg] = new Vector4(0.07f, 0.09f, 0.12f, 0.88f);
        colors[(int)ImGuiCol.Border] = tealFaint;
        colors[(int)ImGuiCol.TitleBg] = new Vector4(0.08f, 0.11f, 0.14f, 1f);
        colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.10f, 0.18f, 0.20f, 1f);
        colors[(int)ImGuiCol.FrameBg] = new Vector4(0.12f, 0.15f, 0.19f, 1f);
        colors[(int)ImGuiCol.FrameBgHovered] = tealFaint;
        colors[(int)ImGuiCol.FrameBgActive] = tealDim;
        colors[(int)ImGuiCol.Button] = tealFaint;
        colors[(int)ImGuiCol.ButtonHovered] = tealDim;
        colors[(int)ImGuiCol.ButtonActive] = teal * new Vector4(1, 1, 1, 0.6f);
        colors[(int)ImGuiCol.Header] = tealFaint;
        colors[(int)ImGuiCol.HeaderHovered] = tealDim;
        colors[(int)ImGuiCol.HeaderActive] = tealDim;
        colors[(int)ImGuiCol.SliderGrab] = teal;
        colors[(int)ImGuiCol.SliderGrabActive] = teal;
        colors[(int)ImGuiCol.CheckMark] = teal;
        colors[(int)ImGuiCol.PlotHistogram] = teal * new Vector4(1, 1, 1, 0.7f);
        colors[(int)ImGuiCol.Separator] = tealFaint;
    }
}
