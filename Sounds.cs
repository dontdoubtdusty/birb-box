using System.Numerics;
using Raylib_cs;

// Every sound in the game is loaded and played from here, except the radio's music and static (see RadioPlayer).
public static class Sounds
{
    // PLAYER VOLUME (the Settings page in the Esc menu). 0 = silent, 1 = full.
    public static float Master = 1f;
    public static float HumLevel = 1f;
    public static float BirdLevel = 1f;
    public static float EffectsLevel = 1f;    // coins, dial clicks, buttons and the projector

    private static Sound dialClick;
    private static Sound coinLand;
    private static Sound coinPickup;
    private static Sound radioButton;
    private static Sound projectorStart;
    private static Sound projectorStop;
    private const float ProjectorVolume = 0.5f;
    private static float projectorWait = -1f;      // seconds until a delayed projector sound starts; below 0 when none is waiting
    private static bool projectorWaitingOn;        // which one is waiting: start (true) or stop

    // The power button (the logo). Your own sounds/power_click.wav if it's there, otherwise the radio's button click.
    // The projector's whir starts PowerClickLead seconds after it, so the click is heard on its own first.
    private static Sound powerClick;
    private static bool hasPowerClick;
    private const float PowerClickVolume = 0.8f;
    public const float PowerClickLead = 0.1f;

    // The BIRBS / SHOP rocker. Your own sounds/rocker_click.wav if it's there, otherwise the radio's button click.
    private static Sound rockerClick;
    private static bool hasRockerClick;
    private const float RockerVolume = 0.8f;

    // The hum is streamed as Music, so it loops on its own without a gap
    private static Music holoHum;
    private const float HumVolume = 0.22f;
    private const float HumQuietVolume = 0.08f;    // while the Esc menu or the Designer is open
    private const float HumFadeRate = 3f;          // how quickly it eases between the two
    private static float humVolume = 0f;           // starts silent and fades in

    // BIRDS. Each species has one or more calls; see tools/make_sounds.py for how they're made.
    private static readonly Dictionary<BirdType, SoundPool[]> calls = new();
    private static SoundPool flapTakeoff = null!;
    private static SoundPool flapLand = null!;
    private static SoundPool[] pecks = null!;

    private const float CallVolume = 0.15f;
    private const float FlapVolume = 0.35f;
    private const float PeckVolume = 0.18f;

    // CROWD HUSH: a bird holds its call if another bird called within this distance in the last few seconds
    private const float HushRadius = 5f;
    private const float HushTime = 4f;
    private static readonly List<(Vector3 Position, double Time)> recentCalls = new();

    // Birds past the hologram's edge are heard softly, fading out over this share of its radius
    private const float OffscreenStartVolume = 0.4f;
    private const float FadeShare = 1f;

    // Bird sounds fade out while the store is showing (the birds keep living, just out of earshot)
    private static float birdVolume = 1f;

    public static void Load()
    {
        Raylib.InitAudioDevice();
        dialClick = Raylib.LoadSound(Assets.PathTo("sounds/dial_click.wav"));
        coinLand = Raylib.LoadSound(Assets.PathTo("sounds/coin_land.wav"));
        coinPickup = Raylib.LoadSound(Assets.PathTo("sounds/coin_pickup.wav"));
        radioButton = Raylib.LoadSound(Assets.PathTo("sounds/radio_button.wav"));
        projectorStart = Raylib.LoadSound(Assets.PathTo("sounds/projector_start.wav"));
        projectorStop = Raylib.LoadSound(Assets.PathTo("sounds/projector_stop.wav"));
        string rockerFile = Assets.PathTo("sounds/rocker_click.wav");
        hasRockerClick = File.Exists(rockerFile);
        if (hasRockerClick) rockerClick = Raylib.LoadSound(rockerFile);
        string powerFile = Assets.PathTo("sounds/power_click.wav");
        hasPowerClick = File.Exists(powerFile);
        if (hasPowerClick) powerClick = Raylib.LoadSound(powerFile);

        holoHum = Raylib.LoadMusicStream(Assets.PathTo("sounds/holo_hum.wav"));
        Raylib.SetMusicVolume(holoHum, 0f);
        Raylib.PlayMusicStream(holoHum);

        // Calls are found by name: call_emberwing_1.wav, call_emberwing_2.wav, and so on.
        foreach (BirdType type in Birds.All)
        {
            calls[type] = LoadNumbered($"call_{type.ToString().ToLowerInvariant()}");
        }
        flapTakeoff = new SoundPool("sounds/flap_takeoff.wav");
        flapLand = new SoundPool("sounds/flap_land.wav");
        pecks = LoadNumbered("peck");
    }

    // Every file named <prefix>_1.wav, <prefix>_2.wav, ... until a number is missing.
    private static SoundPool[] LoadNumbered(string prefix)
    {
        var found = new List<SoundPool>();
        for (int i = 1; File.Exists(Assets.PathTo($"sounds/{prefix}_{i}.wav")); i++)
        {
            found.Add(new SoundPool($"sounds/{prefix}_{i}.wav"));
        }
        if (found.Count == 0) EventLog.Add($"No sound files found for {prefix}.");
        return found.ToArray();
    }

    public static void Unload()
    {
        Raylib.UnloadSound(dialClick);
        Raylib.UnloadSound(coinLand);
        Raylib.UnloadSound(coinPickup);
        Raylib.UnloadSound(radioButton);
        Raylib.UnloadSound(projectorStart);
        Raylib.UnloadSound(projectorStop);
        if (hasRockerClick) Raylib.UnloadSound(rockerClick);
        if (hasPowerClick) Raylib.UnloadSound(powerClick);
        Raylib.UnloadMusicStream(holoHum);

        foreach (SoundPool[] pools in calls.Values)
        {
            foreach (SoundPool pool in pools) pool.Unload();
        }
        flapTakeoff.Unload();
        flapLand.Unload();
        foreach (SoundPool pool in pecks) pool.Unload();

        Raylib.CloseAudioDevice();
    }

    // Called every frame. Keeps the hum fed and eases its volume toward loud or quiet.
    // birdsShowing: false while the store is up or the box is off, which fades the birds out.
    // power: how far the projector is on (0 to 1); the hum follows it.
    public static void Update(float dt, bool quiet, bool birdsShowing, float power)
    {
        Raylib.SetMasterVolume(Master);

        float target = (quiet ? HumQuietVolume : HumVolume) * power;
        humVolume += (target - humVolume) * MathF.Min(1f, HumFadeRate * dt);
        Raylib.SetMusicVolume(holoHum, humVolume * HumLevel);
        Raylib.UpdateMusicStream(holoHum);

        float birdTarget = birdsShowing ? 1f : 0f;
        birdVolume += (birdTarget - birdVolume) * MathF.Min(1f, HumFadeRate * dt);

        // A projector sound waiting behind the power button's click
        if (projectorWait >= 0f)
        {
            projectorWait -= dt;
            if (projectorWait < 0f) StartProjectorSound(projectorWaitingOn);
        }
    }

    // The bezel ring passing a notch (see Dial)
    public static void PlayDialClick()
    {
        Raylib.SetSoundVolume(dialClick, EffectsLevel);
        Raylib.PlaySound(dialClick);
    }

    // The Birb Box switching on (a click and a rising whir) or off (a click and a falling one).
    // Whichever is playing (or waiting) stops, so switching quickly never stacks them.
    // delay: seconds before it starts (the power button's click goes first).
    public static void PlayProjector(bool on, float delay = 0f)
    {
        Raylib.StopSound(projectorStart);
        Raylib.StopSound(projectorStop);
        projectorWait = -1f;
        if (delay > 0f)
        {
            projectorWait = delay;
            projectorWaitingOn = on;
        }
        else StartProjectorSound(on);
    }

    private static void StartProjectorSound(bool on)
    {
        Sound sound = on ? projectorStart : projectorStop;
        Raylib.SetSoundVolume(sound, ProjectorVolume * EffectsLevel);
        Raylib.PlaySound(sound);
    }

    // The power button (the logo) going in
    public static void PlayPowerClick()
    {
        if (hasPowerClick) PlayVaried(powerClick, PowerClickVolume);
        else PlayVaried(radioButton, 0.7f);
    }

    // A push button on the radio going down
    public static void PlayRadioButton() => PlayVaried(radioButton, 0.7f);

    // The rocker switch snapping over to the other scene
    public static void PlayRocker()
    {
        if (hasRockerClick) PlayVaried(rockerClick, RockerVolume);
        else PlayVaried(radioButton, 0.7f);
    }

    // A bird's coin hitting the dirt, and the player collecting one.
    // A small random pitch shift keeps a run of coins from sounding identical.
    public static void PlayCoinLand() => PlayVaried(coinLand, 0.5f);
    public static void PlayCoinPickup() => PlayVaried(coinPickup, 0.6f);

    private static void PlayVaried(Sound sound, float volume)
    {
        Raylib.SetSoundPitch(sound, Rng.Range(0.94f, 1.06f));
        Raylib.SetSoundVolume(sound, volume * EffectsLevel);
        Raylib.PlaySound(sound);
    }

    // ---------------- BIRDS ----------------

    // One of the bird's species calls, at its own voice pitch. Remembered for the crowd hush, even when too far to hear.
    public static void PlayCall(Bird bird)
    {
        recentCalls.Add((bird.Position, Raylib.GetTime()));
        SoundPool[] options = calls[bird.Type];
        if (options.Length == 0) return;
        PlayBird(options[Rng.Next(options.Length)], bird.Position, CallVolume, bird.VoicePitch * Rng.Range(0.97f, 1.03f));
    }

    // True if some bird called within HushRadius of this spot in the last HushTime seconds.
    public static bool CalledNearby(Vector3 position)
    {
        double now = Raylib.GetTime();
        recentCalls.RemoveAll(call => now - call.Time > HushTime);
        foreach ((Vector3 calledAt, _) in recentCalls)
        {
            if (Vector3.Distance(calledAt, position) < HushRadius) return true;
        }
        return false;
    }

    // takeoff: true when leaving the ground, false when touching down
    public static void PlayFlap(Vector3 position, bool takeoff)
    {
        PlayBird(takeoff ? flapTakeoff : flapLand, position, FlapVolume, Rng.Range(0.9f, 1.1f));
    }

    public static void PlayPeck(Vector3 position)
    {
        if (pecks.Length == 0) return;
        PlayBird(pecks[Rng.Next(pecks.Length)], position, PeckVolume, Rng.Range(0.85f, 1.15f));
    }

    private static void PlayBird(SoundPool pool, Vector3 position, float volume, float pitch)
    {
        float heard = volume * HeardShare(position) * birdVolume * BirdLevel;
        if (heard < 0.01f) return;    // too far away to hear, so don't use up a voice
        pool.Play(heard, pitch);
    }

    // How loud something at this world position sounds: 1 inside the hologram,
    // then quieter the further out into the fog it is, and 0 well past the edge.
    private static float HeardShare(Vector3 worldPos)
    {
        ViewWindow view = GameEngine.Instance.View;
        float edge = ViewWindow.EdgeAmount(worldPos.X - view.Center.X, worldPos.Z - view.Center.Y, view.WorldHalfWidth);
        if (edge <= 1f) return 1f;

        float pastEdge = (edge - 1f) / FadeShare;
        return MathF.Max(0f, OffscreenStartVolume * (1f - pastEdge));
    }
}

// A Raylib Sound can only play once at a time; playing it again restarts it.
// A pool keeps a few "aliases" of the same sound (they share the loaded audio)
// and takes turns between them, so several birds can chirp at once.
public class SoundPool
{
    private const int Voices = 4;
    private readonly Sound source;
    private readonly Sound[] aliases = new Sound[Voices];
    private int next = 0;

    public SoundPool(string file)
    {
        source = Raylib.LoadSound(Assets.PathTo(file));
        for (int i = 0; i < Voices; i++) aliases[i] = Raylib.LoadSoundAlias(source);
    }

    public void Play(float volume, float pitch)
    {
        Sound voice = aliases[next];
        next = (next + 1) % Voices;
        Raylib.SetSoundVolume(voice, volume);
        Raylib.SetSoundPitch(voice, pitch);
        Raylib.PlaySound(voice);
    }

    public void Unload()
    {
        foreach (Sound alias in aliases) Raylib.UnloadSoundAlias(alias);
        Raylib.UnloadSound(source);
    }
}
