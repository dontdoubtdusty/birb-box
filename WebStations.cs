using System.Net;
using System.Text.Json;

// Finds live web stations through Radio Browser (radio-browser.info), a free directory run by volunteers.
// What to look for is set in Radio/web-stations.txt (made on first run): genres, or your own picks
// (stations by name, or stream addresses). If any picks are listed, the genres are ignored.
// The search runs in the background at startup; if Radio Browser can't be reached, the last list that worked is used.
public class WebStations
{
    private const string UserAgent = "BirbCube/4.83";
    private const int DefaultPerGenre = 2;
    private const int MaxNameLength = 32;
    private static readonly string[] FallbackServers = { "de1.api.radio-browser.info", "de2.api.radio-browser.info", "fi1.api.radio-browser.info" };

    // A station as Radio Browser describes it. Saved to the cache file as JSON.
    // Stations from a "url:" line have no Uuid.
    public record Info(string Uuid, string Name, string Url, string Genre, string Country);

    // What web-stations.txt asks for.
    private record Config(List<string> Genres, int PerGenre, List<string> Names, List<Info> Urls)
    {
        public bool HasPicks => Names.Count > 0 || Urls.Count > 0;
    }

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
        if (server == null || uuid.Length == 0) return;
        _ = http.GetAsync($"https://{server}/json/url/{uuid}").ContinueWith(_ => { });
    }

    // ---------------- SEARCH (background thread) ----------------

    // Looks up the genres or the named stations on Radio Browser, and adds the stream addresses as they are.
    private async Task<List<Info>> Search()
    {
        Config config = ReadConfig();
        if (!config.HasPicks && config.Genres.Count == 0) return new List<Info>();

        // Only stream addresses: nothing to look up.
        if (config.HasPicks && config.Names.Count == 0)
        {
            message = $"The radio tuned in {config.Urls.Count} web stations from your list.";
            return config.Urls;
        }

        var found = new List<Info>();
        var missing = new List<string>();
        try
        {
            foreach (string host in await FindServers())
            {
                try
                {
                    found.Clear();
                    missing.Clear();
                    if (config.HasPicks)
                    {
                        foreach (string name in config.Names)
                        {
                            Info? station = await SearchName(host, name, found);
                            if (station != null) found.Add(station);
                            else missing.Add(name);
                        }
                    }
                    else
                    {
                        foreach (string genre in config.Genres) found.AddRange(await SearchGenre(host, genre, config.PerGenre, found));
                    }
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

        if (server != null && (found.Count > 0 || config.HasPicks))
        {
            found.AddRange(config.Urls);
            SaveCache(found);
            message = config.HasPicks
                ? $"The radio found {found.Count} of the {config.Names.Count + config.Urls.Count} web stations on your list."
                : $"The radio found {found.Count} live web stations.";
            if (missing.Count > 0) message += $" Not found on Radio Browser: {string.Join(", ", missing)}.";
            return found;
        }

        // Offline: the last list that worked. With picks, the stream addresses come from the file as it is now.
        List<Info> cached = LoadCache();
        if (config.HasPicks) cached = cached.Where(s => s.Uuid.Length > 0).Concat(config.Urls).ToList();
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
            if (!TryReadPlayable(station, out string uuid, out string name, out string stream)) continue;
            if (already.Any(s => s.Uuid == uuid || s.Name == name) || picked.Any(s => s.Uuid == uuid || s.Name == name)) continue;

            picked.Add(new Info(uuid, Shorten(name), stream, genre, Text(station, "countrycode")));
        }
        return picked;
    }

    // One station by name: an exact match (ignoring case) if there is one, otherwise the most-listened
    // station whose name contains it. Null if none of them can be played.
    private static async Task<Info?> SearchName(string host, string wanted, List<Info> already)
    {
        string url = $"https://{host}/json/stations/byname/{Uri.EscapeDataString(wanted)}?hidebroken=true&order=clickcount&reverse=true&limit=30";
        using JsonDocument doc = JsonDocument.Parse(await http.GetStringAsync(url));

        Info? best = null;
        foreach (JsonElement station in doc.RootElement.EnumerateArray())
        {
            if (!TryReadPlayable(station, out string uuid, out string name, out string stream)) continue;
            if (already.Any(s => s.Uuid == uuid)) continue;

            string firstTag = Text(station, "tags").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
            var info = new Info(uuid, Shorten(name), stream, firstTag.Length > 0 ? firstTag : "web station", Text(station, "countrycode"));
            if (name.Equals(wanted, StringComparison.OrdinalIgnoreCase)) return info;
            best ??= info;
        }
        return best;
    }

    // Reads a station's id, name and stream, if Windows can play it (MP3 or AAC, not an HLS playlist).
    private static bool TryReadPlayable(JsonElement station, out string uuid, out string name, out string stream)
    {
        string codec = Text(station, "codec").ToUpperInvariant();
        uuid = Text(station, "stationuuid");
        name = Text(station, "name").Trim();
        stream = Text(station, "url_resolved");
        bool playable = (codec.StartsWith("MP3") || codec.StartsWith("AAC")) && Number(station, "hls") == 0;
        return playable && stream.StartsWith("http") && name.Length > 0;
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

    private static string Shorten(string name) => name.Length > MaxNameLength ? name[..MaxNameLength].Trim() : name;

    // ---------------- FILES ----------------

    private void WriteDefaultConfig()
    {
        if (File.Exists(ConfigPath)) return;
        try
        {
            File.WriteAllText(ConfigPath,
                "# Live web stations for the Birb Cube radio, found through Radio Browser (radio-browser.info),\r\n" +
                "# a free directory run by volunteers. Lines starting with # are skipped.\r\n" +
                "# Changes are read the next time Birb Cube starts. Write \"off\" on its own line to switch web stations off.\r\n" +
                "#\r\n" +
                "# GENRES: one per line. For each genre the radio picks the most-listened stations that are working.\r\n" +
                "#\r\n" +
                "# YOUR OWN PICKS: if any of these lines are here, the genres are ignored.\r\n" +
                "#   station: Radio Paradise              (looked up on Radio Browser by name)\r\n" +
                "#   url: Night Owl FM https://...        (a stream address, played as it is; the name in front is optional)\r\n" +
                "# Streams must be MP3 or AAC, not .m3u8 playlists.\r\n" +
                "#\r\n" +
                $"stations per genre: {DefaultPerGenre}\r\n" +
                "ambient\r\nlofi\r\njazz\r\nclassical\r\nmedieval\r\n");
        }
        catch (Exception) { }
    }

    // Reads genres, "station:" names and "url:" streams from web-stations.txt.
    private Config ReadConfig()
    {
        var config = new Config(new List<string>(), DefaultPerGenre, new List<string>(), new List<Info>());
        int perGenre = DefaultPerGenre;
        try
        {
            foreach (string raw in File.ReadAllLines(ConfigPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (line.Equals("off", StringComparison.OrdinalIgnoreCase))
                    return new Config(new List<string>(), 0, new List<string>(), new List<Info>());

                if (TryValue(line, "stations per genre:", out string count))
                {
                    if (int.TryParse(count, out int n)) perGenre = Math.Clamp(n, 1, 10);
                }
                else if (TryValue(line, "station:", out string name))
                {
                    if (name.Length > 0 && !config.Names.Contains(name, StringComparer.OrdinalIgnoreCase)) config.Names.Add(name);
                }
                else if (TryValue(line, "url:", out string value))
                {
                    Info? stream = ParseUrl(value);
                    if (stream != null) config.Urls.Add(stream);
                }
                else
                {
                    config.Genres.Add(line.ToLowerInvariant());
                }
            }
        }
        catch (Exception) { }
        return config with { PerGenre = perGenre };
    }

    // The text after a "key:" at the start of a line, ignoring case.
    private static bool TryValue(string line, string key, out string value)
    {
        bool match = line.StartsWith(key, StringComparison.OrdinalIgnoreCase);
        value = match ? line[key.Length..].Trim() : "";
        return match;
    }

    // "Night Owl FM https://..." or just "https://...". Without a name, the stream's host name is used.
    private static Info? ParseUrl(string value)
    {
        int at = value.IndexOf("http", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;

        string url = value[at..].Trim();
        string name = value[..at].Trim();
        if (name.Length == 0) name = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Host : url;
        return new Info("", Shorten(name), url, "web stream", "");
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
