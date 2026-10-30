// The simulation numbers the tools panel (Tab) can change. Saved in the settings file (see Settings.cs),
// so they survive a restart and a New world. Defaults keeps the values as written here, for the panel's
// "Default:" hints and right-click reset. Fields rather than properties, so ImGui sliders can take them by ref.
public class SimTuning
{
    public static SimTuning Now { get; set; } = new();
    public static readonly SimTuning Defaults = new();

    // BIRDS
    public float HungerRate = 6f;                  // seconds per 5% hunger lost (about 2 minutes from full to empty)
    public float ArrivalsPerFoodPerMinute = 0.4f;
    public int MaxBirds = 15;

    // BIRD LIFETIMES
    public float LifespanMinMinutes = 60f, LifespanMaxMinutes = 180f;
    public float LoseInterestMinutes = 45f;        // on average, while food is about (sooner when there's none)
    public float RegularChance = 0.5f;             // share of arrivals that are a regular, when one of that kind is away

    // BIRD BEHAVIOR
    public float CallDelayMin = 30f, CallDelayMax = 90f;    // seconds between a bird's own calls
    public float PerchChance = 0.3f;               // chance a pause near a rock becomes a sit on top of it
    public float HomeRadius = 7f;                  // a wandering bird roams this far from home before turning back

    // COINS
    public float CoinDropChance = 0.66f;           // chance to drop a coin after a full meal
    public float CoinLifetime = 30f;               // seconds a coin waits to be clicked (half that out of view)

    // FOOD AND WORLD
    public bool NaturalDrops = true;
    public float DropsPerMinute = 3.5f;             // about 5 seed a minute (WildPileAmount each)
    public int MaxFood = 200;
    public float SpoilSpeed = 1f;                  // times as fast as the spoil times in Seeds.cs
    public float WearSpeed = 1f;                   // times as fast as the wear rates in GroundWear.cs
    public float RegrowSpeed = 1f;                 // times as fast as the regrowth in GroundWear.cs

    // DAY AND NIGHT (see WorldClock.cs)
    public float DayMinutes = 15f;                 // one full day and night, in sim minutes (a RimWorld day, rounded down)
    public float NightBrightness = 1f;             // times the night light: lower is darker (the day isn't touched)
    public float RoostChance = 0.25f;              // chance a bird stays the night (perched on a rock or the ground) instead of flying off
    public int MaxRoosting = 5;                    // at most this many birds stay the night

    // THE POOL (see Pool.cs and the WATER section of BirdMover.cs)
    public float DrinkChance = 0.08f;              // chance a pause near the pool becomes a walk down to sip
    public float BathChance = 0.04f;               // chance it becomes a bath instead
    public int MaxBathers = 3;                     // at most this many birds bathe at once

    // SPROUTS (see Sprout.cs)
    public float SproutChancePerSeed = 0.05f;      // chance each rotted seed comes up as a sprout (1 seed 5%, 10 seed 40%)
    public float SproutMinutes = 15f;              // how long a sprout lives, give or take a little
    public int MaxSprouts = 12;

    // Keeps every number usable, in case the file was edited by hand or a min slider passed its max.
    public void Clamp()
    {
        HungerRate = MathF.Max(0.1f, HungerRate);
        ArrivalsPerFoodPerMinute = MathF.Max(0f, ArrivalsPerFoodPerMinute);
        MaxBirds = Math.Clamp(MaxBirds, 1, 500);
        LifespanMinMinutes = MathF.Max(1f, LifespanMinMinutes);
        LifespanMaxMinutes = MathF.Max(LifespanMinMinutes, LifespanMaxMinutes);
        LoseInterestMinutes = MathF.Max(1f, LoseInterestMinutes);
        RegularChance = Math.Clamp(RegularChance, 0f, 1f);
        CallDelayMin = MathF.Max(1f, CallDelayMin);
        CallDelayMax = MathF.Max(CallDelayMin, CallDelayMax);
        PerchChance = Math.Clamp(PerchChance, 0f, 1f);
        HomeRadius = MathF.Max(1f, HomeRadius);
        CoinDropChance = Math.Clamp(CoinDropChance, 0f, 1f);
        CoinLifetime = MathF.Max(1f, CoinLifetime);
        DropsPerMinute = MathF.Max(0f, DropsPerMinute);
        MaxFood = Math.Clamp(MaxFood, 1, 2000);
        SpoilSpeed = MathF.Max(0.01f, SpoilSpeed);
        WearSpeed = MathF.Max(0f, WearSpeed);
        RegrowSpeed = MathF.Max(0f, RegrowSpeed);
        DayMinutes = MathF.Max(1f, DayMinutes);
        NightBrightness = Math.Clamp(NightBrightness, 0.2f, 3f);
        RoostChance = Math.Clamp(RoostChance, 0f, 1f);
        MaxRoosting = Math.Clamp(MaxRoosting, 0, 100);
        DrinkChance = Math.Clamp(DrinkChance, 0f, 1f);
        BathChance = Math.Clamp(BathChance, 0f, 1f);
        MaxBathers = Math.Clamp(MaxBathers, 0, 50);
        SproutChancePerSeed = Math.Clamp(SproutChancePerSeed, 0f, 1f);
        SproutMinutes = MathF.Max(1f, SproutMinutes);
        MaxSprouts = Math.Clamp(MaxSprouts, 0, 200);
    }
}
