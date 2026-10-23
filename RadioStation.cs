using System.Globalization;

// One station on the radio's dial: either a folder of your songs, or a live web stream.
//
// Folder stations broadcast whether you're listening or not: each keeps its own clock, so when you tune in
// you land partway through whatever it's "playing" right now, just like a real station.
// A folder can hold a station.txt to set its name and frequency:
//     name: Emberwing FM
//     frequency: 92.3
// Without one, the folder's name is used and the radio picks a free frequency.
public class RadioStation
{
    public const float LowestFrequency = 88f, HighestFrequency = 108f;    // the FM band, in MHz
    private const double GuessedSongLength = 210;                         // seconds, until a song's real length is known

    public string Name = "";
    public float Frequency;
    public bool FrequencyChosen;          // set in station.txt, so the radio leaves it where it is
    public bool IsWeb;

    // Where it sits on the dial, 0..1
    public float Position => (Frequency - LowestFrequency) / (HighestFrequency - LowestFrequency);

    // FOLDER STATIONS
    public string FolderPath = "";
    public List<string> Files = new();
    public readonly HashSet<string> Bad = new();     // files that wouldn't play
    public string? Playing;                          // the song on air right now
    private double songStartedAt;                    // radio-clock seconds when that song began
    private List<string> order = new();              // this round of the shuffle
    private int orderPos = -1;
    public bool Dead => !IsWeb && Bad.Count >= Files.Count;

    // WEB STATIONS
    public string Url = "";
    public string Uuid = "";
    public string Details = "";          // genre and country, for the hover tag
    public double RetryAt;               // radio-clock seconds before trying a stream that failed again
    public bool CountedClick;            // Radio Browser asks to be told once when a station is played

    // DRIFT: each station's signal wanders a little on its own schedule
    public readonly float DriftPhaseA = Rng.Range(0f, MathF.Tau), DriftPhaseB = Rng.Range(0f, MathF.Tau);

    // ---------------- FOLDER BROADCAST ----------------

    // Which song is on air now and how far into it, catching up on everything that "played" while nobody listened.
    // The first time, it starts somewhere in the middle of a random song, as if it had been on all along.
    public (string file, double offset)? OnAirNow(double now, Func<string, double?> lengthOf)
    {
        if (Dead) return null;
        if (Playing == null)
        {
            Playing = NextInShuffle();
            if (Playing == null) return null;
            songStartedAt = now - Rng.Range(0.1f, 0.8f) * Length(Playing, lengthOf);
        }

        double elapsed = now - songStartedAt;
        for (int guard = 0; guard < 5000 && elapsed >= Length(Playing, lengthOf); guard++)
        {
            double length = Length(Playing, lengthOf);
            elapsed -= length;
            songStartedAt += length;
            string? next = NextInShuffle();
            if (next == null) return null;
            Playing = next;
        }
        return (Playing, elapsed);
    }

    // Moves on to the next song, starting it now (a song ended, failed, or the next button was pressed).
    public void StartNextSong(double now)
    {
        Playing = NextInShuffle();
        songStartedAt = now;
    }

    private static double Length(string file, Func<string, double?> lengthOf) => lengthOf(file) ?? GuessedSongLength;

    // Shuffle: every song plays once before any repeats, never the same one twice in a row. Skips bad files.
    private string? NextInShuffle()
    {
        for (int tries = 0; tries <= Files.Count; tries++)
        {
            if (orderPos + 1 >= order.Count) Reshuffle();
            orderPos++;
            string file = order[orderPos];
            if (!Bad.Contains(file)) return file;
        }
        return null;
    }

    private void Reshuffle()
    {
        order = new List<string>(Files);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        if (order.Count > 1 && order[0] == Playing) (order[0], order[^1]) = (order[^1], order[0]);
        orderPos = -1;
    }

    // A new set of files from the folder. If it changed, the shuffle starts over.
    public void SetFiles(List<string> files)
    {
        if (!Files.SequenceEqual(files))
        {
            Files = files;
            order.Clear();
            orderPos = -1;
            if (Playing != null && !files.Contains(Playing)) Playing = null;
        }
        Bad.Clear();    // give files another try, in case they were fixed
    }

    // Reads name and frequency from the folder's station.txt, if there is one.
    public void ReadStationFile()
    {
        string path = Path.Combine(FolderPath, "station.txt");
        FrequencyChosen = false;
        if (!File.Exists(path)) return;

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            int colon = line.IndexOf(':');
            if (line.StartsWith('#') || colon < 0) continue;

            string key = line[..colon].Trim().ToLowerInvariant();
            string value = line[(colon + 1)..].Trim();
            if (key == "name" && value.Length > 0) Name = value;
            if (key == "frequency" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float mhz))
            {
                Frequency = Math.Clamp(mhz, LowestFrequency, HighestFrequency);
                FrequencyChosen = true;
            }
        }
    }
}
