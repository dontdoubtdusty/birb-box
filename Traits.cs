// A bird's personality: 2 or 3 traits, fixed for its whole life (Step 12a).
//
// Each trait is one degree on a scale (Nerve, Appetite, Roaming, Voice). A bird holds at most one degree of
// each scale, so opposite traits never roll together. A scale the bird has no trait on is average.
// Traits only nudge numbers the simulation already has (personal space, hunger, home radius, call delay...).
// The numbers live in TraitEffects; each trait in the table below says which ones it changes.

// The scales a trait sits on. A bird has one degree of a scale, or is average on it.
public enum TraitScale
{
    Nerve,
    Appetite,
    Roaming,
    Voice
}

public enum Trait
{
    FaintHearted, Wary, StoutHearted,    // Nerve
    Dainty, BigEater,                // Appetite
    Hearthbound, Wayfarer,               // Roaming
    Hushed, TownCrier                    // Voice
}

// Which birds a bird is easy around: on a rock it hops up to, and at a pile or feeder it picks to eat from.
public enum Company
{
    Anyone,
    KinOnly,     // minds birds of other kinds
    Alone        // minds every other bird
}

// Everything a personality can change. The defaults here are an average bird.
// "Times" values multiply the usual number (2 = twice as much); the others are used as they are.
public class TraitEffects
{
    // NERVE
    public float PersonalSpace = 1f;      // times: how far it keeps from other birds on the ground
    public float PerchChance = 1f;        // times: how often a pause near a rock becomes a sit on top
    public Company Company = Company.Anyone;    // who it minds: it won't share a rock with them, and food they're at counts as farther

    // APPETITE
    public float HungerSpeed = 1f;        // times: how fast it gets hungry
    public float HungryAt = 0.4f;         // starts looking for food below this hunger (1 full, 0 starving)
    public float FullAt = 0.95f;          // stops eating at this hunger
    public float Nourish = 1f;            // times: how much each bite fills it (lower means more bites a meal)
    public float BiteTime = 1f;           // times: seconds between bites
    public float FoodRange = 1f;          // times: how far it looks for food
    public float BuildLean = 1f;          // times: its body size, rolled once when it's first made (see Bird.RollLooks)

    // ROAMING
    public float HomeRadius = 1f;         // times: how far it roams from home (SimTuning.HomeRadius)
    public float LoseInterest = 1f;       // times: how soon it gets bored and leaves (2 = twice as soon)
    public float ReturnWeight = 1f;       // times: how likely it is to be the regular that comes back

    // VOICE
    public float CallDelay = 1f;          // times: seconds between its own calls
    public float IgnoreHush = 0f;         // chance it calls anyway when another bird just called nearby
}

// One trait: its name, the line shown when hovering it, its scale, how common it is, and what it changes.
public class TraitInfo
{
    public string Name;
    public string Flavor;
    public TraitScale Scale;
    public float Commonness;                  // weight when rolling: 2 is twice as likely as 1
    public Action<TraitEffects> Apply;

    public TraitInfo(string name, TraitScale scale, float commonness, string flavor, Action<TraitEffects> apply)
    {
        Name = name;
        Scale = scale;
        Commonness = commonness;
        Flavor = flavor;
        Apply = apply;
    }
}

// The trait table. To add a trait: add it to Trait, give it an entry here, place it on its scale in `stops`,
// and use any numbers it needs in TraitEffects.
public static class Traits
{
    public static readonly Trait[] All = Enum.GetValues<Trait>();
    public static readonly TraitScale[] Scales = Enum.GetValues<TraitScale>();

    private const float ThreeTraitChance = 0.5f;    // the rest get 2

    // Each scale from one end to the other, as the bird card's sliders show it. null is the average spot.
    private static readonly Dictionary<TraitScale, Trait?[]> stops = new()
    {
        [TraitScale.Nerve] = new Trait?[] { Trait.FaintHearted, Trait.Wary, null, Trait.StoutHearted },
        [TraitScale.Appetite] = new Trait?[] { Trait.Dainty, null, Trait.BigEater },
        [TraitScale.Roaming] = new Trait?[] { Trait.Hearthbound, null, Trait.Wayfarer },
        [TraitScale.Voice] = new Trait?[] { Trait.Hushed, null, Trait.TownCrier },
    };

    public static IReadOnlyList<Trait?> Stops(TraitScale scale) => stops[scale];

    private static readonly Dictionary<Trait, TraitInfo> info = new()
    {
        // NERVE
        [Trait.FaintHearted] = new TraitInfo("Faint-hearted", TraitScale.Nerve, 1f,
            "Startles at its own shadow. Keeps well clear of other birds, eats where no one else is, and only sits on an empty rock.",
            e => { e.PersonalSpace = 2.2f; e.PerchChance = 0.6f; e.Company = Company.Alone; }),

        [Trait.Wary] = new TraitInfo("Wary", TraitScale.Nerve, 1.5f,
            "Trusts its own kind and few others. Keeps a little room around itself, and would rather not eat beside a stranger.",
            e => { e.PersonalSpace = 1.5f; e.Company = Company.KinOnly; }),

        [Trait.StoutHearted] = new TraitInfo("Stout-hearted", TraitScale.Nerve, 1f,
            "Fears nothing smaller than a hawk. Shoulders in close and claims the high rocks.",
            e => { e.PersonalSpace = 0.5f; e.PerchChance = 1.8f; }),

        // APPETITE
        [Trait.Dainty] = new TraitInfo("Dainty", TraitScale.Appetite, 1f,
            "Takes its seed one grain at a time, and is full before the others have begun.",
            e => { e.HungerSpeed = 0.7f; e.HungryAt = 0.3f; e.FullAt = 0.6f; e.BiteTime = 1.5f; e.BuildLean = 0.94f; }),

        [Trait.BigEater] = new TraitInfo("Big Eater", TraitScale.Appetite, 1f,
            "Eats as if the granary closes at dusk. Hungry often, gobbles fast, and roams far for a meal.",
            e => { e.HungerSpeed = 1.4f; e.HungryAt = 0.55f; e.FullAt = 0.98f; e.Nourish = 0.6f; e.BiteTime = 0.75f; e.FoodRange = 1.4f; e.BuildLean = 1.06f; }),

        // ROAMING
        [Trait.Hearthbound] = new TraitInfo("Hearthbound", TraitScale.Roaming, 1f,
            "Keeps to its own patch of grass, rarely tires of it, and nearly always comes home.",
            e => { e.HomeRadius = 0.5f; e.LoseInterest = 0.4f; e.ReturnWeight = 3f; }),

        [Trait.Wayfarer] = new TraitInfo("Wayfarer", TraitScale.Roaming, 1f,
            "Ranges wide, tires of a place quickly, and may never pass this way again.",
            e => { e.HomeRadius = 2f; e.LoseInterest = 2.5f; e.ReturnWeight = 0.4f; }),

        // VOICE
        [Trait.Hushed] = new TraitInfo("Hushed", TraitScale.Voice, 1f,
            "Speaks seldom, and never over another bird.",
            e => { e.CallDelay = 2.5f; }),

        [Trait.TownCrier] = new TraitInfo("Town Crier", TraitScale.Voice, 1f,
            "Has news for everyone, often, and will not wait its turn.",
            e => { e.CallDelay = 0.4f; e.IgnoreHush = 0.6f; }),
    };

    // How each species leans: a trait's commonness is multiplied by this when a bird of that kind rolls.
    // Missing means 1 (no lean).
    private static readonly Dictionary<BirdType, Dictionary<Trait, float>> leans = new()
    {
        [BirdType.Emberwing] = new() { [Trait.StoutHearted] = 2.5f, [Trait.FaintHearted] = 0.4f, [Trait.TownCrier] = 1.5f },
        [BirdType.DuskDove] = new() { [Trait.FaintHearted] = 2.5f, [Trait.Wary] = 1.5f, [Trait.StoutHearted] = 0.4f, [Trait.Hushed] = 1.5f },
        [BirdType.Gildfinch] = new() { [Trait.Dainty] = 2f, [Trait.TownCrier] = 1.5f, [Trait.Wayfarer] = 1.5f },
        [BirdType.Frostcrest] = new() { [Trait.Wayfarer] = 2f, [Trait.Hushed] = 2f, [Trait.Wary] = 1.5f },
    };

    public static TraitInfo Get(Trait trait) => info[trait];

    // A new personality for a bird of this kind: 2 or 3 different scales, one degree of each, in scale order.
    public static List<Trait> Roll(BirdType type)
    {
        int count = Rng.Chance(ThreeTraitChance) ? 3 : 2;
        List<TraitScale> scales = Scales.OrderBy(_ => Rng.Range(0f, 1f)).Take(count).OrderBy(scale => scale).ToList();
        return scales.Select(scale => RollDegree(type, scale)).ToList();
    }

    // One degree of a scale, weighted by commonness and the species' lean.
    private static Trait RollDegree(BirdType type, TraitScale scale)
    {
        List<Trait> options = All.Where(trait => info[trait].Scale == scale).ToList();
        float total = options.Sum(trait => Weight(type, trait));
        float roll = Rng.Range(0f, total);
        foreach (Trait trait in options)
        {
            roll -= Weight(type, trait);
            if (roll <= 0f) return trait;
        }
        return options[^1];
    }

    private static float Weight(BirdType type, Trait trait)
    {
        float lean = leans.TryGetValue(type, out var forType) && forType.TryGetValue(trait, out float l) ? l : 1f;
        return info[trait].Commonness * lean;
    }

    // The numbers for a set of traits. No traits gives an average bird.
    public static TraitEffects EffectsOf(IEnumerable<Trait>? traits)
    {
        TraitEffects effects = new();
        if (traits != null) foreach (Trait trait in traits) info[trait].Apply(effects);
        return effects;
    }

    // Keeps a saved list sane: one trait per scale (the first one wins), in scale order.
    public static List<Trait> Clean(IEnumerable<Trait> traits)
    {
        return traits.Where(trait => info.ContainsKey(trait))
                     .GroupBy(trait => info[trait].Scale)
                     .Select(group => group.First())
                     .OrderBy(trait => info[trait].Scale)
                     .ToList();
    }

    // "Wary, Dainty, Town Crier"
    public static string Words(IEnumerable<Trait> traits) => string.Join(", ", traits.Select(trait => info[trait].Name));
}
