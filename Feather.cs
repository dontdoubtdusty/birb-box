using System.Numerics;

// A feather a bird lets fall after a good meal. It pops up a little, drifts down swaying, lies flat on the dirt
// and waits to be clicked. An uncollected feather fades away after a while, twice as fast outside the view window.
// Each one belongs to the species of the bird that dropped it (Species.FeatherName).
public class Feather : Entity
{
    public const float Radius = 0.24f;             // how big it is for clicks and for being pushed off rocks and water
    public const float Length = 0.68f, Width = 0.23f, Thickness = 0.026f;    // the drawn vane (WorldDrawer.DrawFeathers)
    public const float RestHeight = 0.04f;        // how high a feather lying on the dirt sits
    private const float OffscreenDrainRate = 2f;   // life drains this many times faster when hidden
    private const float FadeTime = 6f;             // last seconds spent fading out
    private const float PopSpeed = 1.6f;           // how fast it leaps out of the bird, upward
    private const float FallSpeed = 0.9f;          // it never falls faster than this: it floats
    private const float SideDrag = 2.5f;           // how quickly the sideways push from the pop dies away
    private const float SwayRate = 2.2f;           // radians per second of the side-to-side swing
    private const float SwayDrift = 0.35f;         // how far the swing carries it sideways (units per second)
    private const float SwayTilt = 0.7f;           // how far it rocks while falling, in radians
    private const float TurnRate = 0.8f;           // how fast it turns about while falling

    public Species Species { get; }
    public float LifeLeft { get; private set; }    // starts at SimTuning.FeatherLifetime
    public bool IsLanded { get; private set; }
    public float Heading { get; private set; }     // which way it points on the ground, in radians
    public float Tilt { get; private set; }        // how far it rocks sideways while falling; 0 once it lies flat

    // 1 while fresh, drops to 0 over the last few seconds
    public float Alpha => Math.Clamp(LifeLeft / FadeTime, 0f, 1f);

    private Vector3 velocity;
    private float age;                              // seconds in the air, drives the swaying
    private readonly float phase = Rng.Range(0f, MathF.PI * 2f);
    private bool removed = false;

    public Feather(Vector3 position, Species species)
    {
        Position = position;
        Species = species;
        LifeLeft = SimTuning.Now.FeatherLifetime;
        Heading = Rng.Range(0f, MathF.PI * 2f);

        // A little hop out of the bird in a random direction
        float angle = Rng.Range(0f, MathF.PI * 2f);
        velocity = new Vector3(MathF.Sin(angle) * 0.5f, PopSpeed, MathF.Cos(angle) * 0.5f);
    }

    public override void Update(float dt)
    {
        bool onScreen = GameEngine.Instance.View.Contains(Position);
        LifeLeft -= dt * (onScreen ? 1f : OffscreenDrainRate);

        if (!IsLanded) Drift(dt, onScreen);
        if (LifeLeft <= 0f) Remove();
    }

    // Falling: gravity pulls it down but it never speeds up past FallSpeed, and it swings from side to side as it comes.
    private void Drift(float dt, bool onScreen)
    {
        age += dt;
        velocity.Y = MathF.Max(velocity.Y - World.Gravity * dt, -FallSpeed);
        float decay = MathF.Exp(-SideDrag * dt);
        velocity.X *= decay;
        velocity.Z *= decay;

        float swing = MathF.Sin(age * SwayRate + phase);
        Vector3 sway = new Vector3(swing, 0f, MathF.Cos(age * SwayRate * 0.8f + phase * 1.3f)) * SwayDrift;
        Position += (velocity + sway) * dt;
        Tilt = swing * SwayTilt;
        Heading += swing * TurnRate * dt;

        if (Position.Y > Terrain.SurfaceAt(Position) + RestHeight || velocity.Y > 0f) return;

        // A feather over the water or a rock ends up on the shore or beside it
        Vector3 rested = Obstacles.PushOut(new Vector3(Position.X, 0f, Position.Z), Radius, Radius);
        Position = Terrain.OnGround(rested, RestHeight);
        velocity = Vector3.Zero;
        Tilt = 0f;
        IsLanded = true;
        if (onScreen) Sounds.PlayFeatherLand();
    }

    // Saving (see SaveGame.cs)
    public FeatherSave ToSave() => new()
    {
        Position = Position, Velocity = velocity, LifeLeft = LifeLeft, Landed = IsLanded, Heading = Heading, Species = Species.Key,
    };

    public static Feather FromSave(FeatherSave save, Species species)
    {
        Feather feather = new Feather(save.Position, species);
        feather.velocity = save.Velocity;
        feather.LifeLeft = save.LifeLeft;
        feather.IsLanded = save.Landed;
        feather.Heading = save.Heading;
        return feather;
    }

    // Picks it up: it goes into the count for its species, and the log says whose it was.
    public void Collect()
    {
        if (removed) return;
        GameEngine.Instance.Feathers.Add(Species);
        Sounds.PlayFeatherPickup();
        EventLog.Add($"Collected {WithArticle(Species.FeatherName)}.");
        Remove();
    }

    // "an Ember Plume", "a Dusk Down"
    private static string WithArticle(string name)
    {
        bool vowel = name.Length > 0 && "AEIOUaeiou".Contains(name[0]);
        return (vowel ? "an " : "a ") + name;
    }

    private void Remove()
    {
        if (removed) return;
        removed = true;
        World.Instance.RemoveEntity(this);
    }
}
