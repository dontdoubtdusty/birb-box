using System.Numerics;

// The water half of BirdMover: how a bird goes down to the pool or a rain puddle, sips, bathes and shakes off.
// Same class as BirdMover.cs (a partial class); the walking, flocking, perching and the rest are there.
public partial class BirdMover
{
    // AT THE POOL (see the WATER section): walking down to the shore, then sipping, or wading in to bathe and shaking off after
    private enum WaterVisit { None, Going, Sipping, Bathing, Drying }
    private WaterVisit water;
    private bool bathTrip;               // going to bathe, not just to sip
    private bool shaking;                // out of the water after a bath, shaking off
    private Vector3 waterSpot;           // where it's heading: the shore (to sip), the shallows (to bathe), or back out
    private float waterStand;            // how far from the waterline a sipper stands (its middle; the beak reaches over)
    private float waterTimer;            // seconds left of what it's doing there
    private float splashTimer;           // seconds until its next splash while bathing
    private float wadeRippleTimer;       // seconds until wading leaves its next small ripple
    private Puddle? puddle;              // sipping at this rain puddle instead of the pool (see Puddles.cs)
    private bool thirstTrip;             // went because it was thirsty (Bird.SeeToNeeds), not on a whim: a little hunger won't stop it
    private bool waterFlight;            // flying most of the way there, the water being far off
    private bool loggedBath;             // its first bath already made a log line (so the log isn't flooded)
    private Vector3 flightFrom, flightLanding;
    public bool IsBathTrip => water != WaterVisit.None && bathTrip && !shaking;
    public bool OnWaterTrip => water != WaterVisit.None;

    // What it's doing at the water, for the bird card (null when it isn't on a visit).
    public string? WaterActivity => water switch
    {
        WaterVisit.Going => "Off to the water",
        WaterVisit.Sipping => puddle != null ? "Drinking" : $"At the {Pool.HouseName}",
        WaterVisit.Bathing => $"In the {Pool.HouseName}",
        WaterVisit.Drying => "Drying off",
        _ => null,
    };

    // TUNING (water; the pool's own distances are in BirdMover.cs)
    private const float WaterReach = 7f;          // how far from the shore a bird will walk down to the water
    private const float WaterGiveUp = 15f;        // seconds to reach the water before it gives up
    private const float ShakeTime = 1.3f;         // shaking off after a bath
    private const float PuddleReach = 5f;         // how far from a rain puddle a bird will walk over to sip from it
    private const float PuddleMinFill = 0.4f;     // a puddle must be at least this full to drink from
    private const float FlyToWaterReach = 10f;    // a thirsty bird farther than this from the water flies there
    private const float WaterFlightArc = 2.5f;    // how high that flight goes at its middle (lower for short ones)
    private const float SipDrink = 0.2f;          // how much thirst one sip quenches (a bath quenches it all)
    private const int MaxSips = 6;                // a thirsty bird sips until quenched, up to this many
    public const float SipRate = 4.5f;            // HopPhase speed while sipping: one full turn is one sip
    public const float BathRate = 14f;            // HopPhase speed while bathing (the wing flutter)

    // ---------------- WATER (see Pool.cs) ----------------

    // Sometimes, instead of pausing, a wandering bird that isn't hungry and isn't far from the pool walks down to it:
    // to bathe in the shallows (if fewer than SimTuning.MaxBathers are, and it isn't raining), or just to sip at the shore.
    // Its spot is the shore nearest to it, nudged a little along so birds don't all crowd one spot.
    // A rain puddle nearer than the pool is only for sipping. Returns true when it sets off.
    // (A thirsty bird doesn't wait for this: see StartThirstVisit.)
    private bool TryStartWaterVisit()
    {
        Bird bird = ParentBird;
        if (bird.IsHungry() || bird.Asleep) return false;
        Pool? pool = Pool.Current;
        float toPool = pool?.DistanceAt(bird.Position) ?? float.MaxValue;
        Puddle? near = Weather.Instance.Puddles.Nearest(bird.Position, PuddleReach, PuddleMinFill);

        if (near != null && near.EdgeDistance(bird.Position) < toPool)
        {
            return Rng.Chance(SimTuning.Now.DrinkChance) && StartPuddleVisit(near);
        }
        if (pool == null || toPool > WaterReach) return false;

        bool bath = CanBathe() && Rng.Chance(SimTuning.Now.BathChance * bird.Nature.BathShare);
        if (!bath && !Rng.Chance(SimTuning.Now.DrinkChance)) return false;
        return StartWaterVisit(bath);
    }

    // A thirsty bird (Bird.SeeToNeeds) sets off for the nearest water from anywhere in the garden: a rain puddle if one
    // is nearer than the pool (to sip), otherwise the pool, to bathe now and then (SimTuning.ThirstyBathShare, more for a
    // Dabbler) or to sip. Farther than FlyToWaterReach it flies most of the way. False if there's no water it can get to.
    public bool StartThirstVisit()
    {
        Bird bird = ParentBird;
        Pool? pool = Pool.Current;
        float toPool = pool?.DistanceAt(bird.Position) ?? float.MaxValue;
        Puddle? near = Weather.Instance.Puddles.Nearest(bird.Position, MathF.Min(toPool, World.Instance.HalfSize * 3f), PuddleMinFill);

        bool started = false;
        if (near != null && near.EdgeDistance(bird.Position) < toPool)
        {
            started = StartPuddleVisit(near);
        }
        if (!started && pool != null)
        {
            bool bath = CanBathe() && Rng.Chance(SimTuning.Now.ThirstyBathShare * bird.Nature.BathShare);
            started = StartWaterVisit(bath) || (bath && StartWaterVisit(false));
        }
        if (!started) return false;

        thirstTrip = true;
        if (Vector2.Distance(Flat(bird.Position), Flat(waterSpot)) > FlyToWaterReach)
        {
            // Lands on the shore: right at its sipping spot, or beside the shallows it's going to bathe in
            Vector3 land = waterSpot;
            if (bathTrip)
            {
                Vector2 shore = pool!.MoveTo(Flat(waterSpot), 0.35f);
                land = new Vector3(shore.X, 0f, shore.Y);
            }
            land = Rock.PushOut(land, RockRoom);
            flightFrom = bird.Position;
            flightLanding = new Vector3(land.X, bird.StandY(land.X, land.Z), land.Z);
            waterFlight = true;
        }
        return true;
    }

    // Room for one more bather, and it isn't raining.
    private static bool CanBathe() => !Weather.Instance.IsRaining && Bathers() < SimTuning.Now.MaxBathers;

    // Ends a visit (finished, cut short, or the bird is off to sleep or leaving).
    private void EndWaterVisit()
    {
        water = WaterVisit.None;
        thirstTrip = false;
        waterFlight = false;
    }

    // Sets off for the water now, from wherever it is: to bathe, or to sip. Also the tools panel's cheat.
    // False if there's no pool, or the spot it picked is up against a rock.
    public bool StartWaterVisit(bool bath)
    {
        Bird bird = ParentBird;
        Pool? pool = Pool.Current;
        if (pool == null) return false;
        Vector2 here = Flat(bird.Position);

        // Sippers stand with their middle just short of the water, so the beak reaches over it
        float stand = bath ? -WadeDepth * Rng.Range(0.6f, 1f) : MathF.Max(0.04f, bird.BeakReach * 0.6f);
        Vector2 nudge = new Vector2(Rng.Range(-0.6f, 0.6f), Rng.Range(-0.6f, 0.6f));
        Vector2 spot = pool.MoveTo(pool.MoveTo(here + nudge, 0f) + nudge * 0.5f, stand);
        Vector3 spot3 = new Vector3(spot.X, 0f, spot.Y);
        if (Rock.At(spot3, RockRoom + 0.1f) != null) return false;

        puddle = null;
        thirstTrip = false;
        waterFlight = false;
        water = WaterVisit.Going;
        bathTrip = bath;
        waterStand = stand;
        shaking = false;
        waterSpot = spot3;
        waterTimer = WaterGiveUp + Vector2.Distance(here, spot) / MathF.Max(0.2f, bird.Speed);    // farther takes longer
        pauseTimer = 0f;
        return true;
    }

    // Sets off to sip at a rain puddle: to the edge nearest it, standing so the beak reaches over the water.
    private bool StartPuddleVisit(Puddle target)
    {
        Bird bird = ParentBird;
        Vector2 here = Flat(bird.Position);
        Vector2 away = here - target.Spot;
        away = away.LengthSquared() > 0.0001f ? Vector2.Normalize(away) : HeadingVector(bird.Heading);
        float stand = MathF.Max(0.04f, bird.BeakReach * 0.6f);
        Vector2 spot = target.Spot + away * (target.Radius + stand);
        Vector3 spot3 = new Vector3(spot.X, 0f, spot.Y);
        if (Rock.At(spot3, RockRoom + 0.1f) != null || Pool.IsWater(spot3, 0.1f)) return false;

        puddle = target;
        thirstTrip = false;
        waterFlight = false;
        water = WaterVisit.Going;
        bathTrip = false;
        waterStand = stand;
        shaking = false;
        waterSpot = spot3;
        waterTimer = WaterGiveUp + Vector2.Distance(here, spot) / MathF.Max(0.2f, bird.Speed);
        pauseTimer = 0f;
        return true;
    }

    // One step of the visit. Returns false when it's over (or cut short by hunger, bedtime or leaving,
    // or its puddle drying up), so the bird carries on as usual this frame. A thirsty bird only lets hunger
    // stop it once it's pressing (Bird.IsStarving).
    private bool UpdateWaterVisit(float dt)
    {
        Bird bird = ParentBird;
        Pool? pool = Pool.Current;
        bool hasWater = puddle != null ? Weather.Instance.Puddles.Contains(puddle) && puddle.Fill > 0.15f : pool != null;
        bool fed = thirstTrip ? !bird.IsStarving() : !bird.IsHungry();
        bool canStay = hasWater && bird.State == BirdState.Wandering && fed && !bird.Asleep;
        if (!canStay)
        {
            EndWaterVisit();
            return false;
        }

        switch (water)
        {
            case WaterVisit.Going:
                waterTimer -= dt;
                if (waterTimer <= 0f) { EndWaterVisit(); return false; }
                if (IsCloseTo(waterSpot, 0.12f))
                {
                    water = bathTrip ? WaterVisit.Bathing : WaterVisit.Sipping;
                    waterTimer = bathTrip ? Rng.Range(3f, 6f) * bird.Nature.BathLength : Sips() * MathF.Tau / SipRate;
                    bird.HopPhase = 0f;
                    splashTimer = 0f;
                    if (bathTrip && !loggedBath)
                    {
                        loggedBath = true;
                        EventLog.Add($"{bird.Name} is bathing in the {Pool.HouseName}.");
                    }
                    return true;
                }
                WalkTo(waterSpot, dt);
                return true;

            case WaterVisit.Sipping:
                // Facing the water: head down to scoop, then up to swallow. A ripple where the beak touches.
                TurnToward(puddle != null ? puddle.Spot - Flat(bird.Position) : -pool!.OutwardAt(Flat(bird.Position)), dt);
                float before = bird.HopPhase;
                bird.HopPhase += dt * SipRate;
                if (SipDips(before) != SipDips(bird.HopPhase))
                {
                    Vector2 beak = Flat(bird.Position) + HeadingVector(bird.Heading) * bird.BeakReach;
                    pool?.AddRipple(new Vector3(beak.X, 0f, beak.Y), 0.35f);    // the floor shader draws ripples on puddles too
                    Sounds.PlaySip(bird.Position);
                    bird.Drink(SipDrink);
                }
                bird.Pose = BirdPose.Sip;
                PutOnGround();
                waterTimer -= dt;
                if (waterTimer <= 0f) EndWaterVisit();
                return true;

            case WaterVisit.Bathing:
                // Crouched in the shallows, wings fluttering, throwing up a splash every so often
                bird.HopPhase += dt * BathRate;
                bird.Pose = BirdPose.Bathe;
                splashTimer -= dt;
                if (splashTimer <= 0f)
                {
                    splashTimer = Rng.Range(0.3f, 0.6f);
                    pool!.AddRipple(bird.Position, 0.8f);
                    pool.Splash(bird.Position, 3 + Rng.Next(3), 1f);
                    Sounds.PlaySplash(bird.Position);
                }
                PutOnGround();
                waterTimer -= dt;
                if (waterTimer <= 0f)
                {
                    // Out onto the shore, the way it's facing or straight back if that's quicker. It drank its fill in there.
                    bird.Drink(1f);
                    water = WaterVisit.Drying;
                    Vector2 shore = pool!.MoveTo(Flat(bird.Position), 0.35f);
                    waterSpot = new Vector3(shore.X, 0f, shore.Y);
                    waterTimer = WaterGiveUp;
                }
                return true;

            case WaterVisit.Drying:
                if (!shaking)
                {
                    waterTimer -= dt;
                    if (IsCloseTo(waterSpot, 0.12f) || waterTimer <= 0f)
                    {
                        shaking = true;
                        waterTimer = ShakeTime;
                        return true;
                    }
                    WalkTo(waterSpot, dt);
                    return true;
                }
                bird.Pose = BirdPose.ShakeOff;
                PutOnGround();
                waterTimer -= dt;
                if (waterTimer <= 0f) EndWaterVisit();
                return true;
        }
        return false;
    }

    // How many sips: a thirsty bird drinks until quenched (2 to MaxSips), a passing one 2 to 4.
    private int Sips()
    {
        if (!thirstTrip) return 2 + Rng.Next(3);
        return Math.Clamp((int)MathF.Ceiling((1f - ParentBird.Thirst) / SipDrink), 2, MaxSips);
    }

    // Flies to a spot on the ground in a low arc, highest at the middle (up to WaterFlightArc). True on landing.
    private bool FlyArc(Vector3 from, Vector3 to, float dt)
    {
        Bird bird = ParentBird;
        bird.Pose = BirdPose.Fly;
        Vector2 here = Flat(bird.Position);
        Vector2 target = Flat(to);
        float total = MathF.Max(0.01f, Vector2.Distance(Flat(from), target));
        float left = Vector2.Distance(here, target);
        float step = FlySpeed * dt;
        if (left <= step)
        {
            bird.Position = to;
            return true;
        }

        Vector2 dir = (target - here) / left;
        here += dir * step;
        float progress = Math.Clamp(1f - (left - step) / total, 0f, 1f);
        float height = MathF.Min(WaterFlightArc, total * 0.25f) * MathF.Sin(MathF.PI * progress);
        bird.Position = new Vector3(here.X, to.Y + height, here.Y);
        TurnToward(dir, dt);
        return false;
    }

    // Walks straight for a spot, minding other birds and rocks (but not the water: it's going there).
    private void WalkTo(Vector3 spot, float dt)
    {
        TurnToward(DirectionTo(spot) * 2f + SeparationForce() * 0.5f + RockForce(), dt);
        StepForward(ParentBird.Speed, dt);
    }

    // Counts the sips: goes up by one each time the beak reaches the water, a little over a quarter into each turn
    // (where BirbAnimator's Sip pose has the head lowest).
    private static int SipDips(float phase) => (int)MathF.Floor(phase / MathF.Tau - 0.275f);

    // A bird walking in the shallows leaves a small ripple behind it now and then.
    private void LeaveWadeRipple(float dt)
    {
        Pool? pool = Pool.Current;
        if (pool == null || pool.DistanceAt(ParentBird.Position) >= 0f) return;
        wadeRippleTimer -= dt;
        if (wadeRippleTimer > 0f) return;
        wadeRippleTimer = Rng.Range(0.4f, 0.7f);
        pool.AddRipple(ParentBird.Position, 0.3f);
    }

    private static int Bathers()
    {
        int count = 0;
        foreach (Bird other in World.Instance.GetAllEntities<Bird>())
        {
            if (other.Mover.IsBathTrip) count++;
        }
        return count;
    }
}
