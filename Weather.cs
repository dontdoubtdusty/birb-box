using System.Numerics;

// The bird world's weather (Step 10b; rain only for now). It moves with sim time, like the clock, and is saved.
// Dry spells and rain spells take turns: a dry spell lasts about a day (shorter in spring, longer in summer),
// a rain spell a few hours, as drizzle or steady rain. Clouds gather before the rain starts and clear after it stops,
// so the light dims first and comes back last. Rain soaks the ground (GroundWet), which fills puddles on bare dirt
// (Puddles.cs) and dries out slowly afterward, faster under the sun.
// Only the hologram's bird world has weather: the cabin and the store's floor stay dry (the store's light dims with the clouds).
// State only: the renderer, the floor shader, the birds and the HUD read it.
public class Weather
{
    public static Weather Instance { get; private set; } = null!;

    // SPELLS (world hours; a world hour is SimTuning.DayMinutes / 24 of sim time)
    private const float RainMinHours = 2f, RainMaxHours = 6f;
    private const float DrizzleShare = 0.45f;          // share of rain spells that are only drizzle
    public const float DrizzleStrength = 0.35f;        // how hard drizzle falls (steady rain is 1)
    private const float FirstDryMin = 3f, FirstDryMax = 10f;    // a new world's first dry spell, so rain comes on the first day
    // Average dry hours between rains by season (spring showers, dry summers); each spell is 0.5 to 1.5 times this,
    // then divided by SimTuning.RainOften
    private static readonly Dictionary<Season, float> DryHours = new()
    {
        [Season.Spring] = 20f,
        [Season.Summer] = 36f,
        [Season.Autumn] = 24f,
        [Season.Winter] = 30f,
    };

    // EASING (per world hour)
    private const float CloudRate = 1.2f;              // clouds gather or clear: a little under an hour from none to full
    private const float RainRate = 2f;                 // rain builds up or eases off: about half an hour
    private const float RainNeedsCloud = 0.6f;         // rain only falls once the clouds are this thick
    private const float WetRate = 0.9f;                // steady rain soaks the ground in a little over an hour
    private const float DryRateSun = 0.3f;             // the ground dries in about 3 hours by day...
    private const float DryRateNight = 0.1f;           // ...and much slower at night

    // THE LIGHT UNDER CLOUD (see Overcast and Haze)
    private const float CloudDimsSun = 0.6f;           // full cloud takes this share of the sun's (or moon's) light away
    private const float CloudGraysSky = 0.55f;         // and turns the light from all around this far toward plain gray
    private const float CloudBrightensSky = 1.05f;     // a touch brighter all round, since the sky itself glows gray
    private static readonly Vector3 RainHaze = new Vector3(0.16f, 0.18f, 0.20f);    // the fog's haze in rain, by day
    private const float RainHazeShare = 0.5f;

    // STATE (saved)
    public bool Raining { get; private set; }           // in a rain spell (the rain itself may still be building up)
    public float Strength { get; private set; } = 1f;   // this spell's rain: DrizzleStrength or 1
    public float HoursLeft { get; private set; }        // world hours left of this rain or dry spell
    public float Cloud { get; private set; }            // 0 clear sky, 1 full cloud
    public float Rain { get; private set; }             // how hard it's raining right now: 0 none, 1 steady rain
    public float GroundWet { get; private set; }        // 0 dry ground, 1 soaked
    public Puddles Puddles { get; } = new();
    public bool Held;                                   // dev (tools panel): the weather stays as it is
    public float RainClock { get; private set; }        // sim seconds of falling rain, for drawing the drops (not saved)

    public Weather()
    {
        Instance = this;
        Reset();
    }

    // A new world (or a save from before weather): fair, dry, with rain due some hours into the first day.
    public void Reset()
    {
        Raining = false;
        Strength = 1f;
        HoursLeft = Rng.Range(FirstDryMin, FirstDryMax);
        Cloud = 0f;
        Rain = 0f;
        GroundWet = 0f;
        Puddles.Clear();
    }

    // ---------------- TIME PASSING ----------------

    // Moves the weather on by sim time: the spell runs down, the clouds and rain ease toward it, the ground wets or dries,
    // and the puddles follow the ground.
    public void Update(float simDt, WorldClock clock, World world, GroundWear wear)
    {
        float hours = simDt * 24f / (SimTuning.Now.DayMinutes * 60f);

        if (!Held)
        {
            HoursLeft -= hours;
            if (HoursLeft <= 0f)
            {
                if (Raining) StartDrySpell(clock.Season);
                else StartRainSpell(Rng.Chance(DrizzleShare) ? DrizzleStrength : 1f);
            }
        }

        float cloudTarget = Raining ? float.Lerp(0.7f, 1f, Strength) : 0f;
        Cloud = Approach(Cloud, cloudTarget, CloudRate * hours);
        float rainTarget = Raining && Cloud >= RainNeedsCloud ? Strength : 0f;
        Rain = Approach(Rain, rainTarget, RainRate * hours);

        if (Rain > 0.01f) GroundWet = MathF.Min(1f, GroundWet + Rain * WetRate * hours);
        else GroundWet = MathF.Max(0f, GroundWet - (clock.SunElevation > 0f ? DryRateSun : DryRateNight) * hours);

        if (Rain > 0f) RainClock += simDt;
        Puddles.Update(simDt, this, world, wear);
    }

    private void StartRainSpell(float strength)
    {
        Raining = true;
        Strength = strength;
        HoursLeft = Rng.Range(RainMinHours, RainMaxHours) * (strength < 1f ? 1.3f : 1f);    // drizzle lingers a little longer
    }

    private void StartDrySpell(Season season)
    {
        Raining = false;
        HoursLeft = DryHours[season] * Rng.Range(0.5f, 1.5f) / MathF.Max(0.05f, SimTuning.Now.RainOften);
    }

    // Dev (tools panel): rain now (drizzle or steady), or clear up now. The clouds still gather or part at their usual pace.
    public void StartRain(float strength) => StartRainSpell(strength);
    public void ClearUp(Season season) => StartDrySpell(season);
    public void Soak() => GroundWet = 1f;    // puddles still need rain falling to form

    private static float Approach(float value, float target, float step) =>
        value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);

    // ---------------- WHAT IT DOES ----------------

    // Rain falling hard enough to matter (birds hunch, baths wait, seed spoils faster).
    public bool IsRaining => Rain > 0.15f;

    // How many times as fast seed on the ground spoils right now (SimTuning.RainSpoil in steady rain).
    public float SpoilFactor => 1f + (SimTuning.Now.RainSpoil - 1f) * Rain;

    // The word for the HUD: Fair, Clouding over, Drizzle, Rain, Clearing.
    public string Word
    {
        get
        {
            if (Rain > 0.05f) return Raining ? (Strength < 1f ? "Drizzle" : "Rain") : "Clearing";
            if (Raining) return "Clouding over";
            return Cloud > 0.1f ? "Clearing" : "Fair";
        }
    }

    // The time of day's light under this much cloud: the sun (or moon) dimmed, the light from all around grayer.
    public DayLight Overcast(DayLight light)
    {
        if (Cloud <= 0f) return light;
        Vector3 ambient = light.Ambient;
        float lum = Vector3.Dot(ambient, new Vector3(0.299f, 0.587f, 0.114f));
        Vector3 gray = new Vector3(lum) * CloudBrightensSky;
        light.Ambient = Vector3.Lerp(ambient, gray, CloudGraysSky * Cloud);
        light.Direct *= 1f - CloudDimsSun * Cloud;
        return light;
    }

    // The fog's haze under cloud: toward a soft rain gray, less so at night (when it's already deep blue).
    public Vector3 Haze(Vector3 haze, DayLight light)
    {
        Vector3 gray = RainHaze * (1f - 0.75f * light.Darkness);
        return Vector3.Lerp(haze, gray, RainHazeShare * Cloud);
    }

    // ---------------- SAVING (see SaveGame.cs) ----------------

    public WeatherSave ToSave() => new()
    {
        Raining = Raining,
        Strength = Strength,
        HoursLeft = HoursLeft,
        Cloud = Cloud,
        Rain = Rain,
        GroundWet = GroundWet,
        Puddles = Puddles.ToSave(),
    };

    // A save from before weather (null) starts fair, like a new world.
    public void Restore(WeatherSave? save)
    {
        Reset();
        if (save == null) return;
        Raining = save.Raining;
        Strength = Math.Clamp(save.Strength, 0.1f, 1f);
        HoursLeft = MathF.Max(0f, save.HoursLeft);
        Cloud = Math.Clamp(save.Cloud, 0f, 1f);
        Rain = Math.Clamp(save.Rain, 0f, 1f);
        GroundWet = Math.Clamp(save.GroundWet, 0f, 1f);
        Puddles.Restore(save.Puddles);
    }
}
