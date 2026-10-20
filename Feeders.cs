// The feeders sold at the Gilded Granary.
public enum FeederStyle
{
    Tray,    // a low tray on short legs; doves stand right in it
    Tube     // a seed tube on a post with perches; too narrow for doves
}

// Everything about one kind of feeder. Heights and sizes are in world units, measured up from the dirt.
public class FeederInfo
{
    public string Name;
    public string Label;                          // one line for the price tag
    public int Price;
    public float Capacity;                        // seed it holds; seed past that spills onto the dirt
    public Dictionary<BirdType, float> Appeal;    // how much each bird likes eating here, 0 to 1 (0 = can't use it)
    public float CatchRadius;                     // seed poured within this far of the middle goes in
    public float CatchHeight;                     // where falling seed is caught
    public float SeedHeight;                      // where the seed sits inside
    public float PerchHeight;                     // where a bird's feet are while it eats
    public float PerchRadius;                     // how far from the middle it stands
    public float FootRadius;                      // for clicking it and keeping feeders apart

    public FeederInfo(string name, string label, int price, float capacity, Dictionary<BirdType, float> appeal,
                      float catchRadius, float catchHeight, float seedHeight, float perchHeight, float perchRadius, float footRadius)
    {
        Name = name;
        Label = label;
        Price = price;
        Capacity = capacity;
        Appeal = appeal;
        CatchRadius = catchRadius;
        CatchHeight = catchHeight;
        SeedHeight = seedHeight;
        PerchHeight = perchHeight;
        PerchRadius = perchRadius;
        FootRadius = footRadius;
    }

    public float AppealFor(BirdType bird) => Appeal.TryGetValue(bird, out float appeal) ? appeal : 0f;
}

// The feeder table. To add a feeder: add it to FeederStyle, give it an entry here, and a look in FeederModel.cs.
public static class Feeders
{
    public static readonly FeederStyle[] All = Enum.GetValues<FeederStyle>();

    // Seed in a feeder pulls in new birds this many times harder than the same seed on the dirt,
    // times how much each bird likes that feeder.
    public const float PullBonus = 1.5f;

    private static readonly Dictionary<FeederStyle, FeederInfo> info = new()
    {
        [FeederStyle.Tray] = new FeederInfo(
            "Almoner's Tray", "Doves eat right out of it.", price: 4, capacity: 10f,
            new() { [BirdType.DuskDove] = 1.0f, [BirdType.Emberwing] = 0.8f, [BirdType.Glintfinch] = 0.4f, [BirdType.Frostcrest] = 0.6f },
            catchRadius: 0.4f, catchHeight: 0.22f, seedHeight: 0.21f, perchHeight: 0.21f, perchRadius: 0.22f, footRadius: 0.4f),

        [FeederStyle.Tube] = new FeederInfo(
            "Turret Feeder", "Finches perch. Doves can't.", price: 6, capacity: 8f,
            new() { [BirdType.Glintfinch] = 1.0f, [BirdType.Emberwing] = 0.8f, [BirdType.Frostcrest] = 0.6f },
            catchRadius: 0.25f, catchHeight: 1.0f, seedHeight: 0.56f, perchHeight: 0.6f, perchRadius: 0.2f, footRadius: 0.25f),
    };

    public static FeederInfo Get(FeederStyle style) => info[style];
}
