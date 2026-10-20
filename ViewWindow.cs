using System.Numerics;
using Raylib_cs;

// The patch of the world currently shown inside the round hologram.
// Everything is drawn relative to Center, so the hologram itself always sits at (0, 0, 0).
//
// Three kinds of position:
//  - world: where things really are in the simulation
//  - box:   relative to the cube's center and shrunk by Zoom, before the hologram is turned (ToLocal / ToWorld)
//  - scene: what the camera sees, after the hologram is turned by Yaw (BoxToScene / SceneToBox)
//
// Radius and Height are the hologram's size in box units (it never changes size on the pedestal).
// WorldRadius and WorldHeight are how much of the world fits inside it at the current zoom.
public class ViewWindow
{
    public Vector2 Center = Vector2.Zero;   // world X, Z
    public float HalfSize = 6f;             // sets the pedestal's size; the hologram sits on its emitter well
    public float Height = 6f;               // hologram spans 0..Height on Y

    // SHAPE (Width and Fog start sliders in the Tab tools)
    public float RadiusScale = 0.95f;       // hologram radius as a share of the pedestal; 0.95 = inner edge of the bezel ring, 1.05 = outer rim
    public const float MaxRadiusScale = 1.05f;
    public float FogStart = 0.9f;         // share of the radius that stays clear; fog fills the rest
    public float Radius => HalfSize * RadiusScale;    // hologram is a cylinder this wide; the emitter well matches it
    public float PanSpeed = 8f;
    public float Yaw = 0f;                  // how far the hologram is turned around its vertical axis, in radians

    // ZOOM: box units per world unit. 1 = Close, smaller shows more of the world with smaller birds, bigger shows less.
    public static readonly float[] ZoomLevels = { 0.25f, 0.5f, 1f, 3f };    // Wide, Field, Close, Perch
    public float Zoom = 1f;
    public float WorldRadius => Radius / Zoom;
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

    // Is this world position inside the hologram's circle (plus an optional margin, in world units)?
    public bool Contains(Vector3 worldPos, float margin = 0f)
    {
        float distance = new Vector2(worldPos.X - Center.X, worldPos.Z - Center.Y).Length();
        return distance <= WorldRadius + margin && worldPos.Y <= WorldHeight + margin;
    }

    // Does this box-space ray pass through the hologram's cylinder somewhere between floor and ceiling?
    public bool RayHitsHologram(Ray boxRay)
    {
        // Where the ray enters and leaves the circle when seen from above (a quadratic in t)
        Vector2 o = new Vector2(boxRay.Position.X, boxRay.Position.Z);
        Vector2 d = new Vector2(boxRay.Direction.X, boxRay.Direction.Z);
        float a = Vector2.Dot(d, d);
        float b = 2f * Vector2.Dot(o, d);
        float c = Vector2.Dot(o, o) - Radius * Radius;
        float disc = b * b - 4f * a * c;
        if (a < 0.000001f || disc < 0f) return false;

        float root = MathF.Sqrt(disc);
        float tIn = (-b - root) / (2f * a);
        float tOut = (-b + root) / (2f * a);
        if (tOut < 0f) return false;

        // Inside the circle, does the ray's height overlap 0..Height?
        float yIn = boxRay.Position.Y + boxRay.Direction.Y * tIn;
        float yOut = boxRay.Position.Y + boxRay.Direction.Y * tOut;
        return MathF.Max(yIn, yOut) >= 0f && MathF.Min(yIn, yOut) <= Height;
    }

    // Smooth zoom from the scroll wheel, kept between the widest and closest fixed levels.
    public void ZoomBy(float wheel)
    {
        Zoom = Math.Clamp(Zoom * MathF.Pow(1.1f, wheel), ZoomLevels[0], ZoomLevels[^1]);
    }

    // The +/- buttons: jump to the next fixed level in (direction 1) or out (direction -1).
    public void StepZoom(int direction)
    {
        const float Tolerance = 0.001f;
        if (direction > 0)
        {
            foreach (float level in ZoomLevels)
            {
                if (level > Zoom + Tolerance) { Zoom = level; return; }
            }
        }
        else
        {
            for (int i = ZoomLevels.Length - 1; i >= 0; i--)
            {
                if (ZoomLevels[i] < Zoom - Tolerance) { Zoom = ZoomLevels[i]; return; }
            }
        }
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

    public void JumpTo(Vector2 newCenter)
    {
        float limit = World.Instance.HalfSize;
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
