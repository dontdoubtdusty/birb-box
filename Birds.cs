using Raylib_cs;

// The built-in species, one entry each: name, looks, size, color, common or not, then the seeds it eats,
// the feeders it uses and how its personalities lean. Their keys must never change (saves and sound files use them).
// To add a built-in bird: give it an entry here and add it to Birds.builtIn below.
public partial class Species
{
    public static readonly Species Emberwing = new(
        "Emberwing", "Emberwing", new[] { StarterBirbs.EmberwingCock, StarterBirbs.EmberwingHen },
        0.33f, new Color(227, 74, 74, 255), common: true,
        tastes: new() { [SeedType.Corn] = 1.0f, [SeedType.Wild] = 0.4f },
        feeders: new() { [FeederStyle.Tray] = 0.8f, [FeederStyle.Tube] = 0.8f },
        leans: new() { [Trait.StoutHearted] = 2.5f, [Trait.FaintHearted] = 0.4f, [Trait.TownCrier] = 1.5f, [Trait.EarlyRiser] = 1.5f, [Trait.RainProud] = 1.5f },
        featherName: "Ember Plume");

    public static readonly Species DuskDove = new(
        "DuskDove", "Dusk Dove", new[] { StarterBirbs.DuskDove },
        0.4f, new Color(212, 209, 199, 255), common: true,
        tastes: new() { [SeedType.Millet] = 1.0f, [SeedType.Wild] = 0.4f },
        feeders: new() { [FeederStyle.Tray] = 1.0f },
        leans: new() { [Trait.FaintHearted] = 2.5f, [Trait.Wary] = 1.5f, [Trait.StoutHearted] = 0.4f, [Trait.Hushed] = 1.5f, [Trait.NightOwl] = 2.5f, [Trait.EarlyRiser] = 0.5f, [Trait.RainProud] = 0.5f },
        featherName: "Dusk Down");

    public static readonly Species Gildfinch = new(
        "Gildfinch", "Gildfinch", new[] { StarterBirbs.Gildfinch },
        0.2f, new Color(240, 158, 38, 255), common: true,
        tastes: new() { [SeedType.Millet] = 0.3f, [SeedType.Thistle] = 1.0f, [SeedType.Wild] = 0.4f },
        feeders: new() { [FeederStyle.Tray] = 0.4f, [FeederStyle.Tube] = 1.0f },
        leans: new() { [Trait.Dainty] = 2f, [Trait.TownCrier] = 1.5f, [Trait.Wayfarer] = 1.5f, [Trait.EarlyRiser] = 1.5f },
        featherName: "Gild Quill");

    // Rare: only Blue Barley draws it until the first one has been seen (Birds.IsDiscovered).
    public static readonly Species Frostcrest = new(
        "Frostcrest", "Frostcrest", new[] { StarterBirbs.Frostcrest },
        0.33f, new Color(80, 130, 240, 255), common: false,
        tastes: new() { [SeedType.Corn] = 0.05f, [SeedType.BlueBarley] = 1.0f },
        feeders: new() { [FeederStyle.Tray] = 0.6f, [FeederStyle.Tube] = 0.6f },
        leans: new() { [Trait.Wayfarer] = 2f, [Trait.Hushed] = 2f, [Trait.Wary] = 1.5f, [Trait.NightOwl] = 1.5f, [Trait.RainProud] = 2f },
        lure: SeedType.BlueBarley, featherName: "Rime Feather");
}

// Every species in the game (built-in, then the Designer's from resources/species), finding one by its key,
// which rare species have been seen, and adding Designer saves to their species' looks.
public static class Birds
{
    private static readonly Species[] builtIn = { Species.Emberwing, Species.DuskDove, Species.Gildfinch, Species.Frostcrest };

    // In this order everywhere a list of species is shown (the Designer's dropdown, the pedestal LEDs):
    // the built-in ones first, then the Designer's in A-Z order of their names.
    private static readonly List<Species> all = new(builtIn);

    public static IReadOnlyList<Species> All => all;

    // Goes up whenever the list, a species' details or the seen set changes, so cached lists know to refresh.
    public static int Version { get; private set; }

    // Common species that have a look to wear (a Designer species with no design yet can't show up).
    public static List<Species> Common => all.Where(species => species.Common && species.CanAppear).ToList();

    private const float MinDesignSize = 0.05f;    // a hand-edited file with a size of 0 still gets a visible bird

    // The species with this key (capital letters ignored), or null if there's none.
    public static Species? Find(string? key) =>
        key == null ? null : all.Find(species => SameKey(species.Key, key));

    public static bool SameKey(string? a, string? b) =>
        a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // ---------------- RARE SPECIES ----------------

    // The keys of rare species seen in the view at least once (saved with the world).
    private static readonly HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

    // A common species, or a rare one with no lure, or one already seen: all its tastes count.
    // Otherwise only its lure draws it in.
    public static bool IsDiscovered(Species species) =>
        species.Common || species.Lure == null || seen.Contains(species.Key);

    public static void MarkSeen(Species species)
    {
        if (seen.Add(species.Key)) Version++;
    }

    public static List<string> SaveSeen() => seen.Order().ToList();

    public static void RestoreSeen(IEnumerable<string> keys)
    {
        seen.Clear();
        foreach (string key in keys) seen.Add(key);
        Version++;
    }

    // ---------------- DESIGNER SPECIES ----------------

    // Reads every species file, then every Birb Designer save. Called once at start, before the world loads.
    public static void LoadAll()
    {
        foreach (string key in SpeciesFiles.List())
        {
            SpeciesFile? file = SpeciesFiles.Load(key);
            if (file == null) continue;
            if (builtIn.Any(species => SameKey(species.Key, key)))
            {
                EventLog.Add($"Species file {key}.json has the same name as a built-in species, so it was left out.");
                continue;
            }
            file.Clean();
            Add(new Species(file));
        }
        LoadDesigns();
    }

    // Adds a Designer species to the list, in its place by name.
    public static void Add(Species species)
    {
        all.Add(species);
        Sort();
    }

    // After a Designer species was edited: its details are already changed, so only the order and caches need to follow.
    public static void Changed() => Sort();

    private static void Sort()
    {
        List<Species> designed = all.Where(species => !species.BuiltIn).OrderBy(species => species.Name, StringComparer.OrdinalIgnoreCase).ToList();
        all.Clear();
        all.AddRange(builtIn);
        all.AddRange(designed);
        Version++;
    }

    // ---------------- DESIGNER SAVES ----------------

    private static void LoadDesigns()
    {
        foreach (string file in BirbFiles.List())
        {
            BirbFile? design = BirbFiles.Load(file);
            if (design != null) AddDesign(design, file);
        }
    }

    // Puts a saved design among its species' looks, replacing the look from the same file (in whichever species had it).
    // The look gets its own copy of the shape, so further editing in the Designer leaves the world alone until saved again.
    // Null (and a line in the log) if the design names a species that isn't there.
    public static BirdLook? AddDesign(BirbFile design, string file)
    {
        foreach (Species species in all)
        {
            species.Looks.RemoveAll(look => SameFile(look.File, file));
        }

        Species? kind = Find(design.Species);
        if (kind == null)
        {
            EventLog.Add($"{file}.json is a {design.Species}, but there's no such species file, so it stays out of the cube.");
            return null;
        }

        BirdLook added = new BirdLook(design.Shape.Copy(), MathF.Max(MinDesignSize, design.Size), file);
        kind.Looks.Add(added);
        Version++;
        return added;
    }

    // File names ignore capital letters, like Windows does.
    public static bool SameFile(string? a, string? b) =>
        a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
