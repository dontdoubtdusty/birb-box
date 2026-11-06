using System.Numerics;

// Murmuration (a Big event): a passing flock of one common species sweeps in from one side, lands on the seed lying
// on the ground near the focus, strips it, and lifts off all together a minute or so later, or sooner once the seed it
// eats is gone. Its size follows the seed on the ground, never your progress. Flock birds are passing through, not
// individuals: no names of their own, no chat labels, no feathers, no records, never regulars (Bird.JoinFlock).
// Birds of the garden that watched it from close by get a line in their record.
// Built so the migration stopover can reuse it later with a seasonal species and a different trigger.
public class Murmuration : StoryEvent
{
    public override string Key => "murmuration";
    public override string Name => "Murmuration";
    public override StoryCategory Category => StoryCategory.Big;

    // TUNING
    private const float MinSeed = 4f;               // fresh seed on the ground needed before one can come
    private const int FlockMin = 6, FlockMax = 15;
    private const float BirdsPerSeed = 0.6f;        // flock size: 4 plus this many birds per seed on the ground
    private const float LandSpread = 2.5f;          // how far from a pile each bird lands
    private const float SeedReach = 10f;            // piles this near the focus are where it lands
    private const float ArrivalGapMin = 0.1f, ArrivalGapMax = 0.35f;    // seconds between birds of the flock coming in
    private const float StayMin = 60f, StayMax = 90f;                    // seconds after the last one is in
    private const float SeedCheckInterval = 2f;     // how often it checks whether the seed is gone
    private const float WatchReach = 10f;           // garden birds this near the flock get a record
    private const float MaxRain = 0.5f;

    private Species? species;
    private readonly List<Vector3> toLand = new();     // landing spots for the birds still to come
    private readonly List<Bird> flock = new();
    private float approachAngle;
    private float spawnTimer, stayTimer, checkTimer;

    public override bool CanFire(StoryContext ctx) =>
        ctx.Daytime && ctx.Rain < MaxRain && ctx.GroundSeed >= MinSeed && Birds.Common.Count > 0;

    // More seed on the ground makes a flock likelier.
    public override float Weight(StoryContext ctx) => MathF.Min(2f, ctx.GroundSeed / 8f);

    public override void Start(StoryContext ctx)
    {
        Drop();
        species = PickSpecies() ?? BirdSpawner.GetRandomBirdType();
        int count = Math.Clamp((int)MathF.Round(4f + ctx.GroundSeed * BirdsPerSeed), FlockMin, FlockMax);

        // Landing spots by the piles this species eats near the focus (or the nearest such piles, or the focus itself)
        List<Food> piles = World.Instance.GetAllEntities<Food>()
            .Where(food => food.Feeder == null && food.Plant == null && food.IsLanded && !food.IsFullyEaten && food.FavoriteFor(species) != null)
            .OrderBy(food => Vector3.Distance(food.Position, ctx.Focus))
            .ToList();
        List<Food> near = piles.Where(food => Vector3.Distance(food.Position, ctx.Focus) <= SeedReach).ToList();
        if (near.Count == 0) near = piles.Take(4).ToList();
        for (int i = 0; i < count; i++)
        {
            Vector3 center = near.Count > 0 ? near[i % near.Count].Position : ctx.Focus;
            toLand.Add(World.Instance.PullInside(center + new Vector3(Rng.Range(-LandSpread, LandSpread), 0f, Rng.Range(-LandSpread, LandSpread)), 2f));
        }

        approachAngle = Rng.Range(0f, MathF.PI * 2f);
        spawnTimer = 0f;
        stayTimer = Rng.Range(StayMin, StayMax);
        EventLog.Add($"A murmuration of {count} {Plural(species.Name)} swept in.");
        Storyteller.Instance.Announce("A murmuration sweeps in", ctx.Focus);
    }

    public override bool Update(float dt)
    {
        if (species == null) return true;

        // Coming in, a bird at a time from the same side
        if (toLand.Count > 0)
        {
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                SpawnOne(toLand[0]);
                toLand.RemoveAt(0);
                spawnTimer = Rng.Range(ArrivalGapMin, ArrivalGapMax);
            }
            return false;
        }

        flock.RemoveAll(bird => !World.Instance.Contains(bird) || bird.State == BirdState.Leaving);
        if (flock.Count == 0) return true;

        stayTimer -= dt;
        checkTimer -= dt;
        bool seedGone = false;
        if (checkTimer <= 0f)
        {
            checkTimer = SeedCheckInterval;
            seedGone = !World.Instance.GetAllEntities<Food>().Any(food => food.Feeder == null && food.IsLanded && !food.IsFullyEaten && food.FavoriteFor(species) != null);
        }
        if (stayTimer > 0f && !seedGone) return false;

        LiftOff();
        return true;
    }

    // Stopped early: birds already in the world are sent off by Storyteller.AfterLoad.
    public override void Drop()
    {
        species = null;
        toLand.Clear();
        flock.Clear();
    }

    private void SpawnOne(Vector3 landing)
    {
        Bird bird = new Bird(species!, $"{species!.Name} flock");
        bird.JoinFlock();
        bird.BecomeDayBird();
        bird.State = BirdState.Arriving;
        bird.ArriveHungry();
        BirdSpawner.FlyDownTo(bird, landing, approachAngle + Rng.Range(-0.25f, 0.25f));
        World.Instance.AddEntity(bird);
        flock.Add(bird);
    }

    // The whole flock takes off together; the garden birds close by remember it.
    private void LiftOff()
    {
        if (flock.Count == 0) return;
        Vector3 middle = flock.Aggregate(Vector3.Zero, (sum, bird) => sum + bird.Position) / flock.Count;
        foreach (Bird bird in flock) bird.StartLeaving(null, returns: false);

        string kind = Plural(species!.Name);
        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (bird.FlockMember || bird.State == BirdState.Leaving) continue;
            if (Vector3.Distance(bird.Position, middle) <= WatchReach) bird.AddRecord($"Watched a murmuration of {kind}");
        }
        EventLog.Add($"The murmuration of {kind} lifted off and was gone.");
        flock.Clear();
    }

    // The common species the seed on the ground pulls hardest, picked by weight (like arrivals, see BirdSpawner).
    private static Species? PickSpecies()
    {
        Dictionary<Species, float> pull = new();
        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (food.Feeder != null || food.Plant != null || !food.IsLanded) continue;
            foreach (SeedLayer layer in food.Layers)
            {
                if (!food.IsSpoiled(layer)) Seeds.AddAttraction(pull, layer.Seed, layer.Amount);
            }
        }
        List<(Species Species, float Pull)> common = pull.Where(pair => pair.Key.Common && pair.Key.CanAppear && pair.Value > 0f)
                                                        .Select(pair => (pair.Key, pair.Value)).ToList();
        if (common.Count == 0) return null;

        float roll = Rng.Range(0f, common.Sum(pair => pair.Pull));
        foreach ((Species kind, float amount) in common)
        {
            roll -= amount;
            if (roll <= 0f) return kind;
        }
        return common[^1].Species;
    }

    // "Gildfinch" -> "Gildfinches", "Dusk Dove" -> "Dusk Doves".
    public static string Plural(string name)
    {
        if (name.EndsWith("ch") || name.EndsWith("sh") || name.EndsWith("s") || name.EndsWith("x")) return name + "es";
        return name + "s";
    }
}
