using System.Net;
using System.Text.Json;

// Finds live web stations through Radio Browser (radio-browser.info), a free directory run by volunteers.
// Which genres to look for is set in Radio/web-stations.txt (made on first run). The search runs in the
// background at startup; if Radio Browser can't be reached, the last list that worked is used.
public class WebStations
{
    private const string UserAgent = "BirbBox/4.44";
    private const int DefaultPerGenre = 2;
    private static readonly string[] FallbackServers = { "de1.api.radio-browser.info", "de2.api.radio-browser.info", "fi1.api.radio-browser.info" };

    // A station as Radio Browser describes it. Saved to the cache file as JSON.
    public record Info(string Uuid, string Name, string Url, string Genre, string Country);

    public string ConfigPath { get; }
    private readonly string cachePath;
    private Task<List<Info>>? search;
    private bool taken;
    private volatile string? message;    // for the Bird Log; written on the main thread, since the log isn't thread-safe
    private volatile string? server;     // the Radio Browser server that answered, for click counts
    private static readonly HttpClient http = MakeClient();

    public WebStations(string radioFolder)
    {
        ConfigPath = Path.Combine(radioFolder, "web-stations.txt");
        cachePath = Path.Combine(radioFolder, "web-stations-cache.json");
    }

    private static HttpClient MakeClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    // Starts the background search, unless one is already running or done.
    public void StartSearch()
    {
        if (search != null) return;
        WriteDefaultConfig();
        search = Task.Run(Search);
    }

    // The finished list, handed over once (then null). Also null while still searching.
    // Called on the main thread, which also passes the search's message on to the Bird Log.
    public List<Info>? TakeResult()
    {
        if (taken || search == null || !search.IsCompleted) return null;
        taken = true;
        if (message != null) EventLog.Add(message);
        return search.IsCompletedSuccessfully ? search.Result : new List<Info>();
    }

    // Tells Radio Browser a station was listened to; it uses this to rank stations. Fire and forget.
    public void CountClick(string uuid)
    {
        if (server == null) return;
        _ = http.GetAsync($"https://{server}/json/url/{uuid}").ContinueWith(_ => { });
    }

    // ---------------- SEARCH (background thread) ----------------

    private async Task<List<Info>> Search()
    {
        (List<string> genres, int perGenre) = ReadConfig();
        if (genres.Count == 0) return new List<Info>();

        var found = new List<Info>();
        try
        {
            foreach (string host in await FindServers())
            {
                try
                {
                    found.Clear();
                    foreach (string genre in genres) found.AddRange(await SearchGenre(host, genre, perGenre, found));
                    server = host;
                    break;
                }
                catch (Exception)
                {
                    // try the next server
                }
            }
        }
        catch (Exception)
        {
        }

        if (server != null && found.Count > 0)
        {
            SaveCache(found);
            message = $"The radio found {found.Count} live web stations.";
            return found;
        }

        List<Info> cached = LoadCache();
        message = cached.Count > 0
            ? "The radio couldn't reach Radio Browser, so it's using the web stations it found last time."
            : "The radio couldn't reach Radio Browser. Web stations are off until next time.";
        return cached;
    }

    // The most-listened working stations for one genre that Windows can play (MP3 or AAC, not HLS playlists).
    private static async Task<List<Info>> SearchGenre(string host, string genre, int perGenre, List<Info> already)
    {
        string url = $"https://{host}/json/stations/bytag/{Uri.EscapeDataString(genre)}?hidebroken=true&order=clickcount&reverse=true&limit=40";
        using JsonDocument doc = JsonDocument.Parse(await http.GetStringAsync(url));

        var picked = new List<Info>();
        foreach (JsonElement station in doc.RootElement.EnumerateArray())
        {
            if (picked.Count >= perGenre) break;

            string codec = Text(station, "codec").ToUpperInvariant();
            string stream = Text(station, "url_resolved");
            string uuid = Text(station, "stationuuid");
            string name = Text(station, "name").Trim();
            bool playable = (codec.StartsWith("MP3") || codec.StartsWith("AAC")) && Number(station, "hls") == 0;
            if (!playable || !stream.StartsWith("http") || name.Length == 0) continue;
            if (already.Any(s => s.Uuid == uuid || s.Name == name) || picked.Any(s => s.Uuid == uuid || s.Name == name)) continue;

            picked.Add(new Info(uuid, name.Length > 32 ? name[..32].Trim() : name, stream, genre, Text(station, "countrycode")));
        }
        return picked;
    }

    // Radio Browser asks apps to look up its servers rather than rely on one: all.api.radio-browser.info
    // lists their addresses, and each address gives back its name. Shuffled, so the load is shared.
    private static async Task<List<string>> FindServers()
    {
        var names = new List<string>();
        try
        {
            foreach (IPAddress address in await Dns.GetHostAddressesAsync("all.api.radio-browser.info"))
            {
                try
                {
                    string name = (await Dns.GetHostEntryAsync(address)).HostName;
                    if (name.EndsWith("radio-browser.info") && !names.Contains(name)) names.Add(name);
                }
                catch (Exception) { }
            }
        }
        catch (Exception) { }

        names = names.OrderBy(_ => Rng.Next(1000)).ToList();
        foreach (string fallback in FallbackServers)
        {
            if (!names.Contains(fallback)) names.Add(fallback);
        }
        return names;
    }

    // Some fields have changed type over the API's life, so these read whatever is there.
    private static string Text(JsonElement e, string field)
    {
        return e.TryGetProperty(field, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    }

    private static int Number(JsonElement e, string field)
    {
        if (!e.TryGetProperty(field, out JsonElement v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetInt32(),
            JsonValueKind.True => 1,
            JsonValueKind.String => int.TryParse(v.GetString(), out int n) ? n : 0,
            _ => 0,
        };
    }

    // ---------------- FILES ----------------

    private void WriteDefaultConfig()
    {
        if (File.Exists(ConfigPath)) return;
        try
        {
            File.WriteAllText(ConfigPath,
                "# Live web stations for the Birb Box radio, found through Radio Browser (radio-browser.info),\r\n" +
                "# a free directory run by volunteers. One genre per line; lines starting with # are skipped.\r\n" +
                "# For each genre the radio picks the most-listened stations that are working.\r\n" +
                "# Changes are read the next time Birb Box starts. Write \"off\" on its own line to switch web stations off.\r\n" +
                $"stations per genre: {DefaultPerGenre}\r\n" +
                "ambient\r\nlofi\r\njazz\r\nclassical\r\nmedieval\r\n");
        }
        catch (Exception) { }
    }

    private (List<string> genres, int perGenre) ReadConfig()
    {
        var genres = new List<string>();
        int perGenre = DefaultPerGenre;
        try
        {
            foreach (string raw in File.ReadAllLines(ConfigPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (line.Equals("off", StringComparison.OrdinalIgnoreCase)) return (new List<string>(), 0);
                if (line.StartsWith("stations per genre:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line[(line.IndexOf(':') + 1)..].Trim(), out int n)) perGenre = Math.Clamp(n, 1, 10);
                    continue;
                }
                genres.Add(line.ToLowerInvariant());
            }
        }
        catch (Exception) { }
        return (genres, perGenre);
    }

    private void SaveCache(List<Info> stations)
    {
        try { File.WriteAllText(cachePath, JsonSerializer.Serialize(stations)); }
        catch (Exception) { }
    }

    private List<Info> LoadCache()
    {
        try { return JsonSerializer.Deserialize<List<Info>>(File.ReadAllText(cachePath)) ?? new List<Info>(); }
        catch (Exception) { return new List<Info>(); }
    }
}
