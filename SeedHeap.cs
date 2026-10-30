using System.Numerics;

// One arrival of one kind of seed on a pile: a pour, a natural drop, or part of either.
public class SeedLayer
{
    private static int nextId = 0;

    public SeedType Seed { get; }
    public float Amount { get; set; }
    public float Age { get; set; }               // seconds since it landed; it spoils by this

    // For drawing only (not saved): tells its grains apart from every other layer's (kept when it joins another pile),
    // and where and how hard it hit, for the landing bounce.
    public int Id { get; } = nextId++;
    public Vector2 ImpactOffset { get; set; }    // where it landed, from the middle of the pile it ended up on
    public float ImpactSpeed { get; set; }

    public SeedLayer(SeedType seed, float amount, float age)
    {
        Seed = seed;
        Amount = amount;
        Age = age;
    }
}

// The shape of a heap of seed, shared by the simulation (how wide a pile is) and SeedDrawing.cs (where each grain sits).
// A heap is a rounded dome: the more seed, the wider and taller, at a fixed steepness (Slope).
// Each bite of seed is shown as GrainsPerBite grains. A share of the grains (stragglers) bounce off and lie
// around the heap's foot instead of on it; seed that scatters more (a wider Spread in Seeds.cs) has more of them.
public static class SeedHeap
{
    public const int GrainsPerBite = 6;
    public const float Slope = 0.45f;      // a heap's height over its radius
    public const float Packing = 0.6f;     // share of a heap that's seed rather than gaps between grains

    public static int GrainCount(float amount) => Math.Max(0, (int)MathF.Ceiling(amount / Food.BiteSize * GrainsPerBite - 0.01f));

    // The room one grain takes up in a heap, gaps included
    public static float GrainVolume(SeedType seed)
    {
        Vector3 size = Seeds.Get(seed).Grain.Size;
        return size.X * size.Y * size.Z / Packing;
    }

    public static float StragglerShare(SeedType seed) => Math.Clamp(Seeds.Get(seed).Grain.Spread * 0.8f, 0.08f, 0.3f);

    // The radius of a heap holding this much volume. A heap kept narrower (maxRadius, a feeder's tray) stands taller instead.
    public static float Radius(float volume, float maxRadius = float.MaxValue)
    {
        return MathF.Min(MathF.Cbrt(2f * volume / (MathF.PI * Slope)), maxRadius);
    }

    public static float Height(float volume, float radius) => radius <= 0.0001f ? 0f : 2f * volume / (MathF.PI * radius * radius);

    // The height of the heap's surface at distance r from its middle
    public static float SurfaceAt(float r, float radius, float height)
    {
        if (radius <= 0.0001f) return 0f;
        float share = r / radius;
        return height * MathF.Max(0f, 1f - share * share);
    }
}
