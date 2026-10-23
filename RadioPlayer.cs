using System.Collections.Concurrent;
using System.Diagnostics;
using Raylib_cs;

// The radio's sound; nothing in here draws.
//  - Stations: each sub-folder of Radio is one (loose files in Radio itself make one called "Radio"),
//    plus live web stations found through Radio Browser (see WebStations). All sit on an FM dial, 88 to 108.
//  - Folder stations keep broadcasting while you're tuned away, so you tune in mid-song (see RadioStation).
//  - The station being heard is decoded on a background thread (RadioDecoder), shaped to sound like a small
//    speaker (RadioSpeaker), and fed into one Raylib audio stream. Static is a separate looping sound,
//    so turning the volume or the tuning knob is heard straight away.
//  - Each station's signal drifts a little over time, so now and then static creeps in and you nudge the tuning.
//  - The folder is read again every time the radio is switched on, so new music shows up without a restart.
// While developing, the Radio folder sits in the project (next to BirdsApp.csproj); in a Release copy, next to the .exe.
public class RadioPlayer
{
    // Windows Media Foundation plays all of these except .ogg, which NAudio.Vorbis handles
    private static readonly string[] Playable = { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".wma", ".ogg" };

    // LEVELS
    private const float StartVolume = 0.5f;
    private const float StaticLevel = 0.35f;     // static at full volume, compared to music
    private const float DuckedShare = 0.35f;     // how loud it plays while the Esc menu is open
    private const float DuckRate = 3f;
    private const float WarmUpRate = 1.4f;       // switched on, it warms up over about 0.7 s, like old valves
    private const float CoolDownRate = 5f;

    // RECEPTION
    private const float ClearShare = 0.35f;      // inside this share of a station's reach, reception is perfect
    private const float DriftShare = 0.55f;      // how far a station's signal wanders, as a share of its reach
    private const float WebConnectTimeout = 15f; // seconds to wait for a web station before giving up for now
    private const float WebRetryDelay = 60f;     // seconds before trying a web station that didn't answer

    // OUTPUT: one Raylib stream of 44100 Hz stereo floats, refilled a chunk at a time (about 0.09 s)
    private const int ChunkFrames = 4096;

    public string Folder { get; }
    public bool On { get; private set; }
    public float Volume = StartVolume;           // 0..1, set by the volume knob
    public float Tuning = 0.5f;                  // 0..1 along the dial, set by the tuning knob
    public float Warmth { get; private set; }    // 0 when off, 1 when fully warmed up (also lights the dial)
    public float Signal { get; private set; }    // 0 = pure static, 1 = clear sound
    public IReadOnlyList<RadioStation> Stations => stations;
    public float TunedFrequency => RadioStation.LowestFrequency + Tuning * (RadioStation.HighestFrequency - RadioStation.LowestFrequency);

    private readonly WebStations web;
    private List<RadioStation> folderStations = new();
    private List<RadioStation> webStations = new();
    private List<RadioStation> stations = new();
    private RadioStation? current;               // the station nearest the needle, if the needle is within its reach
    private RadioDecoder? decoder;
    private double decoderStartedAt;
    private readonly RadioSpeaker speaker = new RadioSpeaker();
    private AudioStream output;
    private readonly float[] chunk = new float[ChunkFrames * 2];
    private Music staticHiss;
    private float duck = 1f;

    private static readonly Stopwatch clock = Stopwatch.StartNew();    // the radio's own clock; keeps running while paused
    private static double Now => clock.Elapsed.TotalSeconds;
    private readonly ConcurrentDictionary<string, double> lengths = new();    // song lengths in seconds, filled in the background
    private readonly HashSet<string> reported = new();                        // problems already written to the Bird Log
    private bool reportedEmpty = false;

    public RadioPlayer()
    {
        Folder = Path.Combine(Assets.ProjectFolder ?? AppContext.BaseDirectory, "Radio");
        web = new WebStations(Folder);
    }

    public void Load()
    {
        CreateFolder();
        ScanFolder();
        web.StartSearch();
        if (stations.Count > 0) Tuning = stations[0].Position;

        // The stream's chunk size is set just for this stream, then put back to Raylib's default
        Raylib.SetAudioStreamBufferSizeDefault(ChunkFrames);
        output = Raylib.LoadAudioStream(RadioDecoder.SampleRate, 32, 2);
        Raylib.SetAudioStreamBufferSizeDefault(0);
        Raylib.SetAudioStreamVolume(output, 0f);
        Raylib.PlayAudioStream(output);

        staticHiss = Raylib.LoadMusicStream(Assets.PathTo("sounds/radio_static.wav"));
        Raylib.SetMusicVolume(staticHiss, 0f);
        Raylib.PlayMusicStream(staticHiss);
    }

    public void Unload()
    {
        StopDecoder();
        Raylib.UnloadAudioStream(output);
        Raylib.UnloadMusicStream(staticHiss);
    }

    // The Radio folder, with a note inside saying what goes there.
    private void CreateFolder()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string note = Path.Combine(Folder, "Put music here.txt");
            File.WriteAllText(note,
                "Birb Box radio\r\n\r\n" +
                "Put music files in this folder: mp3, wav, flac, m4a, aac, wma or ogg.\r\n" +
                "Each sub-folder becomes its own station on the dial. Loose files here make one more, called \"Radio\".\r\n" +
                "Folder stations keep broadcasting while you listen to something else, so you tune in mid-song.\r\n\r\n" +
                "To name a station and pick its frequency, put a station.txt in its folder:\r\n" +
                "    name: Emberwing FM\r\n" +
                "    frequency: 92.3\r\n\r\n" +
                "Live web stations come from Radio Browser; pick their genres in web-stations.txt.\r\n" +
                "The radio looks at this folder again every time you switch it on.\r\n");
        }
        catch (Exception e)
        {
            EventLog.Add($"The radio couldn't make its music folder: {e.Message}");
        }
    }

    // ---------------- CONTROLS ----------------

    // The power button. Switching on reads the folder again; if the needle was on a station, it stays on it.
    public void SetOn(bool on)
    {
        if (on == On) return;
        On = on;
        if (!on) return;

        RadioStation? before = current;
        ScanFolder();
        if (before != null && stations.Contains(before)) Tuning = before.Position;

        if (stations.Count == 0 && !reportedEmpty)
        {
            EventLog.Add($"The radio has no stations yet. Put songs in {Folder} (each sub-folder becomes a station).");
            reportedEmpty = true;
        }
    }

    // The next button: skips to the next song on a folder station. Live web stations can't be skipped.
    public void NextTrack()
    {
        if (!On || current == null || current.IsWeb || current.Dead) return;
        StopDecoder();
        current.StartNextSong(Now);
    }

    public bool CanSkip => current != null && !current.IsWeb;

    // How far either side of a station the needle still picks it up, as a share of the dial.
    public float Reach => stations.Count == 0 ? 0f : MathF.Min(0.04f, 0.45f / stations.Count);

    // The station the needle is closest to (counting its drift), and how clearly it comes in (0..1).
    public RadioStation? NearestStation(out float clarity)
    {
        clarity = 0f;
        RadioStation? best = null;
        float bestDistance = float.MaxValue;
        foreach (RadioStation station in stations)
        {
            float distance = MathF.Abs(Tuning - (station.Position + Drift(station)));
            if (distance < bestDistance)
            {
                best = station;
                bestDistance = distance;
            }
        }
        if (best == null || bestDistance >= Reach) return null;

        float t = Math.Clamp((1f - bestDistance / Reach) / (1f - ClearShare), 0f, 1f);
        clarity = t * t * (3f - 2f * t);    // smoothstep, so the sound eases in instead of snapping
        return best;
    }

    // Two slow waves with their own timing per station, so the signals wander out of step.
    private float Drift(RadioStation station)
    {
        float t = (float)Now;
        float wave = 0.6f * MathF.Sin(t * MathF.Tau / 71f + station.DriftPhaseA) + 0.4f * MathF.Sin(t * MathF.Tau / 29f + station.DriftPhaseB);
        return Reach * DriftShare * wave;
    }

    // Two short lines for the hover tag: which station, and what it's doing.
    public (string station, string detail) Describe()
    {
        if (!On) return ("Switched off.", "");
        if (stations.Count == 0) return ("No stations yet.", "See the Bird Log.");
        if (current == null) return ($"{TunedFrequency:0.0} FM", "Static between stations.");

        string name = $"{current.Frequency:0.0} FM  {current.Name}";
        if (current.IsWeb && Now < current.RetryAt) return (name, "Not answering. Trying again soon.");
        if (current.Dead) return (name, "Nothing here will play.");
        if (decoder == null || !decoder.HasSound) return (name, "Tuning in...");
        if (current.IsWeb) return (name, $"Live: {current.Details}");
        return (name, Path.GetFileNameWithoutExtension(current.Playing ?? ""));
    }

    // ---------------- UPDATE ----------------

    // Called every frame, even while the Esc menu is open. ducked: play quieter (the menu is open).
    public void Update(float dt, bool ducked)
    {
        float warmTarget = On ? 1f : 0f;
        float warmRate = On ? WarmUpRate : CoolDownRate;
        Warmth = Math.Clamp(Warmth + MathF.Sign(warmTarget - Warmth) * warmRate * dt, 0f, 1f);
        duck += ((ducked ? DuckedShare : 1f) - duck) * MathF.Min(1f, DuckRate * dt);

        List<WebStations.Info>? found = web.TakeResult();
        if (found != null)
        {
            webStations = found.Select(MakeWebStation).ToList();
            RebuildDial();
        }

        RadioStation? nearest = NearestStation(out float clarity);
        if (nearest != current)
        {
            StopDecoder();
            current = nearest;
        }

        if (On && current != null)
        {
            if (current.IsWeb) KeepWebPlaying(current);
            else KeepFolderPlaying(current);
        }
        else
        {
            StopDecoder();
        }

        // A folder station counts as on air even in the moment between songs, so no static bursts in there;
        // a web station only once its stream is actually coming in.
        bool receiving = On && current != null &&
            (current.IsWeb ? decoder != null && decoder.HasSound && !decoder.Failed : !current.Dead);
        Signal = receiving ? clarity : 0f;
        FeedOutput();

        // Squaring the knob's value makes the low end of the dial finer, which matches how loudness is heard
        float level = Volume * Volume * Warmth * duck;
        Raylib.SetAudioStreamVolume(output, level * Signal);
        Raylib.SetMusicVolume(staticHiss, level * (1f - Signal) * StaticLevel);
        Raylib.UpdateMusicStream(staticHiss);
    }

    // A folder station: start whatever is on air now, and move on when a song ends or won't play.
    private void KeepFolderPlaying(RadioStation station)
    {
        double now = Now;
        if (decoder == null)
        {
            (string file, double offset)? onAir = station.OnAirNow(now, LengthOf);
            if (onAir != null) StartDecoder(onAir.Value.file, onAir.Value.offset);
            return;
        }

        if (decoder.Length != null) lengths[decoder.Source] = decoder.Length.Value;
        if (decoder.Failed)
        {
            station.Bad.Add(decoder.Source);
            Report(decoder.Source, $"The radio can't play {Path.GetFileName(decoder.Source)}. Skipping it.");
            StopDecoder();
            station.StartNextSong(now);
        }
        else if (decoder.Ended && decoder.Available == 0)
        {
            StopDecoder();
            station.StartNextSong(now);
        }
    }

    // A web station: connect, and if it fails, drops out or never answers, wait a while before trying again.
    private void KeepWebPlaying(RadioStation station)
    {
        double now = Now;
        if (decoder == null)
        {
            if (now < station.RetryAt) return;
            StartDecoder(station.Url, 0);
            if (!station.CountedClick)
            {
                web.CountClick(station.Uuid);
                station.CountedClick = true;
            }
            return;
        }

        bool timedOut = !decoder.HasSound && now - decoderStartedAt > WebConnectTimeout;
        bool dropped = decoder.Ended && decoder.Available == 0;
        if (decoder.Failed || timedOut)
        {
            Report(station.Uuid, $"{station.Name} isn't answering. The radio will try it again in a minute.");
            StopDecoder();
            station.RetryAt = now + WebRetryDelay;
        }
        else if (dropped)
        {
            StopDecoder();
            station.RetryAt = now + 3;
        }
    }

    // Tops up the Raylib stream: whenever one of its two chunks has been played, decode the next one into it.
    // Missing sound (still connecting, or nothing tuned) is filled with silence.
    private void FeedOutput()
    {
        for (int i = 0; i < 2 && Raylib.IsAudioStreamProcessed(output); i++)
        {
            int got = decoder?.Read(chunk, 0, chunk.Length) ?? 0;
            if (got < chunk.Length) Array.Clear(chunk, got, chunk.Length - got);
            speaker.Process(chunk, ChunkFrames, Signal);
            Raylib.UpdateAudioStream<float>(output, chunk, ChunkFrames);
        }
    }

    private void StartDecoder(string source, double offset)
    {
        decoder = new RadioDecoder(source, offset);
        decoderStartedAt = Now;
    }

    private void StopDecoder()
    {
        decoder?.Dispose();
        decoder = null;
    }

    private double? LengthOf(string file) => lengths.TryGetValue(file, out double seconds) ? seconds : null;

    private void Report(string key, string message)
    {
        if (reported.Add(key)) EventLog.Add(message);
    }

    // ---------------- STATIONS AND THE DIAL ----------------

    private static RadioStation MakeWebStation(WebStations.Info info)
    {
        string details = info.Country.Length > 0 ? $"{info.Genre}, {info.Country}" : info.Genre;
        return new RadioStation { IsWeb = true, Name = info.Name, Url = info.Url, Uuid = info.Uuid, Details = details };
    }

    // Puts folder and web stations together and gives a frequency to every station that didn't choose one.
    // Each goes where it's farthest from the others (and the dial's ends). Folder stations are placed first,
    // in folder order, so they land in the same spots every time.
    private void RebuildDial()
    {
        stations = folderStations.Concat(webStations).ToList();

        var taken = new List<float> { RadioStation.LowestFrequency, RadioStation.HighestFrequency };
        taken.AddRange(stations.Where(s => s.FrequencyChosen).Select(s => s.Frequency));

        foreach (RadioStation station in stations.Where(s => !s.FrequencyChosen))
        {
            float best = 98.1f, bestGap = -1f;
            for (int i = 0; i < 100; i++)
            {
                float mhz = 88.1f + 0.2f * i;
                float gap = taken.Min(t => MathF.Abs(t - mhz));
                if (gap > bestGap + 0.001f)
                {
                    best = mhz;
                    bestGap = gap;
                }
            }
            station.Frequency = MathF.Round(best * 10f) / 10f;
            taken.Add(station.Frequency);
        }
    }

    // Reads the Radio folder into folder stations. Stations that were already there keep their broadcast going.
    private void ScanFolder()
    {
        var found = new List<RadioStation>();
        try
        {
            AddFolderStation(found, Folder, "Radio");
            foreach (string dir in Directory.GetDirectories(Folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                AddFolderStation(found, dir, Path.GetFileName(dir));
            }
        }
        catch (Exception e)
        {
            EventLog.Add($"The radio couldn't read its music folder: {e.Message}");
        }
        folderStations = found;
        RebuildDial();
        MeasureSongs(found.SelectMany(s => s.Files));
    }

    private void AddFolderStation(List<RadioStation> list, string dir, string name)
    {
        List<string> files = Directory.GetFiles(dir)
            .Where(f => Playable.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0) return;

        RadioStation station = folderStations.Find(s => s.FolderPath == dir) ?? new RadioStation { FolderPath = dir };
        station.Name = name;
        station.ReadStationFile();
        station.SetFiles(files);
        list.Add(station);
    }

    // Looks up song lengths in the background, so the station clocks know when each song ends.
    // Until a length is known, a song is guessed at three and a half minutes.
    private void MeasureSongs(IEnumerable<string> files)
    {
        List<string> todo = files.Where(f => !lengths.ContainsKey(f)).ToList();
        if (todo.Count == 0) return;
        Task.Run(() =>
        {
            foreach (string file in todo)
            {
                try
                {
                    using var reader = RadioDecoder.Open(file);
                    if (reader.TotalTime > TimeSpan.Zero) lengths[file] = reader.TotalTime.TotalSeconds;
                }
                catch (Exception) { }
            }
        });
    }
}
