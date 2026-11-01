using System.Numerics;

public enum Season { Spring, Summer, Autumn, Winter }

public enum DayPhase { Dawn, Day, Dusk, Night }

// How the hologram is lit at one moment: soft light from everywhere, plus one light from the sun (or the moon).
public struct DayLight
{
    public Vector3 Ambient;      // light from all around (0.45 each at midday)
    public Vector3 Direct;       // light from the sun or moon (0.65 each at midday)
    public Vector3 Direction;    // toward that light, in the world (+Y up)
    public float Darkness;       // 0 by day, 1 at full night: the fog's haze turns deep blue
    public Vector3 Shade;        // what faces turned away from the sun or moon tint their fill light by (1, 1, 1 at midday; blue-purple at golden hour)
    public float Golden;         // 0 to 1: how near golden hour (low warm sun). Warms the ground and eases the hologram's teal tint

    // Plain midday, as the hologram looked before days existed. The Birb Designer always uses it.
    public static readonly DayLight Noon = new()
    {
        Ambient = new Vector3(0.45f),
        Direct = new Vector3(0.65f),
        Direction = Vector3.Normalize(new Vector3(0.4f, 1f, 0.3f)),
        Darkness = 0f,
        Shade = Vector3.One,
        Golden = 0f,
    };
}

// The bird world's calendar and time of day (Step 10a). It only moves while the simulation runs: nothing passes
// while the game is closed. Days count from midnight of Day 1, the first day of spring; a year is four seasons of
// DaysPerSeason days. The sun follows the real one at a mid-latitude (about New York's), so summer days are long,
// winter days short, and dawn and dusk run from the first glow (nautical twilight) to sunrise and back.
// Only the hologram (birds and store) follows it: the cabin, its lamps and the starfield window stay as they are.
public class WorldClock
{
    public static WorldClock Instance { get; private set; } = null!;

    // CALENDAR (a day's length is SimTuning.DayMinutes)
    public const int DaysPerSeason = 5;
    public const int DaysPerYear = DaysPerSeason * 4;
    public const float StartHour = 5.5f;            // a new world starts at dawn on the first day of spring
    public const float OldSaveStartHour = 8f;       // a save from before days starts mid-morning, so its birds aren't sent to roost

    // THE SUN (degrees)
    private const float Latitude = 40.7f;           // about 15 h of daylight in summer, 9 h in winter
    private const float Tilt = 23.44f;              // how far the sun swings between summer and winter
    private const float HorizonAngle = -0.833f;     // the sun's top edge on the horizon: sunrise and sunset
    private const float FirstGlowAngle = -12f;      // the first glow before dawn and the last after dusk
    public static readonly Vector3 MoonDirection = Vector3.Normalize(new Vector3(-0.35f, 0.85f, -0.4f));
    private const float LowSunLift = 6f;            // the light never comes from lower than this, so low sun still lights the tops

    // THE MOON (5.05): it waxes and wanes over MoonDays days, rising about 3 hours later each day, and lights the
    // night a little more or less (it never makes the night hard to see: night is a moonlit stage)
    public const float MoonDays = 8f;               // from full moon to full moon
    private const float MoonStartPhase = 0.29f;     // a new world's first night has a waxing moon, a little past half
    private const float NewMoonLight = 0.85f;       // the night's light at new moon, as a share of the usual...
    private const float FullMoonLight = 1.05f;      // ...and at full moon
    private static readonly Vector3 NewMoonTint = new Vector3(0.94f, 0.97f, 1.05f);    // a moonless night is a touch bluer

    // WHEN BIRDS ARE ABOUT (hours, measured from sunrise and sunset; see Bird.UpdateRoosting)
    private const float WakeBeforeSunrise = 0.5f;   // birds wake and the dawn chorus starts this long before sunrise
    private const float RoostAfterSunset = 0.3f;    // and they settle this long after sunset
    private const float ChorusAfterSunrise = 1f;    // the dawn chorus runs until this long after sunrise
    private const float QuietBeforeSunset = 0.5f;   // calls thin out from this long before sunset
    private const float OwlSleepAfterSunrise = 0.5f;  // Night Owls go to sleep this long after sunrise
    private const float OwlWakeBeforeSunset = 0.5f;   // and wake this long before sunset, so they're about at dusk, night and dawn

    // LIGHT KEYS: sun elevation (degrees) -> light from everywhere, light from the sun or moon, the shade tint
    // (what faces turned away from the light lean toward) and how golden the hour is (see DayLight).
    // In between two keys they blend. Midday (the last key) matches the look from before days existed.
    // Night is a moonlit stage (5.04): it reads as night by its silver-blue color, not by being dark, so birds
    // stay easy to see. The blue hour is a touch brighter than full night, so twilight never dips below it.
    private static readonly (float Elevation, Vector3 Ambient, Vector3 Direct, Vector3 Shade, float Golden)[] LightKeys =
    {
        (-12f, new Vector3(0.30f, 0.38f, 0.55f), new Vector3(0.30f, 0.36f, 0.48f), new Vector3(0.90f, 0.95f, 1.10f), 0f),      // night: silver-blue moonlight
        (-6f,  new Vector3(0.42f, 0.44f, 0.68f), new Vector3(0.32f, 0.30f, 0.44f), new Vector3(0.85f, 0.85f, 1.15f), 0f),      // the blue hour
        (-1f,  new Vector3(0.54f, 0.42f, 0.50f), new Vector3(0.78f, 0.42f, 0.30f), new Vector3(0.80f, 0.80f, 1.15f), 0.8f),    // rose glow at the horizon
        (4f,   new Vector3(0.48f, 0.43f, 0.40f), new Vector3(0.98f, 0.66f, 0.32f), new Vector3(0.70f, 0.78f, 1.22f), 1f),      // golden low sun
        (14f,  new Vector3(0.45f, 0.44f, 0.42f), new Vector3(0.72f, 0.64f, 0.50f), new Vector3(0.90f, 0.93f, 1.06f), 0.25f),   // warm morning or afternoon
        (28f,  new Vector3(0.45f, 0.45f, 0.45f), new Vector3(0.65f, 0.65f, 0.65f), Vector3.One, 0f),                           // full day
    };

    // STATE (saved: Days)
    public double Days { get; set; } = StartHour / 24.0;
    public bool Held;    // dev (tools panel): the clock stands still

    // Worked out from Days in Refresh, every frame
    public float Hour { get; private set; }              // 0 to 24
    public float SunElevation { get; private set; }      // degrees above the horizon (negative below)
    public Vector3 SunDirection { get; private set; }    // toward the sun itself, below the horizon at night (the Hologram View's sky)
    public float Sunrise { get; private set; }           // hours, today
    public float Sunset { get; private set; }
    public float FirstGlow { get; private set; }         // the first glow before sunrise
    public float LastGlow { get; private set; }          // the last glow after sunset
    public float MoonPhase { get; private set; }         // 0 new, 0.25 first quarter, 0.5 full, 0.75 last quarter
    public float MoonLit { get; private set; }           // share of the moon's face that is lit (0 new, 1 full)
    public Vector3 MoonSkyDirection { get; private set; } // toward the moon itself, where it is in the sky (the Hologram View draws it there)
    public DayLight Light { get; private set; } = DayLight.Noon;

    public WorldClock()
    {
        Instance = this;
        Refresh();
    }

    // ---------------- TIME PASSING ----------------

    // Moves the clock on by sim time (so sim speed speeds the day up too), unless it's held.
    // Measured in days, so changing the day's length in the tools panel never makes the time jump.
    public void Advance(float simDt)
    {
        if (Held) return;
        Days += simDt / (SimTuning.Now.DayMinutes * 60.0);
    }

    // A new world: dawn on the first day of spring (or another hour of that day).
    public void Reset(float hour = StartHour) => Days = hour / 24.0;

    // Dev (tools panel): jump to an hour of today, to the next day, or to the start of the next season (same hour).
    public void SetHour(float hour) => Days = Math.Floor(Days) + Math.Clamp(hour, 0f, 23.99f) / 24.0;
    public void NextDay() => Days += 1.0;
    public void NextSeason() => Days += DaysPerSeason - DayIndex % DaysPerSeason;

    // ---------------- CALENDAR ----------------

    private int DayIndex => (int)Math.Floor(Days);                     // 0 on Day 1
    public int DayNumber => DayIndex + 1;
    public int Year => DayIndex / DaysPerYear + 1;
    public Season Season => (Season)(DayIndex / DaysPerSeason % 4);
    public int DayOfSeason => DayIndex % DaysPerSeason + 1;

    public DayPhase Phase
    {
        get
        {
            if (SunElevation >= HorizonAngle) return DayPhase.Day;
            if (SunElevation <= FirstGlowAngle) return DayPhase.Night;
            return Hour < 12f ? DayPhase.Dawn : DayPhase.Dusk;
        }
    }

    // "18:42"
    public string TimeText
    {
        get
        {
            int minutes = (int)(Hour * 60f);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }

    // "Day 3, Spring"
    public string DateText => $"Day {DayNumber}, {Season}";

    // "Day 3 18:42", for the Bird Log
    public string Stamp => $"Day {DayNumber} {TimeText}";

    // ---------------- THE SUN ----------------

    // Works out the hour, the sun and the light from Days. Called every frame, also while paused,
    // so the tools panel's hour slider shows at once.
    public void Refresh()
    {
        double dayStart = Math.Floor(Days);
        Hour = (float)((Days - dayStart) * 24.0);

        // The sun's height over the year: 0 at the spring and autumn starts, highest at the start of summer
        float yearShare = (float)(Days / DaysPerYear);
        float declination = Tilt * MathF.Sin(yearShare * MathF.Tau);

        SunElevation = Elevation(Hour, declination, out float azimuth);
        SunDirection = Toward(SunElevation, azimuth);
        Sunrise = 12f - HalfDayHours(HorizonAngle, declination);
        Sunset = 12f + HalfDayHours(HorizonAngle, declination);
        FirstGlow = 12f - HalfDayHours(FirstGlowAngle, declination);
        LastGlow = 12f + HalfDayHours(FirstGlowAngle, declination);

        // The moon: its phase from the calendar; it crosses the sky MoonPhase of a day behind the sun, and at full moon
        // stands opposite it (low in summer, high in winter, like the real one)
        MoonPhase = (float)(((Days / MoonDays + MoonStartPhase) % 1.0 + 1.0) % 1.0);
        MoonLit = (1f - MathF.Cos(MoonPhase * MathF.Tau)) * 0.5f;
        float moonElevation = Elevation(Hour - MoonPhase * 24f, declination * MathF.Cos(MoonPhase * MathF.Tau), out float moonAzimuth);
        MoonSkyDirection = Toward(moonElevation, moonAzimuth);

        Light = LightFor(SunElevation, azimuth, MoonLit);
    }

    // The phase's name, for the tools panel.
    public string MoonName
    {
        get
        {
            string[] names = { "New moon", "Waxing crescent", "First quarter", "Waxing gibbous", "Full moon", "Waning gibbous", "Last quarter", "Waning crescent" };
            return names[(int)MathF.Round(MoonPhase * 8f) % 8];
        }
    }

    // The sun's height above the horizon (degrees) at this hour, and its compass bearing
    // (radians, 0 = due south, + toward the west). Solar time: the sun is highest at 12:00.
    private static float Elevation(float hour, float declination, out float azimuth)
    {
        float lat = Latitude * MathF.PI / 180f;
        float dec = declination * MathF.PI / 180f;
        float hourAngle = (hour - 12f) * 15f * MathF.PI / 180f;

        float sinEl = MathF.Sin(lat) * MathF.Sin(dec) + MathF.Cos(lat) * MathF.Cos(dec) * MathF.Cos(hourAngle);
        azimuth = MathF.Atan2(MathF.Sin(hourAngle), MathF.Cos(hourAngle) * MathF.Sin(lat) - MathF.Tan(dec) * MathF.Cos(lat));
        return MathF.Asin(Math.Clamp(sinEl, -1f, 1f)) * 180f / MathF.PI;
    }

    // How many hours from noon until the sun sinks to this angle (so noon minus this is when it rises past it).
    private static float HalfDayHours(float angle, float declination)
    {
        float lat = Latitude * MathF.PI / 180f;
        float dec = declination * MathF.PI / 180f;
        float cosH = (MathF.Sin(angle * MathF.PI / 180f) - MathF.Sin(lat) * MathF.Sin(dec)) / (MathF.Cos(lat) * MathF.Cos(dec));
        return MathF.Acos(Math.Clamp(cosH, -1f, 1f)) * 180f / MathF.PI / 15f;
    }

    // The light for a sun this high: the blended key colors, from the sun's direction by day and the moon's by night.
    // Night is brightened or darkened by SimTuning.NightBrightness and, a little, by the moon's phase (moonLit, 0 to 1);
    // the day isn't touched.
    private static DayLight LightFor(float elevation, float azimuth, float moonLit)
    {
        Blend(elevation, out Vector3 ambient, out Vector3 direct, out Vector3 shade, out float golden);

        float day = Smooth(FirstGlowAngle, -2f, elevation);
        Vector3 moonlight = Vector3.Lerp(NewMoonTint * NewMoonLight, new Vector3(FullMoonLight), moonLit);
        Vector3 nightScale = Vector3.Lerp(moonlight * SimTuning.Now.NightBrightness, Vector3.One, day);

        // Toward the sun, but never lower than LowSunLift; north is -Z, east is +X
        Vector3 toSun = Toward(MathF.Max(elevation, LowSunLift), azimuth);
        float sunShare = Smooth(FirstGlowAngle, 0f, elevation);    // the light swings slowly from the moon's side to the sun's
        Vector3 direction = Vector3.Normalize(Vector3.Lerp(MoonDirection, toSun, sunShare));

        return new DayLight
        {
            Ambient = ambient * nightScale,
            Direct = direct * nightScale,
            Direction = direction,
            Darkness = 1f - Smooth(FirstGlowAngle, 2f, elevation),
            Shade = shade,
            Golden = golden,
        };
    }

    // The direction toward something this high (degrees) on this compass bearing (radians, 0 = due south, + toward the west).
    // North is -Z, east is +X.
    private static Vector3 Toward(float elevation, float azimuth)
    {
        float lift = elevation * MathF.PI / 180f;
        return new Vector3(-MathF.Sin(azimuth) * MathF.Cos(lift), MathF.Sin(lift), MathF.Cos(azimuth) * MathF.Cos(lift));
    }

    // The key values at this elevation, blended between the keys on either side.
    private static void Blend(float elevation, out Vector3 ambient, out Vector3 direct, out Vector3 shade, out float golden)
    {
        var below = LightKeys[0];
        var above = LightKeys[0];
        float t = 0f;
        if (elevation >= LightKeys[^1].Elevation)
        {
            below = above = LightKeys[^1];
        }
        else if (elevation > LightKeys[0].Elevation)
        {
            int i = 1;
            while (elevation > LightKeys[i].Elevation) i++;
            below = LightKeys[i - 1];
            above = LightKeys[i];
            t = (elevation - below.Elevation) / (above.Elevation - below.Elevation);
        }
        ambient = Vector3.Lerp(below.Ambient, above.Ambient, t);
        direct = Vector3.Lerp(below.Direct, above.Direct, t);
        shade = Vector3.Lerp(below.Shade, above.Shade, t);
        golden = float.Lerp(below.Golden, above.Golden, t);
    }

    private static float Smooth(float from, float to, float value)
    {
        float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    // ---------------- WHEN BIRDS ARE ABOUT ----------------

    // Whether a bird whose hours are moved by this much (Early Risers earlier) should be roosting now.
    public bool IsBedtime(float shift)
    {
        float wake = Sunrise - WakeBeforeSunrise + shift;
        float roost = Sunset + RoostAfterSunset + shift;
        return Hour < wake || Hour > roost;
    }

    // Whether a Night Owl whose hours are moved by this much should be asleep now: through the middle of the day.
    public bool IsOwlBedtime(float shift)
    {
        float sleep = Sunrise + OwlSleepAfterSunrise + shift;
        float wake = Sunset - OwlWakeBeforeSunset + shift;
        return Hour > sleep && Hour < wake;
    }

    // How many day birds arrive, 0 to 1: none at night, building up through dawn, thinning out toward dusk.
    public float ArrivalShare
    {
        get
        {
            float morning = Smooth(Sunrise - WakeBeforeSunrise, Sunrise, Hour);
            float evening = 1f - Smooth(Sunset - QuietBeforeSunset, Sunset + RoostAfterSunset, Hour);
            return MathF.Min(morning, evening);
        }
    }

    // The dawn chorus: from just before waking until a while after sunrise.
    public bool IsDawnChorus => Hour >= Sunrise - WakeBeforeSunrise - 0.75f && Hour <= Sunrise + ChorusAfterSunrise;

    // Toward evening birds call less and less.
    public bool IsQuietEvening => Hour >= Sunset - QuietBeforeSunset && Hour <= Sunset + RoostAfterSunset + 1f;
}
