using Raylib_cs;

public enum BirdType
{
    Emberwing,
    DuskDove,
    Glintfinch,
    Frostcrest    // only shows up for Blue Barley
}

// Everything about one kind of bird: what it's called, how it looks, and whether it's a common visitor.
public class BirdInfo
{
    public string Name;           // shown to the player (the enum can't have spaces)
    public BirbShape[] Shapes;    // each bird picks one at random
    public float Size;            // draw scale: birb units to world units
    public Color Color;           // name text, map dots and pedestal LEDs
    public bool Common;           // drawn by wild seed and the "Call a bird" button; uncommon birds need special seed

    public BirdInfo(string name, BirbShape[] shapes, float size, Color color, bool common)
    {
        Name = name;
        Shapes = shapes;
        Size = size;
        Color = color;
        Common = common;
    }
}

// The bird table. To add a bird: add it to BirdType, then give it an entry here (and its tastes in Seeds.cs).
public static class Birds
{
    public static readonly BirdType[] All = Enum.GetValues<BirdType>();

    private static readonly Dictionary<BirdType, BirdInfo> info = new()
    {
        [BirdType.Emberwing] = new BirdInfo(
            "Emberwing", new[] { StarterBirbs.EmberwingCock, StarterBirbs.EmberwingHen },
            0.33f, new Color(227, 74, 74, 255), common: true),

        [BirdType.DuskDove] = new BirdInfo(
            "Dusk Dove", new[] { StarterBirbs.DuskDove }, 0.4f, new Color(212, 209, 199, 255), common: true),

        [BirdType.Glintfinch] = new BirdInfo(
            "Glintfinch", new[] { StarterBirbs.Glintfinch }, 0.2f, new Color(240, 158, 38, 255), common: true),

        [BirdType.Frostcrest] = new BirdInfo(
            "Frostcrest", new[] { StarterBirbs.Frostcrest }, 0.33f, new Color(80, 130, 240, 255), common: false),
    };

    // Declared after the table, because it reads from it.
    public static readonly BirdType[] Common = All.Where(type => info[type].Common).ToArray();

    public static BirdInfo Get(BirdType type) => info[type];
}
