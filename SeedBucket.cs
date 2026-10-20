// The mixing bucket. Poured seed sits in layers, in the order it went in.
// Stirring blends every layer into one mix at the bottom; seed poured after that
// forms new layers on top until the next stir.
public class SeedBucket
{
    public const float Capacity = 100f;

    public class Layer
    {
        public SeedType Seed;
        public float Amount;

        public Layer(SeedType seed, float amount)
        {
            Seed = seed;
            Amount = amount;
        }
    }

    // Unstirred seed, bottom to top. Pouring the same seed twice in a row grows one layer.
    public List<Layer> Layers { get; } = new();

    // Stirred seed, as an amount per seed type.
    public Dictionary<SeedType, float> Mixed { get; } = new();

    public float MixedTotal => Mixed.Values.Sum();
    public float Total => MixedTotal + Layers.Sum(layer => layer.Amount);
    public bool IsFull => Total >= Capacity - 0.01f;
    public bool IsEmpty => Total <= 0f;

    // Goes up on every change, so the panel knows when to redraw the speckles and redo the prediction.
    public int Version { get; private set; }

    // Returns how much actually went in, since a nearly full bucket takes less.
    public float Pour(SeedType seed, float amount)
    {
        amount = MathF.Min(amount, Capacity - Total);
        if (amount <= 0f) return 0f;

        Layer? top = Layers.Count > 0 ? Layers[Layers.Count - 1] : null;
        if (top != null && top.Seed == seed)
        {
            top.Amount += amount;
        }
        else
        {
            Layers.Add(new Layer(seed, amount));
        }
        Version++;
        return amount;
    }

    // Scoops seed out of the top. Unstirred layers come out one seed at a time, so the pile is pure.
    // Once only the stirred mix is left, the pile gets every seed in the mix's proportions.
    public Dictionary<SeedType, float> Take(float amount)
    {
        Dictionary<SeedType, float> scoop = new();

        if (Layers.Count > 0)
        {
            Layer top = Layers[Layers.Count - 1];
            float taken = MathF.Min(amount, top.Amount);
            top.Amount -= taken;
            if (top.Amount <= 0.01f) Layers.RemoveAt(Layers.Count - 1);

            scoop[top.Seed] = taken;
            Version++;
            return scoop;
        }

        float mixedTotal = MixedTotal;
        if (mixedTotal <= 0.01f) return scoop;

        float share = MathF.Min(1f, amount / mixedTotal);
        foreach (SeedType seed in Mixed.Keys.ToList())
        {
            float taken = Mixed[seed] * share;
            scoop[seed] = taken;
            Mixed[seed] -= taken;
            if (Mixed[seed] <= 0.01f) Mixed.Remove(seed);
        }
        Version++;
        return scoop;
    }

    public void Stir()
    {
        foreach (Layer layer in Layers)
        {
            Mixed.TryGetValue(layer.Seed, out float current);
            Mixed[layer.Seed] = current + layer.Amount;
        }
        Layers.Clear();
        Version++;
    }

    public void Empty()
    {
        Layers.Clear();
        Mixed.Clear();
        Version++;
    }

    public float AmountOf(SeedType seed)
    {
        Mixed.TryGetValue(seed, out float amount);
        foreach (Layer layer in Layers)
        {
            if (layer.Seed == seed) amount += layer.Amount;
        }
        return amount;
    }

    // Each bird's share of the attraction, adding up to 1.
    // A bird's score is the sum over seeds of (amount of that seed x how much the bird likes it).
    public Dictionary<BirdType, float> PredictBirds()
    {
        Dictionary<BirdType, float> scores = new();

        foreach (SeedType seed in Seeds.All)
        {
            float amount = AmountOf(seed);
            if (amount > 0f) Seeds.AddAttraction(scores, seed, amount);
        }
        float totalScore = scores.Values.Sum();

        if (totalScore <= 0f) return scores;

        foreach (BirdType bird in scores.Keys.ToList())
        {
            scores[bird] /= totalScore;
        }
        return scores;
    }
}
