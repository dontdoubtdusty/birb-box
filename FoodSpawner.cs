using System.Numerics;

// Creates food: poured by the player from the bucket, scattered by buttons, or dropped naturally over time.
public class FoodSpawner
{
    // Natural drops, their rate and the food limit are in SimTuning.
    public const float PileAmount = 1f;        // seed in one poured pile
    public const float WildPileAmount = 1.5f;  // seed in one natural or scattered pile

    public bool HasRoom => World.Instance.GetAllEntities<Food>().Count < SimTuning.Now.MaxFood;

    // Natural drops lean toward the middle of the world, so more of them land where you can see.
    public void Update(float dt)
    {
        if (!SimTuning.Now.NaturalDrops) return;

        if (Rng.Chance(SimTuning.Now.DropsPerMinute / 60f * dt))
        {
            SpawnFood(World.Instance.RandomGroundPointNearMiddle());
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

    // Tools panel cheat: every pile lying on the dirt is gone (seed in feeders stays). Emptied piles remove themselves next update.
    public static void ClearGroundSeed()
    {
        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (food.Feeder == null) food.ClearAway();
        }
    }

    public void ScatterInView(int count, ViewWindow view)
    {
        float reach = view.HalfWidth * 0.8f;
        for (int i = 0; i < count; i++)
        {
            // A random spot in the clear middle of the cube
            Vector3 local = new Vector3(Rng.Range(-reach, reach), view.Height, Rng.Range(-reach, reach));
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
        if (position.Y <= 0f) position = Rock.PushOut(position);    // seed placed straight on the ground lands beside a rock, not in it
        Feeder? below = position.Y > 0f ? FeederBelow(position) : null;
        World.Instance.AddEntity(new Food(position, contents) { LandOn = below });
    }

    public static Feeder? FeederBelow(Vector3 position)
    {
        foreach (Feeder feeder in World.Instance.GetAllEntities<Feeder>())
        {
            float distance = Vector2.Distance(new Vector2(position.X, position.Z), new Vector2(feeder.Position.X, feeder.Position.Z));
            if (distance <= feeder.Info.CatchRadius && position.Y > feeder.Info.CatchHeight) return feeder;
        }
        return null;
    }
}
