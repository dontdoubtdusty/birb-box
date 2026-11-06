using System.Numerics;
using Raylib_cs;

// The shape of the ground: a grid of heights over the whole world, rolled from a seed for each new world.
// Heights run from 0 (the old flat floor, so hollows are never below it) up to Amplitude, and fade back to 0 near the
// world's edge so the ground meets the cube's walls level. All zeros is the flat case: an old save, or Amplitude 0.
// Only the seed and the amplitude are saved; the grid is rebuilt from them, the same every time.
// Everything that sits on the ground asks HeightAt for the Y of the dirt at its X and Z.
public static class Terrain
{
    public const float Spacing = 0.5f;           // world units between grid points
    private const float EdgeFade = 5f;           // the ground flattens over this distance from the world's edge
    private const float BigSwell = 14f;          // size of the broad swells (world units per noise cell)
    private const float SmallSwell = 6f;         // size of the finer ones, at a third of the strength
    private const float BankWidth = 2.5f;        // the ground rises from the pool's shore to its full height over this distance

    public static int Seed { get; private set; }
    public static float Amplitude { get; private set; }
    public static bool IsFlat => Amplitude <= 0f;
    public static int Version { get; private set; }    // goes up whenever the grid changes, so the floor mesh knows to be rebuilt

    private static float[,] grid = new float[2, 2];    // [x, z] heights; a 2x2 of zeros until a world is made
    private static float half = 1f;                     // the world's half size the grid was built for
    private static int size = 2;                        // grid points per side

    // The flat ground: used by old saves and before any world exists.
    public static void MakeFlat(float worldHalfSize)
    {
        Build(0, 0f, worldHalfSize);
    }

    // A new roll: a fresh seed at the amplitude the tools panel asks for.
    public static TerrainSave Roll(float worldHalfSize)
    {
        Build(Random.Shared.Next(1, int.MaxValue), SimTuning.Now.TerrainHeight, worldHalfSize);
        return ToSave();
    }

    public static TerrainSave ToSave() => new() { Seed = Seed, Amplitude = Amplitude };

    public static void FromSave(TerrainSave? save, float worldHalfSize)
    {
        if (save == null || save.Amplitude <= 0f) Build(0, 0f, worldHalfSize);
        else Build(save.Seed, save.Amplitude, worldHalfSize);
    }

    // Fills the grid from the seed: two layers of smooth noise added together, scaled to 0..amplitude, flattened at the edge.
    private static void Build(int seed, float amplitude, float worldHalfSize)
    {
        Version++;
        Seed = seed;
        Amplitude = amplitude;
        half = worldHalfSize;
        size = (int)MathF.Round(worldHalfSize * 2f / Spacing) + 1;
        grid = new float[size, size];
        if (amplitude <= 0f) return;

        for (int ix = 0; ix < size; ix++)
        {
            for (int iz = 0; iz < size; iz++)
            {
                float x = ix * Spacing - worldHalfSize;
                float z = iz * Spacing - worldHalfSize;

                float n = ValueNoise(seed, x / BigSwell, z / BigSwell) + ValueNoise(seed + 7919, x / SmallSwell, z / SmallSwell) * 0.33f;
                float unit = Math.Clamp(n / 1.33f * 0.5f + 0.5f, 0f, 1f);    // 0..1, mostly mid-range

                float edge = MathF.Min(worldHalfSize - MathF.Abs(x), worldHalfSize - MathF.Abs(z));
                float fade = SmoothStep(Math.Clamp(edge / EdgeFade, 0f, 1f));
                grid[ix, iz] = unit * unit * amplitude * fade;    // squared: wide low ground with a few higher swells
            }
        }
    }

    // Levels the ground under and around the pool: the shore and the water sit at 0, and the banks rise gently from it.
    // Called whenever a pool is made or loaded (not saved: it is worked out again from the pool's shape).
    public static void FlattenAround(Pool pool)
    {
        if (Amplitude <= 0f) return;
        for (int ix = 0; ix < size; ix++)
        {
            for (int iz = 0; iz < size; iz++)
            {
                float d = pool.DistanceAt(new Vector2(ix * Spacing - half, iz * Spacing - half));
                if (d >= BankWidth) continue;
                grid[ix, iz] *= SmoothStep(Math.Clamp(d / BankWidth, 0f, 1f));
            }
        }
        Version++;
    }

    // The ground's height at a spot (smoothly blended between grid points). Outside the world it is 0.
    public static float HeightAt(float x, float z)
    {
        if (Amplitude <= 0f) return 0f;
        float fx = (x + half) / Spacing;
        float fz = (z + half) / Spacing;
        if (fx < 0f || fz < 0f || fx >= size - 1 || fz >= size - 1) return 0f;

        int ix = (int)fx, iz = (int)fz;
        float tx = fx - ix, tz = fz - iz;
        float a = grid[ix, iz] + (grid[ix + 1, iz] - grid[ix, iz]) * tx;
        float b = grid[ix, iz + 1] + (grid[ix + 1, iz + 1] - grid[ix, iz + 1]) * tx;
        return a + (b - a) * tz;
    }

    public static float HeightAt(Vector3 p) => HeightAt(p.X, p.Z);

    // The same position with its Y set to the ground there plus lift (a bird's feet offset, a seed's radius, and so on).
    public static Vector3 OnGround(Vector3 p, float lift = 0f) => new(p.X, HeightAt(p.X, p.Z) + lift, p.Z);

    // The direction straight up from the slope (unit length), from the heights a little to each side.
    public static Vector3 NormalAt(float x, float z)
    {
        if (Amplitude <= 0f) return Vector3.UnitY;
        const float d = Spacing;
        float dx = HeightAt(x + d, z) - HeightAt(x - d, z);
        float dz = HeightAt(x, z + d) - HeightAt(x, z - d);
        return Vector3.Normalize(new Vector3(-dx, 2f * d, -dz));
    }

    // How steep the ground is at a spot: height gained per unit of distance (0 level, 1 is 45 degrees).
    public static float SlopeAt(float x, float z)
    {
        if (Amplitude <= 0f) return 0f;
        const float d = Spacing;
        float dx = (HeightAt(x + d, z) - HeightAt(x - d, z)) / (2f * d);
        float dz = (HeightAt(x, z + d) - HeightAt(x, z - d)) / (2f * d);
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    // Which way is downhill (unit length on the ground plane, or zero on level ground), for seed that rolls.
    public static Vector2 DownhillAt(float x, float z)
    {
        if (Amplitude <= 0f) return Vector2.Zero;
        const float d = Spacing;
        Vector2 down = new(HeightAt(x - d, z) - HeightAt(x + d, z), HeightAt(x, z - d) - HeightAt(x, z + d));
        return down.LengthSquared() < 1e-6f ? Vector2.Zero : Vector2.Normalize(down);
    }

    // The ground point under a ray, found by stepping along it: used for mouse clicks on slopes. False if it never reaches the ground.
    public static bool RayHit(Ray ray, float maxDistance, out Vector3 hit)
    {
        hit = default;
        if (ray.Direction.Y >= -1e-5f && ray.Position.Y > Amplitude) return false;    // looking level or up from above the highest ground

        float prevT = 0f;
        float prevAbove = ray.Position.Y - HeightAt(ray.Position.X, ray.Position.Z);
        if (prevAbove <= 0f) return false;

        // Big steps while high above the ground, small ones near it (the ground can't rise faster than a slope of about 1).
        float t = 0f;
        for (int n = 0; n < 800 && t < maxDistance; n++)
        {
            t += Math.Clamp(prevAbove * 0.5f, 0.1f, 2f);
            Vector3 p = ray.Position + ray.Direction * t;
            float above = p.Y - HeightAt(p.X, p.Z);
            if (above <= 0f)
            {
                // The ray crossed the ground between the last two steps: narrow it down by halving.
                float lo = prevT, hi = t;
                for (int i = 0; i < 10; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    Vector3 m = ray.Position + ray.Direction * mid;
                    if (m.Y - HeightAt(m.X, m.Z) > 0f) lo = mid; else hi = mid;
                }
                Vector3 h = ray.Position + ray.Direction * hi;
                hit = new Vector3(h.X, HeightAt(h.X, h.Z), h.Z);
                return true;
            }
            prevT = t;
            prevAbove = above;
        }
        return false;
    }

    // ---------------- NOISE ----------------

    // Smooth random values on a lattice, blended between lattice points. Returns -1..1.
    private static float ValueNoise(int seed, float x, float z)
    {
        int x0 = (int)MathF.Floor(x), z0 = (int)MathF.Floor(z);
        float tx = SmoothStep(x - x0), tz = SmoothStep(z - z0);
        float a = Lattice(seed, x0, z0), b = Lattice(seed, x0 + 1, z0);
        float c = Lattice(seed, x0, z0 + 1), d = Lattice(seed, x0 + 1, z0 + 1);
        float top = a + (b - a) * tx;
        float bottom = c + (d - c) * tx;
        return top + (bottom - top) * tz;
    }

    private static float Lattice(int seed, int x, int z) => Rng.Fixed(seed, x, z, 31) * 2f - 1f;

    private static float SmoothStep(float t) => t * t * (3f - 2f * t);
}

// What is saved of the ground: just the seed and the height; the grid is rebuilt from them.
public class TerrainSave
{
    public int Seed { get; set; }
    public float Amplitude { get; set; }
}
