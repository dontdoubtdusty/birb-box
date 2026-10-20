using System.Numerics;
using Raylib_cs;

public class Bird : Entity
{
    // IDENTITY
    public string Name { get; private set; }
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

    // COINS
    private const float CoinDropChance = 0.3f;    // chance to drop a coin after a full meal

    private bool removed = false;

    public Bird(BirdType type, string name)
    {
        Type = type;
        Name = name;
        Size = Info.Size;
        Speed = Rng.Range(0.45f, 1f);
        Lifespan = Rng.Range(240f, 600f);    // 4 to 10 minutes
        Hunger = Rng.Range(0.5f, 1f);
        Heading = Rng.Range(0f, MathF.PI * 2f);
        HopPhase = Rng.Range(0f, 10f);
        foodSearchTimer = Rng.Range(0f, FoodSearchInterval);    // staggered so birds don't all search on one frame
        Mover = new BirdMover(this);

        Shape = Info.Shapes[Rng.Next(Info.Shapes.Length)];
        Bounds = Shape.GetBounds();

        // The origin is not at the feet, so the bottom of the box tells how high to lift the bird.
        GroundY = -Bounds.Min.Y * Size;
    }

    public override void Update(float dt)
    {
        Age += dt;
        TickDownHunger(dt);

        if (State != BirdState.Leaving)
        {
            if (Age >= Lifespan)
            {
                StartLeaving($"{Name} has grown old and flew off.");
            }
            else if (starveTimer >= StarveLimit)
            {
                StartLeaving($"{Name} got too hungry and left to find food elsewhere.");
            }
        }

        switch (State)
        {
            case BirdState.Arriving:
                Mover.UpdateArriving(dt);
                break;

            case BirdState.Wandering:
                if (IsHungry())
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

        // A bird that walks past the world edge has wandered off to somewhere else.
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
    public void Remove()
    {
        if (removed) return;
        removed = true;
        World.Instance.RemoveEntity(this);
    }

    // Blank names are ignored, so the bird keeps its old one.
    public void Rename(string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0 || newName == Name) return;

        EventLog.Add($"{Name} shall henceforth be known as {newName}.");
        Name = newName;
    }

    private void TickDownHunger(float dt)
    {
        // The slider value is "seconds per 5% hunger".
        float rate = 0.05f / BirdSpawner.Instance.HungerTickdownRate;
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
            State = BirdState.Eating;
            eatTimer = 0f;
            perchSpot = feeder?.PerchSpot(Position, GroundY);
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
                if (Rng.Chance(CoinDropChance))
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

    public void StartLeaving(string reason)
    {
        if (State == BirdState.Leaving) return;

        State = BirdState.Leaving;
        TargetFood = null;
        Mover.PickLeavingTarget();
        EventLog.Add(reason);
    }

    // Roughly how far the bird reaches out from its middle when seen from above (for the selection ring).
    public float FootprintRadius => MathF.Max(Bounds.Max.X - Bounds.Min.X, Bounds.Max.Z - Bounds.Min.Z) * 0.5f * Size;

    public string FormatAge()
    {
        return $"{(int)(Age / 60)}m {(int)(Age % 60)}s of {(int)(Lifespan / 60)}m";
    }
}
