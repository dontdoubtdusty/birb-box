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
    private Vector3 flightFrom, flightLanding;
    public bool IsBathTrip => water != WaterVisit.None && bathTrip && !shaking;
    public bool OnWaterTrip => water != WaterVisit.None;

    // What it's doing at the water, for the bird card (null when it isn't on a visit).
    public string? WaterActivity => water switch
    {
        WaterVisit.Going => "Off to the water",
        WaterVisit.Sipping => "Drinking",
        WaterVisit.Bathing => "Bathing",
        WaterVisit.Drying => "Drying off",
        _ => null,
    };

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
    private const float WaterReach = 7f;          // how far from the shore a bird will walk down to the water
    private const float WadeDepth = 0.3f;         // how far into the shallows a bather goes (under Pool.Shallows)
    private const float SinkShare = 0.55f;        // how much of its standing height goes under the surface at WadeDepth
    private const float WaterGiveUp = 15f;        // seconds to reach the water before it gives up
    private const float ShakeTime = 1.3f;         // shaking off after a bath
    private const float PuddleReach = 5f;         // how far from a rain puddle a bird will walk over to sip from it
    private const float PuddleMinFill = 0.4f;     // a puddle must be at least this full to drink from
    private const float FlyToWaterReach = 10f;    // a thirsty bird farther than this from the water flies there
    private const float SitMin = 10f, SitMax = 40f;    // seconds a long sit lasts (how often: SimTuning.SitChance)
    private const float WaterFlightArc = 2.5f;    // how high that flight goes at its middle (lower for short ones)
    private const float SipDrink = 0.2f;          // how much thirst one sip quenches (a bath quenches it all)
    private const int MaxSips = 6;                // a thirsty bird sips until quenched, up to this many
    public const float SipRate = 4.5f;            // HopPhase speed while sipping: one full turn is one sip
    public const float BathRate = 14f;            // HopPhase speed while bathing (the wing flutter)
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
        perchTimer = Rng.Range(5f, 14f);
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
            flightLanding = new Vector3(land.X, bird.GroundY, land.Z);
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
    public Food? FindNearestFood(float range, SeedType favoriteSeed, float favoritePull)
    {
        bool shy = ParentBird.Nature.Company != Company.Anyone;
        Food? best = null;
        float bestScore = float.MaxValue;
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
            if (food.HasFresh(favoriteSeed)) score /= favoritePull;
            if (shy && score < bestScore && IsBusy(food)) score *= BusyFoodPenalty;
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
