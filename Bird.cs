using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Raylib_cs;

public class Bird : Entity
{
    // IDENTITY
    public string Name { get; private set; }
    public bool IsNamed { get; private set; }      // the player renamed it
    public int Visits { get; private set; } = 1;   // 2 and up: a regular that came back
    public BirdType Type { get; private set; }
    public BirdInfo Info => Birds.Get(Type);
    public BirbShape Shape { get; private set; }
    public BoundingBox Bounds { get; private set; }    // around the shape, before Size is applied

    // BODY
    public float Size { get; private set; }        // birb units to world units
    public float Speed { get; private set; }       // walking speed, units per second
    public float Heading { get; set; }             // radians, 0 = facing +Z
    public float HopPhase { get; set; }
    public float GroundY { get; private set; }     // Y that puts the feet on the dirt

    // LIFE & HUNGER
    public float Age { get; private set; }         // seconds of simulation time
    public float Lifespan { get; private set; }
    public float Hunger { get; private set; } = 1f;     // 1 = full, 0 = starving
    public float HungerThreshold { get; private set; } = 0.4f;
    private const float StarveLimit = 25f;         // seconds at zero hunger before leaving
    private float starveTimer = 0f;
    private const float OldSaveLifespan = 3600f;   // saves from before 4.54 had shorter lifespans; those birds get a new one
    // Lifespan, losing interest, coin chance and call delay are tunable: see SimTuning.

    // HOME: the patch a bird keeps coming back to (see BirdMover.HomeForce). Set when it arrives, moved after each meal.
    public Vector3 Home { get; private set; }
    public bool HasHome { get; private set; }
    private const float HomeEdgeMargin = 8f;       // a home is never closer than this to the world's edge

    // LOSING INTEREST: now and then a wandering bird just leaves, sooner when there's nothing it likes to eat.
    private const float InterestCheckInterval = 15f;
    private const float ScarceFoodFactor = 4f;         // how much sooner when there's no seed it can eat
    private float interestTimer;
    private bool returnsLater = true;                  // false once it leaves for good (old age)

    // STATE
    public BirdState State { get; set; } = BirdState.Wandering;
    public BirdPose Pose { get; set; } = BirdPose.Idle;    // set by the mover, drawn by BirbAnimator
    public Gait Gait => Shape.Gait;
    public Food? TargetFood { get; set; }
    private Vector3? perchSpot;                     // where it stands while eating at a feeder
    public Vector3 FlightTarget { get; set; }
    public bool ArrivingByAir { get; set; }
    public bool IsFlying => Position.Y > GroundY + 0.05f;
    public BirdMover Mover { get; private set; }

    // TIMERS
    private float eatTimer = 0f;
    private const float BiteInterval = 1.5f;
    private float foodSearchTimer;                  // hungry birds look for food only when this runs out
    private const float FoodSearchInterval = 0.5f;

    // VOICE
    public float VoicePitch { get; private set; }  // each bird sounds a little higher or lower than the rest
    private float callTimer;                        // seconds until it calls out on its own
    private const float HushRetryMin = 4f, HushRetryMax = 12f;     // wait after holding a call for a nearby one
    private bool? wasFlying;                        // null until the first update, so a bird spawned in the air doesn't flap

    private bool removed = false;

    public Bird(BirdType type, string name)
    {
        Type = type;
        Name = name;
        Size = Info.Size;
        Speed = Rng.Range(0.45f, 1f);
        Lifespan = NewLifespan();
        Hunger = Rng.Range(0.5f, 1f);
        Heading = Rng.Range(0f, MathF.PI * 2f);
        HopPhase = Rng.Range(0f, 10f);
        foodSearchTimer = Rng.Range(0f, FoodSearchInterval);    // staggered so birds don't all search on one frame
        VoicePitch = Rng.Range(0.92f, 1.08f);
        callTimer = NextCallDelay();
        interestTimer = Rng.Range(0f, InterestCheckInterval);
        Mover = new BirdMover(this);

        SetShape(Rng.Next(Info.Shapes.Length));
    }

    // Picks one of its species' shapes. The origin is not at the feet, so the bottom of the box tells how high to lift the bird.
    [MemberNotNull(nameof(Shape))]
    private void SetShape(int index)
    {
        Shape = Info.Shapes[index];
        Bounds = Shape.GetBounds();
        GroundY = -Bounds.Min.Y * Size;
    }

    // ---------------- SAVING (see SaveGame.cs) ----------------

    public BirdSave ToSave() => new()
    {
        Type = Type,
        Name = Name,
        ShapeIndex = Math.Max(0, Array.IndexOf(Info.Shapes, Shape)),
        Position = Position,
        Speed = Speed,
        Heading = Heading,
        HopPhase = HopPhase,
        Age = Age,
        Lifespan = Lifespan,
        Hunger = Hunger,
        State = State,
        FlightTarget = FlightTarget,
        ArrivingByAir = ArrivingByAir,
        VoicePitch = VoicePitch,
        IsNamed = IsNamed,
        Visits = Visits,
        HasHome = HasHome,
        Home = Home,
    };

    // A bird that was heading for or eating some seed goes back to wandering; it finds its food again in a moment.
    // One that was up on a feeder flutters down first, like after any meal there.
    // A save from before the long lives gets a new lifespan in the new range.
    public static Bird FromSave(BirdSave save)
    {
        Bird bird = new Bird(save.Type, save.Name);
        bird.SetShape(Math.Clamp(save.ShapeIndex, 0, bird.Info.Shapes.Length - 1));
        bird.Position = save.Position;
        bird.Speed = save.Speed;
        bird.Heading = save.Heading;
        bird.HopPhase = save.HopPhase;
        bird.Age = save.Age;
        bird.Lifespan = save.Lifespan >= OldSaveLifespan ? save.Lifespan : NewLifespan();
        bird.Hunger = save.Hunger;
        bird.State = save.State is BirdState.SeekingFood or BirdState.Eating ? BirdState.Wandering : save.State;
        bird.FlightTarget = save.FlightTarget;
        bird.ArrivingByAir = save.ArrivingByAir;
        bird.VoicePitch = save.VoicePitch;
        bird.IsNamed = save.IsNamed;
        bird.Visits = Math.Max(1, save.Visits);
        bird.HasHome = save.HasHome;
        bird.Home = save.Home;
        return bird;
    }

    public override void Update(float dt)
    {
        Age += dt;
        TickDownHunger(dt);

        if (State != BirdState.Leaving)
        {
            if (Age >= Lifespan)
            {
                StartLeaving($"{Name} has grown old and flew off for good.", returns: false);
            }
            else if (starveTimer >= StarveLimit)
            {
                StartLeaving($"{Name} got too hungry and left to find food elsewhere.");
            }
            else
            {
                CheckInterest(dt);
            }
        }

        switch (State)
        {
            case BirdState.Arriving:
                Mover.UpdateArriving(dt);
                break;

            case BirdState.Wandering:
                // Not while up on a feeder or a rock: it gets down first (see BirdMover)
                if (IsHungry() && !IsFlying)
                {
                    foodSearchTimer -= dt;
                    if (foodSearchTimer <= 0f)
                    {
                        foodSearchTimer = FoodSearchInterval;
                        TryFindFood();
                    }
                }
                Mover.UpdateWalking(dt);
                break;

            case BirdState.SeekingFood:
                UpdateSeeking(dt);
                break;

            case BirdState.Eating:
                UpdateEating(dt);
                break;

            case BirdState.Leaving:
                Mover.UpdateLeaving(dt);
                break;
        }

        UpdateVoice(dt);

        // A bird that walks past the world edge has wandered off to somewhere else (rare: BirdMover turns them back).
        if (State != BirdState.Arriving && !World.Instance.IsInside(Position, 3f))
        {
            if (State != BirdState.Leaving)
            {
                EventLog.Add($"{Name} wandered off past the edge of the world.");
            }
            Remove();
        }
    }

    // Removes the bird from the world once, even if two things ask in the same frame.
    // Unless it left for good, it joins the regulars who may come back later (see BirdSpawner).
    public void Remove()
    {
        if (removed) return;
        removed = true;
        World.Instance.RemoveEntity(this);
        if (returnsLater) BirdSpawner.Instance.AddRegular(this);
    }

    // Every so often a wandering bird may lose interest and leave. Much likelier when nothing it eats is lying about.
    private void CheckInterest(float dt)
    {
        if (State != BirdState.Wandering || IsFlying) return;

        interestTimer -= dt;
        if (interestTimer > 0f) return;
        interestTimer = InterestCheckInterval;

        bool scarce = !World.Instance.GetAllEntities<Food>().Any(food => !food.IsFullyEaten && CanEat(food));
        float chance = InterestCheckInterval / (SimTuning.Now.LoseInterestMinutes * 60f) * (scarce ? ScarceFoodFactor : 1f);
        if (!Rng.Chance(chance)) return;

        StartLeaving(scarce ? $"{Name} found the pickings slim and flew off." : $"{Name} lost interest and flew off.");
    }

    // Moves its home patch to here, kept well inside the world.
    public void SetHome(Vector3 spot)
    {
        Home = World.Instance.PullInside(new Vector3(spot.X, 0f, spot.Z), HomeEdgeMargin);
        HasHome = true;
    }

    // A regular coming back for another visit (see BirdSpawner.TakeRegular).
    public void StartNewVisit()
    {
        Visits++;
        returnsLater = true;
        starveTimer = 0f;
    }

    // Calls out now and then, and flaps when it leaves or touches the ground.
    // If another bird called close by a moment ago, it holds its call and tries again a little later.
    private void UpdateVoice(float dt)
    {
        callTimer -= dt;
        if (callTimer <= 0f)
        {
            if (Sounds.CalledNearby(Position)) callTimer = Rng.Range(HushRetryMin, HushRetryMax);
            else Call();
        }

        bool flying = IsFlying;
        if (wasFlying != null && flying != wasFlying) Sounds.PlayFlap(Position, takeoff: flying);
        wasFlying = flying;
    }

    // Also used when the player clicks the bird.
    public void Call()
    {
        Sounds.PlayCall(this);
        callTimer = NextCallDelay();
    }

    private static float NextCallDelay() => Rng.Range(SimTuning.Now.CallDelayMin, SimTuning.Now.CallDelayMax);

    // Seconds this bird will live, from the tuned range in minutes.
    private static float NewLifespan() => Rng.Range(SimTuning.Now.LifespanMinMinutes, SimTuning.Now.LifespanMaxMinutes) * 60f;

    // Blank names are ignored, so the bird keeps its old one.
    public void Rename(string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0 || newName == Name) return;

        EventLog.Add($"{Name} shall henceforth be known as {newName}.");
        Name = newName;
        IsNamed = true;
    }

    private void TickDownHunger(float dt)
    {
        // The slider value is "seconds per 5% hunger".
        float rate = 0.05f / SimTuning.Now.HungerRate;
        Hunger = MathF.Max(0f, Hunger - rate * dt);

        if (Hunger <= 0f) starveTimer += dt;
        else starveTimer = 0f;
    }

    public bool IsHungry() => Hunger < HungerThreshold;

    // Birds drawn in by seed arrive ready to eat it.
    public void ArriveHungry()
    {
        Hunger = HungerThreshold - Rng.Range(0.05f, 0.15f);
    }

    // A bird only eats seed it has a taste for (see the seed table in Seeds.cs).
    public bool CanEat(Food food) => food.FavoriteFor(Type) != null;

    private void TryFindFood()
    {
        Food? nearest = Mover.FindNearestFood(10f);
        if (nearest != null)
        {
            TargetFood = nearest;
            State = BirdState.SeekingFood;
        }
    }

    private void UpdateSeeking(float dt)
    {
        if (TargetFood == null || !CanEat(TargetFood))
        {
            TargetFood = null;
            State = BirdState.Wandering;
            return;
        }

        // Close enough to a feeder to flutter up onto it, or to a pile to start pecking
        Feeder? feeder = TargetFood.Feeder;
        float reach = feeder != null ? feeder.Info.PerchRadius + 0.5f : 0.6f;
        if (Mover.IsCloseTo(TargetFood.Position, reach))
        {
            perchSpot = feeder?.TakePerch(this);
            if (feeder != null && perchSpot == null)
            {
                ForgetFood();    // every perch got taken on the way; it looks again in a moment
                return;
            }
            State = BirdState.Eating;
            eatTimer = 0f;
            return;
        }

        Mover.UpdateWalking(dt);
    }

    private void UpdateEating(float dt)
    {
        if (TargetFood == null || !CanEat(TargetFood))
        {
            TargetFood = null;
            State = BirdState.Wandering;
            return;
        }

        // At a feeder, it flies up to its perch before it starts eating
        if (perchSpot != null && !Mover.FlyToward(perchSpot.Value, dt)) return;

        Mover.PeckAt(TargetFood.Position, dt, perched: perchSpot != null);

        eatTimer += dt;
        if (eatTimer >= BiteInterval)
        {
            eatTimer = 0f;
            if (TargetFood.TakeBite(Type))
            {
                Hunger = MathF.Min(1f, Hunger + Food.NutritionPerBite);
            }

            if (Hunger >= 0.95f)
            {
                TargetFood = null;
                State = BirdState.Wandering;
                SetHome(Position);
                if (Rng.Chance(SimTuning.Now.CoinDropChance))
                {
                    World.Instance.AddEntity(new Coin(Position + new Vector3(0, 0.3f, 0)));
                }
            }
        }
    }

    // Stops going for its food, e.g. when the feeder it was eating from gets picked up.
    public void ForgetFood()
    {
        TargetFood = null;
        if (State == BirdState.SeekingFood || State == BirdState.Eating) State = BirdState.Wandering;
    }

    // returns: false when it's gone for good (old age), so it never comes back as a regular.
    public void StartLeaving(string reason, bool returns = true)
    {
        if (State == BirdState.Leaving) return;

        returnsLater = returns;
        State = BirdState.Leaving;
        TargetFood = null;
        Mover.PickLeavingTarget();
        EventLog.Add(reason);
    }

    // How far the tip of its beak sticks out in front of its middle (for sitting the right distance from a feeding port).
    public float BeakReach => Bounds.Max.Z * Size;

    // Roughly how far the bird reaches out from its middle when seen from above (for the selection ring).
    public float FootprintRadius => MathF.Max(Bounds.Max.X - Bounds.Min.X, Bounds.Max.Z - Bounds.Min.Z) * 0.5f * Size;

    public string FormatAge()
    {
        return $"{FormatTime(Age)} of {FormatTime(Lifespan)}";
    }

    // "1h 05m" past an hour, "12m 30s" below it.
    private static string FormatTime(float seconds)
    {
        if (seconds >= 3600f) return $"{(int)(seconds / 3600)}h {(int)(seconds % 3600 / 60):00}m";
        return $"{(int)(seconds / 60)}m {(int)(seconds % 60):00}s";
    }
}
