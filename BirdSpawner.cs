using System.Numerics;

// Decides when new birds arrive. More seed in the world attracts more birds,
// and which birds come depends on which seeds are lying around (see the tastes in Seeds.cs).
public class BirdSpawner
{
    public static BirdSpawner Instance { get; set; } = null!;

    // SETTINGS (changed from the ImGui panel)
    public float HungerTickdownRate = 6f;            // seconds per 5% hunger lost (about 2 minutes from full to empty)
    public float ArrivalsPerFoodPerMinute = 0.6f;
    public int MaxBirds = 80;
    public float AirArrivalChance = 0.35f;           // the rest walk in from the edge
    public float GunkPenalty = 0.08f;                // each gunk pile in the world cuts arrivals by about this much

    private const float ArrivalCheckInterval = 1f;    // seconds between arrival rolls
    private float arrivalTimer = 0f;

    private readonly Dictionary<BirdType, int> nameCounters = new();

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

        // Every pile pulls on the birds that like its seed. Amounts are measured in
        // poured piles, so one pile of a seed a bird loves pulls about 1.
        Dictionary<BirdType, float> pull = new();
        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            foreach ((SeedType seed, float amount) in food.Contents)
            {
                if (food.IsSpoiled(seed)) continue;
                if (food.Feeder == null) Seeds.AddAttraction(pull, seed, amount / FoodSpawner.PileAmount);
                else AddFeederAttraction(pull, food.Feeder, seed, amount / FoodSpawner.PileAmount);
            }
        }

        float totalPull = pull.Values.Sum();
        if (totalPull <= 0f) return;

        // Expected arrivals this check, e.g. 1.3 means one bird plus a 30% chance of a second.
        // Gunk puts birds off: 10 piles of it cuts arrivals almost in half.
        int gunkCount = World.Instance.GetAllEntities<Gunk>().Count;
        float expected = totalPull * ArrivalsPerFoodPerMinute / 60f * elapsed / (1f + gunkCount * GunkPenalty);
        int arrivals = (int)expected + (Rng.Chance(expected % 1f) ? 1 : 0);

        int birdCount = World.Instance.GetAllEntities<Bird>().Count;
        for (int i = 0; i < arrivals && birdCount + i < MaxBirds; i++)
        {
            SpawnArrival(PickByPull(pull, totalPull));
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
    public Bird SpawnArrival(BirdType? type = null)
    {
        Bird bird = CreateBird(type ?? GetRandomBirdType());
        bird.State = BirdState.Arriving;
        bird.ArriveHungry();

        // Frostcrests always fly straight to the Blue Barley; walking in from the edge they might never find it.
        if (bird.Type == BirdType.Frostcrest || Rng.Chance(AirArrivalChance))
        {
            SetupAirArrival(bird);
            EventLog.Add($"{bird.Name} flew down from the sky.");
        }
        else
        {
            SetupEdgeArrival(bird);
            EventLog.Add($"{bird.Name} walked in from the edge of the world.");
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
        landing.Y = bird.GroundY;

        float angle = Rng.Range(0f, MathF.PI * 2f);
        Vector3 start = landing + new Vector3(MathF.Sin(angle) * 15f, 0, MathF.Cos(angle) * 15f);
        start.Y = Rng.Range(10f, 13f);

        bird.Position = start;
        bird.FlightTarget = landing;
        bird.ArrivingByAir = true;
    }

    // Names count up per type and never repeat, even after birds leave.
    private Bird CreateBird(BirdType type)
    {
        nameCounters.TryGetValue(type, out int count);
        count++;
        nameCounters[type] = count;
        return new Bird(type, $"{Birds.Get(type).Name} #{count}");
    }

    public static BirdType GetRandomBirdType()
    {
        return Birds.Common[Rng.Next(Birds.Common.Length)];
    }
}
