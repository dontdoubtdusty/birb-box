using System.Numerics;
using Raylib_cs;

// The BIRB / SHOP rocker switch on the pedestal's front, in the plain wood band just under the brass ring,
// straight above the logo. One rocker that tips side to side: the left half is the birds, the right half the shop.
// The half for the scene the box is showing (or heading to) is pushed in and glows amber from inside;
// the other half sticks out, dark. Pressing the raised half switches scenes (the fog swallow in BoxMode).
// It only works while the box is fully on.
//
// It sits in a black housing set into the wood with a brass bezel, like the pedestal's other fittings.
// The words BIRB and SHOP are brass lettering on the band either side of it; the lit side's word glows.
// Rocker space: X along the pedestal to the right, Y up, Z straight out of the wood, (0, 0, 0) on the body's
// surface at the middle of the switch. The pedestal never turns, so neither does the switch.
// State (hover, tilt) changes in Update; Draw only reads it.
public class Rocker
{
    // PLACEMENT around the pedestal (PI/2 is straight out the front)
    public const float Angle = MathF.PI / 2f;
    private const float CenterY = -0.525f;          // the middle of the wood band (see Pedestal's LipBottom and LipSplit)
    private const float SurfaceShare = 1.03f;       // the band's radius, as a share of view.HalfSize

    // SIZES (rocker space units), leaving a margin inside the band's 0.55 height
    private const float BezelWidth = 1.2f, BezelHeight = 0.32f, BezelDepth = 0.06f;
    private const float HousingWidth = 1.06f, HousingHeight = 0.23f, HousingDepth = 0.08f;
    private const float CapWidth = 0.96f, CapHeight = 0.17f, CapDepth = 0.06f;
    private const float CapZ = 0.09f;               // the cap's middle, out from the wood; it tips around this point

    // THE WORDS either side, on the band
    private const float WordGap = 1.1f;             // from the switch's middle to each word's middle, along the band
    private const float WordWidth = 0.7f, WordHeight = 0.24f;
    private static readonly Color WordBrass = new Color(170, 134, 78, 255);

    // FEEL
    private const float TiltDegrees = 9f;           // how far each half sticks out or sinks in
    private const float RockSpeed = 30f;            // how quickly it snaps over (higher is snappier)
    private const float HeldShare = 0.35f;          // holding the raised half down tips it this share of the way over

    // COLORS
    private static readonly Color BezelBrass = new Color(240, 200, 124, 255);    // matches the pedestal's fittings
    private const float BezelShine = 0.6f;
    private static readonly Color Housing = new Color(16, 14, 13, 255);
    private static readonly Color CapDark = new Color(96, 54, 18, 255);          // unlit amber plastic
    private static readonly Color CapLit = new Color(255, 176, 64, 255);         // lit from inside
    private const float CapShine = 0.3f;
    private static readonly Color Glow = new Color(255, 168, 52, 255);

    // LABELS: one texture, BIRB in the left half, SHOP in the right; each half matches a word's shape on the band
    private const int LabelWidth = 512, LabelHeight = 88;
    private RenderTexture2D labels;

    // STATE
    public BoxScene? Hovered { get; private set; }  // the half the mouse is over, if any
    private float tilt;                             // degrees; positive pushes the right (SHOP) half in

    public void Load()
    {
        labels = Raylib.LoadRenderTexture(LabelWidth, LabelHeight);
        Raylib.SetTextureFilter(labels.Texture, TextureFilter.Bilinear);
        Raylib.BeginTextureMode(labels);
            Raylib.ClearBackground(Color.Blank);
            DrawLabelWord("BIRB", 0);
            DrawLabelWord("SHOP", LabelWidth / 2);
        Raylib.EndTextureMode();
        tilt = -TiltDegrees;    // the box starts on the birbs
    }

    public void Unload() => Raylib.UnloadRenderTexture(labels);

    // White text centered in one half of the label texture, shrunk until it fits with a margin.
    private static void DrawLabelWord(string word, int left)
    {
        int half = LabelWidth / 2;
        int size = 60;
        while (size > 20 && (UiText.Measure(word, size) > half * 0.8f || size * 1.1f > LabelHeight * 0.9f)) size -= 2;
        int x = left + (half - UiText.Measure(word, size)) / 2;
        int y = (LabelHeight - (int)(size * 1.1f)) / 2;
        UiText.Draw(word, x, y, size, Color.White);
    }

    // ---------------- WHERE IT IS (shared by Update and Draw) ----------------

    // The middle of the switch on the wood, and its right and outward directions, in room space.
    private static void Frame(ViewWindow view, out Vector3 origin, out Vector3 right, out Vector3 outward)
    {
        outward = new Vector3(MathF.Cos(Angle), 0f, MathF.Sin(Angle));
        right = new Vector3(MathF.Sin(Angle), 0f, -MathF.Cos(Angle));
        origin = outward * (view.HalfSize * SurfaceShare) + new Vector3(0f, CenterY, 0f);
    }

    // How far something at this angle around the pedestal is turned around the up axis to face straight out.
    private static float FaceDegreesAt(float angle) => (MathF.PI / 2f - angle) * Raylib.RAD2DEG;
    private static float FaceDegrees => FaceDegreesAt(Angle);

    // Which half a room-space ray points at, if it hits the switch at all.
    // The ray is moved into rocker space, where the switch is a plain box.
    public static bool RayHits(Ray roomRay, ViewWindow view, out BoxScene half)
    {
        Frame(view, out Vector3 origin, out Vector3 right, out Vector3 outward);
        Vector3 d = roomRay.Position - origin;
        Vector3 dir = roomRay.Direction;
        Ray local = new Ray(new Vector3(Vector3.Dot(d, right), d.Y, Vector3.Dot(d, outward)),
                            new Vector3(Vector3.Dot(dir, right), dir.Y, Vector3.Dot(dir, outward)));
        Vector3 min = new Vector3(-BezelWidth / 2f, -BezelHeight / 2f, -0.05f);
        Vector3 max = new Vector3(BezelWidth / 2f, BezelHeight / 2f, CapZ + CapDepth / 2f + 0.1f);    // reaches past the raised edge
        RayCollision hit = Raylib.GetRayCollisionBox(local, new BoundingBox(min, max));
        half = hit.Point.X < 0f ? BoxScene.Birds : BoxScene.Store;
        return hit.Hit;
    }

    // Where a fingertip goes to press this half: the middle of its face, as the cap is tipped right now.
    public Vector3 FacePoint(ViewWindow view, BoxScene half, out Vector3 facing)
    {
        Frame(view, out Vector3 origin, out Vector3 right, out Vector3 outward);
        TippedAxes(right, outward, tilt, out Vector3 capRight, out Vector3 capOut);
        float side = half == BoxScene.Birds ? -1f : 1f;
        facing = capOut;
        return origin + outward * CapZ + capRight * (side * CapWidth / 4f) + capOut * (CapDepth / 2f);
    }

    // The cap's own right and outward directions, tipped around the up axis by tiltDegrees.
    private static void TippedAxes(Vector3 right, Vector3 outward, float tiltDegrees, out Vector3 capRight, out Vector3 capOut)
    {
        Matrix4x4 tip = Matrix4x4.CreateRotationY(tiltDegrees * Raylib.DEG2RAD);
        capRight = Vector3.TransformNormal(right, tip);
        capOut = Vector3.TransformNormal(outward, tip);
    }

    // ---------------- UPDATE (no drawing) ----------------

    // heading: the scene the box is showing or switching to; its half is the one pushed in.
    // hovered: the half under the mouse. holding: the mouse is held down on it and the switch can move.
    public void Update(float dt, BoxScene heading, BoxScene? hovered, bool holding)
    {
        Hovered = hovered;
        float target = heading == BoxScene.Birds ? -TiltDegrees : TiltDegrees;
        if (holding && hovered != null && hovered != heading) target *= 1f - HeldShare * 2f;    // starts to give
        tilt += (target - tilt) * MathF.Min(1f, RockSpeed * dt);
    }

    // ---------------- DRAW (in the room pass, inside BeginMode3D) ----------------

    // lit: the half for the scene the box is on (or heading to). power: the projector's Lens level; the light follows it.
    public void Draw(Backdrop backdrop, ViewWindow view, BoxScene lit, float power)
    {
        UnitShapes shapes = backdrop.Shapes;
        Frame(view, out Vector3 origin, out Vector3 right, out Vector3 outward);
        TippedAxes(right, outward, tilt, out Vector3 capRight, out _);
        Vector3 capCenter = origin + outward * CapZ;
        float face = FaceDegrees;

        // Brass bezel set into the wood, the black housing in it, and the two halves of the cap
        backdrop.SetShine(BezelShine);
            shapes.Box(origin + outward * (BezelDepth / 2f - 0.02f), new Vector3(BezelWidth, BezelHeight, BezelDepth), BezelBrass, Vector3.UnitY, face);
        backdrop.SetShine(0f);
            shapes.Box(origin + outward * (HousingDepth / 2f), new Vector3(HousingWidth, HousingHeight, HousingDepth), Housing, Vector3.UnitY, face);
        backdrop.SetShine(CapShine);
            for (int i = 0; i < 2; i++)
            {
                BoxScene half = i == 0 ? BoxScene.Birds : BoxScene.Store;
                float side = i == 0 ? -1f : 1f;
                Color color = half == lit ? Palette.Mix(CapDark, CapLit, power) : CapDark;
                shapes.Box(capCenter + capRight * (side * CapWidth / 4f), new Vector3(CapWidth / 2f - 0.01f, CapHeight, CapDepth),
                           color, Vector3.UnitY, face + tilt);
            }
        backdrop.SetShine(0f);

        // The brass words on the band either side
        DrawWord(view, BoxScene.Birds, WordBrass);
        DrawWord(view, BoxScene.Store, WordBrass);

        // The light from inside: the lit half's face, a soft halo, and a hint on the hovered half; the lit side's word glows
        HologramRenderer.BeginGlowDrawing();
            Rlgl.PushMatrix();
            Rlgl.Translatef(capCenter.X, capCenter.Y, capCenter.Z);
            Rlgl.Rotatef(face + tilt, 0, 1, 0);
                for (int i = 0; i < 2; i++)
                {
                    BoxScene half = i == 0 ? BoxScene.Birds : BoxScene.Store;
                    float glow = (half == lit ? power : 0f) + (half == Hovered ? 0.25f : 0f);
                    if (glow <= 0f) continue;
                    glow = MathF.Min(1f, glow);
                    float x = (i == 0 ? -1f : 1f) * CapWidth / 4f;
                    float z = CapDepth / 2f + 0.008f;
                    Raylib.DrawCube(new Vector3(x, 0f, z), CapWidth / 2f - 0.02f, CapHeight - 0.02f, 0.002f, Raylib.Fade(Glow, 0.22f * glow));
                    Raylib.DrawCube(new Vector3(x, 0f, z - 0.004f), CapWidth / 2f + 0.08f, CapHeight + 0.08f, 0.002f, Raylib.Fade(Glow, 0.08f * glow));
                }
            Rlgl.PopMatrix();
            DrawWord(view, lit, Raylib.Fade(Glow, 0.7f * power), lift: 0.004f);
        HologramRenderer.EndGlowDrawing();
    }

    // One word flat on the band, left of the switch for the birds, right for the shop, facing out from the pedestal.
    // The band curves away under it only slightly, so it sits a hair out from the wood. lift: extra distance out.
    private void DrawWord(ViewWindow view, BoxScene half, Color tint, float lift = 0f)
    {
        float radius = view.HalfSize * SurfaceShare;
        float side = half == BoxScene.Birds ? -1f : 1f;
        float angle = Angle - side * WordGap / radius;    // angles grow toward the left as seen from the front
        Vector3 center = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (radius + 0.012f + lift) + new Vector3(0f, CenterY, 0f);

        Rlgl.PushMatrix();
        Rlgl.Translatef(center.X, center.Y, center.Z);
        Rlgl.Rotatef(FaceDegreesAt(angle), 0, 1, 0);
            DrawLabel(half, tint);
        Rlgl.PopMatrix();
    }

    // One half of the label texture as a WordWidth x WordHeight quad facing +Z. Call between Push and Pop.
    // Render textures are stored upside down, so the top of the quad uses v = 1.
    private void DrawLabel(BoxScene half, Color tint)
    {
        float x0 = -WordWidth / 2f;
        float x1 = WordWidth / 2f;
        float u0 = half == BoxScene.Birds ? 0f : 0.5f;
        float u1 = u0 + 0.5f;
        float hh = WordHeight / 2f;
        const float z = 0f;

        Rlgl.SetTexture(labels.Texture.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(tint.R, tint.G, tint.B, tint.A);
            Rlgl.Normal3f(0, 0, 1);
            Rlgl.TexCoord2f(u0, 1); Rlgl.Vertex3f(x0, hh, z);
            Rlgl.TexCoord2f(u0, 0); Rlgl.Vertex3f(x0, -hh, z);
            Rlgl.TexCoord2f(u1, 0); Rlgl.Vertex3f(x1, -hh, z);
            Rlgl.TexCoord2f(u1, 1); Rlgl.Vertex3f(x1, hh, z);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }
}
