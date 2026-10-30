using System.Numerics;
using Raylib_cs;

// The seeds you can pour into the bucket.
public enum SeedType
{
    Corn,
    Millet,
    Thistle,
    BlueBarley,
    Wild    // the plain seed that drops on its own; not sold in a bag
}

// The basic mesh a grain is squashed and stretched from.
public enum GrainMesh
{
    Round,
    Block
}

// How one grain of a seed looks on the ground.
// Size is the grain's width, height and length. Spread is how far grains scatter from the middle of a pile.
public record GrainLook(GrainMesh Mesh, Vector3 Size, float Spread);

// Everything about one kind of seed: how it looks, what its bag says, and which birds like it.
// Name is what it's called inside the box; ProductName is the brand printed on the bag.
public class SeedInfo
{
    public string Name;
    public string ProductName;
    public Color Color;
    public string Label;
    public Dictionary<BirdType, float> Tastes;    // how much each bird likes it, 0 to 1
    public GrainLook Grain;
    public float? SpoilTime;                      // seconds on the dirt before birds won't eat it; null never spoils
    public int JarPrice;                          // coins for one jar at the store (a sack is a few jars' worth)
    public float StartingSupply;                  // seed in its bag on the bucket panel at the start of a game

    public SeedInfo(string name, string productName, Color color, string label,
                    Dictionary<BirdType, float> tastes, GrainLook grain, float? spoilTime, int jarPrice = 1, float startingSupply = 50f)
    {
        Name = name;
        ProductName = productName;
        Color = color;
        Label = label;
        Tastes = tastes;
        Grain = grain;
        SpoilTime = spoilTime;
        JarPrice = jarPrice;
        StartingSupply = startingSupply;
    }
}

// The seed table. To add a seed: add it to SeedType, then give it an entry here.
public static class Seeds
{
    public static readonly SeedType[] All = Enum.GetValues<SeedType>();

    // The seeds that have a bag on the bucket panel.
    public static readonly SeedType[] Bags = All.Where(seed => seed != SeedType.Wild).ToArray();

    private static readonly Dictionary<SeedType, SeedInfo> info = new()
    {
        [SeedType.Corn] = new SeedInfo(
            "Corn", "Castellan Feed 9", new Color(230, 110, 50, 255),
            "Warm, crackly kernels. A favorite of fire-colored birds.",
            new() { [BirdType.Emberwing] = 1.0f, [BirdType.Frostcrest] = 0.05f },
            new GrainLook(GrainMesh.Block, new Vector3(0.09f, 0.06f, 0.08f), Spread: 0.12f),
            spoilTime: 300f),

        [SeedType.Millet] = new SeedInfo(
            "Millet", "Pip-Mk II Millet", new Color(180, 160, 220, 255),
            "Scatter at dusk. Doves can't resist. Small birds peck at it too.",
            new() { [BirdType.DuskDove] = 1.0f, [BirdType.Gildfinch] = 0.3f },
            new GrainLook(GrainMesh.Round, new Vector3(0.06f, 0.06f, 0.06f), Spread: 0.35f),
            spoilTime: 120f),

        [SeedType.Thistle] = new SeedInfo(
            "Thistle", "Squire Starter Mix", new Color(150, 230, 120, 255),
            "Tiny and bright. Small beaks only.",
            new() { [BirdType.Gildfinch] = 1.0f },
            new GrainLook(GrainMesh.Round, new Vector3(0.03f, 0.03f, 0.10f), Spread: 0.22f),
            spoilTime: 180f),

        [SeedType.BlueBarley] = new SeedInfo(
            "Blue Barley", "Royal Reserve Pellets", new Color(70, 110, 230, 255),
            "Rare. Something pale and blue has been seen nearby.",
            new() { [BirdType.Frostcrest] = 1.0f },
            new GrainLook(GrainMesh.Round, new Vector3(0.06f, 0.05f, 0.10f), Spread: 0.18f),
            spoilTime: 240f, jarPrice: 3, startingSupply: 0f),

        [SeedType.Wild] = new SeedInfo(
            "Wild Seed", "Hedgerow Gleanings", new Color(214, 178, 112, 255),
            "Whatever the wind brings. Common birds pick at it.",
            new() { [BirdType.Emberwing] = 0.4f, [BirdType.DuskDove] = 0.4f, [BirdType.Gildfinch] = 0.4f },
            new GrainLook(GrainMesh.Round, new Vector3(0.06f, 0.045f, 0.07f), Spread: 0.25f),
            spoilTime: 180f, jarPrice: 0, startingSupply: 0f),
    };

    public static SeedInfo Get(SeedType type) => info[type];

    // Frostcrests are a discovery. Until the first one has been seen in the view,
    // only Blue Barley can call them; after that their other tastes count too.
    public static bool FrostcrestSeen = false;

    // A bird's favorite seed, picked from its species' tastes: seed it loves is picked more often than seed it only eats.
    // Seeds it barely touches (taste under FavoriteMinTaste) are never a favorite.
    private const float FavoriteMinTaste = 0.2f;

    public static SeedType RollFavorite(BirdType bird)
    {
        List<(SeedType Seed, float Taste)> liked = All
            .Select(seed => (Seed: seed, Taste: Get(seed).Tastes.TryGetValue(bird, out float taste) ? taste : 0f))
            .Where(pair => pair.Taste >= FavoriteMinTaste)
            .ToList();
        if (liked.Count == 0) return SeedType.Wild;

        float roll = Rng.Range(0f, liked.Sum(pair => pair.Taste));
        foreach ((SeedType seed, float taste) in liked)
        {
            roll -= taste;
            if (roll <= 0f) return seed;
        }
        return liked[^1].Seed;
    }

    // True if this kind of bird eats this seed at all.
    public static bool Eats(BirdType bird, SeedType seed) => Get(seed).Tastes.TryGetValue(bird, out float taste) && taste > 0f;

    // Adds how strongly some seed draws each bird (amount x taste) into a running score.
    // Used by the bucket's prediction and by the bird spawner, so both agree.
    public static void AddAttraction(Dictionary<BirdType, float> scores, SeedType seed, float amount)
    {
        foreach ((BirdType bird, float taste) in Get(seed).Tastes)
        {
            if (bird == BirdType.Frostcrest && seed != SeedType.BlueBarley && !FrostcrestSeen) continue;

            scores.TryGetValue(bird, out float score);
            scores[bird] = score + amount * taste;
        }
    }
}
