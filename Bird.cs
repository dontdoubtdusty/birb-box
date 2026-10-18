using System.Numerics;
using Raylib_cs;

public class Bird : Entity
{
    // IDENTITY
    public string Name { get; private set; }
    public BirdType Type { get; private set; }
    public string ModelKey { get; private set; }

    // BODY
    public float Size { get; private set; }        // model draw scale
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
    public Food? TargetFood { get; set; }
    public Vector3 FlightTarget { get; set; }
    public bool ArrivingByAir { get; set; }
    public bool IsFlying => Position.Y > GroundY + 0.05f;
    public BirdMover Mover { get; private set; }

    // TIMERS
    private float eatTimer = 0f;
    private const float BiteInterval = 1.5f;

    private static readonly Random ran = new();

    public Bird(BirdType type, string name)
    {
        Type = type;
        Name = name;
        ModelKey = PickModelKey();
        Size = GetBirdSize();
        Speed = 0.45f + (float)ran.NextDouble() * 0.55f;
        Lifespan = 240f + (float)ran.NextDouble() * 360f;    // 4 to 10 minutes
        Hunger = 0.5f + (float)ran.NextDouble() * 0.5f;
        Heading = (float)(ran.NextDouble() * Math.PI * 2);
        HopPhase = (float)ran.NextDouble() * 10f;
        Mover = new BirdMover(this);

        // Model origins are not always at the feet, so use the bounding box to find the ground offset.
        BoundingBox box = ModelLibrary.GetBounds(ModelKey);
        GroundY = -box.Min.Y * Size;
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
                    TryFindFood();
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
            World.Instance.RemoveEntity(this);
        }
    }

    private void TickDownHunger(float dt)
    {
        // The slider value is "seconds per 5% hunger", same idea as the old hunger timer.
        float rate = 0.05f / BirdSpawner.Instance.HungerTickdownRate;
        Hunger = MathF.Max(0f, Hunger - rate * dt);

        if (Hunger <= 0f) starveTimer += dt;
        else starveTimer = 0f;
    }

    public bool IsHungry() => Hunger < HungerThreshold;

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
        if (TargetFood == null || TargetFood.IsFullyEaten)
        {
            TargetFood = null;
            State = BirdState.Wandering;
            return;
        }

        if (Mover.IsCloseTo(TargetFood.Position, 0.6f))
        {
            State = BirdState.Eating;
            eatTimer = 0f;
            return;
        }

        Mover.UpdateWalking(dt);
    }

    private void UpdateEating(float dt)
    {
        if (TargetFood == null || TargetFood.IsFullyEaten)
        {
            TargetFood = null;
            State = BirdState.Wandering;
            return;
        }

        Mover.PeckAt(TargetFood.Position, dt);

        eatTimer += dt;
        if (eatTimer >= BiteInterval)
        {
            eatTimer = 0f;
            TargetFood.TakeBite();
            Hunger = MathF.Min(1f, Hunger + TargetFood.NutritionPerBite);

            if (Hunger >= 0.95f)
            {
                TargetFood = null;
                State = BirdState.Wandering;
            }
        }
    }

    public void StartLeaving(string reason)
    {
        if (State == BirdState.Leaving) return;

        State = BirdState.Leaving;
        TargetFood = null;
        Mover.PickLeavingTarget();
        EventLog.Add(reason);
    }

    private string PickModelKey()
    {
        switch (Type)
        {
            case BirdType.Cardinal:
                return ran.Next(0, 10) > 4 ? "cardinal" : "cardinal_f";
            case BirdType.Dove:
                return "dove";
            default:
                return "finch";
        }
    }

    private float GetBirdSize()
    {
        switch (Type)
        {
            case BirdType.Cardinal: return 0.33f;
            case BirdType.Dove: return 0.4f;
            case BirdType.Finch: return 0.2f;
        }
        return 0.33f;
    }

    // Center and radius used for click selection.
    public Vector3 PickCenter => Position + new Vector3(0, PickRadius * 0.6f, 0);
    public float PickRadius => MathF.Max(0.3f, Size * 1.1f);

    public string FormatAge()
    {
        return $"{(int)(Age / 60)}m {(int)(Age % 60)}s of {(int)(Lifespan / 60)}m";
    }
}
