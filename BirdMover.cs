using System.Numerics;

// Handles HOW a bird moves: steering, hopping, flocking and flying.
// The Bird decides WHAT it wants to do; the mover carries it out.
public class BirdMover
{
    public Bird ParentBird { get; private set; }

    // WANDERING
    private float wanderAngle;
    private float pauseTimer = 0f;       // > 0 means standing still
    private float nextPauseIn;
    private BirdPose pausePose;          // what the bird does while it stands still
    private Vector3? landingSpot;        // where it's fluttering down to after eating up on a feeder

    // TUNING
    private const float TurnRate = 4f;            // radians per second
    private const float FlySpeed = 3.2f;
    private const float NeighborRadius = 3f;      // how far a bird looks for its flock
    private const float SeparationRadius = 0.7f;  // personal space
    // HopPhase speeds: how fast the bird's rhythm runs (BirbAnimator draws from it)
    private const float PeckRate = 6f;
    private const float HopRate = 12f;            // times walking speed
    private const float WalkRate = 8f;            // times walking speed; one step per half turn of the phase

    // Each hop is one half turn of HopPhase: in the air for this share of it, then resting on the ground.
    public const float HopAirShare = 0.6f;

    public BirdMover(Bird bird)
    {
        ParentBird = bird;
        wanderAngle = bird.Heading;
        nextPauseIn = Rng.Range(3f, 8f);
    }

    // ---------------- WALKING ----------------

    public void UpdateWalking(float dt)
    {
        Bird bird = ParentBird;

        // A bird still up on a feeder (done eating, or the feeder was moved) flutters down first,
        // backing away from the feeder it was facing.
        if (bird.IsFlying)
        {
            Vector2 back = -HeadingVector(bird.Heading) * 0.5f;
            landingSpot ??= new Vector3(bird.Position.X + back.X, bird.GroundY, bird.Position.Z + back.Y);
            if (FlyToward(landingSpot.Value, dt)) landingSpot = null;
            return;
        }
        landingSpot = null;

        if (bird.State == BirdState.Wandering && UpdatePausing(dt))
        {
            return;
        }

        Vector2 desired;
        if (bird.State == BirdState.SeekingFood && bird.TargetFood != null)
        {
            desired = DirectionTo(bird.TargetFood.Position) * 2f + SeparationForce() + GunkForce();
        }
        else
        {
            wanderAngle += Rng.Range(-1f, 1f) * dt * 2.5f;
            desired = HeadingVector(bird.Heading)
                    + HeadingVector(wanderAngle) * 0.6f
                    + FlockForce()
                    + GunkForce();
        }

        TurnToward(desired, dt);
        StepForward(bird.Speed, dt);
    }

    // Wandering birds stop now and then to peck at the ground, look around, or just stand.
    private bool UpdatePausing(float dt)
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= dt;
            ParentBird.Pose = pausePose;
            if (pausePose == BirdPose.Peck) AdvancePeck(dt);
            PutOnGround();
            return true;
        }

        nextPauseIn -= dt;
        if (nextPauseIn <= 0f)
        {
            pauseTimer = Rng.Range(0.8f, 2.5f);
            nextPauseIn = Rng.Range(3f, 9f);
            pausePose = PickPausePose();
        }
        return false;
    }

    private static BirdPose PickPausePose()
    {
        float roll = Rng.Range(0f, 1f);
        if (roll < 0.5f) return BirdPose.Peck;
        if (roll < 0.8f) return BirdPose.LookAround;
        return BirdPose.Idle;
    }

    // perched: eating up on a feeder, so it stays at the height it's at instead of standing on the dirt.
    public void PeckAt(Vector3 foodPos, float dt, bool perched)
    {
        TurnToward(DirectionTo(foodPos), dt);
        AdvancePeck(dt);
        ParentBird.Pose = BirdPose.Peck;
        if (!perched) PutOnGround();
    }

    // Moves the peck rhythm along, with a tap sound each time the beak reaches the ground.
    // BirbAnimator draws the head lowest when sin(HopPhase) is at its peak or trough,
    // which is once every half turn (PI), starting a quarter turn in.
    private void AdvancePeck(float dt)
    {
        float before = ParentBird.HopPhase;
        ParentBird.HopPhase += dt * PeckRate;
        if (BeakTaps(before) != BeakTaps(ParentBird.HopPhase)) Sounds.PlayPeck(ParentBird.Position);
    }

    private static int BeakTaps(float phase) => (int)MathF.Floor((phase - MathF.PI / 2f) / MathF.PI);

    // The bird stays on the ground; the hop you see is added when it's drawn (BirbAnimator).
    private void StepForward(float speed, float dt)
    {
        Bird bird = ParentBird;
        Vector2 dir = HeadingVector(bird.Heading);
        float travel;

        if (bird.Gait == Gait.Walk)
        {
            bird.HopPhase += dt * speed * WalkRate;
            bird.Pose = BirdPose.Walk;
            travel = speed;
        }
        else
        {
            bird.HopPhase += dt * speed * HopRate;
            bird.Pose = BirdPose.Hop;
            // Only moves while in the air, faster to make up for the rests, so the average speed is the same.
            travel = HopProgress(bird.HopPhase) < HopAirShare ? speed / HopAirShare : 0f;
        }

        bird.Position = new Vector3(
            bird.Position.X + dir.X * travel * dt,
            bird.GroundY,
            bird.Position.Z + dir.Y * travel * dt);
    }

    // How far through the current hop the bird is, 0 to 1.
    public static float HopProgress(float phase)
    {
        float hops = phase / MathF.PI;
        return hops - MathF.Floor(hops);
    }

    private void PutOnGround()
    {
        Vector3 p = ParentBird.Position;
        ParentBird.Position = new Vector3(p.X, ParentBird.GroundY, p.Z);
    }

    // ---------------- FLOCKING ----------------

    // Same-type birds nearby pull together (cohesion) and face the same way (alignment).
    // All birds push apart when too close (separation). One loop does all three.
    private Vector2 FlockForce()
    {
        Bird bird = ParentBird;
        Vector2 center = Vector2.Zero;
        Vector2 alignment = Vector2.Zero;
        Vector2 force = Vector2.Zero;
        int neighbors = 0;

        foreach (Bird other in World.Instance.GetAllEntities<Bird>())
        {
            if (other == bird || other.IsFlying) continue;

            Vector2 away = Flat(bird.Position) - Flat(other.Position);
            float dist = away.Length();
            force += SeparationPush(away, dist);

            if (other.Type == bird.Type && dist < NeighborRadius)
            {
                center += Flat(other.Position);
                alignment += HeadingVector(other.Heading);
                neighbors++;
            }
        }

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
            push += SeparationPush(away, away.Length());
        }
        return push;
    }

    // The push away from one other bird: zero outside personal space, stronger the closer it is.
    private static Vector2 SeparationPush(Vector2 away, float dist)
    {
        if (dist <= 0.001f || dist >= SeparationRadius) return Vector2.Zero;
        return away / (dist * dist) * 0.15f;
    }

    // A push away from any gunk the bird is close to, stronger the nearer it gets.
    private Vector2 GunkForce()
    {
        Vector2 push = Vector2.Zero;

        foreach (Gunk gunk in World.Instance.GetAllEntities<Gunk>())
        {
            Vector2 away = Flat(ParentBird.Position) - Flat(gunk.Position);
            float dist = away.Length();
            float reach = gunk.Radius + Gunk.AvoidDistance;
            if (dist <= 0.001f || dist >= reach) continue;

            push += away / dist * (1f - dist / reach) * 3f;
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
                PutOnGround();
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
        float angle = ParentBird.Heading + Rng.Range(-0.8f, 0.8f);
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
            ParentBird.Remove();
        }
    }

    // Moves straight toward a 3D point. Returns true on arrival.
    public bool FlyToward(Vector3 target, float dt)
    {
        Bird bird = ParentBird;
        bird.Pose = BirdPose.Fly;
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

    // The best food within range. Tastier food counts as closer, so a bird will walk past
    // seed it barely likes to reach seed it loves (distance is divided by taste).
    // A feeder the bird likes counts as closer still (divided by the feeder's appeal too).
    public Food? FindNearestFood(float range)
    {
        Food? best = null;
        float bestScore = float.MaxValue;
        BirdType type = ParentBird.Type;

        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (!food.IsLanded || food.IsFullyEaten) continue;

            SeedType? favorite = food.FavoriteFor(type);
            if (favorite == null) continue;

            float dist = Vector2.Distance(Flat(food.Position), Flat(ParentBird.Position));
            if (dist > range) continue;

            float score = dist / (Seeds.Get(favorite.Value).Tastes[type] * food.AppealFor(type));
            if (score < bestScore)
            {
                best = food;
                bestScore = score;
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
}
