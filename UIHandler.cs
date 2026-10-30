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
        "Click the Birb Cube logo: power on/off",
        "Drag sideways: turn the cube   Drag up/down: tilt",
        "Wheel on hologram or map: zoom view",
        "Wheel elsewhere: move closer / farther",
        "Double-click cube: Hologram View (again: leave)",
        "Hologram View: wheel leans in to the glass",
        "WASD: pan (Shift = faster)",
        "Hold right mouse: pour bucket",
        "Click bird: select   Space: pause",
        "Double-click bird (or its name): follow",
        "Click name on bird card: rename",
        "Click minimap: jump view   +/-: zoom levels",
        "Click feeder: pick up   Click dirt: set down",
        "Store: click Zoo Cam to go back",
        "Radio: click buttons, drag or scroll knobs",
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
        ImGui.Begin("Birb Cube");

        ImGui.Text($"Window center: {engine.View.Center.X:0.0}, {engine.View.Center.Y:0.0}");
        ImGui.Text($"FPS {Raylib.GetFPS()}   frame {Raylib.GetFrameTime() * 1000f:0.0} ms   update {engine.UpdateMs:0.00} ms");
        ImGui.Text($"draw {engine.DrawMs:0.00} ms   model draws {DrawStats.ModelDraws}");
        FramePacing pacing = engine.Pacing;
        ImGui.Text($"slow frames {pacing.RecentSlowFrames} last min, {pacing.SlowFrames} total   {pacing.RefreshRate} Hz {(pacing.TimerCap ? "timer" : "vsync")}");
        ImGui.Text($"Birds {World.Instance.GetAllEntities<Bird>().Count}   food {World.Instance.GetAllEntities<Food>().Count}   sprouts {World.Instance.GetAllEntities<Sprout>().Count}");

        if (ImGui.Button(engine.Paused ? "Play" : "Pause", new Vector2(80, 0)))
        {
            engine.Paused = !engine.Paused;
        }
        ImGui.SameLine();
        if (ImGui.Button("Fullscreen (F)"))
        {
            engine.Window.ToggleFullscreen();
        }
        ImGui.SliderFloat("Sim speed", ref engine.SimSpeed, 0.25f, 4f, "%.2fx");

        SimTuning t = SimTuning.Now, d = SimTuning.Defaults;
        SettingsData start = Settings.Defaults;

        ImGui.SeparatorText("Birds");
        Slider("Hunger rate", ref t.HungerRate, d.HungerRate, 0.5f, 20f, "%.1f s per 5%%");
        Slider("Arrivals", ref t.ArrivalsPerFoodPerMinute, d.ArrivalsPerFoodPerMinute, 0f, 3f, "%.2f per food/min");
        Slider("Max birds", ref t.MaxBirds, d.MaxBirds, 5, 150);
        if (ImGui.Button("Call a bird")) engine.BirdSpawner.SpawnArrival();
        ImGui.SameLine();
        if (ImGui.Button("Call 10 birds"))
        {
            for (int i = 0; i < 10; i++) engine.BirdSpawner.SpawnArrival();
        }
        if (engine.SelectedBird is Bird selected && ImGui.Button("Re-roll selected bird's traits")) selected.RerollPersonality();

        ImGui.SeparatorText("Food");
        FoodSpawner food = engine.FoodSpawner;
        Checkbox("Natural seed drops", ref t.NaturalDrops, d.NaturalDrops);
        Slider("Drops", ref t.DropsPerMinute, d.DropsPerMinute, 0f, 60f, "%.1f per min");
        Slider("Max food", ref t.MaxFood, d.MaxFood, 50, 1000);
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("Amount", ref scatterCount);
        scatterCount = Math.Clamp(scatterCount, 1, 100);
        if (ImGui.Button("Scatter in view")) food.ScatterInView(scatterCount, engine.View);
        ImGui.SameLine();
        if (ImGui.Button("Scatter across world")) food.ScatterAcrossWorld(scatterCount);
        Slider("Sprout chance", ref t.SproutChancePerSeed, d.SproutChancePerSeed, 0f, 0.3f, "%.3f per seed");
        Slider("Sprout lives", ref t.SproutMinutes, d.SproutMinutes, 1f, 60f, "%.0f min");
        Slider("Max sprouts", ref t.MaxSprouts, d.MaxSprouts, 0, 50);

        ImGui.SeparatorText("Coins");
        Slider("Drop chance", ref t.CoinDropChance, d.CoinDropChance, 0f, 1f, "%.2f per meal");
        Slider("Coin lasts", ref t.CoinLifetime, d.CoinLifetime, 5f, 120f, "%.0f s");

        if (ImGui.CollapsingHeader("Bird lifetimes"))
        {
            Slider("Lifespan min", ref t.LifespanMinMinutes, d.LifespanMinMinutes, 5f, 300f, "%.0f min");
            Slider("Lifespan max", ref t.LifespanMaxMinutes, d.LifespanMaxMinutes, 5f, 600f, "%.0f min");
            Slider("Lose interest", ref t.LoseInterestMinutes, d.LoseInterestMinutes, 5f, 180f, "%.0f min");
            Slider("Regulars", ref t.RegularChance, d.RegularChance, 0f, 1f, "%.2f of arrivals");
            ImGui.TextDisabled("Lifespan applies to birds that arrive from now on.");
        }

        if (ImGui.CollapsingHeader("Bird behavior"))
        {
            Slider("Call every min", ref t.CallDelayMin, d.CallDelayMin, 5f, 120f, "%.0f s");
            Slider("Call every max", ref t.CallDelayMax, d.CallDelayMax, 5f, 240f, "%.0f s");
            Slider("Rock perch", ref t.PerchChance, d.PerchChance, 0f, 1f, "%.2f chance");
            Slider("Home radius", ref t.HomeRadius, d.HomeRadius, 2f, 20f, "%.1f");
        }

        if (ImGui.CollapsingHeader("World"))
        {
            Slider("Seed spoils", ref t.SpoilSpeed, d.SpoilSpeed, 0.1f, 10f, "%.2fx");
            Slider("Grass wears", ref t.WearSpeed, d.WearSpeed, 0f, 5f, "%.2fx");
            Slider("Grass regrows", ref t.RegrowSpeed, d.RegrowSpeed, 0f, 10f, "%.2fx");
        }

        if (ImGui.CollapsingHeader("Water"))
        {
            Slider("Sip chance", ref t.DrinkChance, d.DrinkChance, 0f, 1f, "%.2f per pause");
            Slider("Bath chance", ref t.BathChance, d.BathChance, 0f, 1f, "%.2f per pause");
            Slider("Max bathers", ref t.MaxBathers, d.MaxBathers, 0, 15);
        }
        t.Clamp();    // a min slider pushed past its max drags the max along

        if (ImGui.CollapsingHeader("Day and night"))
        {
            DrawClockTools(engine.Clock, t, d);
        }

        if (ImGui.CollapsingHeader("Weather"))
        {
            DrawWeatherTools(engine.Weather, engine.Clock, t, d);
        }

        if (ImGui.CollapsingHeader("Cheats"))
        {
            if (ImGui.Button("+10 coins")) engine.Coins += 10;
            ImGui.SameLine();
            if (ImGui.Button("Fill bucket")) FillBucket(engine.Bucket.Bucket);
            if (ImGui.Button("Clear ground seed")) FoodSpawner.ClearGroundSeed();
            ImGui.SameLine();
            if (ImGui.Button("Sprout in view")) SproutInView(engine.View);
            if (ImGui.Button("Send a bird to bathe")) SendToWater(engine.View, bath: true);
            ImGui.SameLine();
            if (ImGui.Button("...to sip")) SendToWater(engine.View, bath: false);
        }

        if (ImGui.CollapsingHeader("Hologram"))
        {
            HologramRenderer r = engine.Renderer;
            ImGui.ColorEdit3("Color", ref r.HoloColor);
            Slider("Glow", ref r.GlowStrength, start.Glow, 0f, 3f);
            Slider("Scanlines", ref r.ScanlineStrength, start.Scanlines, 0f, 0.5f);
            Slider("Width", ref engine.View.CubeScale, start.CubeWidth, 0.5f, ViewWindow.MaxCubeScale);
            Slider("Fog start", ref engine.View.FogStart, start.FogStart, 0.3f, 0.99f);
            Slider("Edge width", ref r.EdgeWidth, start.EdgeWidth, 0.1f, 2.5f);
            Slider("Edge strength", ref r.EdgeStrength, start.EdgeStrength, 0f, 2f);
            Checkbox("Projector base", ref r.ShowProjector, start.ShowProjector);
        }

        if (ImGui.CollapsingHeader("Controls"))
        {
            foreach (string line in ControlLines) ImGui.TextDisabled(line);
        }

        ImGui.End();
    }

    // ---------------- SLIDERS WITH DEFAULTS ----------------
    // Each one is tinted brass while it's off its default, shows the default on hover, and goes back to it on right-click.

    private static readonly Vector4 ChangedTint = new Vector4(0.42f, 0.30f, 0.10f, 1f);

    // Cheat: the wandering, not hungry bird in view nearest the pool sets off for it now (to bathe or to sip), whatever its chances.
    // Says so in the log if no bird could go.
    private static void SendToWater(ViewWindow view, bool bath)
    {
        Bird? pick = World.Instance.GetAllEntities<Bird>()
            .Where(b => b.State == BirdState.Wandering && !b.IsFlying && !b.Roosting && !b.IsHungry() && view.Contains(b.Position))
            .OrderBy(b => Pool.Distance(b.Position))
            .FirstOrDefault();
        if (pick == null || !pick.Mover.StartWaterVisit(bath)) EventLog.Add("No bird in view could go to the water.");
    }

    // Cheat: a grown sprout of a random seed somewhere near the middle of the view, its heads about to fill.
    private static void SproutInView(ViewWindow view)
    {
        float reach = view.WorldHalfWidth * 0.6f;
        for (int tries = 0; tries < 10; tries++)
        {
            Vector3 spot = new Vector3(view.Center.X + Rng.Range(-reach, reach), 0f, view.Center.Y + Rng.Range(-reach, reach));
            if (Sprout.TryPlant(spot, Seeds.All[Rng.Next(Seeds.All.Length)], grown: true)) return;
        }
    }

    // The world's clock (see WorldClock): today's sun times, the day's length and how dark night gets,
    // and dev controls to set the hour, skip ahead, or hold the clock still.
    private static void DrawClockTools(WorldClock clock, SimTuning t, SimTuning d)
    {
        ImGui.Text($"{clock.DateText}, year {clock.Year}  {clock.TimeText}  ({clock.Phase})");
        ImGui.Text($"Sunrise {Hours(clock.Sunrise)}  sunset {Hours(clock.Sunset)}  first glow {Hours(clock.FirstGlow)}  last {Hours(clock.LastGlow)}");
        ImGui.Text($"Sun {clock.SunElevation:0} deg above the horizon");

        float hour = clock.Hour;
        if (ImGui.SliderFloat("Hour", ref hour, 0f, 23.99f, "%.2f")) clock.SetHour(hour);
        if (ImGui.Button("Next day")) clock.NextDay();
        ImGui.SameLine();
        if (ImGui.Button("Next season")) clock.NextSeason();
        ImGui.SameLine();
        ImGui.Checkbox("Hold the clock", ref clock.Held);

        Slider("Day length", ref t.DayMinutes, d.DayMinutes, 1f, 60f, "%.1f min");
        Slider("Night light", ref t.NightBrightness, d.NightBrightness, 0.2f, 3f, "%.2fx");
        Slider("Stay the night", ref t.RoostChance, d.RoostChance, 0f, 1f, "%.2f chance");
        Slider("Max roosting", ref t.MaxRoosting, d.MaxRoosting, 0, 30);
    }

    // The weather (see Weather.cs): what it's doing and for how long, and dev controls to start or stop the rain,
    // soak the ground at once (so puddles can form), or hold the weather as it is.
    private static void DrawWeatherTools(Weather weather, WorldClock clock, SimTuning t, SimTuning d)
    {
        string spell = weather.Raining ? (weather.Strength < 1f ? "drizzle" : "steady rain") : "dry";
        ImGui.Text($"{weather.Word}: {spell} spell, {weather.HoursLeft:0.0} h left");
        ImGui.Text($"Cloud {weather.Cloud:0.00}  rain {weather.Rain:0.00}  ground wet {weather.GroundWet:0.00}  puddles {weather.Puddles.All.Count}");

        if (ImGui.Button("Rain")) weather.StartRain(1f);
        ImGui.SameLine();
        if (ImGui.Button("Drizzle")) weather.StartRain(Weather.DrizzleStrength);
        ImGui.SameLine();
        if (ImGui.Button("Clear up")) weather.ClearUp(clock.Season);
        ImGui.SameLine();
        if (ImGui.Button("Soak the ground")) weather.Soak();
        ImGui.Checkbox("Hold the weather", ref weather.Held);

        Slider("Rain comes", ref t.RainOften, d.RainOften, 0.1f, 10f, "%.2fx as often");
        Slider("Rain spoils seed", ref t.RainSpoil, d.RainSpoil, 1f, 10f, "%.2fx");
    }

    // 5.5 -> "05:30"
    private static string Hours(float hour)
    {
        int minutes = (int)(hour * 60f);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    private static void Slider(string label, ref float value, float def, float min, float max, string format = "%.2f")
    {
        bool changed = MathF.Abs(value - def) > 0.0001f;
        if (changed) ImGui.PushStyleColor(ImGuiCol.FrameBg, ChangedTint);
        ImGui.SliderFloat(label, ref value, min, max, format);
        if (changed) ImGui.PopStyleColor();
        if (DefaultHint($"{def:0.###}")) value = def;
    }

    private static void Slider(string label, ref int value, int def, int min, int max)
    {
        bool changed = value != def;
        if (changed) ImGui.PushStyleColor(ImGuiCol.FrameBg, ChangedTint);
        ImGui.SliderInt(label, ref value, min, max);
        if (changed) ImGui.PopStyleColor();
        if (DefaultHint(def.ToString())) value = def;
    }

    private static void Checkbox(string label, ref bool value, bool def)
    {
        bool changed = value != def;
        if (changed) ImGui.PushStyleColor(ImGuiCol.FrameBg, ChangedTint);
        ImGui.Checkbox(label, ref value);
        if (changed) ImGui.PopStyleColor();
        if (DefaultHint(def ? "on" : "off")) value = def;
    }

    // The tooltip for the item just drawn. True when it was right-clicked.
    private static bool DefaultHint(string def)
    {
        if (!ImGui.IsItemHovered()) return false;
        ImGui.SetTooltip($"Default: {def}   (right-click to reset)");
        return ImGui.IsItemClicked(ImGuiMouseButton.Right);
    }

    // Cheat: tops the bucket up with an even share of every bagged seed, unstirred.
    private static void FillBucket(SeedBucket bucket)
    {
        float share = (SeedBucket.Capacity - bucket.Total) / Seeds.Bags.Length;
        foreach (SeedType seed in Seeds.Bags) bucket.Pour(seed, share);
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
            // Click: select (again: deselect). Double-click: select and follow.
            // AllowDoubleClick makes the second click of a double-click report on its press, and skip its release.
            if (ImGui.Selectable($"{bird.Name}  ({bird.State})  {Traits.Words(bird.Personality)}", bird == engine.SelectedBird, ImGuiSelectableFlags.AllowDoubleClick))
            {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    engine.SelectedBird = bird;
                    engine.FollowedBird = bird;
                }
                else
                {
                    engine.SelectedBird = bird == engine.SelectedBird ? null : bird;
                }
            }
            if (ImGui.IsItemHovered())
            {
                engine.HoveredBird = bird;
                ImGui.SetTooltip($"HUNGER: {bird.Hunger:0.00}\nSPEED: {bird.Speed * 100:0}\nAGE: {bird.FormatAge()}\n"
                                 + $"FOND OF: {Seeds.Get(bird.FavoriteSeed).Name}\n"
                                 + $"BUILD x{bird.Build:0.00}  SHADE {bird.Shade:0.00}  WARMTH {bird.Warmth:+0.00;-0.00}\n{NatureNumbers(bird.Nature)}");
            }
            ImGui.PopID();
        }

        ImGui.End();
    }

    // The raw numbers a bird's traits set (dev): personal space, perching, appetite, roaming, voice.
    private static string NatureNumbers(TraitEffects n)
    {
        return $"SPACE x{n.PersonalSpace:0.0}  PERCH x{n.PerchChance:0.0}  COMPANY {n.Company}\n"
             + $"HUNGER x{n.HungerSpeed:0.0}  HUNGRY <{n.HungryAt:0.00}  FULL {n.FullAt:0.00}\n"
             + $"BITE x{n.Nourish:0.0}  BITE TIME x{n.BiteTime:0.0}  RANGE x{n.FoodRange:0.0}\n"
             + $"HOME x{n.HomeRadius:0.0}  BORED x{n.LoseInterest:0.0}  RETURN x{n.ReturnWeight:0.0}\n"
             + $"CALLS x{n.CallDelay:0.0}  IGNORE HUSH {n.IgnoreHush:0.0}";
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
