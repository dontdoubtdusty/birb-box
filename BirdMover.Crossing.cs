using System.Numerics;

// The creek-crossing part of BirdMover: a walking bird that comes to the stream heading into it can cross to the far bank
// in a low flutter (a short hop over narrow water, a longer flight over wide). Same class as BirdMover.cs (a partial class).
// Birds never cross the pool itself, only the stream; the walking code keeps them out of the water otherwise.
public partial class BirdMover
{
    private bool crossing;                // fluttering over the stream right now
    private Vector3 crossFrom, crossTo;   // where it took off and where it lands
    private float crossSpeed;             // how fast this crossing flies
    private float crossCooldown;          // seconds until it next thinks about crossing

    // TUNING (crossing)
    private const float CrossReach = 0.9f;       // how near the waterline a bird must be to think about crossing
    private const float CrossLook = 7f;          // how far ahead along its heading it looks for the far bank
    private const float CrossStep = 0.1f;        // spacing of the look-ahead samples
    private const float CrossChance = 0.5f;      // chance a wanderer heading into the stream takes the chance (a bird after food always does)
    private const float HopSpan = 1.5f;          // water this narrow or less is a hop (slow, low); wider is a flight
    private const float HopSpeed = 2f;
    private const float PoolSafe = 0.3f;         // a crossing line may not pass this near the pool proper

    // Called each step while walking. If the bird is at the stream, facing into it (or after food on the other side), and the far
    // bank is within CrossLook with clear ground to land on, it takes off. Returns true when it does.
    private bool TryStartCrossing(float dt)
    {
        Bird bird = ParentBird;
        Pool? pool = Pool.Current;
        if (pool == null || pool.Stream.Count < 2) return false;
        if (bird.State != BirdState.Wandering && bird.State != BirdState.SeekingFood) return false;

        crossCooldown -= dt;
        if (crossCooldown > 0f) return false;
        Vector2 here = Flat(bird.Position);
        if (pool.DistanceAt(here) > CrossReach) return false;    // not at the water yet
        crossCooldown = Rng.Range(1.5f, 3f);

        bool afterFood = bird.State == BirdState.SeekingFood && bird.TargetFood != null;
        if (!afterFood && !Rng.Chance(CrossChance)) return false;

        Vector2 intent = afterFood ? DirectionTo(bird.TargetFood!.Position) : HeadingVector(bird.Heading);
        if (intent.LengthSquared() < 1e-4f) return false;
        intent = Vector2.Normalize(intent);
        if (Vector2.Dot(intent, pool.OutwardAt(here)) > -0.5f) return false;    // not heading into the water

        // Walk the line ahead: where the water starts, and where the far bank is clear again
        float enter = -1f, exit = -1f;
        for (float t = CrossStep; t <= CrossLook; t += CrossStep)
        {
            Vector2 p = here + intent * t;
            float d = pool.DistanceAt(p);
            if (d < 0f)
            {
                if (pool.LobeDistance(p) < PoolSafe) return false;    // that line runs into the pool
                if (enter < 0f) enter = t;
            }
            else if (enter >= 0f && d >= PoolRoom + 0.1f)
            {
                exit = t;
                break;
            }
        }
        if (exit < 0f) return false;

        Vector2 land = here + intent * exit;
        Vector3 land3 = Obstacles.PushOut(new Vector3(land.X, 0f, land.Y), RockRoom, PoolRoom);
        if (Pool.Distance(land3) < PoolRoom * 0.9f || !World.Instance.IsInside(land3, -2f)) return false;

        crossing = true;
        crossFrom = bird.Position;
        crossTo = GroundedAt(land3);
        crossSpeed = exit - enter <= HopSpan ? HopSpeed : FlySpeed;
        pauseTimer = 0f;
        return true;
    }

    // One step of the crossing. Lands, and carries on in the direction it was going, at the far bank.
    private void UpdateCrossing(float dt)
    {
        if (!FlyArc(crossFrom, crossTo, dt, crossSpeed)) return;
        crossing = false;
        wanderAngle = ParentBird.Heading;
    }
}
