// The player's feathers: how many of each species' feather they hold (GameEngine.Feathers). They are only for
// spending, so nothing here is ever shown as an object in the room. A species is listed once its first feather
// has been collected and stays listed at zero, so the HUD's collection fills in as birds are met.
public class FeatherPurse
{
    private readonly Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);

    // Every feather held, of any species.
    public int Total => counts.Values.Sum();

    // How many species have been collected (they are listed in Birds.All order by Collected).
    public int CollectedCount => Birds.All.Count(species => counts.ContainsKey(species.Key));

    public int CountOf(Species species) => counts.TryGetValue(species.Key, out int count) ? count : 0;

    // The species that have been collected at least once, in the order species are shown everywhere.
    public List<Species> Collected() => Birds.All.Where(species => counts.ContainsKey(species.Key)).ToList();

    public void Add(Species species, int amount = 1)
    {
        counts[species.Key] = CountOf(species) + amount;
    }

    // Pays with feathers of any kind, taking from the biggest stack first. False (and nothing taken) if there aren't enough.
    // The store uses this until each species has its own shelf (Step 13b-2).
    public bool TrySpend(int amount)
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
        if (saved == null) return;
        foreach ((string key, int count) in saved) counts[key] = Math.Max(0, count);
    }
}
