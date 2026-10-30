// The feeders sold at the Gilded Granary.
public enum FeederStyle
{
    Tray,    // a low tray on short legs; doves stand right in it
    Tube     // a seed tube on a post with one rod through it: two perches, one under each feeding port; too narrow for doves
}

// Everything about one kind of feeder. Heights and sizes are in world units, measured up from the dirt.
// The numbers in the table are the feeder's model size; they're grown by Feeders.WorldScale here.
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
    public float PerchRadius;                     // how far from the middle it stands (a tray), or how near a bird gets before flying up
    public float FootRadius;                      // for clicking it and keeping feeders apart

    // PERCHES: 0 means birds stand anywhere around it (the tray). Otherwise one bird per perch, at the ends of a rod
    // running along X through the feeder: perch 0 on the +X end, perch 1 on the -X end.
    public int Perches;
    public float PortRadius;                      // the tube's outside: a perched bird's beak just reaches this
    public float RodReach;                        // how far each end of the rod sticks out from the middle

    public FeederInfo(string name, string label, int price, float capacity, Dictionary<BirdType, float> appeal,
                      float catchRadius, float catchHeight, float seedHeight, float perchHeight, float perchRadius, float footRadius,
                      int perches = 0, float portRadius = 0f, float rodReach = 0f)
    {
        Name = name;
        Label = label;
        Price = price;
        Capacity = capacity;
        Appeal = appeal;
        float k = Feeders.WorldScale;
        CatchRadius = catchRadius * k;
        CatchHeight = catchHeight * k;
        SeedHeight = seedHeight * k;
        PerchHeight = perchHeight * k;
        PerchRadius = perchRadius * k;
        FootRadius = footRadius * k;
        Perches = perches;
        PortRadius = portRadius * k;
        RodReach = rodReach * k;
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

    // How much bigger feeders are in the bird world than their model sizes below (and than in the store),
    // so they look like real feeders next to the birds.
    public const float WorldScale = 3f;

    private static readonly Dictionary<FeederStyle, FeederInfo> info = new()
    {
        [FeederStyle.Tray] = new FeederInfo(
            "Almoner's Tray", "Doves eat right out of it.", price: 4, capacity: 10f,
            new() { [BirdType.DuskDove] = 1.0f, [BirdType.Emberwing] = 0.8f, [BirdType.Gildfinch] = 0.4f, [BirdType.Frostcrest] = 0.6f },
            catchRadius: 0.4f, catchHeight: 0.22f, seedHeight: 0.21f, perchHeight: 0.21f, perchRadius: 0.22f, footRadius: 0.4f),

        [FeederStyle.Tube] = new FeederInfo(
            "Turret Feeder", "Finches perch. Doves can't.", price: 6, capacity: 8f,
            new() { [BirdType.Gildfinch] = 1.0f, [BirdType.Emberwing] = 0.8f, [BirdType.Frostcrest] = 0.6f },
            catchRadius: 0.25f, catchHeight: 1.0f, seedHeight: 0.56f, perchHeight: 0.608f, perchRadius: 0.25f, footRadius: 0.3f,
            perches: 2, portRadius: 0.085f, rodReach: 0.3f),
    };

    public static FeederInfo Get(FeederStyle style) => info[style];
}
