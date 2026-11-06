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
    // The three rain levels: how hard each falls (Rain eases up to this) and how often a new rain spell rolls it
    public const float DrizzleStrength = 0.35f;        // Light: Mizzling
    public const float ShowersStrength = 0.65f;        // Showers
    public const float DownpourStrength = 1f;          // Downpour
    private const float DrizzleShare = 0.40f, ShowersShare = 0.35f;    // the rest are downpours
    private const float DrizzleLongerBy = 1.3f;        // light rain lingers a little longer
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

    // BROKEN CLOUDS (5.05): the drifting clouds whose shadows cross the garden and whose shapes fill the Hologram View's sky.
    // Each dry spell picks how cloudy its fair weather is; before rain the cover fills in to a solid deck.
    private const float ClearDayShare = 0.35f;         // share of dry spells with a clear sky (no clouds at all)
    private const float FairCoverMin = 0.25f, FairCoverMax = 0.6f;    // how cloudy the other dry spells are (0 none, 1 solid)
    private const float CoverRate = 1.0f;              // cover fills in or breaks up: about an hour from none to full
    private const float WindSpeed = 0.12f;             // how fast the clouds drift (world units per sim second)
    private const float WindTurn = 0.6f;               // how far (radians) the wind may swing at the start of each spell

    // STATE (saved)
    public bool Raining { get; private set; }           // in a rain spell (the rain itself may still be building up)
    public float Strength { get; private set; } = 1f;   // this spell's rain: DrizzleStrength, ShowersStrength or DownpourStrength
    public float HoursLeft { get; private set; }        // world hours left of this rain or dry spell
    public float Cloud { get; private set; }            // 0 clear sky, 1 full cloud
    public float Rain { get; private set; }             // how hard it's raining right now: 0 none, 1 steady rain
    public float GroundWet { get; private set; }        // 0 dry ground, 1 soaked
    public Puddles Puddles { get; } = new();
    public bool Held;                                   // dev (tools panel): the weather stays as it is
    public float RainClock { get; private set; }        // sim seconds of falling rain, for drawing the drops (not saved)
    public float FairCover { get; private set; }        // this dry spell's cloudiness when fair (0 clear to FairCoverMax)
    public float Cover { get; private set; }            // how much of the sky the drifting clouds cover right now (0 to 1)
    public Vector2 CloudDrift { get; private set; }     // how far the clouds have drifted (world units; not saved)
    private float windAngle = Rng.Range(0f, MathF.Tau); // which way the wind blows (not saved)

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
        FairCover = RollFairCover();
        Cover = FairCover;
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
                else StartRainSpell(RollStrength());
            }
        }

        float cloudTarget = Raining ? float.Lerp(0.7f, 1f, Strength) : 0f;
        Cloud = Approach(Cloud, cloudTarget, CloudRate * hours);
        float rainTarget = Raining && Cloud >= RainNeedsCloud ? Strength : 0f;
        Rain = Approach(Rain, rainTarget, RainRate * hours);

        if (Rain > 0.01f) GroundWet = MathF.Min(1f, GroundWet + Rain * WetRate * hours);
        else GroundWet = MathF.Max(0f, GroundWet - (clock.SunElevation > 0f ? DryRateSun : DryRateNight) * hours);

        // The drifting clouds: they fill in to a solid deck as the rain spell's clouds gather, and break up after
        float coverTarget = MathF.Max(FairCover, Raining ? 1f : 0f);
        Cover = MathF.Max(Approach(Cover, coverTarget, CoverRate * hours), Cloud);
        CloudDrift += new Vector2(MathF.Cos(windAngle), MathF.Sin(windAngle)) * WindSpeed * simDt;

        if (Rain > 0f) RainClock += simDt;
        Puddles.Update(simDt, this, world, wear);
    }

    private void StartRainSpell(float strength)
    {
        Raining = true;
        Strength = strength;
        HoursLeft = Rng.Range(RainMinHours, RainMaxHours) * (strength <= DrizzleStrength ? DrizzleLongerBy : 1f);
        windAngle += Rng.Range(-WindTurn, WindTurn);
    }

    // Picks how hard a new rain spell falls: light, showers or downpour.
    private static float RollStrength()
    {
        float roll = Rng.Range(0f, 1f);
        if (roll < DrizzleShare) return DrizzleStrength;
        return roll < DrizzleShare + ShowersShare ? ShowersStrength : DownpourStrength;
    }

    private void StartDrySpell(Season season)
    {
        Raining = false;
        HoursLeft = DryHours[season] * Rng.Range(0.5f, 1.5f) / MathF.Max(0.05f, SimTuning.Now.RainOften);
        FairCover = RollFairCover();
        windAngle += Rng.Range(-WindTurn, WindTurn);
    }

    // How cloudy a dry spell's fair weather is: some are clear, the rest have scattered clouds.
    private static float RollFairCover() => Rng.Chance(ClearDayShare) ? 0f : Rng.Range(FairCoverMin, FairCoverMax);

    // Dev (tools panel): this spell's fair-weather cloudiness, eased into like any change.
    public void SetFairCover(float cover) => FairCover = Math.Clamp(cover, 0f, 1f);

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

    // This spell's rain level: 1 light, 2 showers, 3 downpour (0 in a dry spell). It stays put as the rain builds or eases.
    public int Level => !Raining ? 0 : Strength <= DrizzleStrength + 0.1f ? 1 : Strength <= ShowersStrength + 0.1f ? 2 : 3;

    // The word for the HUD, in the house style: Fair skies, Gathering, Mizzling, Showers, Downpour, Mending.
    public string Word
    {
        get
        {
            if (Rain > 0.05f) return Raining ? (Level == 1 ? "Mizzling" : Level == 2 ? "Showers" : "Downpour") : "Mending";
            if (Raining) return "Gathering";
            return Cloud > 0.1f ? "Mending" : "Fair skies";
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
        light.Golden *= 1f - Cloud;                                    // no golden hour under cloud
        light.Shade = Vector3.Lerp(light.Shade, Vector3.One, Cloud);  // flat, even light: no warm and cool sides
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
        FairCover = FairCover,
        Cover = Cover,
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
        if (save.FairCover is float fair) FairCover = Math.Clamp(fair, 0f, 1f);    // a save from before 5.05 keeps the cover Reset rolled
        Cover = Math.Clamp(save.Cover ?? FairCover, 0f, 1f);
        Puddles.Restore(save.Puddles);
    }
}
