using System.Numerics;
using Raylib_cs;

// Draws the sprouts (Sprout.cs) inside the hologram, as part of WorldDrawer: each one is a tuft of grass blades
// plus a few stalks that bend under seedheads in their seed's color. Every blade, stalk and head grain goes into
// the same two grain batches as the seed (see SeedDrawing.cs), so sprouts add no draw calls.
// What it looks like at any moment comes from its stages: Growth (it rises from a nub), Ripeness (the heads swell
// and take on the seed's color, bending the stalks), HeadFill (eaten heads lose grains) and Withering (it browns,
// flops over and sinks). Its blades, stalks and lean come from its Look, so a sprout always looks the same.
public partial class WorldDrawer
{
    // SPROUT LOOKS
    private static readonly Vector3 BladeGreen = new(0.36f, 0.66f, 0.28f);
    private static readonly Vector3 StalkGreen = new(0.55f, 0.68f, 0.34f);
    private static readonly Vector3 Straw = new(0.52f, 0.44f, 0.28f);    // what it browns to as it dies back
    private const int BladesMin = 5, BladesMax = 8;
    private const int StalksMin = 2, StalksMax = 4;
    private const float BladeLength = 0.3f, BladeWidth = 0.028f, BladeThickness = 0.008f;
    private const float StalkLength = 0.42f, StalkWidth = 0.014f;
    private const int BladeSegments = 3, StalkSegments = 4;
    private const float MaxBend = 1.75f;          // radians from upright; a blade or stalk never droops further than this
    private const int HeadGrains = 8;             // grains on a full head
    private const float HeadLength = 0.12f;       // how far down the stalk's end a head reaches
    private const float HeadGrainScale = 1.3f;    // head grains are a bit bigger than the seed's own grains
    private const float BudShare = 0.3f;          // before it ripens, each stalk carries a small green bud this share of a head
    private const float SwayAngle = 0.05f, SwaySpeed = 1.3f;    // a gentle breeze along world X

    // Adds every sprout in view to the grain batches. Called by DrawFood before the batches are drawn.
    private void AddSprouts(ViewWindow view)
    {
        float time = (float)Raylib.GetTime();
        foreach (Sprout sprout in World.Instance.GetAllEntities<Sprout>())
        {
            if (!view.Contains(sprout.Position, 0.5f + Sprout.Reach)) continue;

            Vector3 local = view.ToLocal(sprout.Position);
            int look = sprout.Look;
            float wither = sprout.Withering;
            float size = (0.12f + 0.88f * sprout.Growth) * (1f - 0.6f * wither);    // rises from a nub, sinks as it dies
            float slump = 0.9f * wither;                                          // and flops over
            float thin = 0.5f + 0.5f * size;
            float sway = MathF.Sin(time * SwaySpeed + Rng.Fixed(look, 9) * MathF.Tau) * SwayAngle;

            // Blades: spread around the middle, leaning out and curling over a little
            int blades = BladesMin + (int)(Rng.Fixed(look, 1) * (BladesMax - BladesMin + 1));
            for (int b = 0; b < blades; b++)
            {
                float around = (b + Rng.Fixed(look, 2, b)) / blades * MathF.Tau;
                float lean = 0.2f + 0.45f * Rng.Fixed(look, 3, b) + slump + sway * MathF.Cos(around);
                float curl = 0.3f + 0.5f * Rng.Fixed(look, 4, b);
                float length = BladeLength * (0.65f + 0.6f * Rng.Fixed(look, 5, b)) * size;
                Vector3 color = Vector3.Lerp(BladeGreen * (0.85f + 0.3f * Rng.Fixed(look, 6, b)), Straw, wither);
                AddBend(local, around, lean, curl, length, new Vector2(BladeWidth, BladeThickness) * thin, BladeSegments, color, view, out _, out _);
            }

            // Stalks: taller and more upright, bending further the heavier their heads are
            SeedInfo seed = Seeds.Get(sprout.Seed);
            float ripe = sprout.Ripeness;
            float fill = sprout.HeadFill;
            float shown = MathF.Max(fill, BudShare * (1f - ripe) * sprout.Growth);
            int grains = (int)MathF.Round(HeadGrains * shown);
            Vector3 headColor = Vector3.Lerp(Vector3.Lerp(StalkGreen, ToFloats(seed.Color), ripe), Straw, wither);
            Vector3 grainSize = seed.Grain.Size * HeadGrainScale * (0.4f + 0.6f * ripe) * size;

            int stalks = StalksMin + (int)(Rng.Fixed(look, 7) * (StalksMax - StalksMin + 1));
            for (int s = 0; s < stalks; s++)
            {
                float around = (s + 0.5f * Rng.Fixed(look, 8, s)) / stalks * MathF.Tau + 0.4f;
                float lean = 0.1f + 0.25f * Rng.Fixed(look, 10, s) + 0.5f * slump + 1.5f * sway * MathF.Cos(around);
                float curl = 0.3f + ripe * (0.4f + 0.6f * fill) + slump;
                float length = StalkLength * (0.8f + 0.4f * Rng.Fixed(look, 11, s)) * size;
                Vector3 color = Vector3.Lerp(StalkGreen, Straw, wither);
                AddBend(local, around, lean, curl, length, new Vector2(StalkWidth, StalkWidth) * thin, StalkSegments, color, view,
                        out Vector3 tip, out Quaternion tipTurn);
                AddHead(local, tip, tipTurn, grains, grainSize, headColor, seed.Grain.Mesh, look, s, view);
            }
        }
    }

    // One blade or stalk: a chain of thin blocks from the sprout's middle, leaning out toward `around` by `lean`
    // radians from upright and bending over by `curl` more along its length. thickness is its width and depth.
    // Gives back where its tip ends up (from the middle, in world units) and how its last block is turned.
    private void AddBend(Vector3 local, float around, float lean, float curl, float length, Vector2 thickness,
                         int segments, Vector3 color, ViewWindow view, out Vector3 tip, out Quaternion tipTurn)
    {
        Vector3 outward = new Vector3(MathF.Cos(around), 0f, MathF.Sin(around));
        Vector3 axis = new Vector3(outward.Z, 0f, -outward.X);    // level, across the way it leans; it bends around this
        Quaternion face = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f - around);    // its flat side faces outward
        float step = length / segments;
        Vector3 point = Vector3.Zero;
        tipTurn = face;

        for (int i = 0; i < segments; i++)
        {
            float bend = MathF.Min(lean + curl * (i + 0.5f) / segments, MaxBend);
            Vector3 direction = Vector3.UnitY * MathF.Cos(bend) + outward * MathF.Sin(bend);
            tipTurn = Quaternion.Concatenate(face, Quaternion.CreateFromAxisAngle(axis, bend));

            Vector3 middle = point + direction * (step * 0.5f);
            Vector3 scale = new Vector3(thickness.X, step * 1.08f, thickness.Y);    // a touch long, so the joints don't gap
            AddGrain(GrainMesh.Block, local + middle * view.Zoom, tipTurn, scale * view.Zoom, color, view);
            point += direction * step;
        }
        tip = point;
    }

    // A seedhead: grains packed around the end of a stalk, from its tip back down toward its middle, lying along it.
    private void AddHead(Vector3 local, Vector3 tip, Quaternion tipTurn, int grains, Vector3 grainSize, Vector3 color,
                         GrainMesh mesh, int look, int stalk, ViewWindow view)
    {
        if (grains <= 0) return;

        Vector3 along = Vector3.Transform(Vector3.UnitY, tipTurn);    // the way the stalk's end points
        Vector3 side = Vector3.Transform(Vector3.UnitX, tipTurn);
        Vector3 other = Vector3.Transform(Vector3.UnitZ, tipTurn);
        Quaternion lying = Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2f), tipTurn);    // a grain's length along the stalk
        float ring = grainSize.X * 0.55f;

        for (int g = 0; g < grains; g++)
        {
            float back = HeadLength * g / (HeadGrains - 1);
            float angle = g * 2.4f + Rng.Fixed(look, 12, stalk) * MathF.Tau;    // a spiral around the stalk
            Vector3 spot = tip - along * back + (side * MathF.Cos(angle) + other * MathF.Sin(angle)) * ring;
            Vector3 shade = Shade(color, Rng.Fixed(look, 13, stalk, g));
            AddGrain(mesh, local + spot * view.Zoom, lying, grainSize * view.Zoom, shade, view);
        }
    }
}
