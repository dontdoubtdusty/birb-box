using System.Text.Json;
using System.Text.Json.Serialization;

// The player's settings, kept between runs in their own small file next to the save (birbbox-settings.json).
// Separate from the world save, so starting a New world keeps them.
// Read once on start; written when the Esc menu closes and on quit.
public static class Settings
{
    public const string FileName = "birbbox-settings.json";
    public static string FilePath => Path.Combine(Assets.ProjectFolder ?? AppContext.BaseDirectory, FileName);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },    // "Named" instead of 1
    };

    // No file yet means the defaults, which the game already starts with.
    public static void Load(GameEngine engine)
    {
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
}
