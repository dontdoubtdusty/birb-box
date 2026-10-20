using System.Numerics;
using Raylib_cs;

// How far each kind of part is moved from its rest pose, for one bird on one frame.
// Angles are degrees, turned around the part's joint.
public struct BirbMotion
{
    public float Bounce;        // world units the whole bird is lifted (the hop)
    public float BodyPitch;     // + tips the body forward
    public float BodyLift;      // birb units, for breathing
    public float HeadPitch;     // + looks down
    public float HeadYaw;       // + turns to its left
    public float HeadTilt;      // + tilts toward its right shoulder
    public float HeadForward;   // birb units, the pigeon-style neck bob
    public float BeakDip;       // + dips the beak down
    public float WingSpread;    // 0 = folded, about 80 = spread out
    public float WingFlap;      // + raises the wings
    public float TailPitch;     // + lifts the tail
    public float LegTuck;       // + swings both legs back
    public float LegSwing;      // + swings the left leg forward and the right leg back (walking)

    public static BirbMotion Lerp(BirbMotion a, BirbMotion b, float t)
    {
        return new BirbMotion
        {
            Bounce = float.Lerp(a.Bounce, b.Bounce, t),
            BodyPitch = float.Lerp(a.BodyPitch, b.BodyPitch, t),
            BodyLift = float.Lerp(a.BodyLift, b.BodyLift, t),
            HeadPitch = float.Lerp(a.HeadPitch, b.HeadPitch, t),
            HeadYaw = float.Lerp(a.HeadYaw, b.HeadYaw, t),
            HeadTilt = float.Lerp(a.HeadTilt, b.HeadTilt, t),
            HeadForward = float.Lerp(a.HeadForward, b.HeadForward, t),
            BeakDip = float.Lerp(a.BeakDip, b.BeakDip, t),
            WingSpread = float.Lerp(a.WingSpread, b.WingSpread, t),
            WingFlap = float.Lerp(a.WingFlap, b.WingFlap, t),
            TailPitch = float.Lerp(a.TailPitch, b.TailPitch, t),
            LegTuck = float.Lerp(a.LegTuck, b.LegTuck, t),
            LegSwing = float.Lerp(a.LegSwing, b.LegSwing, t),
        };
    }
}

// Draw side only: turns a bird's Pose (set by BirdMover) into part motions. Nothing here changes the simulation.
// When the pose changes, the bird eases from where it was into the new pose over BlendTime.
// The sizes and speeds of each movement come from the birb's MotionStyle.
public class BirbAnimator
{
    private const float BlendTime = 0.2f;     // seconds to ease into a new pose
    private const float FlickTime = 0.45f;    // tail flick after landing

    // What the animator remembers about one bird between frames.
    private class BirdAnim
    {
        public BirdPose Pose;
        public BirbMotion From;       // where the last blend started
        public BirbMotion Shown;      // what was drawn last frame
        public float Blend = 1f;      // 0 = just changed pose, 1 = fully in it
        public float Clock;           // this bird's own animation time
        public float FlickTimer;
        public bool Seen;
    }

    private readonly Dictionary<int, BirdAnim> anims = new();

    public BirbMotion Animate(Bird bird, float dt)
    {
        if (!anims.TryGetValue(bird.EntityID, out BirdAnim? a))
        {
            // A random start time keeps birds from moving in step.
            a = new BirdAnim { Pose = bird.Pose, Clock = Rng.Range(0f, 10f) };
            anims[bird.EntityID] = a;
        }
        a.Seen = true;
        a.Clock += dt;

        if (bird.Pose != a.Pose)
        {
            if (a.Pose == BirdPose.Fly) a.FlickTimer = FlickTime;    // just landed
            a.From = a.Shown;
            a.Pose = bird.Pose;
            a.Blend = 0f;
        }
        a.Blend = MathF.Min(1f, a.Blend + dt / BlendTime);

        MotionStyle style = bird.Shape.Style;
        BirbMotion target = PoseMotion(a.Pose, a.Clock, bird.HopPhase, style);
        a.Shown = BirbMotion.Lerp(a.From, target, Smooth(a.Blend));

        BirbMotion shown = a.Shown;
        if (a.FlickTimer > 0f)
        {
            a.FlickTimer -= dt;
            float done = 1f - a.FlickTimer / FlickTime;
            shown.TailPitch += MathF.Sin(done * MathF.PI) * style.TailFlick;
        }
        return shown;
    }

    // Call once after drawing all birds: forgets birds that weren't drawn (out of view or gone).
    public void ForgetUnseen()
    {
        foreach (int id in anims.Keys.ToList())
        {
            if (!anims[id].Seen) anims.Remove(id);
            else anims[id].Seen = false;
        }
    }

    // ---------------- POSES ----------------

    // The motion a pose aims for, with no blending. time: seconds; phase: the bird's HopPhase.
    // Static so the Birb Designer can preview a pose without a real bird.
    public static BirbMotion PoseMotion(BirdPose pose, float time, float phase, MotionStyle style)
    {
        BirbMotion m = new BirbMotion();
        float peck = MathF.Abs(MathF.Sin(phase));    // 0 to 1 and back for each peck

        switch (pose)
        {
            case BirdPose.Idle:
                m.BodyLift = MathF.Sin(time * 2f) * 0.02f;
                m.HeadYaw = MathF.Sin(time * 0.6f) * 10f;
                m.TailPitch = MathF.Sin(time * 1.3f) * 3f;
                break;

            case BirdPose.Hop:
                // Matches BirdMover's bursts: an arc while in the air, then a rest on the ground.
                float progress = BirdMover.HopProgress(phase);
                float air = progress < BirdMover.HopAirShare ? MathF.Sin(progress / BirdMover.HopAirShare * MathF.PI) : 0f;
                m.Bounce = air * style.HopHeight;
                m.HeadForward = -air * style.HeadBob * 0.5f;    // head pulls back in the air
                m.BodyPitch = -air * 4f;
                m.LegTuck = air * 25f;
                m.TailPitch = -air * 8f;
                break;

            case BirdPose.Walk:
                // Each step is half a turn of the phase. The legs take turns swinging.
                m.LegSwing = MathF.Sin(phase) * style.StepSwing;
                m.HeadForward = HeadThrust(phase) * style.HeadBob;
                m.TailPitch = MathF.Sin(phase * 2f) * 3f;
                break;

            case BirdPose.Peck:
                m.BodyPitch = 25f;
                m.HeadPitch = 15f + peck * 40f;
                m.BeakDip = peck * 10f;
                m.TailPitch = 8f;
                break;

            case BirdPose.LookAround:
                m.HeadYaw = LookTurn(time, style.LookStep, seed: 0f) * style.LookAngle;
                m.HeadTilt = LookTurn(time, style.LookStep, seed: 50f) * style.LookAngle * 0.35f;
                m.HeadPitch = -5f;
                break;

            case BirdPose.Fly:
                m.WingSpread = style.WingSpread;
                m.WingFlap = MathF.Sin(time * style.FlapSpeed) * style.FlapAngle;
                m.LegTuck = 70f;
                m.BodyPitch = -8f;
                m.TailPitch = 5f;
                break;
        }
        return m;
    }

    // The pigeon head bob, -1 (back) to 1 (forward), once per step. The head snaps forward quickly,
    // then holds still in the air while the body walks under it, which looks like a slow slide back.
    private static float HeadThrust(float phase)
    {
        const float SnapShare = 0.25f;    // share of each step spent snapping forward
        float t = BirdMover.HopProgress(phase);
        if (t < SnapShare) return float.Lerp(-1f, 1f, Smooth(t / SnapShare));
        return float.Lerp(1f, -1f, (t - SnapShare) / (1f - SnapShare));
    }

    // Birds look around in quick snaps, not smooth sweeps: every lookStep seconds a new direction
    // from -1 to 1 is picked, and the head turns to it over the first 0.15 seconds.
    private static float LookTurn(float time, float lookStep, float seed)
    {
        float steps = time / lookStep;
        float n = MathF.Floor(steps);
        float secondsIn = (steps - n) * lookStep;
        float t = Smooth(MathF.Min(1f, secondsIn / 0.15f));
        return float.Lerp(Random01(n - 1f + seed), Random01(n + seed), t) * 2f - 1f;
    }

    // ---------------- PARTS ----------------

    // How one part is moved by the motion, turning around its own joint. Its parents are added by BirbBuilder.
    public static Matrix4x4 PartMotion(BirbPart part, BirbMotion m)
    {
        Matrix4x4 turn;
        Vector3 move = Vector3.Zero;

        switch (part.Role)
        {
            case PartRole.Body:
                turn = TurnX(m.BodyPitch);
                move = new Vector3(0f, m.BodyLift, 0f);
                break;
            case PartRole.Head:
                turn = Then(Then(TurnX(m.HeadPitch), TurnZ(m.HeadTilt)), TurnY(m.HeadYaw));
                move = new Vector3(0f, 0f, m.HeadForward);
                break;
            case PartRole.Beak:
                turn = TurnX(m.BeakDip);
                break;
            case PartRole.Wing:
                // swing out to the side, then flap up and down; the right wing does the mirror image
                turn = Then(TurnY(-m.WingSpread * part.Side), TurnZ(m.WingFlap * part.Side));
                break;
            case PartRole.Tail:
                turn = TurnX(m.TailPitch);
                break;
            case PartRole.Leg:
                turn = TurnX(m.LegTuck - m.LegSwing * part.Side);    // forward is a negative turn
                break;
            default:
                return Matrix4x4.Identity;
        }

        // To turn around the joint: move the joint to the origin, turn, then move it back (plus any extra move).
        Vector3 j = part.Offset;
        return Then(Then(Raymath.MatrixTranslate(-j.X, -j.Y, -j.Z), turn),
                    Raymath.MatrixTranslate(j.X + move.X, j.Y + move.Y, j.Z + move.Z));
    }

    private static Matrix4x4 TurnX(float degrees) => Raymath.MatrixRotateX(degrees * Raylib.DEG2RAD);
    private static Matrix4x4 TurnY(float degrees) => Raymath.MatrixRotateY(degrees * Raylib.DEG2RAD);
    private static Matrix4x4 TurnZ(float degrees) => Raymath.MatrixRotateZ(degrees * Raylib.DEG2RAD);
    private static Matrix4x4 Then(Matrix4x4 first, Matrix4x4 second) => Raymath.MatrixMultiply(first, second);

    // Eases 0..1 so motion starts and stops gently.
    private static float Smooth(float t) => t * t * (3f - 2f * t);

    // The same "random" 0..1 number every time for the same n, so a look direction holds still.
    private static float Random01(float n)
    {
        float x = MathF.Sin(n * 12.9898f) * 43758.5453f;
        return x - MathF.Floor(x);
    }
}
