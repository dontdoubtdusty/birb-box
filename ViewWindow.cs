using System.Numerics;
using Raylib_cs;

// The patch of the world currently shown inside the hologram cube.
// Everything is drawn relative to Center, so the cube itself always sits at (0, 0, 0).
public class ViewWindow
{
    public Vector2 Center = Vector2.Zero;   // world X, Z
    public float HalfSize = 6f;             // cube spans -HalfSize..HalfSize on X and Z
    public float Height = 6f;               // cube spans 0..Height on Y
    public float PanSpeed = 8f;

    private Vector3 Offset => new Vector3(Center.X, 0, Center.Y);

    public Vector3 ToLocal(Vector3 worldPos) => worldPos - Offset;
    public Vector3 ToWorld(Vector3 localPos) => localPos + Offset;

    // Is this world position inside the cube (plus an optional margin)?
    public bool Contains(Vector3 worldPos, float margin = 0f)
    {
        Vector3 local = ToLocal(worldPos);
        return MathF.Abs(local.X) <= HalfSize + margin
            && MathF.Abs(local.Z) <= HalfSize + margin
            && local.Y <= Height + margin;
    }

    // WASD pans relative to the way the camera is facing. Returns true if the player panned.
    public bool HandlePanInput(float dt, float cameraYaw)
    {
        Vector2 forward = new Vector2(-MathF.Sin(cameraYaw), -MathF.Cos(cameraYaw));
        Vector2 right = new Vector2(-forward.Y, forward.X);
        Vector2 move = Vector2.Zero;

        if (Raylib.IsKeyDown(KeyboardKey.W)) move += forward;
        if (Raylib.IsKeyDown(KeyboardKey.S)) move -= forward;
        if (Raylib.IsKeyDown(KeyboardKey.D)) move += right;
        if (Raylib.IsKeyDown(KeyboardKey.A)) move -= right;

        if (move == Vector2.Zero) return false;

        float speed = PanSpeed * (Raylib.IsKeyDown(KeyboardKey.LeftShift) ? 2.5f : 1f);
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
