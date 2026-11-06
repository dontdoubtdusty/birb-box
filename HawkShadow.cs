using System.Numerics;

// The Hawk's Shadow (a Big event): a hawk's soft shadow glides across the garden in a straight line through the focus.
// The only tension in the game, and nobody gets hurt. Each bird the shadow comes near reacts once, by its nerve:
//  - a Watchman sees it coming from farther off and calls the alarm, and every bird reacts from farther off after that;
//  - Faint-hearted birds scatter into the fog, Wary ones half the time, average ones sometimes; the rest freeze for a while;
//  - a Stout-hearted bird eating at a feeder holds its place; elsewhere it freezes only briefly;
//  - bathers always burst out of the water and scatter; nappers wake up first. Sleeping Night Owls sleep through it.
// Birds that scattered come back by air near their homes a minute or so after the shadow has passed, one by one.
// Each bird that reacted gets a line in its record. State only: the shadow is drawn from Active (HologramRenderer, hologram.fs).
public class HawkShadow : StoryEvent
{
    public override string Key => "hawk";
    public override string Name => "The Hawk's Shadow";
    public override StoryCategory Category => StoryCategory.Big;

    // TUNING
    private const float Speed = 5.5f;               // world units per second along its line
    private const float Overshoot = 6f;             // it starts and ends this far past the world's edge
    private const float FadeReach = 4f;             // the shadow fades in and out over this much distance at the edge
    private const float Reach = 8f;                 // how near the shadow comes before a bird reacts
    private const float WatchmanReach = 12f;        // a Watchman spots it from this far
    private const float AlarmReach = 1.5f;          // once the alarm is up, every bird reacts from this many times Reach
    private const float FreezeMin = 6f, FreezeMax = 14f;          // seconds a frozen bird keeps still
    private const float BriefFreezeMin = 2f, BriefFreezeMax = 4f; // a Stout-hearted bird's
    private const float WaryScatter = 0.5f;         // chance a Wary bird scatters (the rest freeze)
    private const float AverageScatter = 0.4f;      // chance an average bird scatters
    private const float ReturnDelayMin = 40f, ReturnDelayMax = 70f;    // after the shadow has gone, before the first bird comes back
    private const float ReturnGapMin = 2f, ReturnGapMax = 6f;          // between birds coming back
    private const float GiveUpAfter = 240f;         // seconds after the shadow has gone; any still away come back like ordinary regulars
    private const float MaxRain = 0.5f;             // hawks don't hunt in heavier rain than this
    private const int MinGroundBirds = 4;

    // FOR DRAWING: the hawk overhead right now, or null
    public static HawkShadow? Active { get; private set; }
    public Vector2 Position { get; private set; }   // world X, Z
    public Vector2 Direction { get; private set; }  // which way it flies (unit length)
    public float Strength { get; private set; }     // 0 to 1: fades in and out at the world's edge

    private Vector2 start;
    private float travelled, length;
    private bool passed;
    private float returnTimer, sincePassed;
    private bool creepingLogged;
    private Vector3 alarmSpot;
    private string? alarmName;
    private int froze, held;
    private readonly HashSet<int> reacted = new();
    private readonly List<(string Name, string SpeciesKey)> scattered = new();

    public override bool CanFire(StoryContext ctx) => ctx.Daytime && ctx.Rain < MaxRain && ctx.GroundBirds >= MinGroundBirds;

    // A busier garden makes a hawk likelier.
    public override float Weight(StoryContext ctx) => 0.5f + ctx.Busy;

    public override void Start(StoryContext ctx)
    {
        Reset();
        float angle = Rng.Range(0f, MathF.PI * 2f);
        Direction = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
        Vector2 focus = new Vector2(ctx.Focus.X, ctx.Focus.Z);

        // From past the world's edge on one side, through the focus, to past the edge on the other
        float before = EdgeDistance(focus, -Direction) + Overshoot;
        start = focus - Direction * before;
        length = before + EdgeDistance(focus, Direction) + Overshoot;
        Position = start;
        Active = this;

        Storyteller.Instance.Announce("A hawk's shadow passes over", ctx.Focus);
    }

    public override bool Update(float dt)
    {
        if (!passed)
        {
            travelled += Speed * dt;
            Position = start + Direction * MathF.Min(travelled, length);
            Strength = EdgeFade(Position);
            React();
            if (travelled >= length) Pass();
            return false;
        }

        // The ones that scattered creep back one by one. One still on its way out waits its turn again;
        // after GiveUpAfter the rest come back like any regular.
        if (scattered.Count == 0) return true;
        sincePassed += dt;
        if (sincePassed > GiveUpAfter) return true;
        returnTimer -= dt;
        if (returnTimer > 0f) return false;

        (string name, string key) = scattered[0];
        scattered.RemoveAt(0);
        returnTimer = Rng.Range(ReturnGapMin, ReturnGapMax);
        if (StillFlyingOff(name, key))
        {
            scattered.Add((name, key));
            return false;
        }
        if (BirdSpawner.Instance.CallBack(name, key) != null && !creepingLogged)
        {
            EventLog.Add("The birds that scattered are creeping back.");
            creepingLogged = true;
        }
        return scattered.Count == 0;
    }

    // The scattered bird hasn't left the world yet (it's still flying up and away).
    private static bool StillFlyingOff(string name, string key) =>
        World.Instance.GetAllEntities<Bird>().Any(bird => bird.Name == name && Birds.SameKey(bird.Species.Key, key));

    // Stopped early: the scattered birds stay with the regulars and come back like any of them.
    public override void Drop()
    {
        Reset();
    }

    private void Reset()
    {
        Active = null;
        Strength = 0f;
        travelled = 0f;
        sincePassed = 0f;
        passed = false;
        creepingLogged = false;
        alarmName = null;
        froze = 0;
        held = 0;
        reacted.Clear();
        scattered.Clear();
    }

    // The shadow has gone past: one line saying what happened, then the scattered birds' wait begins.
    private void Pass()
    {
        passed = true;
        Active = null;
        Strength = 0f;
        returnTimer = Rng.Range(ReturnDelayMin, ReturnDelayMax);

        if (reacted.Count == 0)
        {
            EventLog.Add("A hawk's shadow passed over, far from any bird.");
            return;
        }
        List<string> parts = new();
        if (scattered.Count > 0) parts.Add($"{scattered.Count} scattered");
        if (froze > 0) parts.Add($"{froze} froze");
        if (held > 0) parts.Add(held == 1 ? "1 held its feeder" : $"{held} held their feeders");
        string what = string.Join(", ", parts);
        EventLog.Add(alarmName != null
            ? $"A hawk's shadow passed over. {alarmName} called the alarm; {what}."
            : $"A hawk's shadow passed over: {what}.");
    }

    // Every bird the shadow has come near reacts, once. A Watchman first, from farther off.
    private void React()
    {
        if (Strength <= 0f) return;
        Vector3 shadow = new Vector3(Position.X, Terrain.HeightAt(Position.X, Position.Y), Position.Y);
        List<Bird> birds = World.Instance.GetAllEntities<Bird>().Where(CanReact).ToList();

        if (alarmName == null)
        {
            Bird? watchman = birds.FirstOrDefault(bird => bird.Personality.Contains(Trait.Watchman) && Flat(bird.Position, shadow) <= WatchmanReach);
            if (watchman != null) RaiseAlarm(watchman);
        }

        float reach = alarmName != null ? Reach * AlarmReach : Reach;
        foreach (Bird bird in birds)
        {
            if (reacted.Contains(bird.EntityID)) continue;
            bool near = Flat(bird.Position, shadow) <= reach;
            bool heardAlarm = alarmName != null && Flat(bird.Position, alarmSpot) <= Reach;
            if (near || heardAlarm) ReactOne(bird);
        }
    }

    // The Watchman calls out and keeps watch where it stands.
    private void RaiseAlarm(Bird watchman)
    {
        alarmName = watchman.Name;
        alarmSpot = watchman.Position;
        reacted.Add(watchman.EntityID);
        watchman.Startle();
        watchman.Call();
        watchman.Freeze(Rng.Range(FreezeMin, FreezeMax));
        watchman.AddRecord("Called the alarm at the Hawk's Shadow");
    }

    // One bird's reaction, by its nerve.
    private void ReactOne(Bird bird)
    {
        reacted.Add(bird.EntityID);
        bool atFeeder = bird.State == BirdState.Eating && bird.TargetFood?.Feeder != null;
        IReadOnlyList<Trait> traits = bird.Personality;

        bool scatter;
        if (bird.Mover.IsBathTrip) scatter = true;
        else if (traits.Contains(Trait.FaintHearted)) scatter = true;
        else if (traits.Contains(Trait.Wary)) scatter = Rng.Chance(WaryScatter);
        else if (traits.Contains(Trait.StoutHearted))
        {
            if (atFeeder)
            {
                held++;
                bird.AddRecord("Held its feeder under the Hawk's Shadow");
                return;
            }
            bird.Startle();
            bird.Freeze(Rng.Range(BriefFreezeMin, BriefFreezeMax));
            froze++;
            bird.AddRecord("Froze under the Hawk's Shadow");
            return;
        }
        else scatter = Rng.Chance(AverageScatter);

        if (scatter)
        {
            bird.AddRecord("Fled the Hawk's Shadow");    // before it leaves, so the record goes with it to the regulars
            bird.Quiet = true;
            bird.Startle();
            bird.StartLeaving(null);
            scattered.Add((bird.Name, bird.Species.Key));
        }
        else
        {
            bird.Startle();
            bird.Freeze(Rng.Range(FreezeMin, FreezeMax));
            froze++;
            bird.AddRecord("Froze under the Hawk's Shadow");
        }
    }

    // Birds going about their day: not arriving, leaving, flying between places, roosting for the night or passing in a flock.
    // A bird eating up on a feeder counts.
    private static bool CanReact(Bird bird)
    {
        if (bird.FlockMember || bird.Roosting) return false;
        if (bird.State is BirdState.Arriving or BirdState.Leaving) return false;
        if (bird.IsFlying && bird.State != BirdState.Eating) return false;
        return true;
    }

    // How far from this point to the world's edge, going this way.
    private static float EdgeDistance(Vector2 from, Vector2 dir)
    {
        float half = World.Instance.HalfSize;
        float best = float.MaxValue;
        if (MathF.Abs(dir.X) > 0.0001f) best = MathF.Min(best, ((dir.X > 0 ? half : -half) - from.X) / dir.X);
        if (MathF.Abs(dir.Y) > 0.0001f) best = MathF.Min(best, ((dir.Y > 0 ? half : -half) - from.Y) / dir.Y);
        return MathF.Max(0f, best);
    }

    // 1 inside the world, fading to 0 just past its edge.
    private static float EdgeFade(Vector2 at)
    {
        float outside = MathF.Max(MathF.Abs(at.X), MathF.Abs(at.Y)) - World.Instance.HalfSize;
        return Math.Clamp(1f - outside / FadeReach, 0f, 1f);
    }

    private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
