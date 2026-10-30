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

    // Plain midday, as the hologram looked before days existed. The Birb Designer always uses it.
    public static readonly DayLight Noon = new()
    {
        Ambient = new Vector3(0.45f),
        Direct = new Vector3(0.65f),
        Direction = Vector3.Normalize(new Vector3(0.4f, 1f, 0.3f)),
        Darkness = 0f,
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
    private static readonly Vector3 MoonDirection = Vector3.Normalize(new Vector3(-0.35f, 0.85f, -0.4f));
    private const float LowSunLift = 6f;            // the light never comes from lower than this, so low sun still lights the tops

    // WHEN BIRDS ARE ABOUT (hours, measured from sunrise and sunset; see Bird.UpdateRoosting)
    private const float WakeBeforeSunrise = 0.5f;   // birds wake and the dawn chorus starts this long before sunrise
    private const float RoostAfterSunset = 0.3f;    // and they settle this long after sunset
    private const float ChorusAfterSunrise = 1f;    // the dawn chorus runs until this long after sunrise
    private const float QuietBeforeSunset = 0.5f;   // calls thin out from this long before sunset

    // LIGHT KEYS: sun elevation (degrees) -> light from everywhere, light from the sun or moon.
    // In between two keys the colors blend. Midday (the last key) matches the look from before days existed.
    private static readonly (float Elevation, Vector3 Ambient, Vector3 Direct)[] LightKeys =
    {
        (-12f, new Vector3(0.20f, 0.25f, 0.42f), new Vector3(0.15f, 0.19f, 0.32f)),    // night: blue, with moonlight
        (-6f,  new Vector3(0.25f, 0.27f, 0.45f), new Vector3(0.12f, 0.12f, 0.20f)),    // the blue hour
        (-1f,  new Vector3(0.44f, 0.33f, 0.40f), new Vector3(0.38f, 0.22f, 0.16f)),    // rose glow at the horizon
        (4f,   new Vector3(0.44f, 0.38f, 0.34f), new Vector3(0.78f, 0.52f, 0.28f)),    // golden low sun
        (14f,  new Vector3(0.45f, 0.44f, 0.42f), new Vector3(0.70f, 0.63f, 0.52f)),    // warm morning or afternoon
        (28f,  new Vector3(0.45f, 0.45f, 0.45f), new Vector3(0.65f, 0.65f, 0.65f)),    // full day
    };

    // STATE (saved: Days)
    public double Days { get; set; } = StartHour / 24.0;
    public bool Held;    // dev (tools panel): the clock stands still

    // Worked out from Days in Refresh, every frame
    public float Hour { get; private set; }              // 0 to 24
    public float SunElevation { get; private set; }      // degrees above the horizon (negative below)
    public float Sunrise { get; private set; }           // hours, today
    public float Sunset { get; private set; }
    public float FirstGlow { get; private set; }         // the first glow before sunrise
    public float LastGlow { get; private set; }          // the last glow after sunset
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
        Sunrise = 12f - HalfDayHours(HorizonAngle, declination);
        Sunset = 12f + HalfDayHours(HorizonAngle, declination);
        FirstGlow = 12f - HalfDayHours(FirstGlowAngle, declination);
        LastGlow = 12f + HalfDayHours(FirstGlowAngle, declination);

        Light = LightFor(SunElevation, azimuth);
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
    // Night is brightened or darkened by SimTuning.NightBrightness (the day isn't touched).
    private static DayLight LightFor(float elevation, float azimuth)
    {
        Blend(elevation, out Vector3 ambient, out Vector3 direct);

        float day = Smooth(FirstGlowAngle, -2f, elevation);
        float nightScale = float.Lerp(SimTuning.Now.NightBrightness, 1f, day);

        // Toward the sun, but never lower than LowSunLift; north is -Z, east is +X
        float lift = MathF.Max(elevation, LowSunLift) * MathF.PI / 180f;
        Vector3 toSun = new Vector3(-MathF.Sin(azimuth) * MathF.Cos(lift), MathF.Sin(lift), MathF.Cos(azimuth) * MathF.Cos(lift));
        float sunShare = Smooth(FirstGlowAngle, 0f, elevation);    // the light swings slowly from the moon's side to the sun's
        Vector3 direction = Vector3.Normalize(Vector3.Lerp(MoonDirection, toSun, sunShare));

        return new DayLight
        {
            Ambient = ambient * nightScale,
            Direct = direct * nightScale,
            Direction = direction,
            Darkness = 1f - Smooth(FirstGlowAngle, 2f, elevation),
        };
    }

    // The two colors at this elevation, blended between the keys on either side.
    private static void Blend(float elevation, out Vector3 ambient, out Vector3 direct)
    {
        if (elevation <= LightKeys[0].Elevation)
        {
            ambient = LightKeys[0].Ambient;
            direct = LightKeys[0].Direct;
            return;
        }
        for (int i = 1; i < LightKeys.Length; i++)
        {
            if (elevation > LightKeys[i].Elevation) continue;
            var below = LightKeys[i - 1];
            var above = LightKeys[i];
            float t = (elevation - below.Elevation) / (above.Elevation - below.Elevation);
            ambient = Vector3.Lerp(below.Ambient, above.Ambient, t);
            direct = Vector3.Lerp(below.Direct, above.Direct, t);
            return;
        }
        ambient = LightKeys[^1].Ambient;
        direct = LightKeys[^1].Direct;
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
