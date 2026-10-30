using System.Numerics;

// One puddle: a round patch of standing rainwater on bare dirt. It fills as the ground soaks and shrinks as it dries.
public class Puddle
{
    public Vector2 Spot;        // world X, Z
    public float Size;          // its radius when full (world units)
    public float Fill;          // 0 dry to 1 full; its radius right now is Size * Fill

    public float Radius => Size * Fill;

    // How far a spot is from this puddle's edge (below 0 inside it).
    public float EdgeDistance(Vector3 pos) => Vector2.Distance(new Vector2(pos.X, pos.Z), Spot) - Radius;
}

// The puddles rain leaves on bare, worn dirt (Weather.GroundWet). They only form while it rains and the ground is wet,
// on spots worn bare by birds and seed (GroundWear), clear of rocks, the pool, feeders, sprouts and seed heaps.
// Birds sip from them (BirdMover's WATER section); the floor shader draws them (hologram.fs, `puddles`).
// Saved with the weather.
public class Puddles
{
    // TUNING
    public const int MaxPuddles = 6;              // the shader has room for this many
    private const float MinSize = 0.35f, MaxSize = 0.8f;
    private const float FormWet = 0.35f;          // the ground must be at least this wet before puddles form
    private const float MinWear = 0.6f;           // the dirt must be at least this bare in the middle...
    private const float RimWear = 0.35f;          // ...and this bare around its rim
    private const float Gap = 1.2f;               // room kept between two puddles' edges
    private const float TryInterval = 2f;         // sim seconds between looks for a new spot
    private const int TriesPerLook = 12;
    private const float FillRate = 0.08f;         // per sim second toward how full the ground's wetness says
    private const float DryRate = 0.02f;          // per sim second while drying (slower: puddles linger after the rain)

    public IReadOnlyList<Puddle> All => all;
    private readonly List<Puddle> all = new();
    private float tryTimer;

    public void Clear() => all.Clear();

    // How full puddles should be for ground this wet.
    private static float FillFor(float wet) => Math.Clamp((wet - 0.25f) / 0.6f, 0f, 1f);

    public void Update(float dt, Weather weather, World world, GroundWear wear)
    {
        float target = FillFor(weather.GroundWet);
        for (int i = all.Count - 1; i >= 0; i--)
        {
            Puddle p = all[i];
            p.Fill = p.Fill < target ? MathF.Min(target, p.Fill + FillRate * dt) : MathF.Max(target, p.Fill - DryRate * dt);
            if (p.Fill <= 0.01f && target <= 0f) all.RemoveAt(i);
        }

        if (weather.Rain < 0.1f || weather.GroundWet < FormWet || all.Count >= MaxPuddles) return;
        tryTimer -= dt;
        if (tryTimer > 0f) return;
        tryTimer = TryInterval;

        for (int t = 0; t < TriesPerLook && all.Count < MaxPuddles; t++)
        {
            Vector3 spot = world.RandomGroundPoint();
            float size = Rng.Range(MinSize, MaxSize);
            if (CanForm(spot, size, world, wear)) all.Add(new Puddle { Spot = new Vector2(spot.X, spot.Z), Size = size, Fill = 0.05f });
        }
    }

    // A worn-bare spot with room around it: no rock, water, feeder, sprout, seed heap or other puddle in the way.
    private bool CanForm(Vector3 spot, float size, World world, GroundWear wear)
    {
        if (wear.At(spot) < MinWear) return false;
        for (int k = 0; k < 4; k++)
        {
            float angle = k * MathF.PI / 2f;
            Vector3 rim = spot + new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * size * 0.7f;
            if (wear.At(rim) < RimWear) return false;
        }

        if (Rock.At(spot, size + 0.2f) != null) return false;
        if (Pool.Distance(spot) < size + 0.5f) return false;
        foreach (Puddle other in all)
        {
            if (Vector2.Distance(other.Spot, new Vector2(spot.X, spot.Z)) < size + other.Size + Gap) return false;
        }
        foreach (Feeder feeder in world.GetAllEntities<Feeder>())
        {
            if (Flat(feeder.Position - spot) < size + 0.8f) return false;
        }
        foreach (Sprout sprout in world.GetAllEntities<Sprout>())
        {
            if (Flat(sprout.Position - spot) < size + 0.4f) return false;
        }
        foreach (Food food in world.GetAllEntities<Food>())
        {
            if (food.Feeder == null && food.Plant == null && Flat(food.Position - spot) < size + food.HeapRadius + 0.1f) return false;
        }
        return true;
    }

    private static float Flat(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    // The puddle nearest a spot within reach whose water is at least this full, or null.
    public Puddle? Nearest(Vector3 pos, float reach, float minFill)
    {
        Puddle? best = null;
        float bestDistance = reach;
        foreach (Puddle p in all)
        {
            if (p.Fill < minFill) continue;
            float d = p.EdgeDistance(pos);
            if (d < bestDistance)
            {
                best = p;
                bestDistance = d;
            }
        }
        return best;
    }

    public bool Contains(Puddle puddle) => all.Contains(puddle);

    // ---------------- SAVING ----------------

    public List<PuddleSave> ToSave() => all.Select(p => new PuddleSave { Spot = p.Spot, Size = p.Size, Fill = p.Fill }).ToList();

    public void Restore(List<PuddleSave>? saved)
    {
        all.Clear();
        if (saved == null) return;
        foreach (PuddleSave p in saved.Take(MaxPuddles))
        {
            all.Add(new Puddle { Spot = p.Spot, Size = Math.Clamp(p.Size, MinSize, MaxSize), Fill = Math.Clamp(p.Fill, 0f, 1f) });
        }
    }
}
