using Raylib_cs;

// The forest heard from inside the garden: a quiet looping bed of leaves and distant birds, with single far-off
// calls and rustles dropped in at random. Silent in the room view, heard only standing in the Hologram View,
// and only by day (night gets its own sounds later). Files: resources/sounds/ambience/forest_day_bed.ogg and
// forest_day_1.wav, forest_day_2.wav, ... Played from Sounds.Update; none of the files are required.
public static class Ambience
{
    // LOUDNESS. The files are already quiet (the bed sits at about -31 dBFS), so these stay near 1 at most.
    private const float BedVolume = 0.6f;
    private const float ShotVolume = 0.7f;
    private const float QuietShare = 0.4f;         // while the Esc menu or the Designer is open
    private const float RainDuck = 0.8f;           // how much of it a downpour covers up
    private const float RadioDuck = 0.5f;          // how much a loud radio covers up
    private const float FadeRate = 1.5f;           // how quickly it eases in and out, per second
    private const float AudibleBelow = 0.002f;     // below this the stream is stopped to save work

    // THE ONE-SHOTS: a far-off call or rustle at a random gap, each a little different
    private const float GapMin = 8f;
    private const float GapMax = 30f;
    private const float FirstGapMin = 4f;          // the first one after you step in comes sooner
    private const float PitchSpread = 0.05f;       // up to this far above or below the recorded pitch
    private const float ShotVolumeMin = 0.5f;      // each plays at this share of full to full
    private const float PanSpread = 0.35f;         // how far from the centre (0.5) it can sit

    private static Music bed;
    private static Sound[] shots = Array.Empty<Sound>();
    private static bool hasBed;
    private static bool playing;                   // the bed stream is running
    private static float level;                    // how much of full it is right now, 0 to 1
    private static float shotWait;                 // seconds until the next one-shot
    private static int lastShot = -1;

    // Loads the bed and every forest_day_N.wav that is there. Called once the audio device is open.
    public static void Load()
    {
        string bedFile = Assets.PathTo("sounds/ambience/forest_day_bed.ogg");
        hasBed = File.Exists(bedFile);
        if (!hasBed) return;

        bed = Raylib.LoadMusicStream(bedFile);
        Raylib.SetMusicVolume(bed, 0f);

        var found = new List<Sound>();
        for (int i = 1; File.Exists(Assets.PathTo($"sounds/ambience/forest_day_{i}.wav")); i++)
        {
            found.Add(Raylib.LoadSound(Assets.PathTo($"sounds/ambience/forest_day_{i}.wav")));
        }
        shots = found.ToArray();
    }

    // Frees everything Load made.
    public static void Unload()
    {
        if (!hasBed) return;
        Raylib.UnloadMusicStream(bed);
        foreach (Sound shot in shots) Raylib.UnloadSound(shot);
    }

    // Called every frame from Sounds.Update.
    // observer: 0 in the room view, 1 standing in the garden. daylight: 1 by day, fading to 0 through dusk.
    // rain: how hard it's raining (0 to 1). soundShare: the birds' share (the Birds volume, 0 in the store or with the box off).
    // radioHeard: how loud the radio is right now (0 to 1).
    public static void Update(float dt, float observer, float daylight, float rain, bool quiet, float soundShare, float radioHeard)
    {
        if (!hasBed) return;

        float target = TargetLevel(observer, daylight, rain, quiet, soundShare, radioHeard);
        level += (target - level) * MathF.Min(1f, FadeRate * dt);

        if (!playing && target > AudibleBelow) StartBed();
        else if (playing && target <= AudibleBelow && level < AudibleBelow) StopBed();
        if (!playing) return;

        Raylib.SetMusicVolume(bed, level * BedVolume);
        Raylib.UpdateMusicStream(bed);

        shotWait -= dt;
        if (shotWait <= 0f)
        {
            shotWait = Rng.Range(GapMin, GapMax);
            PlayShot();
        }
    }

    // How loud the forest wants to be right now, 0 to 1: only in the Hologram View, only by day, covered by rain and the radio.
    private static float TargetLevel(float observer, float daylight, float rain, bool quiet, float soundShare, float radioHeard)
    {
        float target = observer * daylight * MathF.Min(1f, soundShare);
        target *= 1f - RainDuck * rain;
        target *= 1f - RadioDuck * radioHeard;
        if (quiet) target *= QuietShare;
        return Math.Clamp(target, 0f, 1f);
    }

    // Starts the bed from a random spot, so no two visits to the garden begin the same.
    private static void StartBed()
    {
        playing = true;
        Raylib.SetMusicVolume(bed, 0f);
        Raylib.PlayMusicStream(bed);
        Raylib.SeekMusicStream(bed, Rng.Range(0f, Raylib.GetMusicTimeLength(bed)));
        shotWait = Rng.Range(FirstGapMin, GapMax);
    }

    // Stops the bed once it has faded out.
    private static void StopBed()
    {
        playing = false;
        Raylib.StopMusicStream(bed);
    }

    // One random far-off call or rustle (never the same as the last), at a slightly different pitch, loudness and side.
    private static void PlayShot()
    {
        if (shots.Length == 0 || level < 0.05f) return;

        int pick = Rng.Next(shots.Length);
        if (shots.Length > 1 && pick == lastShot) pick = (pick + 1) % shots.Length;
        lastShot = pick;

        Sound shot = shots[pick];
        Raylib.SetSoundPitch(shot, 1f + Rng.Range(-PitchSpread, PitchSpread));
        Raylib.SetSoundVolume(shot, ShotVolume * level * Rng.Range(ShotVolumeMin, 1f));
        Raylib.SetSoundPan(shot, 0.5f + Rng.Range(-PanSpread, PanSpread));
        Raylib.PlaySound(shot);
    }
}
