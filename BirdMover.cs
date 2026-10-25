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
    private Vector3? landingSpot;        // where it's fluttering down to after eating up on a feeder or sitting on a rock

    // PERCHING on a rock
    private Rock? perchRock;             // the rock it's going up to or sitting on, or null
    private Vector3 perchSpot;           // where on top of it
    private bool perchSeated;            // false while still fluttering up
    private float perchTimer;            // seconds left to sit
    private float perchLookTimer;        // seconds until it looks somewhere else
    public bool IsPerched => perchRock != null;
    public Rock? PerchRock => perchRock;

    // TUNING
    private const float TurnRate = 4f;            // radians per second
    private const float FlySpeed = 3.2f;
    private const float NeighborRadius = 3f;      // how far a bird looks for its flock
    private const float SeparationRadius = 0.7f;  // personal space
    private const float RockRoom = 0.12f;         // how close a bird's middle gets to a rock's edge
    private const float RockSteerReach = 0.9f;    // how far past a rock's edge birds start steering around it
    private const float PerchChance = 0.3f;       // chance a pause near a rock becomes a sit on top of it
    private const float PerchReach = 2.5f;        // how near a rock's edge a bird has to be to hop up
    private const float HomeRadius = 7f;          // a wandering bird roams this far from home before turning back
    private const float EdgeMargin = 5f;          // birds turn back this far from the world's edge
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
        if (perchRock != null)
        {
            UpdatePerching(dt);
            return;
        }

        if (bird.IsFlying)
        {
            Vector2 back = -HeadingVector(bird.Heading) * 0.5f;
            landingSpot ??= Rock.PushOut(new Vector3(bird.Position.X + back.X, bird.GroundY, bird.Position.Z + back.Y), RockRoom);
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
            desired = DirectionTo(bird.TargetFood.Position) * 2f + SeparationForce() + GunkForce() + RockForce();
        }
        else
        {
            wanderAngle += Rng.Range(-1f, 1f) * dt * 2.5f;
            desired = HeadingVector(bird.Heading)
                    + HeadingVector(wanderAngle) * 0.6f
                    + FlockForce()
                    + GunkForce()
                    + RockForce()
                    + HomeForce();
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
        if (nextPauseIn <= 0f && TryStartPerch())
        {
            nextPauseIn = Rng.Range(3f, 9f);
            return true;
        }
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

        bird.Position = Rock.PushOut(new Vector3(
            bird.Position.X + dir.X * travel * dt,
            bird.GroundY,
            bird.Position.Z + dir.Y * travel * dt), RockRoom);
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
        ParentBird.Position = Rock.PushOut(new Vector3(p.X, ParentBird.GroundY, p.Z), RockRoom);
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

    // A push away from rocks the bird is close to, plus a sideways push so it walks around one
    // instead of stopping in front of it. The sideways push goes whichever way the bird is already facing.
    private Vector2 RockForce()
    {
        Vector2 push = Vector2.Zero;
        Vector2 heading = HeadingVector(ParentBird.Heading);

        foreach (Rock rock in World.Instance.GetAllEntities<Rock>())
        {
            Vector2 away = Flat(ParentBird.Position) - rock.Flat;
            float dist = away.Length();
            float reach = rock.Radius + RockSteerReach;
            if (dist <= 0.001f || dist >= reach) continue;

            Vector2 outward = away / dist;
            Vector2 around = new Vector2(-outward.Y, outward.X);
            if (Vector2.Dot(around, heading) < 0f) around = -around;
            push += (outward + around) * (1f - dist / reach) * 3f;
        }
        return push;
    }

    // A pull back toward home once the bird strays past HomeRadius, growing the further it goes,
    // plus a strong push inward near the world's edge, so wandering birds stay where they can be seen again.
    private Vector2 HomeForce()
    {
        Bird bird = ParentBird;
        Vector2 force = Vector2.Zero;

        if (bird.HasHome)
        {
            Vector2 toHome = Flat(bird.Home) - Flat(bird.Position);
            float dist = toHome.Length();
            if (dist > HomeRadius) force += toHome / dist * MathF.Min((dist - HomeRadius) / HomeRadius, 1f) * 1.5f;
        }

        float limit = World.Instance.HalfSize - EdgeMargin;
        Vector2 pos = Flat(bird.Position);
        if (MathF.Abs(pos.X) > limit) force.X -= MathF.Sign(pos.X) * (MathF.Abs(pos.X) - limit) / EdgeMargin * 3f;
        if (MathF.Abs(pos.Y) > limit) force.Y -= MathF.Sign(pos.Y) * (MathF.Abs(pos.Y) - limit) / EdgeMargin * 3f;
        return force;
    }

    // ---------------- PERCHING ----------------

    // Sometimes, instead of pausing on the ground, a wandering bird near a rock hops up onto it,
    // if there's room for one more. Returns true when it sets off.
    private bool TryStartPerch()
    {
        Bird bird = ParentBird;
        if (bird.IsHungry() || !Rng.Chance(PerchChance)) return false;

        Rock? rock = Rock.At(bird.Position, PerchReach);
        if (rock == null || BirdsOn(rock) >= rock.PerchRoom) return false;

        // A spot near the top, not right on the edge
        float angle = Rng.Range(0f, MathF.PI * 2f);
        float r = rock.Radius * 0.35f * MathF.Sqrt(Rng.Range(0f, 1f));
        Vector3 spot = rock.Position + new Vector3(MathF.Cos(angle) * r, 0f, MathF.Sin(angle) * r);
        spot.Y = rock.HeightAt(spot) + bird.GroundY;

        perchRock = rock;
        perchSpot = spot;
        perchSeated = false;
        perchTimer = Rng.Range(5f, 14f);
        perchLookTimer = 0f;
        return true;
    }

    private static int BirdsOn(Rock rock)
    {
        int count = 0;
        foreach (Bird other in World.Instance.GetAllEntities<Bird>())
        {
            if (other.Mover.PerchRock == rock) count++;
        }
        return count;
    }

    // Flutters up, sits looking about for a while, then hops down. It gets down early when it turns hungry
    // or something else needs it (seeking food, leaving).
    private void UpdatePerching(float dt)
    {
        Bird bird = ParentBird;
        bool canStay = bird.State == BirdState.Wandering && !bird.IsHungry();

        if (!perchSeated)
        {
            if (!canStay) { GetDownFromRock(); return; }
            if (FlyToward(perchSpot, dt))
            {
                perchSeated = true;
                bird.Pose = BirdPose.Idle;
            }
            return;
        }

        perchTimer -= dt;
        if (perchTimer <= 0f || !canStay)
        {
            GetDownFromRock();
            return;
        }

        // Now and then it looks another way, or turns a little on the spot
        perchLookTimer -= dt;
        if (perchLookTimer <= 0f)
        {
            perchLookTimer = Rng.Range(1.2f, 3f);
            pausePose = Rng.Chance(0.6) ? BirdPose.LookAround : BirdPose.Idle;
            wanderAngle = bird.Heading + Rng.Range(-1.5f, 1.5f);
        }
        bird.Pose = pausePose;
        TurnToward(HeadingVector(wanderAngle), dt * 0.4f);
        bird.Position = perchSpot;
    }

    // Picks a spot on the ground just past the rock's edge, the way the bird is facing, and flutters down to it
    // (the flying part of UpdateWalking carries it there).
    private void GetDownFromRock()
    {
        Bird bird = ParentBird;
        Rock rock = perchRock!;
        perchRock = null;

        Vector2 dir = HeadingVector(bird.Heading);
        Vector2 flat = rock.Flat + dir * (rock.Radius + 0.35f);
        landingSpot = Rock.PushOut(new Vector3(flat.X, bird.GroundY, flat.Y), RockRoom);
        if (!bird.IsFlying) landingSpot = null;    // never got off the ground
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
                if (!bird.HasHome) bird.SetHome(bird.Position);
            }
        }
        else
        {
            TurnToward(DirectionTo(bird.FlightTarget), dt);
            StepForward(bird.Speed, dt);

            if (World.Instance.IsInside(bird.Position, -3f))
            {
                bird.State = BirdState.Wandering;
                if (!bird.HasHome) bird.SetHome(bird.Position);
            }
        }
    }

    public void PickLeavingTarget()
    {
        // Fly off in roughly the direction the bird is already facing, up and out of the world.
        perchRock = null;
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
            if (food.Feeder != null && !food.Feeder.HasRoomFor(ParentBird)) continue;    // every perch taken

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
