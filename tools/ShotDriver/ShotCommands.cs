using System.Globalization;
using System.Numerics;
using Raylib_cs;

// The script commands. One line per command, words split by spaces, # starts a comment.
// The full list with examples is in README.md; keep the two in step.
public static partial class ShotDriver
{
    private record Command(string Usage, int MinArgs, Action<string[]> Run);

    // Built on first use, after the game has started (the lambdas read the running engine).
    private static Dictionary<string, Command>? commandTable;
    private static Dictionary<string, Command> commands => commandTable ??= BuildCommands();

    private static Dictionary<string, Command> BuildCommands() => new()
    {
        // Time passing and pictures
        ["wait"]     = new("wait <sim seconds>", 1, a => Advance(Float(a[0]))),
        ["frames"]   = new("frames <count>", 1, a => Frames(Int(a[0]))),
        ["shot"]     = new("shot <name>", 1, a => Shot(a[0])),
        ["size"]     = new("size <width> <height>", 2, a => { Raylib.SetWindowSize(Int(a[0]), Int(a[1])); Frames(3); }),

        // The box
        ["power"]    = new("power on|off", 1, a => Power(OnOff(a[0]))),
        ["scene"]    = new("scene birds|store", 1, a => Scene(Enum<BoxScene>(a[0]))),
        ["view"]     = new("view room|hologram", 1, a => View(a[0])),
        ["tools"]    = new("tools on|off", 1, a => engine.UI.ShowTools = OnOff(a[0])),

        // The camera and the bird world's window
        ["yaw"]      = new("yaw <degrees, multiple of 10>", 1, a => engine.View.Yaw = Float(a[0]) * Raylib.DEG2RAD),
        ["pitch"]    = new("pitch <degrees>", 1, a => Pitch(Float(a[0]) * Raylib.DEG2RAD)),
        ["distance"] = new("distance <room camera distance>", 1, a => engine.Camera.Distance = Float(a[0])),
        ["zoom"]     = new("zoom <bird world zoom, 0.25 to 3>", 1, a => engine.View.Zoom = Float(a[0])),
        ["center"]   = new("center <x> <z>", 2, a => engine.View.JumpTo(new Vector2(Float(a[0]), Float(a[1])))),

        // Time of day and weather
        ["hour"]     = new("hour <0-24>", 1, a => Hour(Float(a[0]))),
        ["season"]   = new("season spring|summer|autumn|winter", 1, a => SkipToSeason(Enum<Season>(a[0]))),
        ["clock"]    = new("clock hold|run", 1, a => engine.Clock.Held = a[0] == "hold"),
        ["rain"]     = new("rain [strength, default 1]", 0, a => Rain(a.Length > 0 ? Float(a[0]) : 1f)),
        ["drizzle"]  = new("drizzle", 0, a => Rain(Weather.DrizzleStrength)),
        ["clear"]    = new("clear", 0, a => ClearSky()),
        ["soak"]     = new("soak", 0, a => engine.Weather.Soak()),

        // Life in the garden
        ["birds"]    = new("birds <count>", 1, a => { for (int i = 0; i < Int(a[0]); i++) engine.BirdSpawner.SpawnArrival(); }),
        ["scatter"]  = new("scatter <piles>", 1, a => engine.FoodSpawner.ScatterInView(Int(a[0]), engine.View)),

        // Anything else, by name (see MemberPath.cs)
        ["set"]      = new("set <path> <value>", 2, a => MemberPath.Set(engine, a[0], a[1])),
        ["call"]     = new("call <path> [args...]", 1, a => MemberPath.Call(engine, a[0], a[1..])),
        ["print"]    = new("print <path>", 1, a => Console.WriteLine($"  {a[0]} = {MemberPath.Get(engine, a[0])}")),
        ["log"]      = new("log [lines, default 10]", 0, a => PrintLog(a.Length > 0 ? Int(a[0]) : 10)),
    };

    private record ScriptLine(int Number, string Text, Command Command, string[] Args);

    // Reads every line before the game starts, so a typo fails at once instead of halfway through a run.
    private static List<ScriptLine> ParseScript(string[] lines)
    {
        List<ScriptLine> parsed = new();
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n];
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length == 0) continue;

            string name = words[0].ToLowerInvariant();
            if (!commands.TryGetValue(name, out Command? command))
                throw new ScriptError($"line {n + 1}: unknown command '{words[0]}'. Known: {string.Join(", ", commands.Keys)}");
            string[] args = words[1..];
            if (args.Length < command.MinArgs)
                throw new ScriptError($"line {n + 1}: '{name}' needs: {command.Usage}");
            parsed.Add(new ScriptLine(n + 1, line.Trim(), command, args));
        }
        return parsed;
    }

    // Runs each line in order; any error names the line and stops the run.
    private static void RunScript(List<ScriptLine> script)
    {
        foreach (ScriptLine line in script)
        {
            Console.WriteLine($"> {line.Text}");
            try
            {
                line.Command.Run(line.Args);
            }
            catch (ScriptError e)
            {
                throw new ScriptError($"line {line.Number} ({line.Text}): {e.Message}");
            }
        }
    }

    // ---------------- COMMANDS THAT WAIT FOR THE GAME ----------------

    // Switches the projector and waits until the cube has fully risen (or gone).
    private static void Power(bool on)
    {
        if (on) engine.Projector.TurnOn();
        else if (engine.Projector.IsOn) engine.Projector.Toggle();
        AdvanceUntil(() => on ? engine.Projector.Ready : !engine.Projector.Showing, 15f, $"power {(on ? "on" : "off")}");
    }

    // Fogs over to the scene and waits until the fog has cleared again.
    private static void Scene(BoxScene scene)
    {
        if (!engine.Projector.Ready) throw new ScriptError("the box isn't on (use 'power on' first)");
        engine.Mode.SwitchTo(scene);
        AdvanceUntil(() => engine.Mode.Showing == scene && !engine.Mode.IsSwitching, 10f, $"scene {scene}");
    }

    // Dives into the garden or back out. The dive is timed by the real frame time, so it runs on drawn frames.
    private static void View(string which)
    {
        bool inside = which switch
        {
            "hologram" => true,
            "room" => false,
            _ => throw new ScriptError($"view takes room or hologram, not '{which}'"),
        };
        if (inside && !engine.Projector.Ready) throw new ScriptError("the box isn't on (use 'power on' first)");
        if (inside) engine.Camera.EnterHologramView();
        else engine.Camera.LeaveHologramView();
        FramesUntil(() => engine.Camera.Immersion == (inside ? 1f : 0f) && !engine.Camera.IsChangingView, 600, $"view {which}");
        Advance(HandsSettle);    // the hands that parted or pressed the cube drop away
    }

    // How long the hands take to linger and drop away after the dive (Hands lingers 0.5 s, then fades).
    private const float HandsSettle = 2f;

    // Tilts the camera that's in use: the room camera, or the one standing in the garden.
    private static void Pitch(float radians)
    {
        if (engine.Camera.InHologramView) engine.Camera.HoloPitch = radians;
        else engine.Camera.Pitch = radians;
    }

    // Sets today's hour and holds the clock there, so the light stays put while the script waits.
    private static void Hour(float hour)
    {
        engine.Clock.SetHour(hour);
        engine.Clock.Held = true;
        engine.Clock.Refresh();
    }

    // Skips ahead to the start of the season (same hour), and holds the clock.
    private static void SkipToSeason(Season season)
    {
        for (int i = 0; i < 4 && engine.Clock.Season != season; i++) engine.Clock.NextSeason();
        engine.Clock.Held = true;
        engine.Clock.Refresh();
    }

    // Starts a rain spell and waits for the clouds and the rain to build all the way, then holds the weather.
    private static void Rain(float strength)
    {
        engine.Weather.Held = false;
        engine.Weather.StartRain(strength);
        AdvanceUntil(() => engine.Weather.Cloud >= 0.99f && engine.Weather.Rain >= strength * 0.98f, 180f, "the rain building up");
        engine.Weather.Held = true;
    }

    // Clears up and waits for the sky to be fully clear, then holds the weather.
    private static void ClearSky()
    {
        engine.Weather.Held = false;
        engine.Weather.ClearUp(engine.Clock.Season);
        AdvanceUntil(() => engine.Weather.Cloud <= 0.01f && engine.Weather.Rain <= 0f, 180f, "the sky clearing");
        engine.Weather.Held = true;
    }

    private static void PrintLog(int count)
    {
        foreach (string entry in EventLog.Entries.TakeLast(count)) Console.WriteLine($"  log: {entry}");
    }

    // ---------------- READING ARGUMENTS ----------------

    private static float Float(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : throw new ScriptError($"'{s}' isn't a number");

    private static int Int(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : throw new ScriptError($"'{s}' isn't a whole number");

    private static bool OnOff(string s) => s switch
    {
        "on" => true,
        "off" => false,
        _ => throw new ScriptError($"expected on or off, not '{s}'"),
    };

    private static T Enum<T>(string s) where T : struct, System.Enum =>
        System.Enum.TryParse(s, ignoreCase: true, out T v) ? v : throw new ScriptError($"'{s}' isn't one of: {string.Join(", ", System.Enum.GetNames<T>())}");
}
