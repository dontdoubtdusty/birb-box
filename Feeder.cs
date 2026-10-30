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

    // Who is on each perch (only for feeders with perches; not saved, birds find their way back after a load)
    private readonly Bird?[] perched;

    public Feeder(FeederStyle style)
    {
        Style = style;
        perched = new Bird?[Info.Perches];
    }

    public override void Update(float dt)
    {
        if (Seed != null && Seed.IsFullyEaten) Seed = null;
        FreeLeftPerches();
    }

    // ---------------- PERCHES ----------------

    // A perch is freed once its bird is gone, is no longer going for this feeder's seed, or has stopped eating.
    private void FreeLeftPerches()
    {
        for (int i = 0; i < perched.Length; i++)
        {
            Bird? bird = perched[i];
            if (bird == null) continue;
            bool stillHere = World.Instance.Contains(bird) && Seed != null && bird.TargetFood == Seed
                             && bird.State is BirdState.SeekingFood or BirdState.Eating;
            if (!stillHere) perched[i] = null;
        }
    }

    // True if the bird already has a perch here, a perch is free, or this feeder has no perches to run out of.
    public bool HasRoomFor(Bird bird)
    {
        if (perched.Length == 0) return true;
        return Array.IndexOf(perched, bird) >= 0 || Array.IndexOf(perched, null) >= 0;
    }

    // Where the bird stands to eat, or null if every perch is taken.
    // No perches (a tray): on the side it came from, PerchRadius out from the middle.
    // Perches: it takes the free one nearest to it, and stands on the rod's end facing in, as far out as its beak needs
    // to just reach the feeding port (so a big bird sits further out than a small one).
    public Vector3? TakePerch(Bird bird)
    {
        if (perched.Length == 0) return PerchSpot(bird.Position, bird.GroundY);

        int slot = Array.IndexOf(perched, bird);
        if (slot < 0)
        {
            float bestDistance = float.MaxValue;
            for (int i = 0; i < perched.Length; i++)
            {
                if (perched[i] != null) continue;
                float distance = Vector3.Distance(bird.Position, RodEnd(i, Info.RodReach));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    slot = i;
                }
            }
            if (slot < 0) return null;
            perched[slot] = bird;
        }

        float reach = Math.Clamp(Info.PortRadius + bird.BeakReach, Info.PortRadius + 0.05f, Info.RodReach);
        return RodEnd(slot, reach) + new Vector3(0f, bird.GroundY, 0f);
    }

    // A point on the rod, this far out on perch slot's side, at the height of its top (where the feet go).
    private Vector3 RodEnd(int slot, float distance)
    {
        float side = slot == 0 ? 1f : -1f;
        return new Vector3(Position.X + side * distance, Info.PerchHeight, Position.Z);
    }

    // Takes as much of the falling seed as fits. Returns what didn't fit, to spill onto the dirt.
    // impactSpeed: how fast it was falling (for the landing bounce in the tray).
    public Dictionary<SeedType, float> Catch(Dictionary<SeedType, float> falling, float impactSpeed = 0f)
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
                Seed = new Food(SeedSpot, taken, this, impactSpeed);
                World.Instance.AddEntity(Seed);
            }
            else
            {
                Seed.AddSeed(taken, impactSpeed);
            }
        }
        return spilled;
    }

    // Where a bird stands to eat at a feeder without perches: on the side it came from, PerchRadius out from the middle.
    private Vector3 PerchSpot(Vector3 birdPosition, float birdGroundY)
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

    // ---------------- SAVING (see SaveGame.cs) ----------------

    public FeederSave ToSave() => new()
    {
        Style = Style,
        Position = Position,
        Seed = Seed != null && !Seed.IsFullyEaten ? new(Seed.Contents) : null,
    };

    // Comes back out of the world, holding its seed: the caller sets it down (PlaceAt) or hands it to the player to carry.
    public static Feeder FromSave(FeederSave save)
    {
        Feeder feeder = new Feeder(save.Style) { Position = save.Position };
        if (save.Seed != null && save.Seed.Count > 0)
        {
            feeder.Seed = new Food(feeder.SeedSpot, new(save.Seed), feeder, age: Food.SettledAge);
        }
        return feeder;
    }
}

// Feeders bought at the store wait here until they're set down, then one at a time follows the mouse.
// Clicking a feeder in the box picks it up again to move it.
public class FeederPlacer
{
    private const float MinSpacing = 0.3f;    // gap kept between feeders' edges

    private readonly Queue<FeederStyle> bought = new();
    public Feeder? Carried { get; private set; }

    public void AddBought(FeederStyle style) => bought.Enqueue(style);
    public IEnumerable<FeederStyle> Bought => bought;

    // From a save: the feeders still waiting, and the one in hand (or null).
    public void Restore(List<FeederStyle> waiting, Feeder? carried)
    {
        bought.Clear();
        foreach (FeederStyle style in waiting) bought.Enqueue(style);
        Carried = carried;
    }

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

    // Returns false (and says why) if another feeder or a rock is in the way.
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

        if (Rock.At(groundPoint, Carried.Info.FootRadius + MinSpacing) != null)
        {
            EventLog.Add("Too close to a rock.");
            return false;
        }

        Carried.PlaceAt(groundPoint);
        Carried = null;
        return true;
    }
}
