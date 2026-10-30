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
    public float Squash;        // + stretches the body taller and thinner, - squashes it flatter and wider
    public float Fluff;         // 0 = sleek, about 0.12 = feathers puffed up
    public float WingStretch;   // degrees the left wing reaches out on its own (an idle stretch)
    public float EyeClose;      // 0 = open, 1 = shut (a blink)

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
            Squash = float.Lerp(a.Squash, b.Squash, t),
            Fluff = float.Lerp(a.Fluff, b.Fluff, t),
            WingStretch = float.Lerp(a.WingStretch, b.WingStretch, t),
            EyeClose = float.Lerp(a.EyeClose, b.EyeClose, t),
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

    // SQUASH AND STRETCH
    private const float HopStretch = 0.08f;       // taller at the top of each hop
    private const float HopSquash = 0.12f;        // flatter for a moment as each hop lands
    private const float LandSquash = 0.2f;        // flatter for a moment after a flight lands

    // BLINKS
    private const float BlinkTime = 0.15f;
    private const float BlinkGapMin = 2f, BlinkGapMax = 6f;

    // IDLE ACTS: now and then a standing bird fluffs up or stretches a wing
    private const float IdleGapMin = 8f, IdleGapMax = 20f;
    private const float FluffTime = 1.2f, FluffAmount = 0.12f;
    private const float StretchTime = 1.4f, StretchAngle = 60f;

    // ROOSTING (asleep for the night)
    private const float RoostFluff = 0.1f;        // how puffed up
    private const float RoostSquash = 0.06f;      // how much lower and rounder it sits

    // HUNCHED IN THE RAIN (birds that aren't Rain-proud; at full rain, drizzle does a share): puffed, low, head drawn in, tail down
    private const float RainFluff = 0.07f;
    private const float RainSquash = 0.05f;
    private const float RainHeadIn = 0.035f;      // birb units the head pulls back into the shoulders
    private const float RainHeadDown = 6f;        // degrees
    private const float RainTailDown = 6f;
    private const float HunchRate = 0.6f;         // how quickly a bird hunches or straightens up (per second)

    // What the animator remembers about one bird between frames.
    private class BirdAnim
    {
        public BirdPose Pose;
        public BirbMotion From;       // where the last blend started
        public BirbMotion Shown;      // what was drawn last frame
        public float Blend = 1f;      // 0 = just changed pose, 1 = fully in it
        public float Clock;           // this bird's own animation time
        public float FlickTimer;      // also times the squash after a flight lands
        public float BlinkTimer;      // > 0 while blinking
        public float NextBlink;       // seconds until the next blink
        public IdleAct Act;           // the idle act playing, if any
        public float ActTimer;        // > 0 while it plays
        public float NextAct;         // seconds of standing about until the next one
        public float Hunch;           // 0 standing tall, 1 fully hunched against the rain
        public bool Seen;
    }

    private enum IdleAct { None, Fluff, Stretch }

    private readonly Dictionary<int, BirdAnim> anims = new();

    public BirbMotion Animate(Bird bird, float dt)
    {
        if (!anims.TryGetValue(bird.EntityID, out BirdAnim? a))
        {
            // A random start time keeps birds from moving in step.
            a = new BirdAnim
            {
                Pose = bird.Pose,
                Clock = Rng.Range(0f, 10f),
                NextBlink = Rng.Range(0f, BlinkGapMax),
                NextAct = Rng.Range(IdleGapMin, IdleGapMax),
            };
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
            shown.Squash -= MathF.Sin(MathF.Min(1f, done * 2f) * MathF.PI) * LandSquash;    // squash in the first half
        }
        if (a.Pose != BirdPose.Roost) AddBlink(a, ref shown, dt);    // asleep: the eyes stay shut
        AddIdleAct(a, ref shown, dt);
        AddHunch(a, bird, ref shown, dt);
        return shown;
    }

    // In the rain a bird that minds it hunches up on the ground (not while flying, asleep or bathing), easing in and out.
    private static void AddHunch(BirdAnim a, Bird bird, ref BirbMotion m, float dt)
    {
        bool onGround = a.Pose is not (BirdPose.Fly or BirdPose.Roost or BirdPose.Bathe);
        float target = onGround && !bird.Nature.RainProud ? Weather.Instance.Rain : 0f;
        a.Hunch = a.Hunch < target ? MathF.Min(target, a.Hunch + HunchRate * dt) : MathF.Max(target, a.Hunch - HunchRate * dt);
        if (a.Hunch <= 0f) return;

        m.Fluff += RainFluff * a.Hunch;
        m.Squash -= RainSquash * a.Hunch;
        m.HeadForward -= RainHeadIn * a.Hunch;
        m.HeadPitch += RainHeadDown * a.Hunch;
        m.TailPitch -= RainTailDown * a.Hunch;
    }

    // Every few seconds the eyes shut for a moment, each bird on its own rhythm.
    private static void AddBlink(BirdAnim a, ref BirbMotion m, float dt)
    {
        if (a.BlinkTimer > 0f)
        {
            a.BlinkTimer -= dt;
            m.EyeClose = MathF.Sin(MathF.Max(0f, a.BlinkTimer) / BlinkTime * MathF.PI);
            return;
        }
        a.NextBlink -= dt;
        if (a.NextBlink > 0f) return;
        a.BlinkTimer = BlinkTime;
        a.NextBlink = Rng.Range(BlinkGapMin, BlinkGapMax);
    }

    // While standing about (idle or looking around), a bird now and then fluffs its feathers or stretches a wing.
    // Any other pose cuts the act short; the pose blend smooths that over.
    private static void AddIdleAct(BirdAnim a, ref BirbMotion m, float dt)
    {
        bool standing = a.Pose == BirdPose.Idle || a.Pose == BirdPose.LookAround;
        if (!standing)
        {
            a.Act = IdleAct.None;
            return;
        }

        if (a.Act == IdleAct.None)
        {
            a.NextAct -= dt;
            if (a.NextAct > 0f) return;
            a.Act = Rng.Chance(0.5f) ? IdleAct.Fluff : IdleAct.Stretch;
            a.ActTimer = a.Act == IdleAct.Fluff ? FluffTime : StretchTime;
            a.NextAct = Rng.Range(IdleGapMin, IdleGapMax);
        }

        a.ActTimer -= dt;
        if (a.ActTimer <= 0f)
        {
            a.Act = IdleAct.None;
            return;
        }

        if (a.Act == IdleAct.Fluff)
        {
            // Puff up, give a quick shiver at the fullest, settle back down
            float bump = MathF.Sin((1f - a.ActTimer / FluffTime) * MathF.PI);
            m.Fluff += bump * FluffAmount;
            m.BodyLift += MathF.Sin(a.ActTimer * 60f) * 0.01f * bump;
        }
        else
        {
            // Reach the left wing out, tail lifting and head turning to match, then fold it back
            float bump = MathF.Sin((1f - a.ActTimer / StretchTime) * MathF.PI);
            m.WingStretch += bump * StretchAngle;
            m.TailPitch += bump * 10f;
            m.HeadYaw += bump * 15f;
        }
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
                m.Squash = air * HopStretch - LandingBump(progress) * HopSquash;
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

            case BirdPose.Roost:
                // Puffed up and low, head drawn in and tipped down, eyes shut, breathing slowly
                m.Fluff = RoostFluff;
                m.Squash = -RoostSquash;
                m.HeadPitch = 18f;
                m.HeadForward = -0.04f;
                m.TailPitch = -4f;
                m.BodyLift = MathF.Sin(time * 1.2f) * 0.015f;
                m.EyeClose = 1f;
                break;

            case BirdPose.Sip:
                // One sip per turn of the phase: the head scoops down into the water (body tipping forward),
                // then comes up and tips back to swallow, the way birds drink
                float turn = phase / MathF.Tau - MathF.Floor(phase / MathF.Tau);
                float down = MathF.Sin(Math.Clamp(turn / 0.55f, 0f, 1f) * MathF.PI);
                float up = MathF.Sin(Math.Clamp((turn - 0.55f) / 0.45f, 0f, 1f) * MathF.PI);
                m.BodyPitch = 30f * down;
                m.HeadPitch = 50f * down - 30f * up;
                m.BeakDip = 8f * down;
                m.TailPitch = 8f * down - 3f * up;
                break;

            case BirdPose.Bathe:
                // Crouched low in the shallows, chest dipping, wings half open and fluttering, tail flicking
                m.Squash = -0.15f;
                m.Fluff = 0.06f;
                m.BodyPitch = 15f + MathF.Sin(phase) * 12f;
                m.HeadPitch = 10f + MathF.Sin(phase) * 15f;
                m.HeadTilt = MathF.Sin(phase * 0.5f) * 15f;
                m.WingSpread = style.WingSpread * 0.45f;
                m.WingFlap = MathF.Sin(phase * 1.7f) * style.FlapAngle * 0.4f;
                m.TailPitch = 12f + MathF.Sin(phase * 0.9f) * 10f;
                break;

            case BirdPose.ShakeOff:
                // Fluffed right up and shivering the water off: quick small shakes of the body, head and tail
                m.Fluff = 0.13f;
                m.BodyLift = MathF.Sin(time * 55f) * 0.012f;
                m.HeadYaw = MathF.Sin(time * 30f) * 12f;
                m.WingFlap = MathF.Sin(time * 40f) * 6f;
                m.TailPitch = MathF.Sin(time * 35f) * 8f;
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

    // 0 to 1 and back over the moment just after a hop touches down, for the landing squash.
    private static float LandingBump(float progress)
    {
        const float Share = 0.12f;    // share of each hop spent squashed
        float t = (progress - BirdMover.HopAirShare) / Share;
        return t > 0f && t < 1f ? MathF.Sin(t * MathF.PI) : 0f;
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
                // Squash, stretch and fluff scale the body (and everything hanging from it) around its bottom
                float tall = (1f + m.Squash) * (1f + m.Fluff);
                float wide = (1f - m.Squash * 0.5f) * (1f + m.Fluff);
                turn = Then(Raymath.MatrixScale(wide, tall, wide), TurnX(m.BodyPitch));
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
                // swing out to the side, then flap up and down; the right wing does the mirror image.
                // An idle stretch reaches only the left wing out.
                float spread = m.WingSpread + (part.Side > 0f ? m.WingStretch : 0f);
                turn = Then(TurnY(-spread * part.Side), TurnZ(m.WingFlap * part.Side));
                break;
            case PartRole.Eye:
                // A blink flattens the eye toward its middle (the eye's joint is at its bottom)
                float open = 1f - 0.9f * m.EyeClose;
                turn = Raymath.MatrixScale(1f, open, 1f);
                move = new Vector3(0f, part.Scale.Y * (1f - open), 0f);
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
