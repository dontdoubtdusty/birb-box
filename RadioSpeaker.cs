// Makes clean music sound like it's coming out of a small wooden radio:
//  - cuts the deep bass and the very top (a small speaker can't make them)
//  - narrows the stereo toward the middle
//  - rounds off loud peaks a little, for warmth
//  - when the signal is weak: the volume wavers and the odd crackle pops through
// Works on 44100 Hz stereo floats, the same as RadioDecoder puts out.
public class RadioSpeaker
{
    // TONE (tune these by ear)
    private const float LowCut = 140f;       // Hz; below this fades away
    private const float HighCut = 5500f;     // Hz; above this fades away
    private const float Width = 0.5f;        // 1 = full stereo, 0 = mono
    private const float Drive = 1.3f;        // how hard loud parts are rounded off
    private const float CrackleRate = 0.0004f;    // chance of a pop per sample at the weakest signal
    private const float WaverDepth = 0.5f;        // how far the volume can dip at the weakest signal

    private readonly Biquad[] highPass = { Biquad.HighPass(LowCut), Biquad.HighPass(LowCut) };
    private readonly Biquad[] lowPass = { Biquad.LowPass(HighCut), Biquad.LowPass(HighCut) };
    private readonly Random random = new Random(5);
    private float waver = 1f, waverTarget = 1f;
    private int waverCountdown;
    private float crackle;

    // frames: stereo pairs in buffer. signal: 0 = lost in static, 1 = clear.
    public void Process(float[] buffer, int frames, float signal)
    {
        float weak = 1f - Math.Clamp(signal, 0f, 1f);

        for (int i = 0; i < frames; i++)
        {
            float left = buffer[i * 2], right = buffer[i * 2 + 1];
            float mid = (left + right) * 0.5f;
            left = mid + (left - mid) * Width;
            right = mid + (right - mid) * Width;

            left = Round(lowPass[0].Process(highPass[0].Process(left)));
            right = Round(lowPass[1].Process(highPass[1].Process(right)));

            // Every so often pick a new level to drift toward; the weaker the signal, the deeper it can dip
            if (--waverCountdown <= 0)
            {
                waverTarget = 1f - weak * WaverDepth * random.NextSingle();
                waverCountdown = random.Next(1500, 6000);
            }
            waver += (waverTarget - waver) * 0.0015f;

            if (weak > 0.05f && random.NextSingle() < weak * CrackleRate)
            {
                crackle = (random.NextSingle() * 2f - 1f) * 0.4f * weak;
            }
            float pop = crackle;
            crackle *= 0.9f;

            buffer[i * 2] = left * waver + pop;
            buffer[i * 2 + 1] = right * waver + pop;
        }
    }

    // Quiet sounds pass unchanged; loud peaks are squashed smoothly instead of clipping.
    private static float Round(float x) => MathF.Tanh(x * Drive) / Drive;

    // A standard two-pole filter (the "RBJ cookbook" formulas). One per channel, since it remembers past samples.
    private class Biquad
    {
        private readonly float b0, b1, b2, a1, a2;
        private float x1, x2, y1, y2;

        private Biquad(float b0, float b1, float b2, float a0, float a1, float a2)
        {
            this.b0 = b0 / a0; this.b1 = b1 / a0; this.b2 = b2 / a0;
            this.a1 = a1 / a0; this.a2 = a2 / a0;
        }

        public static Biquad LowPass(float hz)
        {
            (float cos, float alpha) = Shape(hz);
            return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public static Biquad HighPass(float hz)
        {
            (float cos, float alpha) = Shape(hz);
            return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        // A gentle slope (Q of about 0.7) with no bump at the cutoff
        private static (float cos, float alpha) Shape(float hz)
        {
            float w = 2f * MathF.PI * hz / RadioDecoder.SampleRate;
            return (MathF.Cos(w), MathF.Sin(w) / (2f * 0.7071f));
        }

        public float Process(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x;
            y2 = y1; y1 = y;
            return y;
        }
    }
}
