// The player's feathers: how many of each species' feather they hold (GameEngine.Feathers). They are only for
// spending, so nothing here is ever shown as an object in the room. A species is listed once its first feather
// has been collected and stays listed at zero, so the HUD's collection fills in as birds are met.
public class FeatherPurse
{
    // The ledger trades this many feathers of one kind for one of another.
    public const int ExchangeRate = 3;

    private readonly Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);

    // Goes up when a species is collected for the first time or the purse is restored, so the store knows to reset its shelves.
    public int Version { get; private set; }

    // Every feather held, of any species.
    public int Total => counts.Values.Sum();

    // How many species have been collected (they are listed in Birds.All order by Collected).
    public int CollectedCount => Birds.All.Count(species => counts.ContainsKey(species.Key));

    public int CountOf(Species species) => counts.TryGetValue(species.Key, out int count) ? count : 0;

    // True once a feather of this species has been collected (even if none are left).
    public bool Has(Species species) => counts.ContainsKey(species.Key);

    // The species that have been collected at least once, in the order species are shown everywhere.
    public List<Species> Collected() => Birds.All.Where(species => counts.ContainsKey(species.Key)).ToList();

    public void Add(Species species, int amount = 1)
    {
        if (!counts.ContainsKey(species.Key)) Version++;
        counts[species.Key] = CountOf(species) + amount;
    }

    // Pays with feathers of one species. False (and nothing taken) if there aren't enough.
    public bool TrySpend(Species species, int amount)
    {
        if (CountOf(species) < amount) return false;
        counts[species.Key] = CountOf(species) - amount;
        return true;
    }

    // Pays with feathers of any kind, taking from the biggest stack first. False (and nothing taken) if there aren't enough.
    // Only the store's lure seed uses this (see SeedStore), for a rare species that hasn't left a feather yet.
    public bool TrySpendAny(int amount)
    {
        if (Total < amount) return false;

        while (amount > 0)
        {
            KeyValuePair<string, int> biggest = counts.OrderByDescending(pair => pair.Value).First();
            int take = Math.Min(amount, biggest.Value);
            counts[biggest.Key] = biggest.Value - take;
            amount -= take;
        }
        return true;
    }

    // The ledger's trade: ExchangeRate of one species' feathers for one of another's. The other species must already
    // be collected. False (and nothing changes) if the trade isn't allowed or there aren't enough to give.
    public bool Exchange(Species give, Species get)
    {
        if (ReferenceEquals(give, get) || !Has(get) || CountOf(give) < ExchangeRate) return false;
        counts[give.Key] = CountOf(give) - ExchangeRate;
        counts[get.Key] = CountOf(get) + 1;
        return true;
    }

    // Cheat: this many of every species' feather.
    public void AddToAll(int amount)
    {
        foreach (Species species in Birds.All) Add(species, amount);
    }

    // Saving (see SaveGame.cs): species keys with their counts, zeros included.
    public Dictionary<string, int> ToSave() => new(counts);

    public void Restore(Dictionary<string, int>? saved)
    {
        counts.Clear();
        Version++;
        if (saved == null) return;
        foreach ((string key, int count) in saved) counts[key] = Math.Max(0, count);
    }
}
