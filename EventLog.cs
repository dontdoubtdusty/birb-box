// A short history of things that happened, shown in the "Bird log" window.
public static class EventLog
{
    private const int MaxEntries = 60;
    private static readonly List<string> entries = new();

    public static IReadOnlyList<string> Entries => entries;
    public static int Version { get; private set; }

    // From a save: the log as it was, newest last.
    public static void Restore(IEnumerable<string> saved)
    {
        entries.Clear();
        entries.AddRange(saved.TakeLast(MaxEntries));
        Version++;
    }

    public static void Add(string message)
    {
        entries.Add($"[{Clock.Format(GameEngine.Instance.SimTime)}] {message}");

        if (entries.Count > MaxEntries)
        {
            entries.RemoveAt(0);
        }
        Version++;
    }
}
