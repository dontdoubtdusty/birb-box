using System.Numerics;
using Raylib_cs;

// Orbits around the hologram's center. Left-drag rotates, mouse wheel zooms.
// A left click that doesn't drag is reported as a click (for throwing food / selecting).
public class OrbitCamera
{
    public float Yaw = 0.6f;          // rotation around the vertical axis
    public float Pitch = 0.55f;       // tilt up/down
    public float Distance = 24f;
    public Vector3 Target = new Vector3(0, 2.2f, 0);

    public Camera3D Camera;
    public bool ClickedThisFrame { get; private set; }

    private const float DragSensitivity = 0.006f;
    private const float DragThreshold = 4f;     // pixels moved before a press counts as a drag
    private const float MinPitch = 0.08f, MaxPitch = 1.45f;
    private const float MinDistance = 7f, MaxDistance = 35f;

    private bool mouseHeld = false;
    private bool dragging = false;
    private Vector2 pressPosition;

    public OrbitCamera()
    {
        Camera = new Camera3D
        {
            Up = new Vector3(0, 1, 0),
            FovY = 45f,
            Projection = CameraProjection.Perspective
        };
        UpdateCameraPosition();
    }

    public void Update(bool mouseBlockedByUI)
    {
        ClickedThisFrame = false;
        Vector2 mouse = Raylib.GetMousePosition();

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !mouseBlockedByUI)
        {
            mouseHeld = true;
            dragging = false;
            pressPosition = mouse;
        }

        if (mouseHeld && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            if (Vector2.Distance(mouse, pressPosition) > DragThreshold)
            {
                dragging = true;
            }
            if (dragging)
            {
                Vector2 delta = Raylib.GetMouseDelta();
                Yaw -= delta.X * DragSensitivity;
                Pitch = Math.Clamp(Pitch + delta.Y * DragSensitivity, MinPitch, MaxPitch);
            }
        }

        if (mouseHeld && Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            ClickedThisFrame = !dragging;
            mouseHeld = false;
            dragging = false;
        }

        if (!mouseBlockedByUI)
        {
            float wheel = Raylib.GetMouseWheelMove();
            Distance = Math.Clamp(Distance - wheel * 1.2f, MinDistance, MaxDistance);
        }

        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        // Spherical coordinates -> position on a sphere around the target
        Vector3 offset = new Vector3(
            MathF.Cos(Pitch) * MathF.Sin(Yaw),
            MathF.Sin(Pitch),
            MathF.Cos(Pitch) * MathF.Cos(Yaw)) * Distance;

        Camera.Position = Target + offset;
        Camera.Target = Target;
    }
}
