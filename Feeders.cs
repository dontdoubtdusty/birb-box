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
    public FeederStyle Style;
    public string Name;
    public string Label;                          // one line for the price tag
    public int Price;
    public float Capacity;                        // seed it holds; seed past that spills onto the dirt
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

    public string? PaidIn;                        // the key of the species whose feathers pay for it at the store (the one that likes it best)

    public FeederInfo(FeederStyle style, string name, string label, int price, float capacity,
                      float catchRadius, float catchHeight, float seedHeight, float perchHeight, float perchRadius, float footRadius,
                      int perches = 0, float portRadius = 0f, float rodReach = 0f, string? paidIn = null)
    {
        Style = style;
        Name = name;
        Label = label;
        Price = price;
        Capacity = capacity;
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
        PaidIn = paidIn;
    }

    // How much this kind of bird likes eating here, 0 to 1 (0 = can't use it). Each species lists its feeders in Birds.cs.
    public float AppealFor(Species bird) => bird.AppealFor(Style);
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
        [FeederStyle.Tray] = new FeederInfo(FeederStyle.Tray,
            "Almoner's Tray", "Doves eat right out of it.", price: 4, capacity: 10f,
            catchRadius: 0.4f, catchHeight: 0.22f, seedHeight: 0.21f, perchHeight: 0.21f, perchRadius: 0.22f, footRadius: 0.4f,
            paidIn: "DuskDove"),

        [FeederStyle.Tube] = new FeederInfo(FeederStyle.Tube,
            "Turret Feeder", "Finches perch. Doves can't.", price: 6, capacity: 8f,
            catchRadius: 0.25f, catchHeight: 1.0f, seedHeight: 0.56f, perchHeight: 0.608f, perchRadius: 0.25f, footRadius: 0.3f,
            perches: 2, portRadius: 0.085f, rodReach: 0.3f, paidIn: "Gildfinch"),
    };

    public static FeederInfo Get(FeederStyle style) => info[style];
}
