using Raylib_cs;

// One look a bird of a species can have: a shape and how big it is in the world.
// Built-in looks come from StarterBirbs; the rest are Birb Designer saves (File is set for those).
public class BirdLook
{
    public BirbShape Shape;
    public float Size;          // birb units to world units, before each bird's own Build
    public string? File;        // the Designer save it came from (name without ".json"), or null for a built-in look

    public BirdLook(BirbShape shape, float size, string? file = null)
    {
        Shape = shape;
        Size = size;
        File = file;
    }
}

// Everything about one kind of bird: its key, what it's called, how it looks, what it eats, where it likes to eat,
// how its personalities lean, and how it sounds. The built-in species are in Birds.cs; Designer species come from
// their files in resources/species (SpeciesFile.cs). Birds.All is the list of every species.
// There is one Species object per kind of bird, so two birds are the same kind when their Species is the same object.
public partial class Species
{
    public readonly string Key;      // never shown: saves, sound file names and lookups use it, so renaming Name breaks nothing
    public readonly bool BuiltIn;    // written into the game (Birds.cs), as opposed to made in the Designer
    public string Name;              // shown to the player
    public List<BirdLook> Looks;     // each bird picks one at random; built-in looks first, then Designer saves
    public float Size;               // the species' own draw scale: birb units to world units (a new design starts at it)
    public Color Color;              // name text, map dots and pedestal LEDs
    public bool Common;              // drawn by wild seed and the "Call a bird" button; uncommon birds need special seed

    // A rare species' lure: until the first one has been seen in the view, only this seed draws it (Birds.IsDiscovered).
    // Null for common species. The Frostcrest's is Blue Barley.
    public SeedType? Lure;

    public Dictionary<SeedType, float> Tastes;      // how much it likes each seed, 0 to 1 (missing: won't eat it)
    public Dictionary<FeederStyle, float> Feeders;  // how much it likes eating at each feeder, 0 to 1 (missing: can't use it)
    public Dictionary<Trait, float> Leans;          // a trait's commonness is multiplied by this when one of its birds rolls (missing: 1)

    // Its voice: its own call files (call_<key>_1.wav...) if there are any, otherwise this built-in species' calls,
    // raised or lowered by VoicePitch. Built-in species have their own files, so these stay at the defaults.
    public string? VoiceKey;
    public float VoicePitch = 1f;

    // A built-in species, from the table in Birds.cs.
    public Species(string key, string name, BirbShape[] shapes, float size, Color color, bool common,
                   Dictionary<SeedType, float> tastes, Dictionary<FeederStyle, float> feeders, Dictionary<Trait, float> leans,
                   SeedType? lure = null)
    {
        Key = key;
        BuiltIn = true;
        Name = name;
        Looks = shapes.Select(shape => new BirdLook(shape, size)).ToList();
        Size = size;
        Color = color;
        Common = common;
        Lure = lure;
        Tastes = tastes;
        Feeders = feeders;
        Leans = leans;
    }

    // A Designer species, from its file. It has no looks until designs are saved for it.
    public Species(SpeciesFile file)
    {
        Key = file.Key;
        BuiltIn = false;
        Name = file.Name;
        Looks = new List<BirdLook>();
        Size = file.Size;
        Color = file.MapColor;
        Common = file.Common;
        Lure = file.Lure;
        Tastes = new(file.Tastes);
        Feeders = new(file.Feeders);
        Leans = new(file.Leans);
        VoiceKey = file.Voice;
        VoicePitch = file.VoicePitch;
    }

    // Takes on the details from an edited file (everything but the key and the looks). Designer species only.
    public void CopyFrom(SpeciesFile file)
    {
        Name = file.Name;
        Size = file.Size;
        Color = file.MapColor;
        Common = file.Common;
        Lure = file.Lure;
        Tastes = new(file.Tastes);
        Feeders = new(file.Feeders);
        Leans = new(file.Leans);
        VoiceKey = file.Voice;
        VoicePitch = file.VoicePitch;
    }

    // A bird of this kind can only be made once it has at least one look (a new Designer species has none yet).
    public bool CanAppear => Looks.Count > 0;

    // The looks written into the game, in their fixed order (saves count them by number).
    public List<BirdLook> BuiltInLooks => Looks.Where(look => look.File == null).ToList();

    // How much it likes this seed, 0 if not at all.
    public float TasteFor(SeedType seed) => Tastes.TryGetValue(seed, out float taste) ? taste : 0f;

    // True if it eats this seed at all.
    public bool Eats(SeedType seed) => TasteFor(seed) > 0f;

    // How much it likes eating at this kind of feeder, 0 if it can't use it.
    public float AppealFor(FeederStyle feeder) => Feeders.TryGetValue(feeder, out float appeal) ? appeal : 0f;

    // How its birds lean toward this trait (1 is no lean).
    public float LeanFor(Trait trait) => Leans.TryGetValue(trait, out float lean) ? lean : 1f;

    public override string ToString() => Key;
}
