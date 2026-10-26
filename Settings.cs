using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

// The player's settings, kept between runs in their own small file next to the save (birbcube-settings.json).
// Separate from the world save, so starting a New world keeps them.
// Read once on start; written when the Esc menu closes and on quit.
// Also holds the tools panel's numbers: the SimTuning values, the hologram look, and where the radio is tuned.
public static class Settings
{
    public const string FileName = "birbcube-settings.json";
    public static string FilePath => Path.Combine(Assets.ProjectFolder ?? AppContext.BaseDirectory, FileName);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IncludeFields = true,                              // SimTuning and Vector3 keep their numbers in fields
        Converters = { new JsonStringEnumConverter() },    // "Named" instead of 1
    };

    // The settings as the game starts, before the file is read. The tools panel shows these as the hologram sliders' defaults.
    public static SettingsData Defaults { get; private set; } = new();

    // No file yet means the defaults, which the game already starts with.
    public static void Load(GameEngine engine)
    {
        Defaults = Capture(engine);
        Defaults.Tuning = SimTuning.Defaults;    // not the live values, which change as you play
        if (!File.Exists(FilePath)) return;
        try
        {
            SettingsData data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath), Options) ?? new SettingsData();
            Apply(data, engine);
        }
        catch (Exception e)
        {
            EventLog.Add($"Your settings couldn't be read ({e.Message}), so the defaults are in use.");
        }
    }

    // Like the world save: a temporary file first, then swapped in.
    public static void Save(GameEngine engine)
    {
        try
        {
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Capture(engine), Options));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e)
        {
            EventLog.Add($"Your settings couldn't be saved: {e.Message}");
        }
    }

    private static SettingsData Capture(GameEngine engine)
    {
        return new SettingsData
        {
            MasterVolume = Sounds.Master,
            HumVolume = Sounds.HumLevel,
            BirdVolume = Sounds.BirdLevel,
            EffectsVolume = Sounds.EffectsLevel,
            RadioVolume = engine.Radio.Player.Volume,
            Fullscreen = engine.IsFullscreen,
            TrueFullscreen = engine.TrueFullscreen,
            NameTags = engine.Renderer.ChatMessages.NameLabels,
            HandTone = engine.Hands.Tone,
            HandYellow = engine.Hands.Yellow,
            Tuning = SimTuning.Now,
            HoloColor = engine.Renderer.HoloColor,
            Glow = engine.Renderer.GlowStrength,
            Scanlines = engine.Renderer.ScanlineStrength,
            Quality = engine.Renderer.Quality,
            ShowProjector = engine.Renderer.ShowProjector,
            CubeWidth = engine.View.CubeScale,
            FogStart = engine.View.FogStart,
            RadioDial = engine.Radio.Player.Tuning,
        };
    }

    // Numbers are kept in range in case the file was edited by hand.
    private static void Apply(SettingsData data, GameEngine engine)
    {
        Sounds.Master = Math.Clamp(data.MasterVolume, 0f, 1f);
        Sounds.HumLevel = Math.Clamp(data.HumVolume, 0f, 1f);
        Sounds.BirdLevel = Math.Clamp(data.BirdVolume, 0f, 1f);
        Sounds.EffectsLevel = Math.Clamp(data.EffectsVolume, 0f, 1f);
        engine.Radio.Player.Volume = Math.Clamp(data.RadioVolume, 0f, 1f);
        engine.Renderer.ChatMessages.NameLabels = data.NameTags;
        engine.Hands.Tone = Math.Clamp(data.HandTone, 0f, 1f);
        engine.Hands.Yellow = data.HandYellow;

        SimTuning.Now = data.Tuning;
        SimTuning.Now.Clamp();
        engine.Renderer.HoloColor = Vector3.Clamp(data.HoloColor, Vector3.Zero, Vector3.One);
        engine.Renderer.GlowStrength = Math.Clamp(data.Glow, 0f, 3f);
        engine.Renderer.ScanlineStrength = Math.Clamp(data.Scanlines, 0f, 0.5f);
        engine.Renderer.Quality = Math.Clamp(data.Quality, 1f, 2f);
        engine.Renderer.ShowProjector = data.ShowProjector;
        engine.View.CubeScale = Math.Clamp(data.CubeWidth, 0.5f, ViewWindow.MaxCubeScale);
        engine.View.FogStart = Math.Clamp(data.FogStart, 0.3f, 0.99f);
        if (data.RadioDial is float dial) engine.Radio.Player.Tuning = Math.Clamp(dial, 0f, 1f);

        engine.SetTrueFullscreen(data.TrueFullscreen);
        if (data.Fullscreen != engine.IsFullscreen) engine.ToggleFullscreen();
    }
}

// What goes in the settings file. A setting missing from the file keeps the default given here.
public class SettingsData
{
    public float MasterVolume { get; set; } = 1f;
    public float HumVolume { get; set; } = 1f;
    public float BirdVolume { get; set; } = 1f;
    public float EffectsVolume { get; set; } = 1f;
    public float RadioVolume { get; set; } = 0.5f;
    public bool Fullscreen { get; set; } = false;
    public bool TrueFullscreen { get; set; } = false;
    public NameLabelMode NameTags { get; set; } = NameLabelMode.Named;
    public float HandTone { get; set; } = 0.5f;
    public bool HandYellow { get; set; } = false;

    // Tools panel (Tab)
    public SimTuning Tuning { get; set; } = new();
    public Vector3 HoloColor { get; set; } = new Vector3(0.36f, 0.86f, 0.82f);
    public float Glow { get; set; } = 0.7f;
    public float Scanlines { get; set; } = 0.12f;
    public float Quality { get; set; } = 2f;          // hologram quality, 1 to 2 (Esc menu Settings)
    public bool ShowProjector { get; set; } = true;
    public float CubeWidth { get; set; } = 0.7f;
    public float FogStart { get; set; } = 0.97f;
    public float? RadioDial { get; set; } = null;    // 0..1 along the dial; missing means the radio's own start
}
