using System.Numerics;
using Raylib_cs;

// The patch of the world currently shown inside the hologram cube.
// Everything is drawn relative to Center, so the hologram itself always sits at (0, 0, 0).
//
// Three kinds of position:
//  - world: where things really are in the simulation
//  - box:   relative to the cube's center and shrunk by Zoom, before the hologram is turned (ToLocal / ToWorld)
//  - scene: what the camera sees, after the hologram is turned by Yaw (BoxToScene / SceneToBox)
//
// The cube turns with its contents like a turntable, so in box space (and in the world) its sides
// always run along X and Z. The ground reaches its square sides; a thin fog at the walls (FogStart) fades things out.
// HalfWidth and Height are the cube's size in box units (it never changes size on the pedestal).
// WorldHalfWidth and WorldHeight are how much of the world fits inside it at the current zoom.
public class ViewWindow
{
    public Vector2 Center = Vector2.Zero;   // world X, Z
    public float HalfSize = 7.2f;           // sets the pedestal's size; the cube floats above its well
    public float Height = 7.2f;             // hologram spans 0..Height on Y

    // SHAPE (Width and Fog start sliders in the Tab tools)
    public float CubeScale = 0.7f;          // half the cube's width as a share of the pedestal; 0.7 puts its corners over the bezel ring
    public const float MaxCubeScale = 0.74f;
    public float FogStart = 0.97f;          // share of the way to the sides that stays clear; a thin fog fills the rest

    // THE HOLOGRAM VIEW'S FOG (hologram.fs has the same numbers). Standing in the garden, the fog at the sides widens a
    // little, and things also fade with how far they are from the eye along the ground, so the far fog is round like a
    // horizon and no wall of the cube ever shows.
    public const float ObserverFogStart = 0.85f;
    public const float ObserverFogNear = 4.5f, ObserverFogFar = 8f;   // box units from the eye
    public float Immersion;                 // set each frame from HoloCamera: 0 in the room view, 1 standing in the garden
    public float FogStartShown => FogStart + (MathF.Min(FogStart, ObserverFogStart) - FogStart) * Immersion;
    public float HalfWidth => HalfSize * CubeScale;          // the cube spans -HalfWidth..HalfWidth on X and Z
    public float CornerReach => HalfWidth * MathF.Sqrt(2f);  // from the middle out to a corner
    public float PanSpeed = 8f;
    public float Yaw = 0f;                  // how far the hologram is turned around its vertical axis, in radians

    // ZOOM: box units per world unit. 1 = Close, smaller shows more of the world with smaller birds, bigger shows less.
    public static readonly float[] ZoomLevels = { 0.25f, 0.5f, 1f, 3f };    // Wide, Field, Close, Perch
    public float Zoom = 1f;
    public float WorldHalfWidth => HalfWidth / Zoom;
    public float WorldHeight => Height / Zoom;

    private Vector3 Offset => new Vector3(Center.X, 0, Center.Y);

    public Vector3 ToLocal(Vector3 worldPos) => (worldPos - Offset) * Zoom;
    public Vector3 ToWorld(Vector3 localPos) => localPos / Zoom + Offset;

    public Vector3 BoxToScene(Vector3 boxPos) => Vector3.Transform(boxPos, Matrix4x4.CreateRotationY(Yaw));
    public Vector3 SceneToBox(Vector3 scenePos) => Vector3.Transform(scenePos, Matrix4x4.CreateRotationY(-Yaw));
    public float YawDegrees => Yaw * Raylib.RAD2DEG;

    // The world X, Z direction the camera is looking (straight "into" the hologram), length 1.
    public Vector2 LookDirection
    {
        get
        {
            Vector3 into = SceneToBox(new Vector3(0, 0, -1));    // the camera looks toward -Z in the scene
            return new Vector2(into.X, into.Z);
        }
    }

    // How far out toward the cube's sides a point is (x, z measured from the middle): 0 in the middle, 1 at a side.
    // Square, so a corner counts as 1 too. Same math as the fog in hologram.fs, so anything here lines up with what you see.
    public static float EdgeAmount(float x, float z, float halfWidth)
    {
        return MathF.Max(MathF.Abs(x), MathF.Abs(z)) / halfWidth;
    }

    // How much of something at this box position shows through the fog (1 clear, 0 gone), seen from an eye at eyeBox
    // (also in box units). The same math as the fog in hologram.fs, so labels fade with what they float over.
    public float FogFade(Vector3 boxPos, Vector3 eyeBox)
    {
        float start = FogStartShown;
        float side = Math.Clamp((EdgeAmount(boxPos.X, boxPos.Z, HalfWidth) - start) / (1f - start), 0f, 1f);
        float fade = 1f - side * side * (3f - 2f * side);

        float fromEye = Vector2.Distance(new Vector2(boxPos.X, boxPos.Z), new Vector2(eyeBox.X, eyeBox.Z));
        float far = Math.Clamp((fromEye - ObserverFogNear) / (ObserverFogFar - ObserverFogNear), 0f, 1f);
        return fade * (1f - Immersion * far * far * (3f - 2f * far));
    }

    // Is this world position inside the cube (plus an optional margin, in world units)?
    public bool Contains(Vector3 worldPos, float margin = 0f)
    {
        float reach = WorldHalfWidth + margin;
        float dx = worldPos.X - Center.X, dz = worldPos.Z - Center.Y;
        if (reach <= 0f) return false;
        return EdgeAmount(dx, dz, reach) <= 1f && worldPos.Y <= WorldHeight + margin;
    }

    // Does this box-space ray pass through the cube somewhere between floor and ceiling?
    public bool RayHitsHologram(Ray boxRay)
    {
        BoundingBox cube = new BoundingBox(new Vector3(-HalfWidth, 0f, -HalfWidth), new Vector3(HalfWidth, Height, HalfWidth));
        return Raylib.GetRayCollisionBox(boxRay, cube).Hit;
    }

    // Smooth zoom from the scroll wheel, kept between the widest and closest fixed levels.
    public void ZoomBy(float wheel)
    {
        Zoom = Math.Clamp(Zoom * MathF.Pow(1.1f, wheel), ZoomLevels[0], ZoomLevels[^1]);
        JumpTo(Center);    // zooming out near an edge pushes the window back inside
    }

    // The +/- buttons: jump to the next fixed level in (direction 1) or out (direction -1).
    public void StepZoom(int direction)
    {
        const float Tolerance = 0.001f;
        if (direction > 0)
        {
            foreach (float level in ZoomLevels)
            {
                if (level > Zoom + Tolerance) { Zoom = level; break; }
            }
        }
        else
        {
            for (int i = ZoomLevels.Length - 1; i >= 0; i--)
            {
                if (ZoomLevels[i] < Zoom - Tolerance) { Zoom = ZoomLevels[i]; break; }
            }
        }
        JumpTo(Center);
    }

    // WASD pans relative to the screen: W always moves "into" the hologram, however it's turned.
    // Returns true if the player panned.
    public bool HandlePanInput(float dt)
    {
        Vector2 forward = LookDirection;
        Vector2 right = new Vector2(-forward.Y, forward.X);
        Vector2 move = Vector2.Zero;

        if (Raylib.IsKeyDown(KeyboardKey.W)) move += forward;
        if (Raylib.IsKeyDown(KeyboardKey.S)) move -= forward;
        if (Raylib.IsKeyDown(KeyboardKey.D)) move += right;
        if (Raylib.IsKeyDown(KeyboardKey.A)) move -= right;

        if (move == Vector2.Zero) return false;

        // Divided by Zoom so panning feels the same speed on screen at any zoom.
        float speed = PanSpeed / Zoom * (Raylib.IsKeyDown(KeyboardKey.LeftShift) ? 2.5f : 1f);
        JumpTo(Center + Vector2.Normalize(move) * speed * dt);
        return true;
    }

    // Moves the window, stopping where its sides reach the world's edge, so the view never shows past the world.
    // If the window is wider than the world it stays in the middle.
    public void JumpTo(Vector2 newCenter)
    {
        float limit = MathF.Max(0f, World.Instance.HalfSize - WorldHalfWidth);
        Center = new Vector2(
            Math.Clamp(newCenter.X, -limit, limit),
            Math.Clamp(newCenter.Y, -limit, limit));
    }

    // Smoothly slide the window toward a point (used when following a bird).
    public void Follow(Vector3 worldPos, float dt)
    {
        Vector2 target = new Vector2(worldPos.X, worldPos.Z);
        JumpTo(Vector2.Lerp(Center, target, MathF.Min(1f, dt * 3f)));
    }
}
