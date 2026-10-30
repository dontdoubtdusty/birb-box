using Raylib_cs;

public enum BirdType
{
    Emberwing,
    DuskDove,
    Gildfinch,
    Frostcrest    // only shows up for Blue Barley
}

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

// Everything about one kind of bird: what it's called, how it looks, and whether it's a common visitor.
public class BirdInfo
{
    public string Name;              // shown to the player (the enum can't have spaces)
    public List<BirdLook> Looks;     // each bird picks one at random; built-in looks first, then Designer saves
    public float Size;               // the species' own draw scale: birb units to world units
    public Color Color;              // name text, map dots and pedestal LEDs
    public bool Common;              // drawn by wild seed and the "Call a bird" button; uncommon birds need special seed

    public BirdInfo(string name, BirbShape[] shapes, float size, Color color, bool common)
    {
        Name = name;
        Looks = shapes.Select(shape => new BirdLook(shape, size)).ToList();
        Size = size;
        Color = color;
        Common = common;
    }

    // The looks written into the game, in their fixed order (saves count them by number).
    public List<BirdLook> BuiltInLooks => Looks.Where(look => look.File == null).ToList();
}

// The bird table. To add a bird: add it to BirdType, then give it an entry here (and its tastes in Seeds.cs).
public static class Birds
{
    public static readonly BirdType[] All = Enum.GetValues<BirdType>();

    private const float MinDesignSize = 0.05f;    // a hand-edited file with a size of 0 still gets a visible bird

    private static readonly Dictionary<BirdType, BirdInfo> info = new()
    {
        [BirdType.Emberwing] = new BirdInfo(
            "Emberwing", new[] { StarterBirbs.EmberwingCock, StarterBirbs.EmberwingHen },
            0.33f, new Color(227, 74, 74, 255), common: true),

        [BirdType.DuskDove] = new BirdInfo(
            "Dusk Dove", new[] { StarterBirbs.DuskDove }, 0.4f, new Color(212, 209, 199, 255), common: true),

        [BirdType.Gildfinch] = new BirdInfo(
            "Gildfinch", new[] { StarterBirbs.Gildfinch }, 0.2f, new Color(240, 158, 38, 255), common: true),

        [BirdType.Frostcrest] = new BirdInfo(
            "Frostcrest", new[] { StarterBirbs.Frostcrest }, 0.33f, new Color(80, 130, 240, 255), common: false),
    };

    // Declared after the table, because it reads from it.
    public static readonly BirdType[] Common = All.Where(type => info[type].Common).ToArray();

    public static BirdInfo Get(BirdType type) => info[type];

    // ---------------- DESIGNER SAVES ----------------

    // Reads every Birb Designer save and adds it to its species' looks. Called once at start, before the world loads.
    public static void LoadDesigns()
    {
        foreach (string file in BirbFiles.List())
        {
            BirbFile? design = BirbFiles.Load(file);
            if (design != null) AddDesign(design, file);
        }
    }

    // Puts a saved design among its species' looks, replacing the look from the same file (in whichever species had it).
    // The look gets its own copy of the shape, so further editing in the Designer leaves the world alone until saved again.
    public static BirdLook AddDesign(BirbFile design, string file)
    {
        foreach (BirdInfo bird in info.Values)
        {
            bird.Looks.RemoveAll(look => SameFile(look.File, file));
        }

        BirdLook added = new BirdLook(design.Shape.Copy(), MathF.Max(MinDesignSize, design.Size), file);
        Get(design.Species).Looks.Add(added);
        return added;
    }

    // File names ignore capital letters, like Windows does.
    public static bool SameFile(string? a, string? b) =>
        a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
