using System.Numerics;

// A small pile of seed. It can fall from above, then gets eaten a bite at a time.
// A pile can hold several kinds of seed. Each bird only eats the kinds it likes,
// so a mixed pile can outlast its first visitors.
//
// Seed left on the dirt goes bad. After its seed's SpoilTime (Seeds.cs) birds won't eat it,
// and once everything left has sat there GunkAfter times that long, the pile turns into Gunk.
// Wild seed is the exception: once spoiled, it just disappears.
//
// Seed falling onto a feeder is caught by it (LandOn) instead of reaching the dirt.
// The seed a feeder holds is a Food too (Feeder is set): it never spoils, and birds perch to eat it.
public class Food : Entity
{
    public const float BiteSize = 0.5f;           // seed removed per bite
    public const float NutritionPerBite = 0.35f;
    public const float GunkAfter = 2f;            // times the spoil time

    // Amount of each seed left. Only TakeBite and AddSeed change it, so Total and MainSeed are worked out then.
    public Dictionary<SeedType, float> Contents { get; }

    public Feeder? Feeder { get; }              // the feeder holding this seed, or null for seed on the dirt
    public Feeder? LandOn { get; init; }        // while falling: the feeder below that will catch it

    private float fallSpeed = 0f;
    private bool removed = false;

    public bool IsLanded { get; private set; }
    public float Total { get; private set; }
    public bool IsFullyEaten => Total <= 0.01f;
    public float Age { get; private set; }        // seconds since it landed

    // The seed there is most of, used for map dots.
    public SeedType MainSeed { get; private set; }

    public Food(Vector3 position, Dictionary<SeedType, float> contents, Feeder? feeder = null)
    {
        Position = position;
        Contents = contents;
        Feeder = feeder;
        IsLanded = feeder != null || position.Y <= 0f;
        RecountContents();
    }

    public override void Update(float dt)
    {
        if (!IsLanded)
        {
            // A feeder that was picked up while the seed fell can't catch it anymore.
            bool caught = LandOn != null && LandOn.InWorld;
            float floor = caught ? LandOn!.Info.CatchHeight : 0f;

            fallSpeed += World.Gravity * dt;
            float y = MathF.Max(floor, Position.Y - fallSpeed * dt);
            Position = new Vector3(Position.X, y, Position.Z);

            if (y <= floor)
            {
                IsLanded = true;
                if (caught) DropInto(LandOn!);
            }
        }
        else if (Feeder == null)
        {
            Age += dt;
            if (!removed && IsSpoiledWildSeed())
            {
                // Wild seed fades back into the dirt instead of leaving gunk.
                removed = true;
                World.Instance.RemoveEntity(this);
            }
            else if (!removed && HasTurnedToGunk())
            {
                World.Instance.AddEntity(new Gunk(Position, Total));
                removed = true;
                World.Instance.RemoveEntity(this);
            }
        }

        if (IsFullyEaten && !removed)
        {
            removed = true;
            World.Instance.RemoveEntity(this);
        }
    }

    // Hands this seed to the feeder. Whatever doesn't fit carries on falling to the dirt beside it.
    private void DropInto(Feeder feeder)
    {
        Dictionary<SeedType, float> spilled = feeder.Catch(Contents);
        if (spilled.Count > 0)
        {
            Vector3 beside = feeder.Position + new Vector3(feeder.Info.FootRadius + 0.1f, feeder.Info.CatchHeight, 0f);
            World.Instance.AddEntity(new Food(beside, spilled));
        }
        removed = true;
        World.Instance.RemoveEntity(this);
    }

    // Seed added to a feeder's pile.
    public void AddSeed(Dictionary<SeedType, float> more)
    {
        foreach ((SeedType seed, float amount) in more)
        {
            Contents.TryGetValue(seed, out float current);
            Contents[seed] = current + amount;
        }
        RecountContents();
    }

    // How far gone this seed is: 0 fresh, 1 spoiled, GunkAfter rotted through.
    // Always 0 for seed that never spoils and for seed in a feeder.
    public float Rot(SeedType seed)
    {
        float? spoilTime = Seeds.Get(seed).SpoilTime;
        return spoilTime == null || Feeder != null ? 0f : Age / spoilTime.Value;
    }

    // How much the bird likes eating here: 1 on the dirt, the feeder's appeal in a feeder.
    public float AppealFor(BirdType bird) => Feeder?.Info.AppealFor(bird) ?? 1f;

    public bool IsSpoiled(SeedType seed) => Rot(seed) >= 1f;

    private bool IsSpoiledWildSeed()
    {
        return Contents.Count > 0 && Contents.Keys.All(seed => seed == SeedType.Wild) && IsSpoiled(SeedType.Wild);
    }

    private bool HasTurnedToGunk()
    {
        foreach (SeedType seed in Contents.Keys)
        {
            if (Seeds.Get(seed).SpoilTime == null || Rot(seed) < GunkAfter) return false;
        }
        return Contents.Count > 0;
    }

    // The fresh seed left in this pile that the bird likes most, or null if there's nothing here for it
    // (including when it's in a feeder the bird can't use).
    public SeedType? FavoriteFor(BirdType bird)
    {
        if (AppealFor(bird) <= 0f) return null;

        SeedType? best = null;
        float bestTaste = 0f;

        foreach ((SeedType seed, float amount) in Contents)
        {
            if (amount <= 0.01f || IsSpoiled(seed)) continue;

            Seeds.Get(seed).Tastes.TryGetValue(bird, out float taste);
            if (taste > bestTaste)
            {
                best = seed;
                bestTaste = taste;
            }
        }
        return best;
    }

    // Eats one bite of the bird's favorite seed here. Returns false if there was nothing it wanted.
    public bool TakeBite(BirdType bird)
    {
        SeedType? seed = FavoriteFor(bird);
        if (seed == null) return false;

        float left = Contents[seed.Value] - BiteSize;
        if (left <= 0.01f) Contents.Remove(seed.Value);
        else Contents[seed.Value] = left;
        RecountContents();
        return true;
    }

    private void RecountContents()
    {
        Total = 0f;
        MainSeed = SeedType.Wild;
        float most = 0f;
        foreach ((SeedType seed, float amount) in Contents)
        {
            Total += amount;
            if (amount > most)
            {
                most = amount;
                MainSeed = seed;
            }
        }
    }
}
