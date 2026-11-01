using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// Live web stations from Radio Browser (radio-browser.info), a free directory run by volunteers.
// What goes on the dial is kept in Radio/my-stations.json, edited in the game's station finder (click the radio's dial):
//  - your own stations, saved by their Radio Browser id (so the lookup is exact) with the stream address
//    and the frequency they landed on, so they stay put on the dial;
//  - genre tags, each bringing its most-listened working stations (how many is set per tag).
// A station can also be written into the file by hand with an empty "Uuid" and just a "Url".
// Network work runs on background tasks; their results are handed to the main thread through a queue
// (Update runs them), so the lists here are only ever touched on the main thread.
public class WebStations
{
    private const int DefaultPerTag = 2;
    private const int MaxPerTag = 10;
    private const int MaxNameLength = 32;
    private const int SearchLimit = 60;
    private static readonly string[] DefaultTags = { "ambient", "lofi", "jazz", "classical", "medieval" };
    private static readonly string[] FallbackServers = { "de1.api.radio-browser.info", "de2.api.radio-browser.info", "fi1.api.radio-browser.info" };
    private static readonly Regex UuidPattern = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    // A station as the radio needs it.
    public record Info(string Uuid, string Name, string Url, string Genre, string Country)
    {
        [JsonIgnore] public string Key => Uuid.Length > 0 ? Uuid : Url;
    }

    // A search result: the station plus what the finder shows about it.
    // WhyNot is empty when the radio can play it, otherwise the reason it can't (shown greyed out).
    public record Found(Info Info, string Codec, int Bitrate, string Tags, string WhyNot)
    {
        public bool Playable => WhyNot.Length == 0;
    }

    // One of your stations, as saved in my-stations.json. Frequency 0 means "not placed yet".
    public class MyStation
    {
        public string Uuid { get; set; } = "";
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Country { get; set; } = "";
        public float Frequency { get; set; }

        [JsonIgnore] public string Key => Uuid.Length > 0 ? Uuid : Url;
        public Info ToInfo() => new Info(Uuid, Name, Url, Genre.Length > 0 ? Genre : "web station", Country);
    }

    // One genre tag and how many of its stations go on the dial.
    public class TagPick
    {
        public string Tag { get; set; } = "";
        public int Count { get; set; } = DefaultPerTag;
    }

    // A station on the dial, with your saved entry when it's one of yours (null for a tag's station).
    public record DialEntry(Info Info, MyStation? Mine);

    private class DialFile
    {
        public List<MyStation> Stations { get; set; } = new();
        public List<TagPick> Tags { get; set; } = new();
    }

    public string DialPath { get; }
    private readonly string cachePath;
    private DialFile dial = new();
    private Dictionary<string, List<Info>> tagStations = new();   // each tag's playable candidates, most-listened first
    private readonly HashSet<string> tagsSearching = new();

    // Bumped whenever the dial's station list changes, so RadioPlayer knows to rebuild it.
    public int Version { get; private set; }

    public IReadOnlyList<MyStation> MyStations => dial.Stations;
    public IReadOnlyList<TagPick> Tags => dial.Tags;
    public bool IsSearchingTag(string tag) => tagsSearching.Contains(tag);
    public bool HasStation(string key) => dial.Stations.Any(s => s.Key == key);
    public int FoundForTag(string tag) => tagStations.TryGetValue(tag, out List<Info>? found) ? found.Count : 0;

    private readonly ConcurrentQueue<Action> finished = new();    // results from the background, run on the main thread
    private static readonly HttpClient http = MakeClient();
    private static readonly object serverGate = new();
    private static Task<List<string>>? serverList;
    private static volatile string? server;                        // the server that last answered, tried first

    public WebStations(string radioFolder)
    {
        DialPath = Path.Combine(radioFolder, "my-stations.json");
        cachePath = Path.Combine(radioFolder, "web-stations-cache.json");
    }

    private static HttpClient MakeClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BirbCube/" + GameEngine.Version.Replace("Birb Cube ", "").Replace(' ', '-'));
        return client;
    }

    // ---------------- START-UP AND THE MAIN THREAD ----------------

    // Loads your dial (making one with the default tags the first time), puts last time's tag stations
    // straight on it from the cache, then refreshes everything from Radio Browser in the background.
    public void Start()
    {
        LoadDial();
        tagStations = LoadCache();
        Version++;

        List<string> uuids = dial.Stations.Where(s => s.Uuid.Length > 0).Select(s => s.Uuid).ToList();
        List<string> tags = dial.Tags.Select(t => t.Tag).ToList();
        foreach (string tag in tags) tagsSearching.Add(tag);
        Task.Run(() => RefreshAll(uuids, tags));
    }

    // Called every frame on the main thread: applies whatever the background has finished.
    public void Update()
    {
        while (finished.TryDequeue(out Action? apply)) apply();
    }

    // Everything that goes on the dial: your stations first (in your order), then each tag's top stations,
    // skipping any station that's already on.
    public List<DialEntry> DialStations()
    {
        var list = new List<DialEntry>();
        var seen = new HashSet<string>();
        foreach (MyStation mine in dial.Stations)
        {
            if (mine.Url.Length > 0 && seen.Add(mine.Key)) list.Add(new DialEntry(mine.ToInfo(), mine));
        }
        foreach (TagPick pick in dial.Tags)
        {
            if (!tagStations.TryGetValue(pick.Tag, out List<Info>? candidates)) continue;
            int added = 0;
            foreach (Info info in candidates)
            {
                if (added >= pick.Count) break;
                if (seen.Add(info.Key))
                {
                    list.Add(new DialEntry(info, null));
                    added++;
                }
            }
        }
        return list;
    }

    // ---------------- EDITING (main thread, from the station finder) ----------------

    // Adds a station to your list and the dial. Does nothing if it's already yours.
    public void AddStation(Info info)
    {
        if (HasStation(info.Key)) return;
        dial.Stations.Add(new MyStation { Uuid = info.Uuid, Name = info.Name, Url = info.Url, Genre = info.Genre, Country = info.Country });
        SaveDial();
        Version++;
    }

    public void RemoveStation(MyStation station)
    {
        if (!dial.Stations.Remove(station)) return;
        SaveDial();
        Version++;
    }

    // Adds a genre tag and looks up its stations. Tags are kept in lower case, each once.
    public void AddTag(string tag)
    {
        tag = tag.Trim().ToLowerInvariant();
        if (tag.Length == 0 || dial.Tags.Any(t => t.Tag == tag)) return;
        dial.Tags.Add(new TagPick { Tag = tag });
        SaveDial();
        Version++;
        tagsSearching.Add(tag);
        Task.Run(async () =>
        {
            List<Info>? found = await SearchTagSafe(tag);
            finished.Enqueue(() => ApplyTag(tag, found, reportEmpty: true));
        });
    }

    public void RemoveTag(TagPick pick)
    {
        if (!dial.Tags.Remove(pick)) return;
        SaveDial();
        Version++;
    }

    // How many of a tag's stations go on the dial (1 to 10). The candidates are already known, so no lookup.
    public void SetTagCount(TagPick pick, int count)
    {
        count = Math.Clamp(count, 1, MaxPerTag);
        if (pick.Count == count) return;
        pick.Count = count;
        SaveDial();
        Version++;
    }

    // RadioPlayer calls this once your station has landed on the dial, so it keeps that spot next time.
    public void RememberFrequency(MyStation station, float frequency)
    {
        if (MathF.Abs(station.Frequency - frequency) < 0.01f) return;
        station.Frequency = frequency;
        SaveDial();
    }

    // ---------------- SEARCH (for the finder) ----------------

    public enum SearchBy { Name, Genre, Country }

    // Searches Radio Browser, most-listened first. Text holding a station id (or a radio-browser.info link with one)
    // looks up exactly that station. Throws if no server answers; the finder shows that.
    public static async Task<List<Found>> Search(string text, SearchBy by)
    {
        text = text.Trim();
        Match id = UuidPattern.Match(text);
        string path;
        if (id.Success)
        {
            path = $"/json/stations/byuuid?uuids={id.Value}";
        }
        else
        {
            string field = by switch
            {
                SearchBy.Genre => "tag",
                SearchBy.Country => text.Length == 2 ? "countrycode" : "country",
                _ => "name",
            };
            path = $"/json/stations/search?{field}={Uri.EscapeDataString(text)}&hidebroken=true&order=clickcount&reverse=true&limit={SearchLimit}";
        }

        using JsonDocument doc = JsonDocument.Parse(await Get(path));
        var results = new List<Found>();
        foreach (JsonElement station in doc.RootElement.EnumerateArray())
        {
            Found? found = ReadStation(station);
            if (found != null && !results.Any(r => r.Info.Uuid == found.Info.Uuid)) results.Add(found);
        }
        return results;
    }

    // Tells Radio Browser a station was listened to; it uses this to rank stations. Fire and forget.
    public void CountClick(string uuid)
    {
        if (server == null || uuid.Length == 0) return;
        _ = http.GetAsync($"https://{server}/json/url/{uuid}").ContinueWith(_ => { });
    }

    // ---------------- BACKGROUND ----------------

    // At start-up: fresh stream addresses for your stations (they move now and then), then every tag.
    private async Task RefreshAll(List<string> uuids, List<string> tags)
    {
        bool reached = true;
        if (uuids.Count > 0)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(await Get($"/json/stations/byuuid?uuids={string.Join(",", uuids)}"));
                var fresh = new Dictionary<string, string>();
                foreach (JsonElement station in doc.RootElement.EnumerateArray())
                {
                    Found? found = ReadStation(station);
                    if (found != null && found.Playable) fresh[found.Info.Uuid] = found.Info.Url;
                }
                finished.Enqueue(() => ApplyFreshUrls(fresh));
            }
            catch (Exception)
            {
                reached = false;
            }
        }

        int tagStationsFound = 0;
        foreach (string tag in tags)
        {
            List<Info>? found = await SearchTagSafe(tag);
            if (found == null) reached = false;
            else tagStationsFound += found.Count;
            finished.Enqueue(() => ApplyTag(tag, found, reportEmpty: false));
        }

        int mine = uuids.Count;
        finished.Enqueue(() =>
        {
            if (reached) EventLog.Add($"The radio tuned in {mine} of your web stations and found {tagStationsFound} more for your genres.");
            else if (DialStations().Count > 0) EventLog.Add("The radio couldn't reach Radio Browser, so it's using the web stations it knew last time.");
            else EventLog.Add("The radio couldn't reach Radio Browser. Web stations are off until it can.");
        });
    }

    // A tag's playable stations, most-listened first, or null if Radio Browser couldn't be reached.
    private static async Task<List<Info>?> SearchTagSafe(string tag)
    {
        try
        {
            string path = $"/json/stations/bytag/{Uri.EscapeDataString(tag)}?hidebroken=true&order=clickcount&reverse=true&limit=40";
            using JsonDocument doc = JsonDocument.Parse(await Get(path));
            var picked = new List<Info>();
            foreach (JsonElement station in doc.RootElement.EnumerateArray())
            {
                if (picked.Count >= MaxPerTag) break;
                Found? found = ReadStation(station);
                if (found == null || !found.Playable) continue;
                if (picked.Any(s => s.Uuid == found.Info.Uuid || s.Name == found.Info.Name)) continue;
                picked.Add(found.Info with { Genre = tag });
            }
            return picked;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ---------------- APPLYING RESULTS (main thread) ----------------

    private void ApplyFreshUrls(Dictionary<string, string> fresh)
    {
        bool changed = false;
        foreach (MyStation station in dial.Stations)
        {
            if (fresh.TryGetValue(station.Uuid, out string? url) && url != station.Url)
            {
                station.Url = url;
                changed = true;
            }
        }
        if (!changed) return;
        SaveDial();
        Version++;
    }

    // A tag's search came back. Null (unreachable) keeps the cached stations it already had.
    private void ApplyTag(string tag, List<Info>? found, bool reportEmpty)
    {
        tagsSearching.Remove(tag);
        if (found == null)
        {
            if (reportEmpty) EventLog.Add($"The radio couldn't reach Radio Browser to look up \"{tag}\". It'll try again next time.");
            return;
        }
        if (found.Count == 0 && reportEmpty) EventLog.Add($"Radio Browser has no stations the radio can play tagged \"{tag}\".");
        tagStations[tag] = found;
        SaveCache();
        Version++;
    }

    // ---------------- READING RADIO BROWSER ----------------

    // One station from Radio Browser's JSON, with the reason the radio can't play it if it can't.
    // The radio plays MP3 and AAC streams through Windows; HLS playlists and other codecs won't work.
    private static Found? ReadStation(JsonElement station)
    {
        string uuid = Text(station, "stationuuid");
        string name = Text(station, "name").Trim();
        if (uuid.Length == 0 || name.Length == 0) return null;

        string stream = Text(station, "url_resolved");
        if (stream.Length == 0) stream = Text(station, "url");
        string codec = Text(station, "codec").Trim().ToUpperInvariant();
        string tags = Text(station, "tags");
        string firstTag = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";

        string whyNot = "";
        if (Number(station, "hls") != 0 || stream.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)) whyNot = "HLS playlist";
        else if (codec.Length == 0 || codec == "UNKNOWN") whyNot = "unknown format";
        else if (!codec.StartsWith("MP3") && !codec.StartsWith("AAC")) whyNot = $"{codec} stream";
        else if (!stream.StartsWith("http", StringComparison.OrdinalIgnoreCase)) whyNot = "no stream link";

        var info = new Info(uuid, Shorten(name), stream, firstTag.Length > 0 ? firstTag : "web station", Text(station, "countrycode"));
        return new Found(info, codec, Number(station, "bitrate"), tags, whyNot);
    }

    // A GET against Radio Browser, trying the server that answered last, then the others. Throws if none answer.
    private static async Task<string> Get(string pathAndQuery)
    {
        List<string> hosts = new(await Servers());
        string? last = server;
        if (last != null)
        {
            hosts.Remove(last);
            hosts.Insert(0, last);
        }

        Exception? error = null;
        foreach (string host in hosts)
        {
            try
            {
                string body = await http.GetStringAsync($"https://{host}{pathAndQuery}");
                server = host;
                return body;
            }
            catch (Exception e)
            {
                error = e;
            }
        }
        throw error ?? new HttpRequestException("No Radio Browser server answered.");
    }

    // The server list, looked up once per run (see FindServers).
    private static Task<List<string>> Servers()
    {
        lock (serverGate) return serverList ??= FindServers();
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
            JsonValueKind.Number => v.TryGetInt32(out int n) ? n : 0,
            JsonValueKind.True => 1,
            JsonValueKind.String => int.TryParse(v.GetString(), out int n) ? n : 0,
            _ => 0,
        };
    }

    private static string Shorten(string name) => name.Length > MaxNameLength ? name[..MaxNameLength].Trim() : name;

    // ---------------- FILES ----------------

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    // Reads my-stations.json. Missing: a new one with the default tags. Unreadable: kept aside as .broken.json.
    private void LoadDial()
    {
        if (File.Exists(DialPath))
        {
            try
            {
                dial = JsonSerializer.Deserialize<DialFile>(File.ReadAllText(DialPath)) ?? new DialFile();
                dial.Stations.RemoveAll(s => s.Url.Length == 0 && s.Uuid.Length == 0);
                foreach (TagPick pick in dial.Tags)
                {
                    pick.Tag = pick.Tag.Trim().ToLowerInvariant();
                    pick.Count = Math.Clamp(pick.Count, 1, MaxPerTag);
                }
                dial.Tags.RemoveAll(t => t.Tag.Length == 0);
                return;
            }
            catch (Exception e)
            {
                EventLog.Add($"The radio couldn't read my-stations.json ({e.Message}). It was renamed my-stations.broken.json.");
                try { File.Move(DialPath, Path.ChangeExtension(DialPath, ".broken.json"), overwrite: true); } catch (Exception) { }
            }
        }

        dial = new DialFile { Tags = DefaultTags.Select(t => new TagPick { Tag = t }).ToList() };
        SaveDial();
    }

    private void SaveDial()
    {
        try { File.WriteAllText(DialPath, JsonSerializer.Serialize(dial, Indented)); }
        catch (Exception) { }
    }

    private void SaveCache()
    {
        try { File.WriteAllText(cachePath, JsonSerializer.Serialize(tagStations)); }
        catch (Exception) { }
    }

    // Last time's tag stations. The cache from before 5.08 was a plain list; that just reads as empty.
    private Dictionary<string, List<Info>> LoadCache()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, List<Info>>>(File.ReadAllText(cachePath)) ?? new(); }
        catch (Exception) { return new(); }
    }
}
