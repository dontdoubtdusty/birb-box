using System.Numerics;
using Raylib_cs;

// The glowing edges of the hologram cube, drawn back to front in the hologram pass (Draw):
//  - scanline walls on the four sides (fieldwall.fs): faint lines drifting up, fading toward the top and on the side facing the camera
//  - a curtain of light rising from the floor
//  - the floor and ceiling squares and the four corners, kept soft so the cube reads as projected light, not a frame
// While the projector starts up, the walls and the ceiling square rise with the picture.
//
// And in the room pass (DrawProjection): the light from the lens inside the pedestal up to the cube's floor,
// four beams to its corners and a faint pyramid of light between them. It's drawn with the room so the
// pedestal's front wall hides the part still inside the well.
// The cube turns with its contents, so everything here is turned by the same yaw as the bezel ring.
public class FieldEdges
{
    private Shader wallShader;
    private int locWallViewPos, locWallHeight, locWallTime;

    // BEAMS: how wide each of the three layers is, and how much of the brightness is left at the far end
    private const float BeamCore = 0.035f, BeamMiddle = 0.09f, BeamHalo = 0.3f;
    private const float BeamTopShare = 0.3f;       // the cube's corners, at the top
    private const float BeamFloorShare = 0.55f;    // the projector's beams, where they reach the cube's floor
    private const int PyramidAlpha = 14;           // the faint light between the beams, at the lens

    public void Load()
    {
        wallShader = Raylib.LoadShader(Assets.PathTo("shaders/fieldwall.vs"), Assets.PathTo("shaders/fieldwall.fs"));
        locWallViewPos = Raylib.GetShaderLocation(wallShader, "viewPos");
        locWallHeight = Raylib.GetShaderLocation(wallShader, "wallHeight");
        locWallTime = Raylib.GetShaderLocation(wallShader, "time");
    }

    public void Unload()
    {
        Raylib.UnloadShader(wallShader);
    }

    // Called once a frame before the scene pass.
    public void SetUniforms(Camera3D camera, float wallHeight)
    {
        Raylib.SetShaderValue(wallShader, locWallViewPos, camera.Position, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(wallShader, locWallHeight, wallHeight, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(wallShader, locWallTime, (float)Raylib.GetTime(), ShaderUniformDataType.Float);
    }

    // The cube's floor corner i (0 to 3, going around), turned by yaw, in scene space.
    public static Vector3 Corner(float halfWidth, float yaw, int i)
    {
        Vector3 boxCorner = new Vector3(i == 0 || i == 3 ? halfWidth : -halfWidth, 0f, i < 2 ? halfWidth : -halfWidth);
        return Vector3.Transform(boxCorner, Matrix4x4.CreateRotationY(yaw));
    }

    // Draws the edges of a cube standing on (0, 0, 0), in the hologram pass.
    // level: how far the projector has the cube up (0 to 1); the walls and ceiling rise with it and everything brightens.
    // wallStrength 0 hides the scanline walls. shown: 1 normally, fading to 0 as the camera steps into the garden
    // (the Hologram View), where no edge of the cube should show; nothing is drawn at 0.
    public void Draw(float halfWidth, float height, float yaw, float curtainHeight, float level,
                     Vector3 holoColor, float wallStrength, Vector3 cameraPos, float shown = 1f)
    {
        if (shown <= 0.001f) return;
        float risen = height * level;
        level *= shown;    // from here on only the brightness
        Color middle = Palette.ToColor(holoColor, Palette.GlowAlpha(30, level));
        Color halo = Palette.ToColor(holoColor, Palette.GlowAlpha(16, level));

        Span<Vector3> corners = stackalloc Vector3[4];
        for (int i = 0; i < 4; i++) corners[i] = Corner(halfWidth, yaw, i);

        HologramRenderer.BeginGlowDrawing();
        Rlgl.DisableBackfaceCulling();
        if (wallStrength > 0f)
        {
            DrawWalls(corners, risen, Palette.ToColor(holoColor, Palette.GlowAlpha((int)(110 * wallStrength), level)));
        }
        DrawCurtain(corners, curtainHeight, holoColor, Palette.GlowAlpha(40, level));

        // Floor and ceiling squares: a soft middle and a wide halo, no hard core
        Vector3 up = new Vector3(0f, risen, 0f);
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = corners[i], b = corners[(i + 1) % 4];
            foreach (Vector3 lift in new[] { Vector3.Zero, up })
            {
                Raylib.DrawCylinderEx(a + lift, b + lift, 0.12f, 0.12f, 8, halo);
                Raylib.DrawCylinderEx(a + lift, b + lift, 0.05f, 0.05f, 6, middle);
            }
        }

        // The corners: soft light rising from each floor corner, fading toward the top
        for (int i = 0; i < 4; i++)
        {
            DrawBeam(corners[i], corners[i] + up, 0.5f, BeamTopShare, holoColor, level, cameraPos, withCore: false);
        }

        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableBackfaceCulling();
        HologramRenderer.EndGlowDrawing();
    }

    // The projector's light, in the room pass (inside BeginMode3D with the room camera): from the lens up to the
    // cube's four floor corners, which sit 'lift' above the rim. level: how far the beams are on (0 to 1).
    public static void DrawProjection(Vector3 lens, float halfWidth, float yaw, float lift, Vector3 holoColor,
                                      float level, Vector3 cameraPos)
    {
        Span<Vector3> corners = stackalloc Vector3[4];
        for (int i = 0; i < 4; i++) corners[i] = Corner(halfWidth, yaw, i) + new Vector3(0f, lift, 0f);

        HologramRenderer.BeginGlowDrawing();
        Rlgl.DisableBackfaceCulling();

        // The faint pyramid: one triangle between each pair of neighboring beams, brightest at the lens
        Color atLens = Palette.ToColor(holoColor, Palette.GlowAlpha(PyramidAlpha, level));
        Color atCube = Palette.ToColor(holoColor, Palette.GlowAlpha(PyramidAlpha / 3, level));
        Rlgl.Begin(DrawMode.Triangles);
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = corners[i], b = corners[(i + 1) % 4];
            Rlgl.Color4ub(atLens.R, atLens.G, atLens.B, atLens.A);
            Rlgl.Vertex3f(lens.X, lens.Y, lens.Z);
            Rlgl.Color4ub(atCube.R, atCube.G, atCube.B, atCube.A);
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
        }
        Rlgl.End();

        for (int i = 0; i < 4; i++)
        {
            DrawBeam(lens, corners[i], 1f, BeamFloorShare, holoColor, level, cameraPos, withCore: true);
        }

        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableBackfaceCulling();
        HologramRenderer.EndGlowDrawing();
    }

    // The see-through sides of the cube, drawn with fieldwall.fs, which adds the scanlines and the fading.
    private void DrawWalls(ReadOnlySpan<Vector3> corners, float height, Color color)
    {
        Rlgl.DrawRenderBatchActive();
        Raylib.BeginShaderMode(wallShader);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = corners[i], b = corners[(i + 1) % 4];
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
            Rlgl.Vertex3f(b.X, b.Y + height, b.Z);
            Rlgl.Vertex3f(a.X, a.Y + height, a.Z);
        }
        Rlgl.End();
        Rlgl.DrawRenderBatchActive();
        Raylib.EndShaderMode();
    }

    // A faint sheet of light rising from the floor square, like the edge of a projected field.
    // Bright at the floor, fading to nothing a little way up.
    private static void DrawCurtain(ReadOnlySpan<Vector3> corners, float curtainHeight, Vector3 holoColor, int bottomAlpha)
    {
        Color bottom = Palette.ToColor(holoColor, bottomAlpha);
        Color top = Palette.ToColor(holoColor, 0);

        Rlgl.Begin(DrawMode.Quads);
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = corners[i], b = corners[(i + 1) % 4];
            Rlgl.Color4ub(bottom.R, bottom.G, bottom.B, bottom.A);
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
            Rlgl.Color4ub(top.R, top.G, top.B, top.A);
            Rlgl.Vertex3f(b.X, b.Y + curtainHeight, b.Z);
            Rlgl.Vertex3f(a.X, a.Y + curtainHeight, a.Z);
        }
        Rlgl.End();
    }

    // One beam from 'from' to 'to': flat strips (halo, middle, and a bright core if withCore) that always turn
    // to face the camera, so the beam looks round from every side. startShare and endShare scale its brightness at each end.
    private static void DrawBeam(Vector3 from, Vector3 to, float startShare, float endShare,
                                 Vector3 holoColor, float brightness, Vector3 cameraPos, bool withCore)
    {
        Vector3 along = to - from;
        Vector3 toCamera = cameraPos - (from + to) * 0.5f;
        Vector3 side = Vector3.Cross(along, toCamera);
        if (side.LengthSquared() < 1e-8f) return;
        side = Vector3.Normalize(side);

        DrawStrip(from, to, side * BeamHalo, Palette.GlowAlpha(30, brightness), startShare, endShare, holoColor);
        DrawStrip(from, to, side * BeamMiddle, Palette.GlowAlpha(80, brightness), startShare, endShare, holoColor);
        if (withCore) DrawStrip(from, to, side * BeamCore, Palette.GlowAlpha(230, brightness), startShare, endShare, holoColor);
    }

    // A flat strip 2 * halfWidth wide, its alpha fading from alpha * startShare to alpha * endShare.
    private static void DrawStrip(Vector3 from, Vector3 to, Vector3 halfWidth, int alpha,
                                  float startShare, float endShare, Vector3 holoColor)
    {
        Color a = Palette.ToColor(holoColor, (int)(alpha * startShare));
        Color b = Palette.ToColor(holoColor, (int)(alpha * endShare));

        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(a.R, a.G, a.B, a.A);
            Rlgl.Vertex3f(from.X - halfWidth.X, from.Y - halfWidth.Y, from.Z - halfWidth.Z);
            Rlgl.Vertex3f(from.X + halfWidth.X, from.Y + halfWidth.Y, from.Z + halfWidth.Z);
            Rlgl.Color4ub(b.R, b.G, b.B, b.A);
            Rlgl.Vertex3f(to.X + halfWidth.X, to.Y + halfWidth.Y, to.Z + halfWidth.Z);
            Rlgl.Vertex3f(to.X - halfWidth.X, to.Y - halfWidth.Y, to.Z - halfWidth.Z);
        Rlgl.End();
    }
}
