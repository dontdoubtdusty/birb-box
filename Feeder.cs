using System.Numerics;

// A feeder standing on the dirt. Seed poured over it is caught (see Food) and kept fresh,
// and birds perch on it to eat. Its seed is an ordinary Food pile sitting inside it,
// so birds find and eat it the same way as seed on the ground.
// A feeder can be picked up and set down somewhere else; its seed goes with it.
public class Feeder : Entity
{
    public FeederStyle Style { get; }
    public FeederInfo Info => Feeders.Get(Style);

    public Food? Seed { get; private set; }       // null when empty
    public float Held => Seed?.Total ?? 0f;
    public bool InWorld { get; private set; }     // false while it's being carried

    public Feeder(FeederStyle style)
    {
        Style = style;
    }

    public override void Update(float dt)
    {
        if (Seed != null && Seed.IsFullyEaten) Seed = null;
    }

    // Takes as much of the falling seed as fits. Returns what didn't fit, to spill onto the dirt.
    public Dictionary<SeedType, float> Catch(Dictionary<SeedType, float> falling)
    {
        float total = falling.Values.Sum();
        float share = total <= 0f ? 0f : MathF.Min(1f, (Info.Capacity - Held) / total);

        Dictionary<SeedType, float> taken = new();
        Dictionary<SeedType, float> spilled = new();
        foreach ((SeedType seed, float amount) in falling)
        {
            if (amount * share > 0.01f) taken[seed] = amount * share;
            if (amount * (1f - share) > 0.01f) spilled[seed] = amount * (1f - share);
        }

        if (taken.Count > 0)
        {
            if (Seed == null || Seed.IsFullyEaten)
            {
                Seed = new Food(SeedSpot, taken, this);
                World.Instance.AddEntity(Seed);
            }
            else
            {
                Seed.AddSeed(taken);
            }
        }
        return spilled;
    }

    // Where a bird stands to eat: on the side it came from, PerchRadius out from the middle.
    public Vector3 PerchSpot(Vector3 birdPosition, float birdGroundY)
    {
        Vector2 away = new Vector2(birdPosition.X - Position.X, birdPosition.Z - Position.Z);
        away = away.LengthSquared() > 0.0001f ? Vector2.Normalize(away) : new Vector2(0f, 1f);
        return new Vector3(
            Position.X + away.X * Info.PerchRadius,
            Info.PerchHeight + birdGroundY,
            Position.Z + away.Y * Info.PerchRadius);
    }

    // Lifts the feeder (and its seed) out of the world. Birds heading for its seed give up.
    public void PickUp()
    {
        InWorld = false;
        World.Instance.RemoveEntity(this);
        if (Seed == null) return;

        World.Instance.RemoveEntity(Seed);
        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (bird.TargetFood == Seed) bird.ForgetFood();
        }
    }

    public void PlaceAt(Vector3 groundPoint)
    {
        Position = new Vector3(groundPoint.X, 0f, groundPoint.Z);
        InWorld = true;
        World.Instance.AddEntity(this);
        if (Seed == null) return;

        Seed.Position = SeedSpot;
        World.Instance.AddEntity(Seed);
    }

    private Vector3 SeedSpot => Position + new Vector3(0f, Info.SeedHeight, 0f);
}

// Feeders bought at the store wait here until they're set down, then one at a time follows the mouse.
// Clicking a feeder in the box picks it up again to move it.
public class FeederPlacer
{
    private const float MinSpacing = 0.3f;    // gap kept between feeders' edges

    private readonly Queue<FeederStyle> bought = new();
    public Feeder? Carried { get; private set; }

    public void AddBought(FeederStyle style) => bought.Enqueue(style);

    // The next bought feeder is handed over once the box shows the birds.
    public void Update(bool birdsShowing)
    {
        if (Carried != null || bought.Count == 0 || !birdsShowing) return;

        Carried = new Feeder(bought.Dequeue());
        EventLog.Add($"Click the dirt to set down the {Carried.Info.Name}.");
    }

    public void PickUp(Feeder feeder)
    {
        if (Carried != null) return;
        feeder.PickUp();
        Carried = feeder;
        EventLog.Add($"Picked up the {feeder.Info.Name}. Click the dirt to set it down.");
    }

    // Returns false (and says why) if another feeder is in the way.
    public bool TryPlace(Vector3 groundPoint)
    {
        if (Carried == null) return false;

        foreach (Feeder other in World.Instance.GetAllEntities<Feeder>())
        {
            float gap = Vector2.Distance(new Vector2(groundPoint.X, groundPoint.Z), new Vector2(other.Position.X, other.Position.Z));
            if (gap < Carried.Info.FootRadius + other.Info.FootRadius + MinSpacing)
            {
                EventLog.Add("Too close to another feeder.");
                return false;
            }
        }

        Carried.PlaceAt(groundPoint);
        Carried = null;
        return true;
    }
}
