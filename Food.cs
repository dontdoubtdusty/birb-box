using System.Numerics;

// A small pile of seed. It can fall from above, then gets eaten a bite at a time.
public class Food : Entity
{
    public string Name { get; private set; }
    public const int MaxBites = 3;
    public int Bites { get; private set; } = MaxBites;
    public float NutritionPerBite { get; private set; } = 0.35f;

    private float fallSpeed = 0f;
    private const float Gravity = 9.8f;
    private bool removed = false;

    public bool IsLanded => Position.Y <= 0f;
    public bool IsFullyEaten => Bites <= 0;

    public Food(string name, Vector3 position)
    {
        Name = name;
        Position = position;
    }

    public override void Update(float dt)
    {
        if (!IsLanded)
        {
            fallSpeed += Gravity * dt;
            float y = MathF.Max(0f, Position.Y - fallSpeed * dt);
            Position = new Vector3(Position.X, y, Position.Z);
        }

        if (IsFullyEaten && !removed)
        {
            removed = true;
            World.Instance.RemoveEntity(this);
        }
    }

    public void TakeBite()
    {
        if (Bites > 0) Bites--;
    }
}
