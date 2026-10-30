using Raylib_cs;

// Keeps frames evenly paced, and counts the ones that came late.
//
// The screen's vsync sets the pace on its own. A second 60 FPS timer on top of it (as before 4.84) runs on a
// different clock, and every so often the two disagree and a frame is shown twice, in short bursts.
// If vsync turns out to be off (some drivers ignore the request), frames come much faster than the screen
// can show them; then a timer cap at the screen's rate is switched on instead.
//
// A slow frame is one that took clearly longer than one screen refresh. They're counted for the tools panel,
// so a stutter can be told apart from a smooth run.
public class FramePacing
{
    private const float SkipSeconds = 1f;           // the first moments after loading are always uneven, so they're skipped
    private const float MeasureSeconds = 2f;        // then frames are counted this long to see if vsync is working
    private const float TooFastShare = 1.25f;       // more frames than the screen's rate times this means vsync is off
    private const float SlowShare = 1.5f;           // a frame longer than this many refreshes counts as slow
    private const double RecentWindow = 60.0;       // seconds the "recent" slow-frame count looks back

    public int RefreshRate { get; private set; } = 60;
    public bool TimerCap { get; private set; }      // true when vsync was off and the timer cap took over
    public int SlowFrames { get; private set; }     // since the game started
    public int RecentSlowFrames => recentSlow.Count;

    private float measured;
    private int measuredFrames;
    private bool measuring = true;
    private readonly Queue<double> recentSlow = new();

    // Call once, after the window is open.
    public void Start()
    {
        int rate = Raylib.GetMonitorRefreshRate(Raylib.GetCurrentMonitor());
        RefreshRate = rate > 0 ? rate : 60;
    }

    // Call once a frame with the frame's real length.
    public void Update(float dt)
    {
        CheckVsync(dt);
        CountSlowFrame(dt);
    }

    // During the first few seconds: if frames come much faster than the screen refreshes, vsync is off, so cap them.
    private void CheckVsync(float dt)
    {
        if (!measuring) return;

        measured += dt;
        if (measured < SkipSeconds) return;
        measuredFrames++;
        if (measured < SkipSeconds + MeasureSeconds) return;

        measuring = false;
        float framesPerSecond = measuredFrames / MeasureSeconds;
        if (framesPerSecond > RefreshRate * TooFastShare)
        {
            Raylib.SetTargetFPS(RefreshRate);
            TimerCap = true;
        }
    }

    // Remembers when each slow frame happened, forgetting the ones older than RecentWindow.
    private void CountSlowFrame(float dt)
    {
        double now = Raylib.GetTime();
        if (dt > SlowShare / RefreshRate && measured >= SkipSeconds)
        {
            SlowFrames++;
            recentSlow.Enqueue(now);
        }
        while (recentSlow.Count > 0 && now - recentSlow.Peek() > RecentWindow) recentSlow.Dequeue();
    }
}
