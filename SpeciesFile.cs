using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

// One Designer species, as saved: everything about the kind of bird except its looks (those are the designs in
// resources/birbs that name this species). Each species is one .json file in resources/species/, named by its key.
// Plain text, so it can be read and carefully hand-edited. Delete the file and the species is gone from the game.
public class SpeciesFile
{
    public int FormatVersion = 1;
    public string Key = "";                       // never changes once made: designs, saves and sound files point at it
    public string Name = "";
    public float Size = 0.33f;                    // where a new design of it starts
    public Color MapColor = new Color(200, 200, 200, 255);
    public bool Common = true;
    public SeedType? Lure;                        // a rare species' lure (see Species.Lure); ignored when Common
    public Dictionary<SeedType, float> Tastes = new();
    public Dictionary<FeederStyle, float> Feeders = new();
    public Dictionary<Trait, float> Leans = new();
    public string? Voice;                         // the built-in species whose calls it borrows
    public float VoicePitch = 1f;

    // A copy of a species' details, for editing or as the start of a new one.
    public static SpeciesFile From(Species species) => new()
    {
        Key = species.Key,
        Name = species.Name,
        Size = species.Size,
        MapColor = species.Color,
        Common = species.Common,
        Lure = species.Lure,
        Tastes = new(species.Tastes),
        Feeders = new(species.Feeders),
        Leans = new(species.Leans),
        Voice = species.BuiltIn ? species.Key : species.VoiceKey,
        VoicePitch = species.BuiltIn ? 1f : species.VoicePitch,
    };

    public SpeciesFile Copy()
    {
        SpeciesFile copy = (SpeciesFile)MemberwiseClone();
        copy.Tastes = new(Tastes);
        copy.Feeders = new(Feeders);
        copy.Leans = new(Leans);
        return copy;
    }

    // Keeps hand-edited numbers in range and drops what makes no sense: zero tastes and appeals, a lure it doesn't eat.
    public void Clean()
    {
        Name = Name.Trim();
        if (Name.Length == 0) Name = Key;
        Size = Math.Clamp(Size, 0.1f, 0.6f);
        MapColor = new Color(MapColor.R, MapColor.G, MapColor.B, (byte)255);
        Tastes = Tastes.Where(pair => pair.Value > 0f).ToDictionary(pair => pair.Key, pair => Math.Min(pair.Value, 1f));
        Feeders = Feeders.Where(pair => pair.Value > 0f).ToDictionary(pair => pair.Key, pair => Math.Min(pair.Value, 1f));
        Leans = Leans.Where(pair => pair.Value != 1f).ToDictionary(pair => pair.Key, pair => Math.Clamp(pair.Value, 0f, 4f));
        if (Common || Lure is not SeedType lure || !Tastes.ContainsKey(lure)) Lure = Common ? null : BestSeed();
        VoicePitch = Math.Clamp(VoicePitch, 0.5f, 2f);
        if (Birds.Find(Voice) is not { BuiltIn: true }) Voice = Species.Emberwing.Key;
    }

    // The seed it likes most, or null if it eats nothing.
    public SeedType? BestSeed() =>
        Tastes.Count == 0 ? null : Tastes.MaxBy(pair => pair.Value).Key;
}

// Finding, saving and loading species files. Laid out like BirbFiles.
public static class SpeciesFiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    // Next to the designs: the project's resources/species while developing, the game's own resources folder otherwise.
    public static string Folder { get; } = Assets.ProjectFolder != null
        ? Path.Combine(Assets.ProjectFolder, "resources", "species")
        : Assets.PathTo("species");

    // The species files' names (their keys), in A-Z order.
    public static List<string> List()
    {
        if (!Directory.Exists(Folder)) return new List<string>();
        return Directory.GetFiles(Folder, "*.json")
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Order()
            .ToList();
    }

    // A key made from a name: letters and digits only ("Verdewing", "MossThrush"), never one already taken.
    // Blank if the name has no letters or digits in it.
    public static string NewKey(string name)
    {
        string baseKey = string.Concat(name.Where(char.IsAsciiLetterOrDigit));
        if (baseKey.Length == 0) return "";
        string key = baseKey;
        for (int n = 2; Birds.Find(key) != null || File.Exists(PathFor(key)); n++) key = $"{baseKey}{n}";
        return key;
    }

    // Returns false (and says why in the log) if the file couldn't be written.
    public static bool Save(SpeciesFile species)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathFor(species.Key), JsonSerializer.Serialize(species, Options));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            EventLog.Add($"Couldn't save {species.Key}.json: {e.Message}");
            return false;
        }
    }

    // Returns null (and says why in the log) if the file can't be read as a species.
    // The key always comes from the file name, so renaming the file renames the key (and orphans its birds).
    public static SpeciesFile? Load(string key)
    {
        try
        {
            SpeciesFile? species = JsonSerializer.Deserialize<SpeciesFile>(File.ReadAllText(PathFor(key)), Options);
            if (species == null) return null;
            species.Key = key;
            species.Name ??= key;
            species.Tastes ??= new();
            species.Feeders ??= new();
            species.Leans ??= new();
            return species;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            EventLog.Add($"Couldn't open species file {key}.json: {e.Message}");
            return null;
        }
    }

    private static string PathFor(string key) => Path.Combine(Folder, key + ".json");
}
