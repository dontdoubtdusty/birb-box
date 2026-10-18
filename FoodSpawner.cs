using System.Numerics;

// Creates food: thrown by the player, scattered by buttons, or dropped naturally over time.
public class FoodSpawner
{
    public static FoodSpawner Instance { get; set; } = null!;

    // SETTINGS (changed from the ImGui panel)
    public bool NaturalDrops = true;
    public float DropsPerMinute = 10f;
    public int MaxFood = 200;

    private int foodCounter = 0;
    private readonly Random ran = new();

    public FoodSpawner()
    {
        Instance = this;
    }

    public void Update(float dt)
    {
        if (!NaturalDrops) return;

        if (ran.NextDouble() < DropsPerMinute / 60f * dt)
        {
            SpawnFood(World.Instance.RandomGroundPoint());
        }
    }

    // Player click: the food drops in from the top of the hologram.
    public void ThrowFood(Vector3 groundPoint, float dropHeight)
    {
        SpawnFood(new Vector3(groundPoint.X, dropHeight, groundPoint.Z));
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
        float r = view.HalfSize * 0.8f;
        for (int i = 0; i < count; i++)
        {
            Vector3 local = new Vector3(RandomRange(-r, r), view.Height, RandomRange(-r, r));
            local.Y += RandomRange(0f, 2f);    // stagger so they don't all land at once
            SpawnFood(view.ToWorld(local));
        }
    }

    private void SpawnFood(Vector3 position)
    {
        if (World.Instance.GetAllEntities<Food>().Count >= MaxFood) return;

        foodCounter++;
        World.Instance.AddEntity(new Food($"Food #{foodCounter}", position));
    }

    private float RandomRange(float min, float max) => min + (float)ran.NextDouble() * (max - min);
}
