using System.Numerics;

// Creates food: poured by the player from the bucket, scattered by buttons, or dropped naturally over time.
public class FoodSpawner
{
    // SETTINGS (changed from the ImGui panel)
    public bool NaturalDrops = true;
    public float DropsPerMinute = 10f;
    public int MaxFood = 200;

    public const float PileAmount = 2f;        // seed in one poured pile
    public const float WildPileAmount = 1.5f;  // seed in one natural or scattered pile

    public bool HasRoom => World.Instance.GetAllEntities<Food>().Count < MaxFood;

    public void Update(float dt)
    {
        if (!NaturalDrops) return;

        if (Rng.Chance(DropsPerMinute / 60f * dt))
        {
            SpawnFood(World.Instance.RandomGroundPoint());
        }
    }

    // Poured from the bucket: the pile drops in from the top of the hologram.
    public void ThrowFood(Vector3 groundPoint, float dropHeight, Dictionary<SeedType, float> contents)
    {
        SpawnFood(new Vector3(groundPoint.X, dropHeight, groundPoint.Z), contents);
    }

    public void ScatterAcrossWorld(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnFood(World.Instance.RandomGroundPoint());
        }
    }

    public void ScatterInView(int count, ViewWindow view)
    {
        float maxDistance = view.Radius * 0.8f;
        for (int i = 0; i < count; i++)
        {
            // A random spot in the circle. The square root spreads them evenly instead of bunching at the center.
            float angle = Rng.Range(0f, MathF.PI * 2f);
            float distance = maxDistance * MathF.Sqrt(Rng.Range(0f, 1f));
            Vector3 local = new Vector3(MathF.Cos(angle) * distance, view.Height, MathF.Sin(angle) * distance);
            local.Y += Rng.Range(0f, 2f);    // stagger so they don't all land at once
            SpawnFood(view.ToWorld(local));
        }
    }

    // Natural drops and scatter buttons always make wild seed. Bagged seed only comes from the bucket.
    // Seed dropped from above a feeder lands in it.
    private void SpawnFood(Vector3 position, Dictionary<SeedType, float>? contents = null)
    {
        if (!HasRoom) return;

        contents ??= new() { [SeedType.Wild] = WildPileAmount };
        Feeder? below = position.Y > 0f ? FeederBelow(position) : null;
        World.Instance.AddEntity(new Food(position, contents) { LandOn = below });
    }

    private static Feeder? FeederBelow(Vector3 position)
    {
        foreach (Feeder feeder in World.Instance.GetAllEntities<Feeder>())
        {
            float distance = Vector2.Distance(new Vector2(position.X, position.Z), new Vector2(feeder.Position.X, feeder.Position.Z));
            if (distance <= feeder.Info.CatchRadius && position.Y > feeder.Info.CatchHeight) return feeder;
        }
        return null;
    }
}
