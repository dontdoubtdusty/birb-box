using System.Numerics;
using Raylib_cs;

// The outer camera: it looks at the pedestal from the front and never circles around.
// Left-drag sideways is reported as TurnThisFrame, and GameEngine uses it to turn whatever the box is showing.
// Left-drag up/down tilts the camera a little.
// Each drag locks to whichever direction it started in, so turning never tilts and tilting never turns.
// The mouse wheel zooms this camera in the room view unless something else is using it (see GameEngine).
// A left click that doesn't drag is reported as a click (for selecting / picking up coins), and two quick
// clicks in the same spot as a double-click (GameEngine uses it to enter and leave the Hologram View).
//
// Two views, each with its own tilt, and an eased move between them:
//  - Room view: the whole pedestal and its readouts in view. Zooming lowers the aim.
//  - Hologram View (the observer mode): the hands push the cube apart, it grows out of the pedestal
//    (GrowScale, GrowLift), and the camera dives through its front wall to stand inside the garden, low,
//    looking across it. The walls slide past the screen's edges, the room fades away (Immersion) and the fog
//    widens, so nothing but the garden and its haze is left. Up/down drag tilts from near the ground to
//    looking down on it; the wheel zooms the bird world. The dive eases in slowly and rushes at the end,
//    with the field of view widening on the way (FovPush).
// Leaving the Hologram View pulls back out and returns to the room view exactly as it was.
//
// The hologram floats above the pedestal's rim. Rather than moving and scaling everything in it, it is drawn
// and clicked through HologramCamera: the room camera moved down to the cube's floor and shrunk by its size.
// Seeing the same picture from a camera that's closer and lower makes the hologram look bigger and higher,
// while its own floor stays at height 0 and its own size never changes. The room always uses Camera.
public class HoloCamera
{
    public const float HoloLift = 1.0f;     // how far the hologram's floor floats above the rim, on the pedestal

    // HOLOGRAM VIEW GROWTH
    public const float GrowScale = 3.0f;    // how big the cube gets, compared with its size on the pedestal
    public const float GrowLift = 1.5f;     // how much higher its floor rises

    // ROOM VIEW
    public float Pitch = 0.45f;             // downward tilt, in radians
    public float Distance = 29f;

    // HOLOGRAM VIEW
    public bool InHologramView { get; private set; }
    public float HoloPitch = 0.22f;

    public Camera3D Camera;
    public Camera3D HologramCamera { get; private set; }
    public float HoloScale { get; private set; } = 1f;          // the cube's size right now: 1 on the pedestal, GrowScale grown
    public float HoloFloorY { get; private set; } = HoloLift;   // the cube's floor right now, in room height
    public float Growth { get; private set; }                   // 0 on the pedestal, 1 fully grown, eased
    public float Immersion { get; private set; }                // 0 in the room view, 1 standing in the garden (the room gone)
    public bool RoomHidden => Immersion > 0.5f;                 // the room is too faded to see, so its controls don't take the mouse
    public bool IsChangingView => viewBlend > 0f && viewBlend < 1f;
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

    // Hologram View, standing in the garden. These are in box units (the cube is 10 wide and 7.2 tall),
    // so the camera stays inside the cube at every tilt.
    private const float InsideAimY = 0.4f;          // the camera looks at the middle of the garden, just above the ground
    private const float InsideDistance = 4.4f;      // how far from that spot it stands
    private const float HoloMinPitch = 0.08f, HoloMaxPitch = 1.2f;  // from nearly at the birds' eye level to looking down on them
    private const float HoloStartPitch = 0.22f;     // tilt on arriving: low, looking across the garden

    // THE DIVE
    private const float EnterTime = 1.1f;       // seconds to dive in
    private const float LeaveTime = 0.8f;       // seconds to pull back out
    private const float DiveCurve = 1.4f;       // above 1: the dive starts slow and rushes toward the end
    private const float BaseFov = 45f;
    private const float FovPush = 12f;          // degrees the view widens halfway through the dive, for a sense of speed
    private const float ImmerseFrom = 0.55f;    // share of the dive after which the room starts fading away

    private float viewBlend = 0f;   // 0 room view, 1 Hologram View; moves toward whichever is chosen at a steady pace

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
            FovY = BaseFov,
            Projection = CameraProjection.Perspective
        };
        UpdateCameraPosition();
    }

    // Switches to the Hologram View: the cube starts growing and the camera dives in, arriving low in the garden.
    public void EnterHologramView()
    {
        if (InHologramView) return;
        InHologramView = true;
        HoloPitch = HoloStartPitch;
    }

    // Pulls back out to the room view as it was before, shrinking the cube back onto the pedestal.
    public void LeaveHologramView()
    {
        InHologramView = false;
    }

    // wheelUsedElsewhere: the mouse wheel is zooming the bird world, so leave the distance alone.
    // In the Hologram View the wheel never moves this camera (GameEngine gives it to the bird world).
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

        if (!InHologramView && !mouseBlockedByUI && !wheelUsedElsewhere)
        {
            float wheel = Raylib.GetMouseWheelMove();
            Distance = Math.Clamp(Distance - wheel * 1.2f, MinDistance, MaxDistance);
        }

        float step = Raylib.GetFrameTime() / (InHologramView ? EnterTime : LeaveTime);
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
    // widens the view mid-dive, then makes the HologramCamera that sees the grown, lifted cube.
    private void UpdateCameraPosition()
    {
        PoseFor(RoomAim(), Pitch, Distance, out Vector3 roomPosition, out Vector3 roomTarget);
        InsidePose(out Vector3 holoPosition, out Vector3 holoTarget);

        float dive = MathF.Pow(viewBlend, DiveCurve);
        Growth = Smooth(dive);
        Immersion = Smooth(Math.Clamp((Growth - ImmerseFrom) / (1f - ImmerseFrom), 0f, 1f));
        Camera.Position = Vector3.Lerp(roomPosition, holoPosition, Growth);
        Camera.Target = Vector3.Lerp(roomTarget, holoTarget, Growth);
        Camera.FovY = BaseFov + FovPush * MathF.Sin(MathF.PI * Growth);

        HoloScale = 1f + (GrowScale - 1f) * Growth;
        HoloFloorY = HoloLift + GrowLift * Growth;
        Vector3 floor = new Vector3(0f, HoloFloorY, 0f);
        Camera3D hologram = Camera;
        hologram.Position = (Camera.Position - floor) / HoloScale;
        hologram.Target = (Camera.Target - floor) / HoloScale;
        HologramCamera = hologram;
    }

    // Standing in the fully grown cube: InsideDistance out the front of the garden's middle, tilted down by HoloPitch.
    // Worked out in box units, then grown and lifted into the room, so the hologram camera lands exactly there.
    private void InsidePose(out Vector3 position, out Vector3 target)
    {
        PoseFor(new Vector3(0f, InsideAimY, 0f), HoloPitch, InsideDistance, out Vector3 boxPosition, out Vector3 boxTarget);
        Vector3 floor = new Vector3(0f, HoloLift + GrowLift, 0f);
        position = boxPosition * GrowScale + floor;
        target = boxTarget * GrowScale + floor;
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

    // Eases 0..1 in and out (smoothstep).
    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
