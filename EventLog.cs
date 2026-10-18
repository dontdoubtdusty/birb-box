// A short history of things that happened, shown in the "Bird log" window.
public static class EventLog
{
    private const int MaxEntries = 60;
    private static readonly List<string> entries = new();

    public static IReadOnlyList<string> Entries => entries;
    public static int Version { get; private set; }

    public static void Add(string message)
    {
        float t = GameEngine.Instance.SimTime;
        int minutes = (int)(t / 60);
        int seconds = (int)(t % 60);
        entries.Add($"[{minutes:00}:{seconds:00}] {message}");

        if (entries.Count > MaxEntries)
        {
            entries.RemoveAt(0);
        }
        Version++;
    }
}
