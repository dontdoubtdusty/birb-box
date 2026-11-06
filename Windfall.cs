using System.Numerics;

// Windfall (a Gift): a gust of wind shakes loose a few small piles of seed over one patch near the focus, dropping in
// from above over a couple of seconds. The seed is what the birds here like (or the common species, when the garden is
// empty), never a rare species' lure seed. More birds about, a few more piles. Likelier in a quiet garden with little seed down.
public class Windfall : StoryEvent
{
    public override string Key => "windfall";
    public override string Name => "Windfall";
    public override StoryCategory Category => StoryCategory.Gift;

    // TUNING
    private const int PilesMin = 3, PilesMax = 6;
    private const float BirdsPerPile = 4f;          // one more pile for every this many birds here
    private const float Spread = 5f;                // piles land within this far of the focus
    private const float DropHeight = 5f;            // world units above the ground they fall from
    private const float DropGapMin = 0.2f, DropGapMax = 0.7f;    // seconds between piles
    private const float LowSeed = 6f;               // below this much seed on the ground, a windfall is likelier

    private readonly List<(Vector3 Spot, SeedType Seed)> toDrop = new();
    private FoodSpawner? food;
    private float dropTimer;

    public override bool CanFire(StoryContext ctx) => ctx.Daytime && ctx.Food.HasRoom;

    public override float Weight(StoryContext ctx) => (1.5f - ctx.Busy) * (ctx.GroundSeed < LowSeed ? 1.5f : 0.7f);

    public override void Start(StoryContext ctx)
    {
        Drop();
        food = ctx.Food;
        List<SeedType> kinds = PickSeeds(ctx);
        int piles = Math.Clamp(PilesMin + (int)(ctx.Birds.Count / BirdsPerPile), PilesMin, PilesMax);
        for (int i = 0; i < piles; i++)
        {
            Vector3 spot = ctx.Focus + new Vector3(Rng.Range(-Spread, Spread), 0f, Rng.Range(-Spread, Spread));
            spot = Obstacles.PushOut(World.Instance.PullInside(spot, 2f));
            toDrop.Add((spot, kinds[i % kinds.Count]));
        }
        dropTimer = 0f;

        string names = string.Join(" and ", kinds.Select(seed => Seeds.Get(seed).Name.ToLowerInvariant()));
        EventLog.Add($"A gust of wind brought a windfall of {names}.");
        Storyteller.Instance.Announce("A gust brings a windfall", ctx.Focus);
    }

    public override bool Update(float dt)
    {
        if (toDrop.Count == 0 || food == null) return true;
        dropTimer -= dt;
        if (dropTimer > 0f) return false;

        (Vector3 spot, SeedType seed) = toDrop[0];
        toDrop.RemoveAt(0);
        food.ThrowFood(spot, DropHeight, new Dictionary<SeedType, float> { [seed] = FoodSpawner.WildPileAmount });
        dropTimer = Rng.Range(DropGapMin, DropGapMax);
        return toDrop.Count == 0;
    }

    public override void Drop()
    {
        toDrop.Clear();
        food = null;
    }

    // One or two kinds of seed the birds here eat (the common species when nobody's here), never a lure seed.
    private static List<SeedType> PickSeeds(StoryContext ctx)
    {
        HashSet<SeedType> lures = Birds.All.Where(species => species.Lure != null).Select(species => species.Lure!.Value).ToHashSet();
        IEnumerable<Species> eaters = ctx.Birds.Count > 0 ? ctx.Birds.Select(bird => bird.Species).Distinct() : Birds.Common;
        List<SeedType> liked = Seeds.All.Where(seed => !lures.Contains(seed) && eaters.Any(species => species.Eats(seed))).ToList();
        if (liked.Count == 0) return new() { SeedType.Wild };

        List<SeedType> picked = new() { liked[Rng.Next(liked.Count)] };
        if (liked.Count > 1 && Rng.Chance(0.6f))
        {
            List<SeedType> others = liked.Where(seed => seed != picked[0]).ToList();
            picked.Add(others[Rng.Next(others.Count)]);
        }
        return picked;
    }
}
