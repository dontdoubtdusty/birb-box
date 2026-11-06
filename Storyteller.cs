using System.Numerics;

// The Storyteller (Step 11): one pacer that decides when something happens in the garden and what.
// Built the way RimWorld's storytellers are (plain rules, no AI), with the cozy rule flipped where theirs punishes:
//  - A steady rhythm: a quiet stretch, a light beat (a Gift or a Small event), quiet again, a Big beat, then a longer rest.
//    Gaps get a little jitter. Only one event runs at a time and the countdown waits for it to end, so events never clump.
//  - Each category has its own cooldown, so a Big event never follows a Big one closely.
//  - Among the events allowed right now (StoryEvent.CanFire) it rolls by weight (StoryEvent.Weight): a quiet garden leans
//    toward gifts, a crowded one toward the hawk or a murmuration.
//  - No difficulty or pity tracking: there is nothing to fail. An event's size follows what's there, never your progress.
//  - While you watch, most events center on the patch in view; otherwise anywhere. They happen with the box off too, and
//    are logged. In view, they also put a label in the hologram (TakeAnnouncements).
// State only. Only the pacer's timers are saved; a running event is dropped on save and load (see AfterLoad).
public class Storyteller
{
    public static Storyteller Instance { get; private set; } = null!;

    // PACING (the minutes are in SimTuning: StoryPace, StoryQuietMinutes, StoryRestMinutes, BigCooldownMinutes, GiftCooldownMinutes)
    private const float GapJitter = 0.3f;        // quiet stretches vary by this share either way
    private const float RestJitter = 0.2f;
    private const float RetryDelay = 60f;        // seconds before trying again when nothing was allowed
    private const float FocusOnView = 0.7f;      // chance an event centers on the patch in view, while you watch
    private const float FocusSpread = 0.5f;      // how far from the view's middle, as a share of its half width

    private readonly ViewWindow view;
    private readonly FoodSpawner food;
    private readonly List<StoryEvent> events = new() { new HawkShadow(), new Murmuration(), new Windfall() };
    private readonly Dictionary<StoryCategory, float> cooldowns = new();
    private readonly List<(string Text, Vector3 Position)> announcements = new();
    private bool watching;

    public IReadOnlyList<StoryEvent> Events => events;
    public StoryEvent? Running { get; private set; }
    public bool BigBeatNext { get; private set; }     // false: the light beat comes next; true: the Big one
    public float NextBeatIn { get; private set; }     // sim seconds until the next beat (counts only while nothing runs)

    public Storyteller(ViewWindow view, FoodSpawner food)
    {
        Instance = this;
        this.view = view;
        this.food = food;
        NextBeatIn = QuietStretch();
    }

    public float CooldownLeft(StoryCategory category) => cooldowns.TryGetValue(category, out float left) ? left : 0f;

    // Moves the running event on, or counts down to the next beat and fires one.
    // watching: the bird scene is up and the box is on, so events may center on the view and show labels.
    public void Update(float dt, bool watching)
    {
        this.watching = watching;
        foreach (StoryCategory category in cooldowns.Keys.ToList()) cooldowns[category] = MathF.Max(0f, cooldowns[category] - dt);

        if (Running != null)
        {
            if (Running.Update(dt)) Running = null;
            return;
        }

        float pace = SimTuning.Now.StoryPace;
        if (pace <= 0f) return;
        NextBeatIn -= dt * pace;
        if (NextBeatIn > 0f) return;

        StoryContext ctx = Snapshot();
        StoryEvent? pick = BigBeatNext
            ? Pick(ctx, StoryCategory.Big) ?? Pick(ctx, StoryCategory.Gift, StoryCategory.Small)
            : Pick(ctx, StoryCategory.Gift, StoryCategory.Small);
        if (pick == null)
        {
            NextBeatIn = RetryDelay;
            return;
        }

        Begin(pick, ctx);
        NextBeatIn = BigBeatNext ? RestStretch() : QuietStretch();
        BigBeatNext = !BigBeatNext;
    }

    // Tools panel: starts this event now, whatever its cooldown or conditions. Refused while another one runs.
    public void Fire(string key)
    {
        StoryEvent? ev = events.FirstOrDefault(e => e.Key == key);
        if (ev == null) return;
        if (Running != null)
        {
            EventLog.Add($"{Running.Name} is still going; try again when it's over.");
            return;
        }
        Begin(ev, Snapshot());
    }

    private void Begin(StoryEvent ev, StoryContext ctx)
    {
        Running = ev;
        cooldowns[ev.Category] = CooldownFor(ev.Category);
        ev.Start(ctx);
    }

    // Weighted roll among the events in these categories that are off cooldown and allowed now. Null if none.
    private StoryEvent? Pick(StoryContext ctx, params StoryCategory[] categories)
    {
        List<(StoryEvent Event, float Weight)> allowed = events
            .Where(e => categories.Contains(e.Category) && CooldownLeft(e.Category) <= 0f && e.CanFire(ctx))
            .Select(e => (e, MathF.Max(0f, e.Weight(ctx))))
            .Where(pair => pair.Item2 > 0f)
            .ToList();
        if (allowed.Count == 0) return null;

        float roll = Rng.Range(0f, allowed.Sum(pair => pair.Weight));
        foreach ((StoryEvent ev, float weight) in allowed)
        {
            roll -= weight;
            if (roll <= 0f) return ev;
        }
        return allowed[^1].Event;
    }

    private static float CooldownFor(StoryCategory category) => 60f * (category == StoryCategory.Big
        ? SimTuning.Now.BigCooldownMinutes
        : SimTuning.Now.GiftCooldownMinutes);

    private static float QuietStretch() => SimTuning.Now.StoryQuietMinutes * 60f * Rng.Range(1f - GapJitter, 1f + GapJitter);
    private static float RestStretch() => SimTuning.Now.StoryRestMinutes * 60f * Rng.Range(1f - RestJitter, 1f + RestJitter);

    // How the garden is right now, and where the event should center.
    private StoryContext Snapshot()
    {
        StoryContext ctx = new() { Food = food };
        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (bird.State == BirdState.Leaving || bird.FlockMember) continue;
            ctx.Birds.Add(bird);
            if (!bird.IsFlying && !bird.Asleep && bird.State != BirdState.Arriving) ctx.GroundBirds++;
        }
        ctx.GroundSeed = GroundSeed();

        WorldClock clock = WorldClock.Instance;
        ctx.Daytime = clock.Phase == DayPhase.Day && clock.ArrivalShare > 0.6f;
        ctx.Rain = Weather.Instance.Rain;
        ctx.Busy = Math.Clamp(ctx.Birds.Count / (float)Math.Max(1, SimTuning.Now.MaxBirds), 0f, 1f);

        if (watching && Rng.Chance(FocusOnView))
        {
            float reach = view.WorldHalfWidth * FocusSpread;
            Vector3 spot = new Vector3(view.Center.X + Rng.Range(-reach, reach), 0f, view.Center.Y + Rng.Range(-reach, reach));
            ctx.Focus = World.Instance.PullInside(spot, 4f);
        }
        else
        {
            ctx.Focus = World.Instance.RandomGroundPointNearMiddle();
        }
        return ctx;
    }

    // Fresh seed lying on the dirt, all piles together (seed in feeders and on sprouts doesn't count).
    public static float GroundSeed()
    {
        float total = 0f;
        foreach (Food pile in World.Instance.GetAllEntities<Food>())
        {
            if (pile.Feeder != null || pile.Plant != null || !pile.IsLanded) continue;
            foreach (SeedLayer layer in pile.Layers)
            {
                if (!pile.IsSpoiled(layer)) total += layer.Amount;
            }
        }
        return total;
    }

    // ---------------- LABELS ----------------

    // An event's label for the hologram: kept only while you're watching and the spot is in view.
    public void Announce(string text, Vector3 position)
    {
        if (watching && view.Contains(position)) announcements.Add((text, position));
    }

    // The labels waiting to be shown (GameEngine hands them to ChatMessages), emptied.
    public List<(string Text, Vector3 Position)> TakeAnnouncements()
    {
        List<(string, Vector3)> taken = new(announcements);
        announcements.Clear();
        return taken;
    }

    // ---------------- SAVING (see SaveGame.cs) ----------------

    public StorySave ToSave() => new()
    {
        BigBeatNext = BigBeatNext,
        NextBeatIn = NextBeatIn,
        Cooldowns = cooldowns.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
    };

    // A new world (or a save from before 5.22) starts with a quiet stretch and no cooldowns. A running event is dropped.
    public void Restore(StorySave? save)
    {
        Running?.Drop();
        Running = null;
        announcements.Clear();
        cooldowns.Clear();
        BigBeatNext = save?.BigBeatNext ?? false;
        NextBeatIn = save?.NextBeatIn ?? QuietStretch();
        if (save == null) return;
        foreach ((string name, float left) in save.Cooldowns)
        {
            if (Enum.TryParse(name, out StoryCategory category)) cooldowns[category] = left;
        }
    }

    // After a load: a passing flock that was saved mid-visit moves on at once, all together.
    public static void AfterLoad()
    {
        List<Bird> flock = World.Instance.GetAllEntities<Bird>().Where(bird => bird.FlockMember && bird.State != BirdState.Leaving).ToList();
        if (flock.Count == 0) return;
        foreach (Bird bird in flock) bird.StartLeaving(null, returns: false);
        EventLog.Add("The visiting flock moved on.");
    }
}
