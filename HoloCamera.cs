using System.Numerics;
using Raylib_cs;

// The outer camera: it looks at the pedestal from the front and never circles around.
// Left-drag sideways is reported as TurnThisFrame, and GameEngine uses it to turn whatever the box is showing.
// Left-drag up/down tilts the camera a little.
// Each drag locks to whichever direction it started in, so turning never tilts and tilting never turns.
// The mouse wheel zooms this camera unless something else is using it (see GameEngine).
// A left click that doesn't drag is reported as a click (for selecting / picking up coins), and two quick
// clicks in the same spot as a double-click (GameEngine uses it to enter and leave the Hologram View).
//
// Two views, each with its own zoom and tilt, and an eased move between them:
//  - Room view: the whole pedestal and its readouts in view. Zooming lowers the aim.
//  - Hologram View: the cube grows out of the pedestal (GrowScale, GrowLift) and the camera frames it.
//    The closest the camera gets is where the whole grown cube just fits on screen, between the side columns,
//    however it's turned (FitDistance). The wheel moves between that and HoloMaxFrame times as far;
//    past the closest, GameEngine hands the wheel to the bird world's zoom instead.
// Leaving the Hologram View shrinks the cube back and returns to the room view exactly as it was.
//
// The hologram floats above the pedestal's rim. Rather than moving and scaling everything in it, it is drawn
// and clicked through HologramCamera: the room camera moved down to the cube's floor and shrunk by its size.
// Seeing the same picture from a camera that's closer and lower makes the hologram look bigger and higher,
// while its own floor stays at height 0 and its own size never changes. The room always uses Camera.
public class HoloCamera
{
    public const float HoloLift = 1.0f;     // how far the hologram's floor floats above the rim, on the pedestal

    // HOLOGRAM VIEW GROWTH
    public const float GrowScale = 1.6f;    // how big the cube gets, compared with its size on the pedestal
    public const float GrowLift = 1.5f;     // how much higher its floor rises

    // ROOM VIEW
    public float Pitch = 0.45f;             // downward tilt, in radians
    public float Distance = 29f;

    // HOLOGRAM VIEW
    public bool InHologramView { get; private set; }
    public float HoloPitch = 0.45f;
    public float HoloFrame = 1f;            // camera distance as a share of the closest one where the whole cube fits

    public Camera3D Camera;
    public Camera3D HologramCamera { get; private set; }
    public float HoloScale { get; private set; } = 1f;          // the cube's size right now: 1 on the pedestal, GrowScale grown
    public float HoloFloorY { get; private set; } = HoloLift;   // the cube's floor right now, in room height
    public float Growth { get; private set; }                   // 0 on the pedestal, 1 fully grown, eased
    public bool IsChangingView => viewBlend > 0f && viewBlend < 1f;
    public bool AtClosestFit => HoloFrame <= 1.0001f;           // in the Hologram View the camera can't come any closer
    public bool ClickedThisFrame { get; private set; }
    public bool DoubleClickedThisFrame { get; private set; }
    public float TurnThisFrame { get; private set; }    // radians of sideways drag this frame
    public bool IsHolding => mouseHeld;                 // the left button was pressed on the box and is still down
    public bool IsTurning => mouseHeld && dragging && dragTurns;    // a sideways drag is turning the ring right now
    public bool TurnStartedRight { get; private set; }              // this turning drag began toward the right

    private const float DragSensitivity = 0.006f;
    private const float TiltSensitivity = 0.003f;
    private const float DragThreshold = 4f;     // pixels moved before a press counts as a drag
    private const float DoubleClickTime = 0.35f;    // seconds between the two clicks
    private const float DoubleClickReach = 8f;      // pixels the second click may land from the first

    // Room view: tilt is kept small so the box stays framed, and at every distance the whole box and the
    // pedestal readouts stay on screen.
    private const float MinPitch = 0.35f, MaxPitch = 0.6f;
    private const float MinDistance = 18f, MaxDistance = 32.5f;
    private const float NearAimY = -0.35f, FarAimY = 0.8f;    // raised by half the lift, to keep the floating cube framed

    // Hologram View
    private const float HoloMinPitch = 0.1f, HoloMaxPitch = 1.25f;
    private const float HoloStartPitch = 0.2f;  // tilt on entering: near eye level, so the camera stays about as high as in the room view
    private const float HoloMaxFrame = 1.35f;   // how far back the wheel can pull, as a share of the fitting distance
    private const float HoloZoomStep = 0.93f;   // each wheel notch multiplies the distance by this
    private const float ViewChangeTime = 0.6f;  // seconds to ease between the two views (and to grow the cube)
    private const float FitMargin = 0.92f;      // share of the screen the cube may fill, so its edges keep a little room
    private const float FitTurnAllowance = 1f;  // plan for turns up to this share of 45 degrees: 1 any turn (corner-on), 0 face-on only
    private const int FitTurnSamples = 6;       // turns checked between face-on and the allowance

    private ViewWindow? cube;       // the cube being framed; set on entering, kept for the move back
    private float viewBlend = 0f;   // 0 room view, 1 Hologram View; eases toward whichever is chosen

    private bool mouseHeld = false;
    private bool dragging = false;
    private bool dragTurns = false;     // true: this drag turns the hologram; false: it tilts the camera
    private Vector2 pressPosition;
    private double lastClickTime = -10.0;
    private Vector2 lastClickPosition;

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

    // Switches to the Hologram View: the cube starts growing and the camera moves to just fit it, near eye level.
    public void EnterHologramView(ViewWindow view)
    {
        if (InHologramView) return;
        InHologramView = true;
        cube = view;
        HoloFrame = 1f;
        HoloPitch = HoloStartPitch;
    }

    // Eases back to the room view as it was before, shrinking the cube back onto the pedestal.
    public void LeaveHologramView()
    {
        InHologramView = false;
    }

    // wheelUsedElsewhere: the mouse wheel is zooming the bird world, so leave the distance alone.
    public void Update(bool mouseBlockedByUI, bool wheelUsedElsewhere)
    {
        ClickedThisFrame = false;
        DoubleClickedThisFrame = false;
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
                TurnStartedRight = moved.X > 0f;
            }
            if (dragging)
            {
                Vector2 delta = Raylib.GetMouseDelta();
                if (dragTurns)
                {
                    TurnThisFrame += delta.X * DragSensitivity;
                }
                else if (InHologramView)
                {
                    HoloPitch = Math.Clamp(HoloPitch + delta.Y * TiltSensitivity, HoloMinPitch, HoloMaxPitch);
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
            if (ClickedThisFrame) CheckDoubleClick(mouse);
        }

        if (!mouseBlockedByUI && !wheelUsedElsewhere)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (InHologramView)
                HoloFrame = Math.Clamp(HoloFrame * MathF.Pow(HoloZoomStep, wheel), 1f, HoloMaxFrame);
            else
                Distance = Math.Clamp(Distance - wheel * 1.2f, MinDistance, MaxDistance);
        }

        float step = Raylib.GetFrameTime() / ViewChangeTime;
        viewBlend = Math.Clamp(viewBlend + (InHologramView ? step : -step), 0f, 1f);

        UpdateCameraPosition();
    }

    // A click counts as the second half of a double-click when it comes quickly after the last one, close by.
    // A double-click uses up both clicks, so a third quick click starts over.
    private void CheckDoubleClick(Vector2 mouse)
    {
        double now = Raylib.GetTime();
        bool quick = now - lastClickTime <= DoubleClickTime;
        bool close = Vector2.Distance(mouse, lastClickPosition) <= DoubleClickReach;
        if (quick && close)
        {
            DoubleClickedThisFrame = true;
            lastClickTime = -10.0;
        }
        else
        {
            lastClickTime = now;
            lastClickPosition = mouse;
        }
    }

    // Places the camera for each view and eases between the two, grows the cube along with it,
    // then makes the HologramCamera that sees the grown, lifted cube.
    private void UpdateCameraPosition()
    {
        PoseFor(RoomAim(), Pitch, Distance, out Vector3 roomPosition, out Vector3 roomTarget);
        Vector3 holoPosition = roomPosition, holoTarget = roomTarget;
        if (cube != null)
        {
            Vector3 grownMiddle = new Vector3(0f, HoloLift + GrowLift + cube.Height * GrowScale * 0.5f, 0f);
            PoseFor(grownMiddle, HoloPitch, FitDistance(cube, HoloPitch) * HoloFrame, out holoPosition, out holoTarget);
        }

        Growth = viewBlend * viewBlend * (3f - 2f * viewBlend);    // smoothstep, so it eases in and out
        Camera.Position = Vector3.Lerp(roomPosition, holoPosition, Growth);
        Camera.Target = Vector3.Lerp(roomTarget, holoTarget, Growth);

        HoloScale = 1f + (GrowScale - 1f) * Growth;
        HoloFloorY = HoloLift + GrowLift * Growth;
        Vector3 floor = new Vector3(0f, HoloFloorY, 0f);
        Camera3D hologram = Camera;
        hologram.Position = (Camera.Position - floor) / HoloScale;
        hologram.Target = (Camera.Target - floor) / HoloScale;
        HologramCamera = hologram;
    }

    // The closest the camera can be to the grown cube's middle with all eight corners on screen,
    // checked at several turns of the cube so it still fits while you turn it.
    // For each corner: how far it sits toward the camera, plus the distance needed for its offset
    // up/down and sideways to fit inside the view's half-angles.
    private float FitDistance(ViewWindow view, float pitch)
    {
        float halfWidth = view.HalfWidth * GrowScale;
        float halfHeight = view.Height * GrowScale * 0.5f;
        Vector3 back = new Vector3(0f, MathF.Sin(pitch), MathF.Cos(pitch));    // from the middle toward the camera
        Vector3 up = new Vector3(0f, MathF.Cos(pitch), -MathF.Sin(pitch));     // the screen's up
        float tanUp = MathF.Tan(Camera.FovY * 0.5f * Raylib.DEG2RAD) * FitMargin;
        float tanSide = tanUp * UsableAspect();

        float closest = 0f;
        for (int sample = 0; sample <= FitTurnSamples; sample++)
        {
            float turn = MathF.PI / 4f * FitTurnAllowance * sample / FitTurnSamples;
            float cos = MathF.Cos(turn), sin = MathF.Sin(turn);
            for (int corner = 0; corner < 8; corner++)
            {
                float x = (corner & 1) == 0 ? -halfWidth : halfWidth;
                float z = (corner & 2) == 0 ? -halfWidth : halfWidth;
                float y = (corner & 4) == 0 ? -halfHeight : halfHeight;
                Vector3 offset = new Vector3(x * cos - z * sin, y, x * sin + z * cos);

                float toward = Vector3.Dot(offset, back);
                closest = MathF.Max(closest, toward + MathF.Abs(Vector3.Dot(offset, up)) / tanUp);
                closest = MathF.Max(closest, toward + MathF.Abs(offset.X) / tanSide);
            }
        }
        return closest;
    }

    // Width over height of the screen between the two side columns, where the cube has to fit.
    private static float UsableAspect()
    {
        float height = MathF.Max(1f, Raylib.GetScreenHeight());
        float width = Raylib.GetScreenWidth() - 2f * (SidePanels.Margin + SidePanels.ColumnWidth);
        return MathF.Max(width, height * 0.5f) / height;
    }

    // Room view aim: lower as you zoom in, so the pedestal readouts stay in view.
    private Vector3 RoomAim()
    {
        float zoom = (Distance - MinDistance) / (MaxDistance - MinDistance);
        return new Vector3(0, NearAimY + (FarAimY - NearAimY) * zoom, 0);
    }

    // The camera sits straight out the front (+Z) of the aim point, tilted down by pitch.
    private static void PoseFor(Vector3 aim, float pitch, float distance, out Vector3 position, out Vector3 target)
    {
        Vector3 offset = new Vector3(0, MathF.Sin(pitch), MathF.Cos(pitch)) * distance;
        position = aim + offset;
        target = aim;
    }
}
