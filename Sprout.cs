using System.Numerics;

// A sprout: a grassy tuft that comes up where seed rotted away (see Food.TrySprout).
// Its life has three stages: it grows up from a nub (GrowShare of its life), stands ripe with seedheads of the
// seed it came from, then browns and sinks away (DieShare). While ripe, birds that like that seed stand beside it
// and peck the heads, the heads slowly fill again, and now and then they shed a little seed onto the dirt.
// As it starts to die back it drops whatever is left on its heads.
// The seed on its heads is a Food (Heads, with Plant set), so birds find and eat it like any other seed.
// How likely sprouts are, how long they live and how many there can be are in SimTuning. Drawing is in SproutDrawing.cs.
public class Sprout : Entity
{
    // STAGES (shares of its whole life)
    public const float GrowShare = 0.2f;          // growing up from a nub (3 of 15 minutes)
    public const float RipenShare = 0.06f;        // then the heads swell and fill
    public const float DieShare = 0.07f;          // browning and sinking at the very end

    // SEEDHEADS
    public const float HeadCapacity = 2f;         // seed the heads hold when full (4 bites)
    private const float RefillPerMinute = 0.5f;   // seed the heads grow back each minute while ripe
    private const float RefillStep = 0.1f;        // grown seed is added to the heads in steps this big
    private const float ShedEveryMin = 90f, ShedEveryMax = 180f;    // seconds between sheds while ripe
    private const float ShedAmount = 0.5f;        // seed in one shed
    private const float ShedReach = 0.45f;        // how far from the middle shed seed lands
    private const float ShedHeight = 0.3f;        // it falls from about head height
    public const float Reach = 0.3f;              // how far the heads hang out from the middle (birds stand just past it)

    // PLACING
    private const float Spacing = 1f;             // no new sprout this close to another
    private const float FeederRoom = 0.4f;        // nor this close to a feeder's foot
    private const float LifeSpread = 0.15f;       // lives up to this share longer or shorter than SimTuning.SproutMinutes

    public SeedType Seed { get; }
    public float Age { get; private set; }        // seconds of sim time
    public float Lifespan { get; }
    public int Look { get; }                      // picks its blades, stalks and lean when drawn
    public Food Heads { get; }

    private float shedTimer;
    private float refillCarry;                    // seed grown back but not yet added to the heads
    private float ripened;                        // seed added while the heads swelled (they swell to HeadCapacity)
    private bool dropped;                         // already dropped what was left as it started to die back
    private bool removed;

    private Sprout(Vector3 position, SeedType seed, float lifespan, int look)
    {
        Position = new Vector3(position.X, 0f, position.Z);
        Seed = seed;
        Lifespan = lifespan;
        Look = look;
        shedTimer = Rng.Range(ShedEveryMin, ShedEveryMax);
        Heads = Food.OnSprout(this);
    }

    // ---------------- STAGES ----------------

    public float Life => Math.Clamp(Age / Lifespan, 0f, 1f);       // 0 just sprouted, 1 gone
    public float Growth => Smooth(Life / GrowShare);                // 0 a nub, 1 fully grown
    public float Ripeness => Smooth((Life - GrowShare) / RipenShare);    // 0 no heads yet, 1 heads full-sized and colored
    public float Withering => Smooth((Life - (1f - DieShare)) / DieShare);    // 0 green, 1 brown and sunk away
    public float HeadFill => Math.Clamp(Heads.Total / HeadCapacity, 0f, 1f);  // how much seed is left on the heads
    public bool IsRipe => Life >= GrowShare && Life < 1f - DieShare;

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    // Grows older; while ripe it fills its heads and sheds now and then; it drops what's left as it starts to
    // die back, and is gone at the end of its life.
    public override void Update(float dt)
    {
        if (removed) return;
        Age += dt;

        if (Age >= Lifespan)
        {
            Remove();
            return;
        }
        if (Life >= 1f - DieShare)
        {
            if (!dropped) DropHeads();
            return;
        }
        if (Life < GrowShare) return;

        FillHeads(dt);
        shedTimer -= dt;
        if (shedTimer <= 0f)
        {
            shedTimer = Rng.Range(ShedEveryMin, ShedEveryMax);
            if (Heads.Total >= ShedAmount) Shed(ShedAmount);
        }
    }

    // While the heads swell they fill up along with it; after that they grow back slowly, up to HeadCapacity.
    private void FillHeads(float dt)
    {
        float grown;
        if (ripened < HeadCapacity - 0.001f)
        {
            grown = MathF.Max(0f, HeadCapacity * Ripeness - ripened);
            ripened += grown;
        }
        else
        {
            grown = RefillPerMinute / 60f * dt;
        }

        refillCarry += grown;
        if (refillCarry < RefillStep) return;

        float amount = MathF.Min(refillCarry, HeadCapacity - Heads.Total);
        refillCarry = 0f;
        if (amount > 0.01f) Heads.AddSeed(new() { [Seed] = amount });
    }

    // Takes up to this much seed off the heads and drops it on the dirt nearby, falling from about head height.
    // False when nothing came off, or there's no room for more seed in the world (SimTuning.MaxFood).
    private bool Shed(float amount)
    {
        if (World.Instance.GetAllEntities<Food>().Count >= SimTuning.Now.MaxFood) return false;
        float taken = Heads.TakeFromTop(amount);
        if (taken <= 0.01f) return false;

        float angle = Rng.Range(0f, MathF.Tau);
        float distance = Rng.Range(0.15f, ShedReach);
        Vector3 spot = Pool.PushOut(Rock.PushOut(Position + new Vector3(MathF.Cos(angle) * distance, 0f, MathF.Sin(angle) * distance)));
        spot.Y = ShedHeight;
        World.Instance.AddEntity(new Food(spot, new() { [Seed] = taken }) { LandOn = FoodSpawner.FeederBelow(spot) });
        return true;
    }

    // Starting to die back: whatever is left on the heads falls to the dirt (anything that doesn't fit in the world is lost).
    private void DropHeads()
    {
        dropped = true;
        for (int i = 0; i < 8 && Heads.Total > 0.05f; i++)
        {
            if (!Shed(MathF.Min(ShedAmount, Heads.Total))) break;
        }
        if (Heads.Total > 0f) Heads.TakeFromTop(Heads.Total);
    }

    // Gone from the world, heads and all. Birds going for its heads stop.
    private void Remove()
    {
        removed = true;
        Heads.RemoveNow();
        World.Instance.RemoveEntity(this);
    }

    // Puts the sprout and its heads into the world (they're two entities).
    public void AddToWorld(World world)
    {
        world.AddEntity(this);
        world.AddEntity(Heads);
    }

    // ---------------- PLANTING ----------------

    // Plants a new sprout of this seed here, unless there are already as many as allowed (SimTuning.MaxSprouts),
    // or another sprout or a feeder is too close. It's moved off any rock. grown: it starts fully grown, its heads
    // about to fill (the tools panel's cheat). Returns true if it was planted.
    public static bool TryPlant(Vector3 spot, SeedType seed, bool grown = false)
    {
        World world = World.Instance;
        if (world.GetAllEntities<Sprout>().Count >= SimTuning.Now.MaxSprouts) return false;

        spot = Rock.PushOut(new Vector3(spot.X, 0f, spot.Z), Reach);
        if (!world.IsInside(spot, -1f) || Pool.IsWater(spot, Reach + 0.2f)) return false;    // never in or right at the water

        Vector2 here = new Vector2(spot.X, spot.Z);
        foreach (Sprout other in world.GetAllEntities<Sprout>())
        {
            if (Vector2.Distance(here, new Vector2(other.Position.X, other.Position.Z)) < Spacing) return false;
        }
        foreach (Feeder feeder in world.GetAllEntities<Feeder>())
        {
            if (!feeder.InWorld) continue;
            if (Vector2.Distance(here, new Vector2(feeder.Position.X, feeder.Position.Z)) < feeder.Info.FootRadius + FeederRoom) return false;
        }

        float lifespan = SimTuning.Now.SproutMinutes * 60f * Rng.Range(1f - LifeSpread, 1f + LifeSpread);
        Sprout sprout = new Sprout(spot, seed, lifespan, Rng.Next(1 << 20));
        if (grown) sprout.Age = lifespan * GrowShare;
        sprout.AddToWorld(world);
        return true;
    }

    // ---------------- SAVING (see SaveGame.cs) ----------------
    // The heads are saved here as one amount, not as a Food of their own.

    public SproutSave ToSave() => new()
    {
        Position = Position,
        Seed = Seed,
        Age = Age,
        Lifespan = Lifespan,
        Look = Look,
        HeadSeed = Heads.Total,
        ShedTimer = shedTimer,
    };

    public static Sprout FromSave(SproutSave save)
    {
        Sprout sprout = new Sprout(save.Position, save.Seed, MathF.Max(1f, save.Lifespan), save.Look) { Age = save.Age };
        if (save.ShedTimer > 0f) sprout.shedTimer = save.ShedTimer;
        sprout.ripened = HeadCapacity * sprout.Ripeness;
        sprout.dropped = sprout.Life >= 1f - DieShare;
        if (save.HeadSeed > 0.01f) sprout.Heads.AddSeed(new() { [save.Seed] = save.HeadSeed });
        return sprout;
    }
}
