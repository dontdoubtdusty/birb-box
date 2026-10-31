using System.Reflection;
using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;

// Runs the real game on a virtual screen and follows a plain-text script of commands
// (see README.md and ShotCommands.cs), saving named screenshots. Used by Claude, never shipped.
public static partial class ShotDriver
{
    // The step the simulation takes per update: the same every run, whatever the virtual screen's frame rate.
    public const float StepSeconds = 1f / 30f;

    private static GameEngine engine = null!;
    private static Action<float> update = null!;    // GameEngine's private Update(dt)
    private static Action draw = null!;             // GameEngine's private Draw()
    private static string outFolder = "";
    private static int shotsTaken = 0;

    // Reads the arguments, starts the game, runs the script; exits 1 with a clear message on any error.
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: ShotDriver <script.txt> [--out <folder>]");
            return 2;
        }
        string scriptPath = args[0];
        outFolder = Path.GetFullPath(ArgAfter(args, "--out") ?? "shots");
        Directory.CreateDirectory(outFolder);

        try
        {
            var script = ParseScript(File.ReadAllLines(scriptPath));
            StartGame();
            RunScript(script);
            Console.WriteLine($"Done: {shotsTaken} shot(s) in {outFolder}");
            return 0;
        }
        catch (ScriptError e)
        {
            Console.Error.WriteLine($"SCRIPT ERROR: {e.Message}");
            return 1;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"DRIVER ERROR: {(e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e)}");
            return 1;
        }
        finally
        {
            if (Raylib.IsWindowReady()) Raylib.CloseWindow();
        }
    }

    // The same start-up as GameEngine.Run, minus its loop and the save on quit.
    // KEEP IN STEP with GameEngine.Run: if Run gains a load step, add it here too.
    private static void StartGame()
    {
        engine = new GameEngine();
        update = BindPrivate<Action<float>>("Update");
        draw = BindPrivate<Action>("Draw");

        engine.Window.Open(GameEngine.Version);
        engine.Pacing.Start();

        Sounds.Load();
        engine.Radio.Load();
        engine.Renderer.Load(engine.View);
        engine.Rocker.Load();
        BirbBuilder.Load(engine.Renderer.HoloShader);
        Birds.LoadAll();

        rlImGui.BeginInitImGui();
        ImGui.StyleColorsDark();
        UIHandler.LoadFont();
        rlImGui.EndInitImGui();
        engine.UI.ApplyStyle();

        engine.Saves.LoadOrStartNew(engine);
        Settings.Load(engine);

        Console.WriteLine($"{GameEngine.Version} started, window {Raylib.GetScreenWidth()}x{Raylib.GetScreenHeight()}, project folder {Assets.ProjectFolder}");
        Frames(2);    // one real frame so timers and the frame time have a value
    }

    // Finds one of GameEngine's private methods by name, failing loudly if it was renamed.
    private static T BindPrivate<T>(string name) where T : Delegate
    {
        MethodInfo? method = typeof(GameEngine).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new ScriptError($"GameEngine.{name} was not found (renamed?). Update ShotDriver.BindPrivate's callers.");
        return (T)Delegate.CreateDelegate(typeof(T), engine, method);
    }

    // ---------------- STEPPING THE GAME ----------------

    // Runs the simulation forward by sim seconds without drawing (fast).
    private static void Advance(float seconds)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(seconds / StepSeconds));
        for (int i = 0; i < steps; i++) update(StepSeconds);
    }

    // Steps without drawing until the condition holds, or fails after the given sim seconds.
    private static void AdvanceUntil(Func<bool> done, float maxSeconds, string what)
    {
        for (float t = 0f; !done(); t += StepSeconds)
        {
            if (t > maxSeconds) throw new ScriptError($"{what} didn't happen within {maxSeconds} s of sim time");
            update(StepSeconds);
        }
    }

    // Updates and draws whole frames (needed for anything timed by the real frame time, like the camera's dive).
    private static void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            update(StepSeconds);
            draw();
        }
    }

    // Draws frames until the condition holds, or fails after maxFrames.
    private static void FramesUntil(Func<bool> done, int maxFrames, string what)
    {
        for (int i = 0; !done(); i++)
        {
            if (i >= maxFrames) throw new ScriptError($"{what} didn't happen within {maxFrames} frames");
            Frames(1);
        }
    }

    // Draws two settling frames, then one through the game's own screenshot path (GameWindow), and moves the file to <out>/<name>.png.
    private static void Shot(string name)
    {
        Frames(2);
        string folder = Path.Combine(Assets.ProjectFolder ?? AppContext.BaseDirectory, "screenshots");
        DateTime before = DateTime.Now.AddSeconds(-1);

        engine.Window.RequestScreenshot();
        Frames(1);

        FileInfo? newest = Directory.Exists(folder)
            ? new DirectoryInfo(folder).GetFiles("*.png").Where(f => f.LastWriteTime >= before).OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
            : null;
        if (newest == null) throw new ScriptError($"shot {name}: the game didn't write a screenshot to {folder}");

        string target = Path.Combine(outFolder, name + ".png");
        File.Move(newest.FullName, target, overwrite: true);
        shotsTaken++;
        Console.WriteLine($"  shot {target}");
    }

    private static string? ArgAfter(string[] args, string flag)
    {
        int i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}

// A problem in the script, or a game member the driver can't find. Ends the run with exit code 1.
public class ScriptError(string message) : Exception(message);
