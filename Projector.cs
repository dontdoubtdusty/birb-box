// The Birb Box's power: off, starting up, on, or shutting down. Clicking the logo flips it (see GameEngine).
// Like a real projector it takes a moment to start: the lens behind the logo lights first, then the beams
// sputter on, then the cube rises up out of its floor. Shutting down runs the same steps backward, faster.
// The world keeps simulating either way; only what's shown and clickable changes.
// This holds state only. Pedestal, FieldEdges, HologramRenderer and Sounds read the levels.
public class Projector
{
    public bool IsOn { get; private set; }    // starts off; the player switches it on
    public bool LogoHovered;                  // set by GameEngine, so the logo can hint that it's a button

    // START-UP TIMING (seconds into the start-up): each level eases from 0 to 1 between its two times
    private const float LensStart = 0f, LensEnd = 0.6f;
    private const float BeamStart = 0.5f, BeamEnd = 1.5f;
    private const float CubeStart = 1.3f, CubeEnd = 3.2f;
    private const float ShutDownSpeed = 3.5f;    // shutting down runs this many times faster than starting
    private const float FlickerStep = 0.05f;     // how often the sputtering beams pick a new brightness

    private float progress;    // seconds into the start-up, 0 to CubeEnd
    private float flicker = 1f, flickerTimer;

    public float Lens { get; private set; }     // the lens, the logo, the readouts and LEDs, and the hum
    public float Beams { get; private set; }    // the beams and the pyramid of light
    public float Cube { get; private set; }     // how much of the cube has risen: 0 none, 1 all of it

    public bool Showing => Cube > 0f;            // anything of the hologram to draw
    public bool Ready => IsOn && Cube >= 1f;     // fully up, so the box takes clicks

    public void Toggle()
    {
        IsOn = !IsOn;
        Sounds.PlayProjector(IsOn);
    }

    public void TurnOn()
    {
        if (!IsOn) Toggle();
    }

    // Moves along the start-up (or back down it) and works out each level.
    public void Update(float dt)
    {
        progress = IsOn ? MathF.Min(CubeEnd, progress + dt) : MathF.Max(0f, progress - dt * ShutDownSpeed);

        flickerTimer -= dt;
        if (flickerTimer <= 0f)
        {
            flicker = Rng.Range(0.25f, 1f);
            flickerTimer = FlickerStep;
        }

        Lens = Stage(LensStart, LensEnd);
        float beams = Stage(BeamStart, BeamEnd);
        Beams = IsOn && beams < 1f ? beams * flicker : beams;    // they only sputter while catching
        Cube = Stage(CubeStart, CubeEnd);
    }

    // 0 before start, 1 after end, easing in and out between.
    private float Stage(float start, float end)
    {
        float t = Math.Clamp((progress - start) / (end - start), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
