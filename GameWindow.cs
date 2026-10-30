using System.Numerics;
using Raylib_cs;

// The game's window: opening it, fullscreen (borderless or true) and back, and screenshots (F9).
public class GameWindow
{
    private const int StartWidth = 1280;
    private const int StartHeight = 720;

    public bool IsFullscreen { get; private set; }
    // True fullscreen takes over the monitor, which keeps Windows popups (like the volume bar) off the game.
    // Off means a borderless window the size of the monitor, which Alt-Tabs faster.
    public bool TrueFullscreen { get; private set; } = false;
    private Vector2 windowedPos;     // where the window was before going fullscreen,
    private Vector2 windowedSize;    // so it can go back there

    // Screenshots: Update asks for one, Draw takes it once the frame is finished.
    // Not F12: Raylib has its own built-in F12 screenshot that can't be switched off.
    private bool screenshotRequested = false;

    // Opens the window with its title and icon. Esc is left to the menu instead of quitting.
    public void Open(string title)
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(StartWidth, StartHeight, title);
        Raylib.SetExitKey(KeyboardKey.Null);

        Image icon = Raylib.LoadImage(Assets.PathTo("icon.png"));
        Raylib.SetWindowIcon(icon);
        Raylib.UnloadImage(icon);
    }

    // A plain window with no border, stretched over the monitor. Raylib's own ToggleBorderlessWindowed
    // switches to real monitor fullscreen, which stays on top and blocks alt-tab.
    public void ToggleFullscreen()
    {
        if (!IsFullscreen)
        {
            if (Raylib.IsWindowMaximized()) Raylib.RestoreWindow();
            windowedPos = Raylib.GetWindowPosition();
            windowedSize = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

            int monitor = Raylib.GetCurrentMonitor();
            if (TrueFullscreen)
            {
                // Raylib's fullscreen uses the window's size as the screen resolution, so grow it to the monitor first
                Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
                Raylib.ToggleFullscreen();
            }
            else
            {
                Vector2 monitorPos = Raylib.GetMonitorPosition(monitor);
                Raylib.SetWindowState(ConfigFlags.UndecoratedWindow);
                Raylib.SetWindowPosition((int)monitorPos.X, (int)monitorPos.Y);
                Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
            }
        }
        else
        {
            if (Raylib.IsWindowFullscreen()) Raylib.ToggleFullscreen();
            else Raylib.ClearWindowState(ConfigFlags.UndecoratedWindow);
            Raylib.SetWindowSize((int)windowedSize.X, (int)windowedSize.Y);
            Raylib.SetWindowPosition((int)windowedPos.X, (int)windowedPos.Y);
        }
        IsFullscreen = !IsFullscreen;
    }

    // Switching type while fullscreen steps out to a window and back in the new way.
    public void SetTrueFullscreen(bool on)
    {
        if (on == TrueFullscreen) return;
        bool wasFullscreen = IsFullscreen;
        if (wasFullscreen) ToggleFullscreen();
        TrueFullscreen = on;
        if (wasFullscreen) ToggleFullscreen();
    }

    // Asks for a screenshot of the next finished frame.
    public void RequestScreenshot() => screenshotRequested = true;

    // Called at the end of Draw, before EndDrawing: saves the frame if one was asked for.
    public void TakeRequestedScreenshot()
    {
        if (!screenshotRequested) return;
        SaveScreenshot();
        screenshotRequested = false;
    }

    // Reads the frame that was just drawn, before EndDrawing hands it to Windows,
    // so the picture is exactly this frame and not one still waiting in the display queue.
    private static void SaveScreenshot()
    {
        Rlgl.DrawRenderBatchActive();    // raylib batches draw calls; push any leftovers to the screen first

        // While developing, screenshots go in the project folder; with no project around, next to the .exe.
        string folder = Path.Combine(Assets.ProjectFolder ?? AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        string name = $"birbcube_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";

        Image shot = Raylib.LoadImageFromScreen();
        bool saved = Raylib.ExportImage(shot, Path.Combine(folder, name));
        Raylib.UnloadImage(shot);

        EventLog.Add(saved ? $"Screenshot saved: {name}" : "Screenshot failed to save.");
    }
}
