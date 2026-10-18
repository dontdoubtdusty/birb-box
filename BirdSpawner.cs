using System.Numerics;

// Decides when new birds arrive. More food in the world attracts more birds.
public class BirdSpawner
{
    public static BirdSpawner Instance { get; set; } = null!;

    // SETTINGS (changed from the ImGui panel)
    public float HungerTickdownRate = 1.0f;          // seconds per 5% hunger lost
    public float ArrivalsPerFoodPerMinute = 0.6f;
    public int MaxBirds = 80;
    public float AirArrivalChance = 0.35f;           // the rest walk in from the edge

    private readonly Dictionary<BirdType, int> nameCounters = new();
    private readonly Random ran = new();

    public BirdSpawner()
    {
        Instance = this;
    }

    public void Update(float dt)
    {
        int birdCount = World.Instance.GetAllEntities<Bird>().Count;
        int foodCount = World.Instance.GetAllEntities<Food>().Count;
        if (birdCount >= MaxBirds) return;

        // Each food pile adds a small chance per second that a bird shows up.
        float chancePerSecond = foodCount * ArrivalsPerFoodPerMinute / 60f;
        if (ran.NextDouble() < chancePerSecond * dt)
        {
            SpawnArrival();
        }
    }

    public Bird SpawnArrival()
    {
        Bird bird = CreateBird();
        bird.State = BirdState.Arriving;

        if (ran.NextDouble() < AirArrivalChance)
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

    // Birds already living here when the game starts. They skip the arrival step.
    public void SpawnResidents(int count, Vector2 near, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            Bird bird = CreateBird();
            float x = near.X + RandomRange(-radius, radius);
            float z = near.Y + RandomRange(-radius, radius);
            bird.Position = new Vector3(x, bird.GroundY, z);
            World.Instance.AddEntity(bird);
        }
    }

    private void SetupEdgeArrival(Bird bird)
    {
        float edge = World.Instance.HalfSize + 2f;
        float along = RandomRange(-edge, edge);
        Vector3 start;

        switch (ran.Next(4))
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
        // Aim near a food pile if there is one, since food is what attracts them.
        List<Food> foods = World.Instance.GetAllEntities<Food>();
        Vector3 landing = foods.Count > 0
            ? foods[ran.Next(foods.Count)].Position
            : World.Instance.RandomGroundPoint();

        landing += new Vector3(RandomRange(-3f, 3f), 0, RandomRange(-3f, 3f));
        landing.Y = bird.GroundY;

        float angle = RandomRange(0f, MathF.PI * 2f);
        Vector3 start = landing + new Vector3(MathF.Sin(angle) * 15f, 0, MathF.Cos(angle) * 15f);
        start.Y = RandomRange(10f, 13f);

        bird.Position = start;
        bird.FlightTarget = landing;
        bird.ArrivingByAir = true;
    }

    // Names count up per type and never repeat, even after birds leave.
    private Bird CreateBird()
    {
        BirdType type = GetRandomBirdType();
        nameCounters.TryGetValue(type, out int count);
        count++;
        nameCounters[type] = count;
        return new Bird(type, $"{type} #{count}");
    }

    public static BirdType GetRandomBirdType()
    {
        BirdType[] types = Enum.GetValues<BirdType>();
        return types[Random.Shared.Next(types.Length)];
    }

    private float RandomRange(float min, float max) => min + (float)ran.NextDouble() * (max - min);
}
