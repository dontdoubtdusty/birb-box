// A bird's personality: 2 or 3 traits, fixed for its whole life (Step 12a).
//
// Each trait is one degree on a scale (Nerve, Appetite, Roaming, Voice, Hours, Weather, Bathing). A bird holds at most one degree of
// each scale, so opposite traits never roll together. A scale the bird has no trait on is average.
// Traits only nudge numbers the simulation already has (personal space, hunger, home radius, call delay...).
// The numbers live in TraitEffects; each trait in the table below says which ones it changes.

// The scales a trait sits on. A bird has one degree of a scale, or is average on it.
public enum TraitScale
{
    Nerve,
    Appetite,
    Roaming,
    Voice,
    Hours,       // when it keeps to its roost (added at 4.92 with day and night)
    Weather,     // how it takes the rain (added at 4.96 with rain; one trait only, so it's rolled less often, see ScaleWeight)
    Bathing      // how it takes to the water (added at 5.10 with thirst; one trait only, like Weather)
}

public enum Trait
{
    FaintHearted, Wary, StoutHearted,    // Nerve
    Dainty, BigEater,                // Appetite
    Hearthbound, Wayfarer,               // Roaming
    Hushed, TownCrier,                   // Voice
    EarlyRiser, NightOwl,                // Hours
    RainProud,                           // Weather
    Dabbler                              // Bathing
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
    public float ThirstSpeed = 1f;        // times: how fast it gets thirsty

    // ROAMING
    public float HomeRadius = 1f;         // times: how far it roams from home (SimTuning.HomeRadius)
    public float LoseInterest = 1f;       // times: how soon it gets bored and leaves (2 = twice as soon)
    public float ReturnWeight = 1f;       // times: how likely it is to be the regular that comes back
    public float RoostChance = 1f;        // times: how likely it is to stay the night instead of flying off (SimTuning.RoostChance)

    // VOICE
    public float CallDelay = 1f;          // times: seconds between its own calls
    public float IgnoreHush = 0f;         // chance it calls anyway when another bird just called nearby

    // HOURS
    public float HourShift = 0f;          // hours: when it wakes and goes to roost, earlier (-) or later (+) than most
    public bool NightOwl = false;         // sleeps by day, up at dusk, night and dawn; the only kind of bird that arrives at night
    public float RestSpeed = 1f;          // times: how fast it tires while awake

    // WEATHER
    public bool RainProud = false;        // doesn't mind the rain: no hunching, no leaving early, calls as usual

    // BATHING
    public float BathShare = 1f;          // times: how likely a trip to the pool is a bath rather than a sip
    public float BathLength = 1f;         // times: how long a bath lasts
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

    // How likely each scale is to be one of a bird's 2 or 3 (missing means 1). Weather has only one trait,
    // so it gets a low weight: about 1 bird in 9 is Rain-proud (Frostcrests 1 in 5, Dusk Doves 1 in 18), plus some of
    // the birds that arrive in the rain (BirdSpawner).
    private static readonly Dictionary<TraitScale, float> ScaleWeight = new()
    {
        [TraitScale.Weather] = 0.2f,
        [TraitScale.Bathing] = 0.2f,
    };

    // Each scale from one end to the other, as the bird card's sliders show it. null is the average spot.
    private static readonly Dictionary<TraitScale, Trait?[]> stops = new()
    {
        [TraitScale.Nerve] = new Trait?[] { Trait.FaintHearted, Trait.Wary, null, Trait.StoutHearted },
        [TraitScale.Appetite] = new Trait?[] { Trait.Dainty, null, Trait.BigEater },
        [TraitScale.Roaming] = new Trait?[] { Trait.Hearthbound, null, Trait.Wayfarer },
        [TraitScale.Voice] = new Trait?[] { Trait.Hushed, null, Trait.TownCrier },
        [TraitScale.Hours] = new Trait?[] { Trait.EarlyRiser, null, Trait.NightOwl },
        [TraitScale.Weather] = new Trait?[] { null, Trait.RainProud },
        [TraitScale.Bathing] = new Trait?[] { null, Trait.Dabbler },
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
            e => { e.HungerSpeed = 0.7f; e.HungryAt = 0.3f; e.FullAt = 0.6f; e.BiteTime = 1.5f; e.BuildLean = 0.94f; e.ThirstSpeed = 0.75f; }),

        [Trait.BigEater] = new TraitInfo("Big Eater", TraitScale.Appetite, 1f,
            "Eats as if the granary closes at dusk. Hungry often, gobbles fast, and roams far for a meal.",
            e => { e.HungerSpeed = 1.4f; e.HungryAt = 0.55f; e.FullAt = 0.98f; e.Nourish = 0.6f; e.BiteTime = 0.75f; e.FoodRange = 1.4f; e.BuildLean = 1.06f; e.ThirstSpeed = 1.3f; }),

        // ROAMING
        [Trait.Hearthbound] = new TraitInfo("Hearthbound", TraitScale.Roaming, 1f,
            "Keeps to its own patch of grass, rarely tires of it, and nearly always comes home.",
            e => { e.HomeRadius = 0.5f; e.LoseInterest = 0.4f; e.ReturnWeight = 3f; e.RoostChance = 2f; }),

        [Trait.Wayfarer] = new TraitInfo("Wayfarer", TraitScale.Roaming, 1f,
            "Ranges wide, tires of a place quickly, and may never pass this way again.",
            e => { e.HomeRadius = 2f; e.LoseInterest = 2.5f; e.ReturnWeight = 0.4f; e.RoostChance = 0.5f; }),

        // VOICE
        [Trait.Hushed] = new TraitInfo("Hushed", TraitScale.Voice, 1f,
            "Speaks seldom, and never over another bird.",
            e => { e.CallDelay = 2.5f; }),

        [Trait.TownCrier] = new TraitInfo("Town Crier", TraitScale.Voice, 1f,
            "Has news for everyone, often, and will not wait its turn.",
            e => { e.CallDelay = 0.4f; e.IgnoreHush = 0.6f; }),

        // HOURS
        [Trait.EarlyRiser] = new TraitInfo("Early Riser", TraitScale.Hours, 1f,
            "Up before the first glow and singing about it. Tires early, and is off to roost while the others are still at supper.",
            e => { e.HourShift = -0.75f; e.RestSpeed = 1.15f; }),

        [Trait.NightOwl] = new TraitInfo("Night Owl", TraitScale.Hours, 0.6f,
            "Keeps lantern hours. Sleeps the day away, and wanders the dark garden long after the rest have gone to roost.",
            e => { e.NightOwl = true; }),

        // WEATHER
        [Trait.RainProud] = new TraitInfo("Rain-proud", TraitScale.Weather, 1f,
            "Takes a downpour as a compliment. Stays out in it, eats, and sings while the others huddle.",
            e => { e.RainProud = true; }),

        // BATHING
        [Trait.Dabbler] = new TraitInfo("Dabbler", TraitScale.Bathing, 1f,
            "Never passes water without wading in. Bathes long and often, and comes out looking pleased with itself.",
            e => { e.BathShare = 3f; e.BathLength = 1.8f; }),
    };

    // How each species leans is in its entry in Birds.cs (Species.Leans).

    public static TraitInfo Get(Trait trait) => info[trait];

    // A new personality for a bird of this kind: 2 or 3 different scales, one degree of each, in scale order.
    // Scales are picked one at a time by their weight (ScaleWeight, times the species' lean for a one-trait scale).
    public static List<Trait> Roll(Species type)
    {
        int count = Rng.Chance(ThreeTraitChance) ? 3 : 2;
        List<TraitScale> left = Scales.ToList();
        List<TraitScale> picked = new();
        while (picked.Count < count && left.Count > 0)
        {
            float total = left.Sum(scale => PickWeight(type, scale));
            float roll = Rng.Range(0f, total);
            TraitScale chosen = left[^1];
            foreach (TraitScale scale in left)
            {
                roll -= PickWeight(type, scale);
                if (roll <= 0f) { chosen = scale; break; }
            }
            picked.Add(chosen);
            left.Remove(chosen);
        }
        return picked.OrderBy(scale => scale).Select(scale => RollDegree(type, scale)).ToList();
    }

    // How likely a scale is to be picked. A scale with only one trait (Weather) has no other degree to lean between,
    // so the species' lean for that trait goes on the scale instead.
    private static float PickWeight(Species type, TraitScale scale)
    {
        float weight = ScaleWeight.TryGetValue(scale, out float w) ? w : 1f;
        List<Trait> options = All.Where(trait => info[trait].Scale == scale).ToList();
        if (options.Count == 1) weight *= Lean(type, options[0]);
        return weight;
    }

    // One degree of a scale, weighted by commonness and the species' lean.
    private static Trait RollDegree(Species type, TraitScale scale)
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

    private static float Weight(Species type, Trait trait) => info[trait].Commonness * Lean(type, trait);

    private static float Lean(Species type, Trait trait) => type.LeanFor(trait);

    // The same personality made a Night Owl (birds that arrive at night all are): its Hours trait swapped,
    // or, with no room left for one, another trait dropped at random, so it still has 2 or 3.
    public static List<Trait> AsNightOwl(IEnumerable<Trait> traits)
    {
        List<Trait> kept = traits.Where(trait => info[trait].Scale != TraitScale.Hours).ToList();
        if (kept.Count >= 3) kept.RemoveAt(Rng.Next(kept.Count));
        kept.Add(Trait.NightOwl);
        return Clean(kept);
    }

    // The same personality with no Night Owl in it (a new bird that comes by day, when Night Owls are asleep):
    // its Hours trait dropped, and if that leaves it with only one, a trait on another scale rolled in its place.
    public static List<Trait> AsDayBird(IEnumerable<Trait> traits, Species type)
    {
        List<Trait> kept = traits.Where(trait => trait != Trait.NightOwl).ToList();
        if (kept.Count >= 2 || kept.Count == traits.Count()) return Clean(kept);

        // Picked by the same weights as a first roll, so the rarer scales stay rare
        List<TraitScale> free = Scales.Where(scale => scale != TraitScale.Hours && kept.All(trait => info[trait].Scale != scale)).ToList();
        if (free.Count == 0) return Clean(kept);
        float roll = Rng.Range(0f, free.Sum(scale => PickWeight(type, scale)));
        TraitScale chosen = free[^1];
        foreach (TraitScale scale in free)
        {
            roll -= PickWeight(type, scale);
            if (roll <= 0f) { chosen = scale; break; }
        }
        kept.Add(RollDegree(type, chosen));
        return Clean(kept);
    }

    // The same personality made Rain-proud (some birds that arrive in the rain are): a Weather trait added, dropping one
    // other trait at random (never Night Owl) if it already has 3.
    public static List<Trait> AsRainProud(IEnumerable<Trait> traits)
    {
        List<Trait> kept = traits.Where(trait => info[trait].Scale != TraitScale.Weather).ToList();
        if (kept.Count >= 3)
        {
            List<Trait> droppable = kept.Where(trait => trait != Trait.NightOwl).ToList();
            kept.Remove(droppable[Rng.Next(droppable.Count)]);
        }
        kept.Add(Trait.RainProud);
        return Clean(kept);
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
