// A short history of things that happened, shown in the "Bird log" window.
public static class EventLog
{
    private const int MaxEntries = 120;    // dusk and dawn add a line per bird going to roost or coming back
    private static readonly List<string> entries = new();

    public static IReadOnlyList<string> Entries => entries;
    public static int Version { get; private set; }

    private static bool restoredOnce;    // the first Restore (the save loading at start) keeps what start-up already said

    // From a save: the log as it was, newest last. At start, lines logged while loading (a design or species file
    // that couldn't be read) are kept after it; a New world later starts with the new world's log alone.
    public static void Restore(IEnumerable<string> saved)
    {
        List<string> startUp = restoredOnce ? new() : new(entries);
        restoredOnce = true;
        entries.Clear();
        entries.AddRange(saved.Concat(startUp).TakeLast(MaxEntries));
        Version++;
    }

    public static void Add(string message)
    {
        entries.Add($"[{WorldClock.Instance.Stamp}] {message}");    // "[Day 3 18:42] ..." in the world's own time

        if (entries.Count > MaxEntries)
        {
            entries.RemoveAt(0);
        }
        Version++;
    }
}
