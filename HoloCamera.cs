using System.Numerics;
using Raylib_cs;

// The outer camera: it looks at the pedestal from the front and never circles around.
// Left-drag sideways is reported as TurnThisFrame, and GameEngine uses it to turn whatever the box is showing.
// Left-drag up/down tilts the camera a little.
// Each drag locks to whichever direction it started in, so turning never tilts and tilting never turns.
// The mouse wheel zooms this camera unless something else is using it (see GameEngine).
// A left click that doesn't drag is reported as a click (for selecting / picking up coins).
public class HoloCamera
{
    public float Pitch = 0.45f;             // downward tilt, in radians
    public float Distance = 24f;

    public Camera3D Camera;
    public bool ClickedThisFrame { get; private set; }
    public float TurnThisFrame { get; private set; }    // radians of sideways drag this frame

    private const float DragSensitivity = 0.006f;
    private const float TiltSensitivity = 0.003f;
    private const float DragThreshold = 4f;     // pixels moved before a press counts as a drag

    // Tilt and zoom ranges are kept small so the box and the pedestal readouts never leave the screen.
    // Zooming in also lowers the aim, like leaning in toward the toy.
    private const float MinPitch = 0.35f, MaxPitch = 0.6f;
    private const float MinDistance = 15f, MaxDistance = 27f;
    private const float NearAimY = -0.7f, FarAimY = 0.25f;

    private bool mouseHeld = false;
    private bool dragging = false;
    private bool dragTurns = false;     // true: this drag turns the hologram; false: it tilts the camera
    private Vector2 pressPosition;

    public HoloCamera()
    {
        Camera = new Camera3D
        {
            Up = new Vector3(0, 1, 0),
            FovY = 45f,
            Projection = CameraProjection.Perspective
        };
        UpdateCameraPosition();
    }

    // wheelUsedElsewhere: the mouse wheel is zooming the hologram, so leave Distance alone.
    public void Update(bool mouseBlockedByUI, bool wheelUsedElsewhere)
    {
        ClickedThisFrame = false;
        TurnThisFrame = 0f;
        Vector2 mouse = Raylib.GetMousePosition();

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !mouseBlockedByUI)
        {
            mouseHeld = true;
            dragging = false;
            pressPosition = mouse;
        }

        if (mouseHeld && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            if (!dragging && Vector2.Distance(mouse, pressPosition) > DragThreshold)
            {
                dragging = true;
                Vector2 moved = mouse - pressPosition;
                dragTurns = MathF.Abs(moved.X) >= MathF.Abs(moved.Y);
            }
            if (dragging)
            {
                Vector2 delta = Raylib.GetMouseDelta();
                if (dragTurns)
                {
                    TurnThisFrame += delta.X * DragSensitivity;
                }
                else
                {
                    Pitch = Math.Clamp(Pitch + delta.Y * TiltSensitivity, MinPitch, MaxPitch);
                }
            }
        }

        if (mouseHeld && Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            ClickedThisFrame = !dragging;
            mouseHeld = false;
            dragging = false;
        }

        if (!mouseBlockedByUI && !wheelUsedElsewhere)
        {
            float wheel = Raylib.GetMouseWheelMove();
            Distance = Math.Clamp(Distance - wheel * 1.2f, MinDistance, MaxDistance);
        }

        UpdateCameraPosition();
    }

    // The camera sits straight out the front (+Z), tilted down by Pitch.
    private void UpdateCameraPosition()
    {
        float zoom = (Distance - MinDistance) / (MaxDistance - MinDistance);
        Vector3 aim = new Vector3(0, NearAimY + (FarAimY - NearAimY) * zoom, 0);

        Vector3 offset = new Vector3(0, MathF.Sin(Pitch), MathF.Cos(Pitch)) * Distance;
        Camera.Position = aim + offset;
        Camera.Target = aim;
    }
}
