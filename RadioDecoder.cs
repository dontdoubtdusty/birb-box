using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

// Turns one song file or one web stream into raw sound, on its own background thread,
// so a slow file or a slow connection never stalls the game.
// The sound comes out as 44100 Hz stereo floats into a small ring buffer that RadioPlayer reads from each frame.
// NAudio does the decoding: Windows Media Foundation for most formats and web streams, NAudio.Vorbis for .ogg.
public sealed class RadioDecoder : IDisposable
{
    public const int SampleRate = 44100;
    private const int BufferSeconds = 4;

    public string Source { get; }
    public bool Failed { get; private set; }
    public bool Ended { get; private set; }            // the decoder reached the end (a song finished, or a stream dropped)
    public string? Error { get; private set; }
    public bool HasSound => totalWritten > 0;          // false while still connecting or opening
    public double? Length { get; private set; }        // seconds, once a file is open (null for live streams)

    private readonly float[] ring = new float[SampleRate * 2 * BufferSeconds];
    private int readPos, writePos, count;
    private long totalWritten;
    private readonly object gate = new();
    private volatile bool stop;

    // source: a file path or an http(s) link. startAt: seconds into a file to begin from.
    public RadioDecoder(string source, double startAt)
    {
        Source = source;
        var thread = new Thread(() => Run(startAt)) { IsBackground = true, Name = "Radio decoder" };
        thread.Start();
    }

    // Opens a file or stream with the right reader. Also used to look up song lengths.
    public static WaveStream Open(string source)
    {
        if (!source.StartsWith("http", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(source).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return new VorbisWaveReader(source);
        }
        return new MediaFoundationReader(source);
    }

    // The background thread: open, skip ahead, convert to 44100 Hz stereo, and keep the ring buffer topped up.
    private void Run(double startAt)
    {
        try
        {
            using WaveStream reader = Open(Source);
            bool isFile = reader.TotalTime > TimeSpan.Zero;
            if (isFile)
            {
                Length = reader.TotalTime.TotalSeconds;
                if (startAt > 0 && reader.CanSeek) reader.CurrentTime = TimeSpan.FromSeconds(Math.Min(startAt, Length.Value));
            }

            ISampleProvider samples = reader.ToSampleProvider();
            if (samples.WaveFormat.Channels == 1) samples = new MonoToStereoSampleProvider(samples);
            else if (samples.WaveFormat.Channels > 2) throw new NotSupportedException("more than two channels");
            if (samples.WaveFormat.SampleRate != SampleRate) samples = new WdlResamplingSampleProvider(samples, SampleRate);

            float[] chunk = new float[4096];
            while (!stop)
            {
                if (FreeSpace() < chunk.Length)
                {
                    Thread.Sleep(10);
                    continue;
                }
                int got = samples.Read(chunk, 0, chunk.Length);
                if (got <= 0)
                {
                    Ended = true;
                    return;
                }
                Write(chunk, got);
            }
        }
        catch (Exception e)
        {
            Error = e.Message;
            Failed = true;
        }
    }

    // Copies up to count floats into destination. Returns how many it had ready (always a whole number of stereo frames).
    public int Read(float[] destination, int offset, int wanted)
    {
        lock (gate)
        {
            int n = Math.Min(wanted, count) & ~1;
            for (int i = 0; i < n; i++)
            {
                destination[offset + i] = ring[readPos];
                readPos = (readPos + 1) % ring.Length;
            }
            count -= n;
            return n;
        }
    }

    public int Available
    {
        get { lock (gate) return count; }
    }

    private int FreeSpace()
    {
        lock (gate) return ring.Length - count;
    }

    private void Write(float[] data, int n)
    {
        lock (gate)
        {
            for (int i = 0; i < n; i++)
            {
                ring[writePos] = data[i];
                writePos = (writePos + 1) % ring.Length;
            }
            count += n;
            totalWritten += n;
        }
    }

    // Asks the thread to stop. It finishes on its own (a stalled web stream may take a while to let go).
    public void Dispose() => stop = true;
}
