using System.Numerics;

// How worn the ground is, all over the world: 0 is full grass, 1 is bare dirt.
// Birds on the ground and seed lying on it slowly wear the grass away; it grows back on its own.
// Around each rock the grass is lusher (Lush): it wears more slowly there, and the floor shader draws it greener.
// Simulation only: WorldDrawer turns this into a texture for the floor shader whenever Version changes.
public class GroundWear
{
    // TUNING (per second of sim time; SimTuning.WearSpeed and RegrowSpeed scale them from the tools panel)
    public const int CellsPerUnit = 2;               // grid resolution: 2 cells per world unit
    private const float WalkWear = 0.004f;           // a bird walking or standing about
    private const float EatWear = 0.012f;            // a bird pecking and scratching at seed
    private const float SeedWear = 0.002f;           // a pile of seed lying on the grass
    private const float Regrow = 0.0008f;            // grass growing back (bare to full in about 20 minutes)
    private const float BrushRadius = 0.8f;          // world units each wear spot reaches
    private const float RegrowInterval = 0.5f;       // regrowth is applied in steps this far apart
    private const float LushRing = 0.9f;             // world units past a rock's edge that the lush grass reaches
    private const float LushWearCut = 0.6f;          // fully lush grass wears this much slower (0.6 = 60% slower)

    public int Size { get; private set; }            // cells along each side
    public float HalfSize { get; private set; }      // the world's half size this grid covers
    public float[] Cells { get; private set; } = Array.Empty<float>();
    public float[] Lush { get; private set; } = Array.Empty<float>();    // 0 normal grass, 1 right at a rock (not saved: made from the rocks)
    public int Version { get; private set; }         // goes up whenever the cells change

    private float regrowTimer = 0f;

    // Fits the grid to the world and clears it to full grass.
    public void Reset(float worldHalfSize)
    {
        HalfSize = worldHalfSize;
        Size = (int)MathF.Ceiling(worldHalfSize * 2f * CellsPerUnit);
        Cells = new float[Size * Size];
        Lush = new float[Size * Size];
        Version++;
    }

    // Works out the lush ring around every rock. Called whenever the rocks are placed or loaded.
    public void RebuildLush(World world)
    {
        Array.Clear(Lush);
        foreach (Rock rock in world.GetAllEntities<Rock>())
        {
            float cx = (rock.Position.X + HalfSize) * CellsPerUnit;
            float cz = (rock.Position.Z + HalfSize) * CellsPerUnit;
            float inner = rock.Radius * CellsPerUnit;
            float outer = (rock.Radius + LushRing) * CellsPerUnit;

            int x0 = Math.Max(0, (int)(cx - outer)), x1 = Math.Min(Size - 1, (int)(cx + outer));
            int z0 = Math.Max(0, (int)(cz - outer)), z1 = Math.Min(Size - 1, (int)(cz + outer));
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (z + 0.5f - cz) * (z + 0.5f - cz));
                    float t = Math.Clamp((outer - d) / (outer - inner), 0f, 1f);
                    float lush = t * t * (3f - 2f * t);    // smooth fade from the edge outward
                    int i = z * Size + x;
                    Lush[i] = MathF.Max(Lush[i], lush);
                }
            }
        }
        Version++;
    }

    // Adds wear from everything standing or lying on the ground, and lets the grass grow back.
    public void Update(float dt, World world)
    {
        if (Size == 0) return;

        foreach (Bird bird in world.GetAllEntities<Bird>())
        {
            if (bird.IsFlying) continue;
            if (bird.State is BirdState.Arriving or BirdState.Leaving) continue;
            float rate = bird.State == BirdState.Eating ? EatWear : WalkWear;
            AddWear(bird.Position, rate * SimTuning.Now.WearSpeed * dt);
        }

        foreach (Food food in world.GetAllEntities<Food>())
        {
            if (food.Feeder != null || !food.IsLanded) continue;
            AddWear(food.Position, SeedWear * SimTuning.Now.WearSpeed * dt);
        }

        regrowTimer += dt;
        if (regrowTimer >= RegrowInterval)
        {
            float amount = Regrow * SimTuning.Now.RegrowSpeed * regrowTimer;
            for (int i = 0; i < Cells.Length; i++)
            {
                if (Cells[i] > 0f) Cells[i] = MathF.Max(0f, Cells[i] - amount);
            }
            regrowTimer = 0f;
        }
        Version++;
    }

    // Wears a soft round patch around a world position, strongest in the middle.
    private void AddWear(Vector3 pos, float amount)
    {
        float cx = (pos.X + HalfSize) * CellsPerUnit;
        float cz = (pos.Z + HalfSize) * CellsPerUnit;
        float reach = BrushRadius * CellsPerUnit;

        int x0 = Math.Max(0, (int)(cx - reach)), x1 = Math.Min(Size - 1, (int)(cx + reach));
        int z0 = Math.Max(0, (int)(cz - reach)), z1 = Math.Min(Size - 1, (int)(cz + reach));
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float d = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (z + 0.5f - cz) * (z + 0.5f - cz)) / reach;
                if (d >= 1f) continue;
                int i = z * Size + x;
                Cells[i] = MathF.Min(1f, Cells[i] + amount * (1f - d) * (1f - LushWearCut * Lush[i]));
            }
        }
    }

    // Saving (see SaveGame.cs): one byte per cell, as base64 text. A grid of another size (the world changed) is dropped.
    public string ToSave()
    {
        byte[] bytes = new byte[Cells.Length];
        for (int i = 0; i < Cells.Length; i++) bytes[i] = (byte)MathF.Round(Cells[i] * 255f);
        return Convert.ToBase64String(bytes);
    }

    public void Restore(string saved, float worldHalfSize)
    {
        Reset(worldHalfSize);
        if (string.IsNullOrEmpty(saved)) return;

        byte[] bytes;
        try { bytes = Convert.FromBase64String(saved); }
        catch (FormatException) { return; }
        if (bytes.Length != Cells.Length) return;

        for (int i = 0; i < Cells.Length; i++) Cells[i] = bytes[i] / 255f;
        Version++;
    }
}
