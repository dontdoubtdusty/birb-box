using System.Numerics;
using Raylib_cs;

// Draws the seed piles inside the hologram, as part of WorldDrawer. Also owns the two grain batches
// (round and block) that the sprouts (SproutDrawing.cs) and the pool's splash droplets add to as well.
public partial class WorldDrawer
{
    // Each pile is drawn as a heap of small grains, shaped by its seed type (GrainLook in Seeds.cs) and laid out by SeedHeap:
    // each grain sits where the heap's surface was when it arrived, so newer layers cover older ones.
    // Stragglers lie around the heap's foot. A big heap gets a dark core, so no ground shows between its grains.
    // A grain's spot and turn come from its layer and number, so the rest stay put while others are eaten.
    // Falling seed is a stream that stretches as it speeds up. On landing each grain arrives in turn and hops
    // to its resting spot (stragglers hop farther). As seed goes bad its grains darken and slump.
    // Seed in a tray stays inside its rim; seed in a tube is shown by the tube itself (FeederModel).
    // All grains go into two batches (round and block grains), each drawn in one go with the grain shader (grain.vs).
    // Sprouts (SproutDrawing.cs) add their blades, stalks and heads to the same batches.
    private const float TraySpread = 0.24f * Feeders.WorldScale;
    private static readonly Color RotColor = new Color(78, 74, 40, 255);    // the dark brown spoiling seed turns

    // THE FALL AND LANDING
    private const float StreamPerSpeed = 0.1f;    // a falling stream's length per unit of fall speed...
    private const float MaxStream = 1.2f;         // ...up to this long
    private const float MinStream = 0.12f;        // seed that landed without falling still trickles in from this high
    private const float SlowDrop = 1.5f;          // the speed it trickles in at
    private const float StreamRadius = 0.07f;     // how wide the stream is
    private const float LandingTime = 0.8f;       // a layer older than this has finished landing
    private const float SpinRate = 12f;           // radians a second a grain tumbles while in the air

    // THE HEAP
    private const float HeapTilt = 0.5f, StragglerTilt = 0.25f;    // how far grains lean over (radians)
    private const int CoreFrom = 20, CoreFull = 40;                 // heap grains before the core starts to show, and is full size
    private const float CoreShrink = 0.75f;       // the core's size as a share of the heap, so it stays under the grains
    private const float CoreDarken = 0.4f;

    private Mesh grainRoundMesh;    // unit-sized; each seed type stretches these into its own grain shape
    private Mesh grainBlockMesh;
    private Material grainMaterial;
    private Matrix4x4[] roundBatch = new Matrix4x4[512];
    private Matrix4x4[] blockBatch = new Matrix4x4[512];
    private int roundCount, blockCount;

    private void DrawFood(ViewWindow view)
    {
        roundCount = 0;
        blockCount = 0;

        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (food.Layers.Count == 0 || food.Plant != null) continue;    // seedheads are drawn with their sprout
            if (!view.Contains(food.Position, 0.5f + food.HeapRadius)) continue;
            if (food.Feeder?.Style == FeederStyle.Tube) continue;

            Vector3 local = view.ToLocal(food.Position);
            if (food.IsLanded) AddHeap(food, local, view);
            else AddStream(food, local, view);
        }
        AddSprouts(view);
        AddDroplets(view);

        if (roundCount > 0)
        {
            Raylib.DrawMeshInstanced(grainRoundMesh, grainMaterial, roundBatch, roundCount);
            DrawStats.CountModel();
        }
        if (blockCount > 0)
        {
            Raylib.DrawMeshInstanced(grainBlockMesh, grainMaterial, blockBatch, blockCount);
            DrawStats.CountModel();
        }
    }

    // Seed still in the air (or rolling off a rock): a loose stream trailing above the pile's position.
    private void AddStream(Food food, Vector3 local, ViewWindow view)
    {
        float trail = MathF.Min(MaxStream, food.FallSpeed * StreamPerSpeed);
        float tumble = (float)Raylib.GetTime() * SpinRate;

        foreach (SeedLayer layer in food.Layers)
        {
            SeedInfo info = Seeds.Get(layer.Seed);
            Vector3 size = info.Grain.Size;
            Vector3 color = ToFloats(info.Color);
            int grains = SeedHeap.GrainCount(layer.Amount);

            for (int g = 0; g < grains; g++)
            {
                float place = Pick(layer, g, 10);    // 0 at the front of the stream, 1 at the back
                Vector2 spot = StreamSpot(layer, g, place);
                Vector3 offset = new Vector3(spot.X, place * trail + size.Y * 0.5f, spot.Y);
                Quaternion turn = Quaternion.Concatenate(RestTurn(layer, g, HeapTilt), Spin(layer, g, tumble + Pick(layer, g, 16) * MathF.Tau));
                AddGrain(info.Grain.Mesh, local + offset * view.Zoom, turn, size * view.Zoom, Shade(color, Pick(layer, g, 7)), view);
            }
        }
    }

    // A landed pile: every grain on the heap (or around its foot), plus the dark core under a big heap.
    private void AddHeap(Food food, Vector3 local, ViewWindow view)
    {
        bool inTray = food.Feeder != null;
        float maxRadius = inTray ? TraySpread : float.MaxValue;

        // The whole heap's size first: the landing grains and the core need it. Grains still landing don't count for the core yet.
        float volume = 0f, coreVolume = 0f;
        int heapGrains = 0;
        foreach (SeedLayer layer in food.Layers)
        {
            float share = inTray ? 0f : SeedHeap.StragglerShare(layer.Seed);
            float grainVolume = SeedHeap.GrainVolume(layer.Seed);
            float landed = Math.Clamp(layer.Age / LandingTime, 0f, 1f);
            int grains = SeedHeap.GrainCount(layer.Amount);
            for (int g = 0; g < grains; g++)
            {
                if (Pick(layer, g, 1) < share) continue;
                volume += grainVolume;
                coreVolume += grainVolume * landed;
                heapGrains++;
            }
        }
        float radius = SeedHeap.Radius(volume, maxRadius);
        float height = SeedHeap.Height(volume, radius);

        // Each grain rests on the heap as it was when that grain arrived (running is the heap's volume up to it)
        float running = 0f;
        foreach (SeedLayer layer in food.Layers)
        {
            SeedInfo info = Seeds.Get(layer.Seed);
            GrainLook look = info.Grain;
            float share = inTray ? 0f : SeedHeap.StragglerShare(layer.Seed);
            float grainVolume = SeedHeap.GrainVolume(layer.Seed);
            float gone = Math.Clamp((food.Rot(layer) - 0.5f) / (Food.FadeAfter - 0.5f), 0f, 1f);    // 0 fresh, 1 rotted through
            Vector3 size = look.Size * new Vector3(1f, 1f - 0.5f * gone, 1f);
            Vector3 color = ToFloats(Palette.Mix(info.Color, RotColor, gone));
            int grains = SeedHeap.GrainCount(layer.Amount);

            for (int g = 0; g < grains; g++)
            {
                bool straggler = Pick(layer, g, 1) < share;
                float angle = Pick(layer, g, 3) * MathF.Tau;
                float distance;
                float y = size.Y * 0.5f;

                if (!straggler)
                {
                    running += grainVolume;
                    float radiusThen = SeedHeap.Radius(running, maxRadius);
                    float heightThen = SeedHeap.Height(running, radiusThen);
                    distance = radiusThen * MathF.Sqrt(Pick(layer, g, 2));    // the square root spreads grains evenly instead of bunching at the middle
                    y += SeedHeap.SurfaceAt(distance, radiusThen, heightThen) * (1f - 0.4f * gone);
                }
                else
                {
                    float footThen = SeedHeap.Radius(running, maxRadius);
                    distance = footThen * 0.9f + look.Spread * (0.3f + 0.9f * MathF.Sqrt(Pick(layer, g, 2))) * (1f - 0.3f * gone);
                }

                Vector3 rest = new Vector3(MathF.Cos(angle) * distance, y, MathF.Sin(angle) * distance);
                Quaternion turn = RestTurn(layer, g, straggler ? StragglerTilt : HeapTilt);

                if (layer.Age < LandingTime && Landing(layer, g, straggler, rest, size.Y, radius, height, inTray, out Vector3 now, out float spin))
                {
                    rest = now;
                    turn = Quaternion.Concatenate(turn, Spin(layer, g, spin));
                }
                AddGrain(look.Mesh, local + rest * view.Zoom, turn, size * view.Zoom, Shade(color, Pick(layer, g, 7)), view);
            }
        }

        // The core: a dark squashed ball half under the ground, just inside the heap, growing in as the heap does
        if (!inTray && heapGrains >= CoreFrom)
        {
            float grow = Math.Clamp((heapGrains - CoreFrom) / (float)(CoreFull - CoreFrom), 0f, 1f);
            float coreRadius = SeedHeap.Radius(coreVolume);
            float coreHeight = SeedHeap.Height(coreVolume, coreRadius);
            SeedLayer top = food.Layers[^1];
            float topGone = Math.Clamp((food.Rot(top) - 0.5f) / (Food.FadeAfter - 0.5f), 0f, 1f);
            Vector3 coreColor = ToFloats(Palette.Mix(Seeds.Get(top.Seed).Color, RotColor, topGone)) * CoreDarken;
            Vector3 coreSize = new Vector3(coreRadius, coreHeight, coreRadius) * (2f * CoreShrink * grow);
            AddGrain(GrainMesh.Round, local, Quaternion.Identity, coreSize * view.Zoom, coreColor, view);
        }
    }

    // Where a grain of a layer that's still landing is right now: falling the rest of the way to where its
    // part of the stream hit, then hopping from there to its resting spot. False once it has come to rest.
    // spin is how far it's still turned from its resting turn (it tumbles less and less until it lies still).
    private static bool Landing(SeedLayer layer, int g, bool straggler, Vector3 rest, float grainHeight,
                                float heapRadius, float heapHeight, bool inTray, out Vector3 now, out float spin)
    {
        float place = Pick(layer, g, 10);
        float speed = MathF.Max(layer.ImpactSpeed, SlowDrop);
        float trail = MathF.Max(MinStream, MathF.Min(MaxStream, layer.ImpactSpeed * StreamPerSpeed));
        float arrives = place * trail / speed;
        float hopTime = straggler ? 0.25f + 0.15f * Pick(layer, g, 13) : 0.1f + 0.08f * Pick(layer, g, 13);
        float hopHeight = straggler ? 0.05f + 0.07f * Pick(layer, g, 14) : 0.01f + 0.025f * Pick(layer, g, 14);
        float restsAt = arrives + hopTime;

        float t = layer.Age;
        now = rest;
        spin = 0f;
        if (t >= restsAt) return false;

        // Where it hit: its spot in the stream, on the heap's surface there
        Vector2 spot = layer.ImpactOffset + StreamSpot(layer, g, place);
        float surface = inTray ? rest.Y - grainHeight * 0.5f : SeedHeap.SurfaceAt(spot.Length(), heapRadius, heapHeight);
        Vector3 hit = new Vector3(spot.X, surface + grainHeight * 0.5f, spot.Y);

        spin = (restsAt - t) * SpinRate;
        if (t < arrives)
        {
            now = hit + new Vector3(0f, (arrives - t) * speed, 0f);
        }
        else
        {
            float p = (t - arrives) / hopTime;
            now = Vector3.Lerp(hit, rest, p) + new Vector3(0f, 4f * hopHeight * p * (1f - p), 0f);
        }
        return true;
    }

    // A grain's own spot across the falling stream, from the pile's middle; the back of the stream fans out wider
    private static Vector2 StreamSpot(SeedLayer layer, int g, float place)
    {
        float angle = Pick(layer, g, 11) * MathF.Tau;
        float distance = StreamRadius * (0.4f + place) * MathF.Sqrt(Pick(layer, g, 12));
        return new Vector2(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance);
    }

    // How a grain lies once it has come to rest: any way round, leaning over up to maxTilt
    private static Quaternion RestTurn(SeedLayer layer, int g, float maxTilt)
    {
        float yaw = Pick(layer, g, 4) * MathF.Tau;
        float tiltAround = Pick(layer, g, 6) * MathF.Tau;
        Vector3 tiltAxis = new Vector3(MathF.Cos(tiltAround), 0f, MathF.Sin(tiltAround));
        return Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw),
                                      Quaternion.CreateFromAxisAngle(tiltAxis, Pick(layer, g, 5) * maxTilt));
    }

    // A tumble of this many radians around the grain's own spin axis
    private static Quaternion Spin(SeedLayer layer, int g, float radians)
    {
        float around = Pick(layer, g, 15) * MathF.Tau;
        Vector3 axis = Vector3.Normalize(new Vector3(MathF.Cos(around), 0.6f, MathF.Sin(around)));
        return Quaternion.CreateFromAxisAngle(axis, radians);
    }

    // The pool's splash droplets (thrown up by bathing birds) as tiny pale grains, in the round batch.
    private static readonly Vector3 DropletColor = new Vector3(0.62f, 0.82f, 0.9f);

    private void AddDroplets(ViewWindow view)
    {
        Pool? pool = Pool.Current;
        if (pool == null) return;
        foreach (Pool.Droplet droplet in pool.Droplets)
        {
            if (!view.Contains(droplet.Position, 0.2f)) continue;
            Vector3 stretch = new Vector3(1f, 1.4f, 1f) * droplet.Size * view.Zoom;    // a little long, like a falling drop
            AddGrain(GrainMesh.Round, view.ToLocal(droplet.Position), Quaternion.Identity, stretch, DropletColor, view);
        }
    }

    // A fixed random number for one grain; the same layer, grain and purpose always give the same number
    private static float Pick(SeedLayer layer, int g, int purpose) => Rng.Fixed(layer.Id, g, purpose);

    // Adds one grain to its batch. boxPos is in box space; the grain is turned with the hologram.
    // Its color rides in the transform's unused bottom row (grain.vs takes it out). Raylib wants the matrix transposed.
    private void AddGrain(GrainMesh mesh, Vector3 boxPos, Quaternion turn, Vector3 scale, Vector3 color, ViewWindow view)
    {
        Matrix4x4 transform = Matrix4x4.CreateScale(scale)
                            * Matrix4x4.CreateFromQuaternion(turn)
                            * Matrix4x4.CreateRotationY(view.Yaw)
                            * Matrix4x4.CreateTranslation(view.BoxToScene(boxPos));
        transform.M14 = color.X;
        transform.M24 = color.Y;
        transform.M34 = color.Z;
        transform = Matrix4x4.Transpose(transform);

        if (mesh == GrainMesh.Block) AddToBatch(ref blockBatch, ref blockCount, transform);
        else AddToBatch(ref roundBatch, ref roundCount, transform);
    }

    private static void AddToBatch(ref Matrix4x4[] batch, ref int count, Matrix4x4 transform)
    {
        if (count == batch.Length) Array.Resize(ref batch, batch.Length * 2);
        batch[count++] = transform;
    }

    private static Vector3 ToFloats(Color c) => new Vector3(c.R, c.G, c.B) / 255f;

    // A little lighter or darker per grain (shade from 0 to 1, about 15% either way), the way ColorBrightness does it
    private static Vector3 Shade(Vector3 color, float shade)
    {
        float amount = shade * 0.3f - 0.15f;
        return amount < 0f ? color * (1f + amount) : color + (Vector3.One - color) * amount;
    }
}
