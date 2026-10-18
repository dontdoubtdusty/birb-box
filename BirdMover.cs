using System.Numerics;

// Handles HOW a bird moves: steering, hopping, flocking and flying.
// The Bird decides WHAT it wants to do; the mover carries it out.
public class BirdMover
{
    public Bird ParentBird { get; private set; }

    // WANDERING
    private float wanderAngle;
    private float pauseTimer = 0f;       // > 0 means standing still and pecking
    private float nextPauseIn;

    // TUNING
    private const float TurnRate = 4f;            // radians per second
    private const float FlySpeed = 3.2f;
    private const float NeighborRadius = 3f;      // how far a bird looks for its flock
    private const float SeparationRadius = 0.7f;  // personal space
    private const float HopHeight = 0.12f;

    private static readonly Random ran = new();

    public BirdMover(Bird bird)
    {
        ParentBird = bird;
        wanderAngle = bird.Heading;
        nextPauseIn = RandomRange(3f, 8f);
    }

    // ---------------- WALKING ----------------

    public void UpdateWalking(float dt)
    {
        Bird bird = ParentBird;

        if (bird.State == BirdState.Wandering && UpdatePausing(dt))
        {
            return;
        }

        Vector2 desired;
        if (bird.State == BirdState.SeekingFood && bird.TargetFood != null)
        {
            desired = DirectionTo(bird.TargetFood.Position) * 2f + SeparationForce();
        }
        else
        {
            wanderAngle += RandomRange(-1f, 1f) * dt * 2.5f;
            desired = HeadingVector(bird.Heading)
                    + HeadingVector(wanderAngle) * 0.6f
                    + FlockForce();
        }

        TurnToward(desired, dt);
        StepForward(bird.Speed, dt);
    }

    // Wandering birds stop now and then to peck at the ground.
    private bool UpdatePausing(float dt)
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= dt;
            SetHeightOnGround(0f);
            return true;
        }

        nextPauseIn -= dt;
        if (nextPauseIn <= 0f)
        {
            pauseTimer = RandomRange(0.8f, 2.5f);
            nextPauseIn = RandomRange(3f, 9f);
        }
        return false;
    }

    public void PeckAt(Vector3 foodPos, float dt)
    {
        TurnToward(DirectionTo(foodPos), dt);
        ParentBird.HopPhase += dt * 6f;
        SetHeightOnGround(MathF.Abs(MathF.Sin(ParentBird.HopPhase)) * HopHeight * 0.4f);
    }

    private void StepForward(float speed, float dt)
    {
        Bird bird = ParentBird;
        Vector2 dir = HeadingVector(bird.Heading);
        bird.HopPhase += dt * speed * 12f;

        float hop = MathF.Abs(MathF.Sin(bird.HopPhase)) * HopHeight;
        bird.Position = new Vector3(
            bird.Position.X + dir.X * speed * dt,
            bird.GroundY + hop,
            bird.Position.Z + dir.Y * speed * dt);
    }

    private void SetHeightOnGround(float extra)
    {
        Vector3 p = ParentBird.Position;
        ParentBird.Position = new Vector3(p.X, ParentBird.GroundY + extra, p.Z);
    }

    // ---------------- FLOCKING ----------------

    // Same-type birds nearby pull together (cohesion) and face the same way (alignment).
    // All birds push apart when too close (separation).
    private Vector2 FlockForce()
    {
        Bird bird = ParentBird;
        Vector2 center = Vector2.Zero;
        Vector2 alignment = Vector2.Zero;
        int neighbors = 0;

        foreach (Bird other in World.Instance.GetAllEntities<Bird>())
        {
            if (other == bird || other.Type != bird.Type || other.IsFlying) continue;

            float dist = Vector2.Distance(Flat(other.Position), Flat(bird.Position));
            if (dist < NeighborRadius)
            {
                center += Flat(other.Position);
                alignment += HeadingVector(other.Heading);
                neighbors++;
            }
        }

        Vector2 force = SeparationForce();
        if (neighbors > 0)
        {
            center /= neighbors;
            Vector2 toCenter = center - Flat(bird.Position);
            if (toCenter.LengthSquared() > 0.0001f)
            {
                force += Vector2.Normalize(toCenter) * 0.5f;
            }
            force += Vector2.Normalize(alignment + new Vector2(0.0001f, 0)) * 0.4f;
        }
        return force;
    }

    private Vector2 SeparationForce()
    {
        Bird bird = ParentBird;
        Vector2 push = Vector2.Zero;

        foreach (Bird other in World.Instance.GetAllEntities<Bird>())
        {
            if (other == bird || other.IsFlying) continue;

            Vector2 away = Flat(bird.Position) - Flat(other.Position);
            float dist = away.Length();
            if (dist > 0.001f && dist < SeparationRadius)
            {
                push += away / (dist * dist) * 0.15f;
            }
        }
        return push;
    }

    // ---------------- ARRIVING & LEAVING ----------------

    public void UpdateArriving(float dt)
    {
        Bird bird = ParentBird;

        if (bird.ArrivingByAir)
        {
            if (FlyToward(bird.FlightTarget, dt))
            {
                SetHeightOnGround(0f);
                bird.State = BirdState.Wandering;
            }
        }
        else
        {
            TurnToward(DirectionTo(bird.FlightTarget), dt);
            StepForward(bird.Speed, dt);

            if (World.Instance.IsInside(bird.Position, -3f))
            {
                bird.State = BirdState.Wandering;
            }
        }
    }

    public void PickLeavingTarget()
    {
        // Fly off in roughly the direction the bird is already facing, up and out of the world.
        float angle = ParentBird.Heading + RandomRange(-0.8f, 0.8f);
        Vector2 dir = HeadingVector(angle);
        float dist = World.Instance.HalfSize * 2f;
        ParentBird.FlightTarget = new Vector3(
            ParentBird.Position.X + dir.X * dist, 14f, ParentBird.Position.Z + dir.Y * dist);
    }

    public void UpdateLeaving(float dt)
    {
        FlyToward(ParentBird.FlightTarget, dt);

        if (ParentBird.Position.Y >= 12f)
        {
            World.Instance.RemoveEntity(ParentBird);
        }
    }

    // Moves straight toward a 3D point. Returns true on arrival.
    private bool FlyToward(Vector3 target, float dt)
    {
        Bird bird = ParentBird;
        Vector3 toTarget = target - bird.Position;
        float dist = toTarget.Length();
        float step = FlySpeed * dt;

        if (dist <= step)
        {
            bird.Position = target;
            return true;
        }

        Vector3 dir = toTarget / dist;
        bird.Position += dir * step;
        TurnToward(new Vector2(dir.X, dir.Z), dt);
        return false;
    }

    // ---------------- HELPERS ----------------

    public Food? FindNearestFood(float range)
    {
        Food? best = null;
        float bestDist = range;

        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (!food.IsLanded || food.IsFullyEaten) continue;

            float dist = Vector2.Distance(Flat(food.Position), Flat(ParentBird.Position));
            if (dist < bestDist)
            {
                best = food;
                bestDist = dist;
            }
        }
        return best;
    }

    public bool IsCloseTo(Vector3 point, float distance)
    {
        return Vector2.Distance(Flat(point), Flat(ParentBird.Position)) <= distance;
    }

    private void TurnToward(Vector2 desired, float dt)
    {
        if (desired.LengthSquared() < 0.0001f) return;

        float targetAngle = MathF.Atan2(desired.X, desired.Y);
        float diff = targetAngle - ParentBird.Heading;
        diff = MathF.Atan2(MathF.Sin(diff), MathF.Cos(diff));   // wrap to -PI..PI

        float maxTurn = TurnRate * dt;
        ParentBird.Heading += Math.Clamp(diff, -maxTurn, maxTurn);
    }

    private Vector2 DirectionTo(Vector3 point)
    {
        Vector2 d = Flat(point) - Flat(ParentBird.Position);
        return d.LengthSquared() > 0.0001f ? Vector2.Normalize(d) : Vector2.Zero;
    }

    private static Vector2 Flat(Vector3 v) => new Vector2(v.X, v.Z);
    private static Vector2 HeadingVector(float angle) => new Vector2(MathF.Sin(angle), MathF.Cos(angle));
    private static float RandomRange(float min, float max) => min + (float)ran.NextDouble() * (max - min);
}
