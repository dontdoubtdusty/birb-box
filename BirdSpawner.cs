using System.Numerics;

// Decides when new birds arrive. More seed in the world attracts more birds,
// and which birds come depends on which seeds are lying around (see the tastes in Seeds.cs).
public class BirdSpawner
{
    public static BirdSpawner Instance { get; set; } = null!;

    // SETTINGS (hunger, arrivals, max birds and regulars are in SimTuning)
    public float MinArrivalGap = 5f;                 // seconds; at most one new bird this often, however much food is down
    public float AirArrivalChance = 0.35f;           // the rest walk in from the edge

    private const float ArrivalCheckInterval = 1f;    // seconds between arrival rolls
    private float arrivalTimer = 0f;
    private float sinceLastArrival = 0f;

    private readonly Dictionary<BirdType, int> nameCounters = new();

    // REGULARS: birds that left (not of old age) and may come back under the same name.
    private readonly List<BirdSave> regulars = new();
    private const int MaxRegulars = 40;           // the longest-gone are forgotten past this

    public BirdSpawner()
    {
        Instance = this;
    }

    // Adding up every pile's pull is the slow part, so it only happens once per ArrivalCheckInterval.
    public void Update(float dt)
    {
        arrivalTimer += dt;
        if (arrivalTimer < ArrivalCheckInterval) return;
        float elapsed = arrivalTimer;
        arrivalTimer = 0f;
        sinceLastArrival += elapsed;

        // Every pile pulls on the birds that like its seed. Amounts are measured in
        // poured piles, so one pile of a seed a bird loves pulls about 1.
        Dictionary<BirdType, float> pull = new();
        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            foreach (SeedLayer layer in food.Layers)
            {
                if (food.IsSpoiled(layer)) continue;
                if (food.Feeder == null) Seeds.AddAttraction(pull, layer.Seed, layer.Amount / FoodSpawner.PileAmount);
                else AddFeederAttraction(pull, food.Feeder, layer.Seed, layer.Amount / FoodSpawner.PileAmount);
            }
        }

        float totalPull = pull.Values.Sum();
        if (totalPull <= 0f) return;

        if (sinceLastArrival < MinArrivalGap) return;
        if (World.Instance.GetAllEntities<Bird>().Count >= SimTuning.Now.MaxBirds) return;

        // The square root gives diminishing returns: 30 piles pull about 5x as hard as one, not 30x.
        float chance = MathF.Sqrt(totalPull) * SimTuning.Now.ArrivalsPerFoodPerMinute / 60f * elapsed;

        if (Rng.Chance(chance))
        {
            SpawnArrival(PickByPull(pull, totalPull));
            sinceLastArrival = 0f;
        }
    }

    // Seed in a feeder pulls harder on the birds that like that feeder, and not at all on birds that can't use it.
    private static void AddFeederAttraction(Dictionary<BirdType, float> pull, Feeder feeder, SeedType seed, float amount)
    {
        Dictionary<BirdType, float> plain = new();
        Seeds.AddAttraction(plain, seed, amount);
        foreach ((BirdType bird, float score) in plain)
        {
            pull.TryGetValue(bird, out float current);
            pull[bird] = current + score * feeder.Info.AppealFor(bird) * Feeders.PullBonus;
        }
    }

    // Bigger pull means a better chance of being the bird that shows up.
    private BirdType PickByPull(Dictionary<BirdType, float> pull, float totalPull)
    {
        float roll = Rng.Range(0f, totalPull);
        BirdType last = BirdType.Emberwing;
        foreach ((BirdType bird, float amount) in pull)
        {
            last = bird;
            roll -= amount;
            if (roll <= 0f) return bird;
        }
        return last;
    }

    // With no type given, picks one of the common birds.
    // Often it's a regular of that kind coming back instead of a new bird.
    public Bird SpawnArrival(BirdType? type = null)
    {
        BirdType birdType = type ?? GetRandomBirdType();
        Bird? regular = TakeRegular(birdType);
        Bird bird = regular ?? CreateBird(birdType);
        bird.State = BirdState.Arriving;
        bird.ArriveHungry();
        string who = regular != null ? $"{bird.Name} is back, and" : bird.Name;

        // Frostcrests always fly straight to the Blue Barley; walking in from the edge they might never find it.
        if (bird.Type == BirdType.Frostcrest || Rng.Chance(AirArrivalChance))
        {
            SetupAirArrival(bird);
            EventLog.Add($"{who} flew down from the sky.");
        }
        else
        {
            SetupEdgeArrival(bird);
            EventLog.Add($"{who} walked in from the edge of the world.");
        }

        World.Instance.AddEntity(bird);
        return bird;
    }

    private void SetupEdgeArrival(Bird bird)
    {
        float edge = World.Instance.HalfSize + 2f;
        float along = Rng.Range(-edge, edge);
        Vector3 start;

        switch (Rng.Next(4))
        {
            case 0: start = new Vector3(along, 0, -edge); break;
            case 1: start = new Vector3(along, 0, edge); break;
            case 2: start = new Vector3(-edge, 0, along); break;
            default: start = new Vector3(edge, 0, along); break;
        }

        bird.Position = new Vector3(start.X, bird.GroundY, start.Z);
        Vector3 inward = Vector3.Normalize(new Vector3(-start.X, 0, -start.Z));
        bird.FlightTarget = start + inward * 8f;
        bird.ArrivingByAir = false;
    }

    private void SetupAirArrival(Bird bird)
    {
        // Aim near a pile this bird can eat from, if there is one.
        List<Food> foods = World.Instance.GetAllEntities<Food>().Where(bird.CanEat).ToList();
        Vector3 landing = foods.Count > 0
            ? foods[Rng.Next(foods.Count)].Position
            : World.Instance.RandomGroundPoint();

        landing += new Vector3(Rng.Range(-3f, 3f), 0, Rng.Range(-3f, 3f));
        FlyDownTo(bird, landing);
    }

    // Starts the bird high up and a way off, flying down to land at this spot (moved off any rock).
    private static void FlyDownTo(Bird bird, Vector3 landing)
    {
        landing = Rock.PushOut(landing, 0.3f);
        landing.Y = bird.GroundY;

        float angle = Rng.Range(0f, MathF.PI * 2f);
        Vector3 start = landing + new Vector3(MathF.Sin(angle) * 15f, 0, MathF.Cos(angle) * 15f);
        start.Y = Rng.Range(10f, 13f);

        bird.Position = start;
        bird.FlightTarget = landing;
        bird.ArrivingByAir = true;
    }

    // The Birb Designer's Release button: a new bird wearing a saved design flies down near a spot (the middle of the view).
    // Always a new bird, never a regular coming back.
    public Bird Release(BirdType type, BirdLook look, Vector3 spot)
    {
        Bird bird = CreateBird(type);
        bird.UseLook(look);
        bird.State = BirdState.Arriving;
        bird.ArriveHungry();

        Vector3 landing = spot + new Vector3(Rng.Range(-1f, 1f), 0, Rng.Range(-1f, 1f));
        FlyDownTo(bird, World.Instance.PullInside(landing));
        EventLog.Add($"{bird.Name} was released into the cube.");

        World.Instance.AddEntity(bird);
        return bird;
    }

    // Names count up per type and never repeat, even after birds leave.
    private Bird CreateBird(BirdType type)
    {
        nameCounters.TryGetValue(type, out int count);
        count++;
        nameCounters[type] = count;
        return new Bird(type, $"{Birds.Get(type).Name} #{count}");
    }

    // ---------------- REGULARS ----------------

    // Called when a bird leaves the world for now (Bird.Remove). A bird past its lifespan is gone for good instead.
    public void AddRegular(Bird bird)
    {
        if (bird.Age >= bird.Lifespan) return;
        regulars.Add(bird.ToSave());
        if (regulars.Count > MaxRegulars) regulars.RemoveAt(0);
    }

    // Half the time, if any bird of this kind is away, one of them comes back. Null means a new bird comes instead.
    // Hearthbound birds are likelier to be the one, Wayfarers less (TraitEffects.ReturnWeight).
    private Bird? TakeRegular(BirdType type)
    {
        List<BirdSave> away = regulars.Where(save => save.Type == type).ToList();
        if (away.Count == 0 || !Rng.Chance(SimTuning.Now.RegularChance)) return null;

        BirdSave pick = PickByReturnWeight(away);
        regulars.Remove(pick);
        Bird bird = Bird.FromSave(pick);
        bird.StartNewVisit();
        return bird;
    }

    private static BirdSave PickByReturnWeight(List<BirdSave> away)
    {
        List<float> weights = away.Select(save => Traits.EffectsOf(save.Traits).ReturnWeight).ToList();
        float roll = Rng.Range(0f, weights.Sum());
        for (int i = 0; i < away.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f) return away[i];
        }
        return away[^1];
    }

    public List<BirdSave> SaveRegulars() => new(regulars);

    public void RestoreRegulars(List<BirdSave> saved)
    {
        regulars.Clear();
        regulars.AddRange(saved);
    }

    // The first Frostcrest to enter the view unlocks their other tastes (see Seeds.FrostcrestSeen).
    public void CheckFrostcrestSeen(ViewWindow view)
    {
        if (Seeds.FrostcrestSeen) return;

        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (bird.Type == BirdType.Frostcrest && bird.State != BirdState.Leaving && view.Contains(bird.Position))
            {
                Seeds.FrostcrestSeen = true;
                EventLog.Add("A Frostcrest has been spotted! Now that they know this place, corn may draw them in too.");
                return;
            }
        }
    }

    // Saving (see SaveGame.cs): the name counters, so a loaded world never repeats a name.
    public Dictionary<BirdType, int> SaveNameCounters() => new(nameCounters);

    public void RestoreNameCounters(Dictionary<BirdType, int> counters)
    {
        nameCounters.Clear();
        foreach ((BirdType type, int count) in counters) nameCounters[type] = count;
    }

    public static BirdType GetRandomBirdType()
    {
        return Birds.Common[Rng.Next(Birds.Common.Length)];
    }
}
