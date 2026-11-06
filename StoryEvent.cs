using System.Numerics;

// One kind of event the Storyteller can fire (see Storyteller.cs), like RimWorld's incident workers.
// Each says when it's allowed (CanFire), how likely it is right now (Weight), and runs itself once started.
// Events are simulation only: anything drawn reads their state (see HawkShadow.Active).

// Big events never follow each other closely; Gifts and Small ones fill the gaps (each category has its own cooldown).
public enum StoryCategory { Big, Gift, Small }

public abstract class StoryEvent
{
    public abstract string Key { get; }               // for the tools panel's fire buttons; never shown
    public abstract string Name { get; }              // shown in the tools panel
    public abstract StoryCategory Category { get; }

    // Whether it could happen right now (no hawk at night, no murmuration without seed on the ground...).
    public abstract bool CanFire(StoryContext ctx);

    // How likely it is to be picked among the allowed ones, by how the garden is right now. 1 is ordinary.
    public abstract float Weight(StoryContext ctx);

    // Sets it going: places things, picks the birds it casts, writes its first log line.
    public abstract void Start(StoryContext ctx);

    // Moves it on. True once it's over.
    public abstract bool Update(float dt);

    // Stopped early (a New world or a loaded save): forget everything without touching the world.
    public virtual void Drop() { }
}

// A snapshot of the garden, taken when the Storyteller rolls for an event.
public class StoryContext
{
    public List<Bird> Birds = new();       // in the garden and staying (not leaving, not a passing flock)
    public int GroundBirds;                // of those, awake and not flying
    public float GroundSeed;               // fresh seed lying on the dirt (not in feeders or on sprouts)
    public bool Daytime;                   // the sun is up and it's not getting late
    public float Rain;                     // Weather.Rain
    public float Busy;                     // birds here as a share of SimTuning.MaxBirds, 0 to 1
    public Vector3 Focus;                  // where the event centers: the patch in view when you're watching, often
    public FoodSpawner Food = null!;
}
