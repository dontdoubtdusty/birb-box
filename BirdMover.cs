using System.Numerics;

// Handles HOW a bird moves: steering, hopping, flocking and flying.
// The Bird decides WHAT it wants to do; the mover carries it out.
public partial class BirdMover
{
    public Bird ParentBird { get; private set; }

    // WANDERING
    private float wanderAngle;
    private float pauseTimer = 0f;       // > 0 means standing still
    private float nextPauseIn;
    private BirdPose pausePose;          // what the bird does while it stands still
    private bool sitting;                // this pause is a long sit (looks about, turns a little)
    private float sitLookTimer;          // seconds until a sitting bird looks somewhere else
    private Vector3? landingSpot;        // where it's fluttering down to after eating up on a feeder or sitting on a rock

    // PERCHING on a rock
    private Rock? perchRock;             // the rock it's going up to or sitting on, or null
    private Vector3 perchSpot;           // where on top of it
    private bool perchSeated;            // false while still fluttering up
    private float perchTimer;            // seconds left to sit
    private float perchLookTimer;        // seconds until it looks somewhere else
    public bool IsPerched => perchRock != null;
    public Rock? PerchRock => perchRock;

    // The water state (visits to the pool and to rain puddles) and its code are in BirdMover.Water.cs.

    // TUNING
    private const float TurnRate = 4f;            // radians per second
    private const float FlySpeed = 3.2f;
    private const float NeighborRadius = 3f;      // how far a bird looks for its flock
    private const float SeparationRadius = 0.7f;  // personal space of an average bird (times Nature.PersonalSpace)
    private const float RockRoom = 0.12f;         // how close a bird's middle gets to a rock's edge
    private const float RockSteerReach = 0.9f;    // how far past a rock's edge birds start steering around it
    private const float PerchReach = 2.5f;        // how near a rock's edge a bird has to be to hop up
    private const float RoostReach = 8f;          // at bedtime a bird flies this far to a rock to sleep on
    private const float BusyFoodPenalty = 4f;     // food with a bird it minds at it counts as this many times farther (Nature.Company)
    // Perch chance and home radius are tunable: see SimTuning.
    private const float EdgeMargin = 5f;          // birds turn back this far from the world's edge
    // THE POOL (drink and bath chances, max bathers: SimTuning)
    private const float PoolRoom = 0.15f;         // how close a bird's middle gets to the waterline, unless it's wading in
    private const float PoolSteerReach = 1f;      // how far from the water birds start steering around it
    private const float WadeDepth = 0.3f;         // how far into the shallows a bather goes (under Pool.Shallows)
    private const float SinkShare = 0.55f;        // how much of its standing height goes under the surface at WadeDepth
    private const float SitMin = 10f, SitMax = 40f;    // seconds a long sit lasts (how often: SimTuning.SitChance)
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

        // On its way to far-off water: flying, then it walks the last bit like any visit
        if (waterFlight)
        {
            if (FlyArc(flightFrom, flightLanding, dt)) waterFlight = false;
            return;
        }

        if (bird.IsFlying)
        {
            Vector2 back = -HeadingVector(bird.Heading) * 0.5f;
            landingSpot ??= Pool.PushOut(Rock.PushOut(new Vector3(bird.Position.X + back.X, bird.GroundY, bird.Position.Z + back.Y), RockRoom), PoolRoom);
            if (FlyToward(landingSpot.Value, dt)) landingSpot = null;
            return;
        }
        landingSpot = null;

        // Asleep on the ground (roosting, or a nap): it sits still where it is until it wakes
        if (bird.Asleep)
        {
            bird.Pose = BirdPose.Roost;
            PutOnGround();
            return;
        }

        if (water != WaterVisit.None && UpdateWaterVisit(dt))
        {
            return;
        }

        if (bird.State == BirdState.Wandering && UpdatePausing(dt))
        {
            return;
        }

        Vector2 desired;
        if (bird.State == BirdState.SeekingFood && bird.TargetFood != null)
        {
            desired = DirectionTo(bird.TargetFood.Position) * 2f + SeparationForce() + RockForce() + PoolForce();
        }
        else
        {
            wanderAngle += Rng.Range(-1f, 1f) * dt * 2.5f;
            desired = HeadingVector(bird.Heading)
                    + HeadingVector(wanderAngle) * 0.6f
                    + FlockForce()
                    + RockForce()
                    + PoolForce()
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
            if (sitting && ParentBird.IsHungry()) pauseTimer = 0f;    // hunger ends a sit (thirst and naps end it on their own)
            pauseTimer -= dt;
            if (sitting) LookAboutWhileSitting(dt);
            ParentBird.Pose = pausePose;
            if (pausePose == BirdPose.Peck) AdvancePeck(dt);
            PutOnGround();
            return true;
        }

        nextPauseIn -= dt;
        if (nextPauseIn <= 0f && (TryStartPerch() || TryStartWaterVisit()))
        {
            nextPauseIn = Rng.Range(3f, 9f);
            return true;
        }
        if (nextPauseIn <= 0f)
        {
            pauseTimer = Rng.Range(0.8f, 2.5f);
            nextPauseIn = Rng.Range(3f, 9f);
            pausePose = PickPausePose();
            sitting = pausePose != BirdPose.Peck && Rng.Chance(SimTuning.Now.SitChance);
            if (sitting)
            {
                pauseTimer = Rng.Range(SitMin, SitMax);
                sitLookTimer = 0f;
            }
        }
        return false;
    }

    // A long sit: every few seconds it looks another way or just stands, and turns a little on the spot
    // (the same as sitting on a rock). The animator's idle acts (preen, scratch...) play in between.
    private void LookAboutWhileSitting(float dt)
    {
        sitLookTimer -= dt;
        if (sitLookTimer <= 0f)
        {
            sitLookTimer = Rng.Range(1.2f, 3f);
            pausePose = Rng.Chance(0.6) ? BirdPose.LookAround : BirdPose.Idle;
            wanderAngle = ParentBird.Heading + Rng.Range(-1.5f, 1.5f);
        }
        TurnToward(HeadingVector(wanderAngle), dt * 0.4f);
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

        bird.Position = OnGround(new Vector3(bird.Position.X + dir.X * travel * dt, 0f, bird.Position.Z + dir.Y * travel * dt));
        LeaveWadeRipple(dt);
    }

    // How far through the current hop the bird is, 0 to 1.
    public static float HopProgress(float phase)
    {
        float hops = phase / MathF.PI;
        return hops - MathF.Floor(hops);
    }

    private void PutOnGround()
    {
        ParentBird.Position = OnGround(ParentBird.Position);
    }

    // A spot on the ground kept off the rocks and out of the water, at the height its feet stand.
    // A bather may go into the shallows (up to WadeDepth). Any other bird that's in the water (a bath cut short)
    // can't go any deeper than it is, and PoolForce walks it out; a roosting one steps straight out.
    // In the water it stands lower, so its legs are under the surface.
    private Vector3 OnGround(Vector3 spot)
    {
        Bird bird = ParentBird;
        spot = Rock.PushOut(spot, RockRoom);

        float now = Pool.Distance(bird.Position);
        bool wading = bathTrip && (water == WaterVisit.Going || water == WaterVisit.Bathing);
        float margin = wading ? -WadeDepth : bird.Asleep ? PoolRoom : MathF.Min(PoolRoom, now - 0.001f);
        bool sipping = !bathTrip && (water == WaterVisit.Going || water == WaterVisit.Sipping);
        if (sipping) margin = MathF.Min(margin, waterStand);
        spot = Pool.PushOut(spot, margin);

        float depth = -Pool.Distance(spot);
        float sink = depth > 0f ? Math.Clamp(depth / WadeDepth, 0f, 1f) * SinkShare * bird.GroundY : 0f;
        return new Vector3(spot.X, bird.GroundY - sink, spot.Z);
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
            force += SeparationPush(away, dist, bird.Nature.PersonalSpace);

            if (other.Species == bird.Species && dist < NeighborRadius)
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
            push += SeparationPush(away, away.Length(), bird.Nature.PersonalSpace);
        }
        return push;
    }

    // The push away from one other bird: zero outside personal space, stronger the closer it is.
    // space: how much room this bird wants (1 average). A shy bird's space is wider and pushes harder at the same distance.
    private static Vector2 SeparationPush(Vector2 away, float dist, float space)
    {
        if (dist <= 0.001f || dist >= SeparationRadius * space) return Vector2.Zero;
        return away / (dist * dist) * 0.15f * space;
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

    // The same for the pool: a push out and along the shore, so birds walk around the water instead of into it.
    // Strongest for a bird already in it (a bath cut short), so it wades out.
    private Vector2 PoolForce()
    {
        Pool? pool = Pool.Current;
        if (pool == null) return Vector2.Zero;

        Vector2 here = Flat(ParentBird.Position);
        float d = pool.DistanceAt(here);
        if (d >= PoolSteerReach) return Vector2.Zero;

        Vector2 outward = pool.OutwardAt(here);
        if (d < 0f) return outward * 3f;
        Vector2 around = new Vector2(-outward.Y, outward.X);
        if (Vector2.Dot(around, HeadingVector(ParentBird.Heading)) < 0f) around = -around;
        return (outward + around) * (1f - d / PoolSteerReach) * 3f;
    }

    // A pull back toward home once the bird strays past SimTuning.HomeRadius, growing the further it goes,
    // plus a strong push inward near the world's edge, so wandering birds stay where they can be seen again.
    private Vector2 HomeForce()
    {
        Bird bird = ParentBird;
        Vector2 force = Vector2.Zero;

        if (bird.HasHome)
        {
            Vector2 toHome = Flat(bird.Home) - Flat(bird.Position);
            float dist = toHome.Length();
            float homeRadius = SimTuning.Now.HomeRadius * bird.Nature.HomeRadius;
            if (dist > homeRadius) force += toHome / dist * MathF.Min((dist - homeRadius) / homeRadius, 1f) * 1.5f;
        }

        float limit = World.Instance.HalfSize - EdgeMargin;
        Vector2 pos = Flat(bird.Position);
        if (MathF.Abs(pos.X) > limit) force.X -= MathF.Sign(pos.X) * (MathF.Abs(pos.X) - limit) / EdgeMargin * 3f;
        if (MathF.Abs(pos.Y) > limit) force.Y -= MathF.Sign(pos.Y) * (MathF.Abs(pos.Y) - limit) / EdgeMargin * 3f;
        return force;
    }

    // ---------------- PERCHING ----------------

    // Sometimes, instead of pausing on the ground, a wandering bird near a rock hops up onto it,
    // if there's room for one more and it's happy with who's up there already (Nature.Company). Returns true when it sets off.
    private bool TryStartPerch()
    {
        Bird bird = ParentBird;
        if (bird.IsHungry() || !Rng.Chance(SimTuning.Now.PerchChance * bird.Nature.PerchChance)) return false;

        Rock? rock = Rock.At(bird.Position, PerchReach);
        if (rock == null || BirdsOn(rock) >= rock.PerchRoom || !WillShare(rock)) return false;

        SetPerch(rock, SpotOnTop(rock), seated: false);
        return true;
    }

    private void SetPerch(Rock rock, Vector3 spot, bool seated)
    {
        perchRock = rock;
        perchSpot = spot;
        perchSeated = seated;
        perchTimer = Rng.Range(5f, 14f) * ParentBird.Nature.PerchStay;
        perchLookTimer = 0f;
    }

    // A spot near the top of a rock, not right on the edge, at this bird's standing height.
    private Vector3 SpotOnTop(Rock rock)
    {
        float angle = Rng.Range(0f, MathF.PI * 2f);
        float r = rock.Radius * 0.35f * MathF.Sqrt(Rng.Range(0f, 1f));
        Vector3 spot = rock.Position + new Vector3(MathF.Cos(angle) * r, 0f, MathF.Sin(angle) * r);
        spot.Y = rock.HeightAt(spot) + ParentBird.GroundY;
        return spot;
    }

    // ---------------- ROOSTING (see Bird.UpdateNight) ----------------

    // Bedtime, or a nap (Bird.StartNap): a bird already up on a rock sleeps there. Otherwise it flies to the nearest rock within RoostReach
    // that has room and company it's happy with, or, with none about, sleeps on the ground where it is.
    public void StartRoost()
    {
        pauseTimer = 0f;
        EndWaterVisit();
        if (perchRock != null) return;

        Rock? best = null;
        float bestDistance = float.MaxValue;
        foreach (Rock rock in World.Instance.GetAllEntities<Rock>())
        {
            float distance = Vector2.Distance(new Vector2(ParentBird.Position.X, ParentBird.Position.Z), rock.Flat) - rock.Radius;
            if (distance > RoostReach || distance >= bestDistance) continue;
            if (BirdsOn(rock) >= rock.PerchRoom || !WillShare(rock)) continue;
            best = rock;
            bestDistance = distance;
        }
        if (best != null) SetPerch(best, SpotOnTop(best), seated: false);
    }

    // Loading a save: a roosting bird that was up on a rock sits on it again, already settled.
    public void ResumeRoost()
    {
        Bird bird = ParentBird;
        if (!bird.IsFlying) return;
        Rock? rock = Rock.At(bird.Position);
        if (rock != null) SetPerch(rock, bird.Position, seated: true);
    }

    // Morning, or the end of a nap: a bird up on a rock sits a few seconds more, then hops down (see UpdatePerching).
    public void EndRoost()
    {
        if (perchRock != null) perchTimer = Rng.Range(1f, 4f);
    }

    // Whether this bird will sit with the birds already on the rock.
    private bool WillShare(Rock rock)
    {
        if (ParentBird.Nature.Company == Company.Anyone) return true;

        foreach (Bird other in World.Instance.GetAllEntities<Bird>())
        {
            if (other != ParentBird && other.Mover.PerchRock == rock && Minds(other)) return false;
        }
        return true;
    }

    // True if another bird is eating at this food, or on its way to it, that this bird minds (see Company).
    // Seed in a feeder is one food, so this also covers the free perch beside a taken one.
    private bool IsBusy(Food food)
    {
        foreach (Bird other in World.Instance.GetAllEntities<Bird>())
        {
            if (other == ParentBird || other.TargetFood != food) continue;
            if ((other.State is BirdState.SeekingFood or BirdState.Eating) && Minds(other)) return true;
        }
        return false;
    }

    // Whether this bird would rather not be near the other one.
    private bool Minds(Bird other)
    {
        return ParentBird.Nature.Company switch
        {
            Company.Alone => true,
            Company.KinOnly => other.Species != ParentBird.Species,
            _ => false,
        };
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
    // A roosting bird stays put all night, tucked in, without looking about.
    private void UpdatePerching(float dt)
    {
        Bird bird = ParentBird;
        bool canStay = bird.State == BirdState.Wandering && (bird.Asleep || !bird.IsHungry());

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

        if (!bird.Asleep) perchTimer -= dt;
        if (perchTimer <= 0f || !canStay)
        {
            GetDownFromRock();
            return;
        }
        if (bird.Asleep)
        {
            bird.Pose = BirdPose.Roost;
            bird.Position = perchSpot;
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
        landingSpot = Pool.PushOut(Rock.PushOut(new Vector3(flat.X, bird.GroundY, flat.Y), RockRoom), PoolRoom);
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
        EndWaterVisit();
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
    // A feeder the bird likes counts as closer still (divided by the feeder's appeal too),
    // and so does food holding the bird's own favorite seed (divided by favoritePull).
    // A shy bird (Nature.Company) counts food with a bird it minds at it as farther, so it eats elsewhere when it can.
    // A Particular bird goes for the best pile that has its favorite seed over any other, unless it is starving.
    public Food? FindNearestFood(float range, SeedType favoriteSeed, float favoritePull)
    {
        bool shy = ParentBird.Nature.Company != Company.Anyone;
        Food? best = null;
        float bestScore = float.MaxValue;
        Food? bestWithFavorite = null;
        float bestFavoriteScore = float.MaxValue;
        Species type = ParentBird.Species;

        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (!food.IsLanded || food.IsFullyEaten) continue;
            if (food.Feeder != null && !food.Feeder.HasRoomFor(ParentBird)) continue;    // every perch taken

            SeedType? favorite = food.FavoriteFor(type);
            if (favorite == null) continue;

            float dist = Vector2.Distance(Flat(food.Position), Flat(ParentBird.Position));
            if (dist > range) continue;

            float score = dist / (type.TasteFor(favorite.Value) * food.AppealFor(type));
            bool hasFavorite = food.HasFresh(favoriteSeed);
            if (hasFavorite) score /= favoritePull;
            if (shy && score < bestScore && IsBusy(food)) score *= BusyFoodPenalty;
            if (score < bestScore)
            {
                best = food;
                bestScore = score;
            }
            if (hasFavorite && score < bestFavoriteScore)
            {
                bestWithFavorite = food;
                bestFavoriteScore = score;
            }
        }

        if (ParentBird.Nature.Particular && bestWithFavorite != null && !ParentBird.IsStarving()) return bestWithFavorite;
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
