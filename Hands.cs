using System.Numerics;
using Raylib_cs;

public enum HandSide { Left, Right }

// The player's two hands. They stay tucked below the bottom of the screen until something asks for them:
//  - Press: a finger hovers in front of a button, and pushes in while the mouse is down (the logo, radio buttons)
//  - GripKnob: the fingers close around a knob and the wrist turns with it (the radio's knobs)
//  - HoldRing: a hand rests its fingertips on the bezel ring and slides along with it while you drag
//    (the right hand for a drag that starts to the right, the left for one to the left; see GameEngine.RingHand)
//  - on the Esc menu's Settings page the right hand is held up beside the menu, palm out, to show its color
//  - Spread: both hands rise in front of the view and part as the cube grows into the Hologram View,
//    and turn palms-in to press it back together as it shrinks
// GameEngine asks during Update, every frame a hand is needed. A hand fades in near what it reaches for,
// lingers a moment after the last ask, then fades away where it is. Just hands: each ends in a closed brass cuff.
// The fade is a dither (see 'dissolve' in room.fs), so the overlapping parts never show through each other.
//
// Built from simple shapes, lit by room.fs. Drawn last, over the hologram (see HologramRenderer).
// Hand-local space: the wrist is at the origin, +Z runs out along the fingers, +Y is the back of the hand
// (the palm faces -Y), and +X is the hand's left side when palm-down, which is the right hand's thumb side.
// Sizes are in hand units; HandScale turns them into room units (1 room unit is about 7.5 cm).
public class Hands
{
    // SIZE AND FEEL
    private const float HandScale = 1.3f;        // a little bigger than life, so they read at this camera distance
    private const float HoverGap = 0.35f;        // how far in front of a button the fingertip waits
    private const float ReachSpeed = 14f;        // how quickly a hand moves to what it's reaching for
    private const float FadeInSpeed = 7f;        // share of full visibility gained per second
    private const float FadeOutSpeed = 4f;       // and lost per second once it's done
    private const float ArriveFrom = 0.6f;       // how far back toward the player a hand appears before easing in
    private const float Linger = 0.5f;           // seconds a hand stays after it's no longer needed
    private const float TapTime = 0.18f;         // how long a quick click keeps the finger pushed in
    private const float MaxKnobTwist = 1.3f;     // the wrist turns this far with a knob, then the fingers slip
    private const float RingGripAngle = 0.3f;    // how far left of the front the left hand holds the ring
    private const float RingSlideMax = 0.5f;     // how far the hand slides along with the ring before it slips
    private const float SpreadDistance = 8f;     // Spread: how far in front of the camera the hands are held
    private const float SpreadDrop = 3.0f;       // how far below the middle of the view the wrists sit
    private const float SpreadNear = 1.5f;       // each wrist's distance from the middle with the cube on the pedestal
    private const float SpreadFar = 4.2f;        // and with the cube fully grown

    // COLOR (Esc menu > Settings; saved in the settings file)
    public float Tone = 0.5f;                    // along the alien shades, 0 deep blue to 1 moss green
    public bool Yellow = false;                  // Groening yellow instead of the slider's shade
    public static readonly Color GroeningYellow = new Color(255, 217, 15, 255);
    private static readonly Color[] ToneStops =
    {
        new Color(64, 110, 196, 255),    // deep blue
        new Color(70, 158, 192, 255),    // sky teal
        new Color(62, 168, 128, 255),    // sea green
        new Color(122, 176, 84, 255),    // moss green
    };
    public Color SkinColor => Yellow ? GroeningYellow : ToneColor(Tone);

    private static readonly Color CuffBrass = new Color(224, 180, 108, 255);
    private const float SkinShine = 0.12f;
    private const float CuffShine = 0.6f;

    // FINGERS: index, middle, ring, pinky. Knuckle X is for the right hand; the left hand mirrors it.
    private const float KnuckleZ = 1.0f;
    private static readonly float[] KnuckleX = { 0.33f, 0.11f, -0.11f, -0.33f };
    private static readonly float[] Proximal = { 0.50f, 0.55f, 0.50f, 0.38f };
    private static readonly float[] Distal = { 0.42f, 0.45f, 0.42f, 0.33f };
    private static readonly float[] FingerRadius = { 0.105f, 0.11f, 0.10f, 0.09f };
    private const float ThumbRadius = 0.12f;

    // FINGER CURLS for each kind of reach: index, middle, ring, pinky, thumb. 0 straight, 1 a fist.
    private static readonly float[] RestCurls = { 0.35f, 0.4f, 0.45f, 0.5f, 0.3f };
    private static readonly float[] PressCurls = { 0.08f, 0.85f, 0.9f, 0.9f, 0.65f };
    private static readonly float[] KnobCurls = { 0.5f, 0.9f, 1.0f, 1.0f, 0.55f };
    private static readonly float[] RingCurls = { 0.4f, 0.42f, 0.45f, 0.5f, 0.3f };
    private static readonly float[] ShowCurls = { 0.05f, 0.05f, 0.08f, 0.1f, 0.15f };
    private static readonly float[] SpreadCurls = { 0.1f, 0.08f, 0.1f, 0.14f, 0.2f };

    private enum Reach { None, Press, Knob, Ring, Show, Spread }

    private class Hand
    {
        public readonly float Side;              // +1 right hand, -1 left hand
        public Hand(float side) { Side = side; }

        // What it was asked to do this frame (cleared after each Update)
        public Reach Asked;
        public Vector3 AskedPoint, AskedFacing;
        public float AskedAmount;                // Press: 1 pushed in. Knob: 1 gripping, plus the twist.
        public float AskedTwist;

        // What it's doing, kept for a moment after the last ask
        public Reach Doing;
        public Vector3 Point, Facing;
        public float Amount, Twist;
        public float LingerTimer, TapTimer;
        public float RingSlide;                  // how far it has slid along with the ring this grip

        // The pose it's easing toward, and where it is now
        public Vector3 Wrist;
        public Vector3 From;                     // below the screen on its own side: the poses aim the hand as if it came from here
        public Quaternion Turn = Quaternion.Identity;
        public readonly float[] Curls = (float[])RestCurls.Clone();
        public float Fade;                       // 0 gone, 1 fully there
        public bool Visible => Fade > 0.01f;
    }

    private readonly Hand left = new Hand(-1f);
    private readonly Hand right = new Hand(1f);
    private bool spreadClosing;     // Spread: the cube is shrinking, so the palms face in instead of out

    private Hand Get(HandSide side) => side == HandSide.Right ? right : left;

    // Blends along the alien shades: 0 deep blue, 1 moss green.
    public static Color ToneColor(float tone)
    {
        float t = Math.Clamp(tone, 0f, 1f) * (ToneStops.Length - 1);
        int i = Math.Min((int)t, ToneStops.Length - 2);
        return Palette.Mix(ToneStops[i], ToneStops[i + 1], t - i);
    }

    // ---------------- ASKING (called during Update) ----------------

    // A fingertip at a button's face. facing: the way the face points, out toward the player.
    public void Press(HandSide side, Vector3 face, Vector3 facing, bool pushed)
    {
        Ask(Get(side), Reach.Press, face, facing, pushed ? 1f : 0f, 0f);
    }

    // The finger stays pushed in for a moment, so even a quick click shows.
    public void Tap(HandSide side) => Get(side).TapTimer = TapTime;

    // Fingers around a knob. center: the middle of the knob; twist: how far it has turned since it was grabbed.
    public void GripKnob(HandSide side, Vector3 center, Vector3 facing, bool gripping, float twist)
    {
        Ask(Get(side), Reach.Knob, center, facing, gripping ? 1f : 0f, twist);
    }

    // Fingertips on the bezel ring. turned: how far the ring turned this frame, so the hand slides with it.
    public void HoldRing(HandSide side, ViewWindow view, float turned)
    {
        Hand hand = Get(side);
        if (hand.Doing != Reach.Ring) hand.RingSlide = 0f;    // a new grip starts where the hand lands

        // A spot on the ring moves backward in angle as the ring turns (see Pedestal.DrawBezelMarkings)
        hand.RingSlide = Math.Clamp(hand.RingSlide - turned, -RingSlideMax, RingSlideMax);
        float angle = MathF.PI / 2f - hand.Side * RingGripAngle + hand.RingSlide;
        Vector3 spot = Pedestal.RingTop(view, angle, out Vector3 outward);
        Ask(hand, Reach.Ring, spot, outward, 0f, 0f);
    }

    // Both hands held up in front of the view. Growing: palms out, parting. Shrinking (closing): palms in, closing.
    // amount: 0 close together (cube on the pedestal), 1 wide apart (fully grown).
    // A hand already asked for something else this frame keeps that job.
    public void Spread(float amount, bool closing)
    {
        spreadClosing = closing;
        if (left.Asked == Reach.None) Ask(left, Reach.Spread, Vector3.Zero, Vector3.UnitZ, amount, 0f);
        if (right.Asked == Reach.None) Ask(right, Reach.Spread, Vector3.Zero, Vector3.UnitZ, amount, 0f);
    }

    private static void Ask(Hand hand, Reach reach, Vector3 point, Vector3 facing, float amount, float twist)
    {
        hand.Asked = reach;
        hand.AskedPoint = point;
        hand.AskedFacing = Vector3.Normalize(facing);
        hand.AskedAmount = amount;
        hand.AskedTwist = twist;
    }

    // ---------------- UPDATE (no drawing) ----------------

    // Moves each hand toward what it was asked for this frame. showColor: the Settings page is open,
    // so the right hand is held up beside the menu.
    public void Update(float dt, Camera3D camera, bool showColor)
    {
        if (showColor) Ask(right, Reach.Show, Vector3.Zero, Vector3.UnitZ, 0f, 0f);
        UpdateHand(left, dt, camera);
        UpdateHand(right, dt, camera);
    }

    private void UpdateHand(Hand hand, float dt, Camera3D camera)
    {
        // Take this frame's ask, or keep doing the last one for a moment
        if (hand.Asked != Reach.None)
        {
            hand.Doing = hand.Asked;
            hand.Point = hand.AskedPoint;
            hand.Facing = hand.AskedFacing;
            hand.Amount = hand.AskedAmount;
            hand.Twist = hand.AskedTwist;
            hand.LingerTimer = Linger;
        }
        else
        {
            hand.LingerTimer -= dt;
            if (hand.LingerTimer <= 0f) hand.Doing = Reach.None;
        }
        hand.Asked = Reach.None;
        hand.TapTimer = MathF.Max(0f, hand.TapTimer - dt);

        // Work out the pose it should be in
        CameraFrame(camera, out Vector3 forward, out Vector3 across, out Vector3 up, out _, out _);
        Vector3 goalWrist;
        Quaternion goalTurn;
        float[] goalCurls;
        switch (hand.Doing)
        {
            case Reach.Press:
            {
                hand.From = ApproachFrom(camera, hand.Point, hand.Side);
                bool pushed = hand.Amount > 0f || hand.TapTimer > 0f;
                Vector3 touch = hand.Point + hand.Facing * (pushed ? -0.02f : HoverGap);
                PosePress(hand, touch, out goalWrist, out goalTurn);
                goalCurls = PressCurls;
                break;
            }
            case Reach.Knob:
            {
                hand.From = ApproachFrom(camera, hand.Point, hand.Side);
                bool gripping = hand.Amount > 0f;
                Vector3 grip = hand.Point + hand.Facing * (gripping ? 0f : HoverGap);
                float twist = gripping ? Math.Clamp(hand.Twist, -MaxKnobTwist, MaxKnobTwist) : 0f;
                PoseKnob(hand, grip, twist, out goalWrist, out goalTurn);
                goalCurls = KnobCurls;
                break;
            }
            case Reach.Ring:
                hand.From = ApproachFrom(camera, hand.Point, hand.Side);
                PoseRing(hand, out goalWrist, out goalTurn);
                goalCurls = RingCurls;
                break;
            case Reach.Show:
                // Down at the lower right of the screen, beside the menu panel, palm toward the camera
                goalWrist = camera.Position + forward * 10f + across * 3.3f - up * 2.6f;
                goalTurn = FacingTurn(up - across * 0.15f, forward);
                hand.From = ApproachFrom(camera, goalWrist, hand.Side);
                goalCurls = ShowCurls;
                break;
            case Reach.Spread:
            {
                // Rising from the bottom of the view on its own side, fingers up, palm turned toward the cube
                // and outward while it grows, or inward (wrists turned the other way) while it shrinks
                float apart = SpreadNear + (SpreadFar - SpreadNear) * hand.Amount;
                goalWrist = camera.Position + forward * SpreadDistance + across * (hand.Side * apart) - up * SpreadDrop;
                Vector3 fingers = up + forward * 0.35f + across * (hand.Side * 0.2f);
                float palmSide = spreadClosing ? -1f : 1f;
                goalTurn = FacingTurn(fingers, -(forward + across * (hand.Side * palmSide * 0.8f)));
                hand.From = ApproachFrom(camera, goalWrist, hand.Side);
                goalCurls = SpreadCurls;
                break;
            }
            default:
                // Done: it stays where it is, relaxes its fingers and fades away
                goalWrist = hand.Wrist;
                goalTurn = hand.Turn;
                goalCurls = RestCurls;
                break;
        }

        // A hand that isn't showing yet appears just short of its pose, so it eases the last bit in as it fades up
        if (hand.Doing != Reach.None && !hand.Visible)
        {
            hand.Wrist = goalWrist + Vector3.Normalize(hand.From - goalWrist) * ArriveFrom;
            hand.Turn = goalTurn;
            Array.Copy(goalCurls, hand.Curls, hand.Curls.Length);
        }

        float k = 1f - MathF.Exp(-ReachSpeed * dt);
        hand.Wrist = Vector3.Lerp(hand.Wrist, goalWrist, k);
        hand.Turn = Quaternion.Normalize(Quaternion.Slerp(hand.Turn, goalTurn, k));
        for (int i = 0; i < hand.Curls.Length; i++) hand.Curls[i] += (goalCurls[i] - hand.Curls[i]) * k;

        float fadeGoal = hand.Doing != Reach.None ? 1f : 0f;
        float fadeStep = (fadeGoal > hand.Fade ? FadeInSpeed : FadeOutSpeed) * dt;
        hand.Fade = Math.Clamp(hand.Fade + Math.Clamp(fadeGoal - hand.Fade, -fadeStep, fadeStep), 0f, 1f);
    }

    // ---------------- POSES ----------------

    // Index finger pointing in and a little down at the button, coming from the hand's own side, thumb tilted up a little.
    // Coming from above keeps the curled fingers out of the table when the button sits low (the radio's).
    // The wrist is placed so the index fingertip lands on 'touch'.
    private static void PosePress(Hand hand, Vector3 touch, out Vector3 wrist, out Quaternion turn)
    {
        Vector3 arm = Vector3.Normalize(touch - hand.From);
        Vector3 forward = Vector3.Normalize(-hand.Facing + arm * 0.6f - Vector3.UnitY * 0.5f);
        turn = FacingTurn(forward, Roll(Vector3.UnitY, forward, hand.Side * 0.35f));

        FingerPoints(hand.Side, 0, PressCurls[0], out _, out _, out Vector3 tip, out Vector3 tipDir);
        Vector3 pad = tip + tipDir * FingerRadius[0];
        wrist = touch - ToRoom(pad, turn);
    }

    // Coming from above, thumb and index pinching the knob from either side, the other fingers tucked in
    // (the knobs sit low, so a hand on its side would reach into the table). The whole hand turns with the knob.
    // The wrist is placed so the point between the index tip and thumb tip sits on the knob.
    private static void PoseKnob(Hand hand, Vector3 grip, float twist, out Vector3 wrist, out Quaternion turn)
    {
        Vector3 arm = Vector3.Normalize(grip - hand.From);
        Vector3 forward = Vector3.Normalize(-hand.Facing + arm * 0.5f + Vector3.UnitY * 0.3f);
        Quaternion upright = FacingTurn(forward, Roll(Vector3.UnitY, forward, -hand.Side * 0.6f));
        // A knob's angle grows clockwise as seen from the front, which is backward around the facing direction
        turn = Quaternion.Concatenate(upright, Quaternion.CreateFromAxisAngle(hand.Facing, -twist));

        FingerPoints(hand.Side, 0, KnobCurls[0], out _, out _, out Vector3 indexTip, out _);
        ThumbPoints(hand.Side, KnobCurls[4], out _, out _, out Vector3 thumbTip);
        Vector3 between = (indexTip + thumbTip) / 2f;
        wrist = grip - ToRoom(between, turn);
    }

    // Palm-down over the ring's front, fingertips resting on its top, pointing in toward the middle.
    // The wrist is placed so the middle fingertip lands on the spot.
    private static void PoseRing(Hand hand, out Vector3 wrist, out Quaternion turn)
    {
        Vector3 touch = hand.Point + Vector3.UnitY * 0.06f;
        Vector3 arm = Vector3.Normalize(touch - hand.From);
        Vector3 forward = Vector3.Normalize(-hand.Facing * 0.7f + arm * 0.5f - Vector3.UnitY * 0.25f);
        turn = FacingTurn(forward, Roll(Vector3.UnitY, forward, hand.Side * 0.15f));

        FingerPoints(hand.Side, 1, RingCurls[1], out _, out _, out Vector3 tip, out Vector3 tipDir);
        Vector3 pad = tip + tipDir * FingerRadius[1];
        wrist = touch - ToRoom(pad, turn);
    }

    // ---------------- HAND SHAPE (hand-local, before HandScale) ----------------

    // One finger's knuckle, middle joint and tip for a curl from 0 (straight) to 1 (a fist).
    // tipDir is the way the last segment points. Straight fingers spread a little; curled ones close up.
    private static void FingerPoints(float side, int finger, float curl,
                                     out Vector3 knuckle, out Vector3 joint, out Vector3 tip, out Vector3 tipDir)
    {
        knuckle = new Vector3(KnuckleX[finger] * side, 0f, KnuckleZ);
        float spread = KnuckleX[finger] * side * 0.3f * (1f - curl);
        float first = curl * 1.5f;
        float second = first + curl * 1.7f;
        joint = knuckle + Bend(first, spread) * Proximal[finger];
        tipDir = Bend(second, spread);
        tip = joint + tipDir * Distal[finger];
    }

    // Forward along the fingers, bent down toward the palm by 'angle'.
    private static Vector3 Bend(float angle, float spread)
    {
        return Vector3.Normalize(new Vector3(spread, -MathF.Sin(angle), MathF.Cos(angle)));
    }

    // The thumb comes off the side of the palm near the wrist; curling swings it in under the palm.
    private static void ThumbPoints(float side, float curl, out Vector3 thumbBase, out Vector3 joint, out Vector3 tip)
    {
        thumbBase = new Vector3(0.45f * side, -0.08f, 0.3f);
        Vector3 open = Vector3.Normalize(new Vector3(0.75f * side, -0.15f, 0.65f));
        Vector3 closed = Vector3.Normalize(new Vector3(-0.3f * side, -0.6f, 0.75f));
        joint = thumbBase + Vector3.Normalize(Vector3.Lerp(open, closed, curl * 0.6f)) * 0.42f;
        tip = joint + Vector3.Normalize(Vector3.Lerp(open, closed, curl)) * 0.36f;
    }

    // ---------------- MATH HELPERS ----------------

    // A hand-local point (in hand units) moved into the room, relative to the wrist.
    private static Vector3 ToRoom(Vector3 local, Quaternion turn) => Vector3.Transform(local * HandScale, turn);

    // The turn that points the fingers along 'forward' with the back of the hand toward 'upHint'.
    private static Quaternion FacingTurn(Vector3 forward, Vector3 upHint)
    {
        Vector3 f = Vector3.Normalize(forward);
        Vector3 x = Vector3.Cross(upHint, f);
        if (x.LengthSquared() < 1e-6f) x = Vector3.Cross(Vector3.UnitZ, f);
        x = Vector3.Normalize(x);
        Vector3 y = Vector3.Cross(f, x);

        // Each row is where one of the hand's own axes ends up in the room
        Matrix4x4 axes = new Matrix4x4(
            x.X, x.Y, x.Z, 0f,
            y.X, y.Y, y.Z, 0f,
            f.X, f.Y, f.Z, 0f,
            0f, 0f, 0f, 1f);
        return Quaternion.CreateFromRotationMatrix(axes);
    }

    // 'v' turned around 'axis' by 'angle' radians.
    private static Vector3 Roll(Vector3 v, Vector3 axis, float angle)
    {
        return Vector3.Transform(v, Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), angle));
    }

    // The camera's directions. below: along the bottom edge of the view. inward: across that edge, into the view.
    private static void CameraFrame(Camera3D camera, out Vector3 forward, out Vector3 across, out Vector3 up,
                                    out Vector3 below, out Vector3 inward)
    {
        forward = Vector3.Normalize(camera.Target - camera.Position);
        across = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        up = Vector3.Cross(across, forward);
        float half = camera.FovY * 0.5f * Raylib.DEG2RAD;
        below = Vector3.Normalize(forward * MathF.Cos(half) - up * MathF.Sin(half));
        inward = Vector3.Normalize(forward * MathF.Sin(half) + up * MathF.Cos(half));
    }

    // Where the hand is imagined to come from: just below the bottom of the screen, partway out toward what it
    // reaches for, off to its own side. The poses aim the hand along this, so it looks like it reached in naturally.
    private static Vector3 ApproachFrom(Camera3D camera, Vector3 target, float side)
    {
        CameraFrame(camera, out _, out Vector3 across, out _, out Vector3 below, out Vector3 inward);
        float distance = Vector3.Distance(camera.Position, target);
        return camera.Position + below * (distance * 0.45f) - inward * 1.5f + across * side * (2.5f + 0.06f * distance);
    }

    // ---------------- DRAW (no changes) ----------------

    // Called in the room's 3D pass, after the hologram has been laid over the room.
    public void Draw(Backdrop backdrop)
    {
        Color skin = SkinColor;
        if (left.Visible) DrawHand(backdrop, left, skin);
        if (right.Visible) DrawHand(backdrop, right, skin);
        backdrop.SetShine(0f);
        backdrop.SetDissolve(0f);
    }

    // Brass cuff (closed at the back), wrist, palm, four fingers and a thumb, each finger a pair of rounded segments.
    private static void DrawHand(Backdrop backdrop, Hand hand, Color skin)
    {
        UnitShapes shapes = backdrop.Shapes;
        Vector3 arm = Vector3.Transform(Vector3.UnitZ, hand.Turn);    // along the hand, from the wrist toward the fingers
        float s = HandScale;

        backdrop.SetDissolve(1f - hand.Fade);
        backdrop.SetShine(CuffShine);
            Vector3 cuffBack = hand.Wrist - arm * 1.25f * s;
            shapes.CylinderAlong(cuffBack, arm, 0.4f * s, 0.85f * s, CuffBrass);
            shapes.Ball(cuffBack, new Vector3(0.4f * s), CuffBrass);
        backdrop.SetShine(SkinShine);
            shapes.CylinderAlong(hand.Wrist - arm * 0.5f * s, arm, 0.27f * s, 0.5f * s, skin);
            shapes.Ball(hand.Wrist, new Vector3(0.29f * s), skin);
            shapes.BallTurned(hand.Wrist + ToRoom(new Vector3(0f, 0f, 0.55f), hand.Turn), new Vector3(0.5f, 0.19f, 0.6f) * s, skin, hand.Turn);

            for (int finger = 0; finger < 4; finger++)
            {
                FingerPoints(hand.Side, finger, hand.Curls[finger], out Vector3 knuckle, out Vector3 joint, out Vector3 tip, out _);
                DrawSegments(shapes, hand, knuckle, joint, tip, FingerRadius[finger], skin);
            }
            ThumbPoints(hand.Side, hand.Curls[4], out Vector3 thumbBase, out Vector3 thumbJoint, out Vector3 thumbTip);
            DrawSegments(shapes, hand, thumbBase, thumbJoint, thumbTip, ThumbRadius, skin);
    }

    // Two cylinders joined by balls, so the finger looks rounded at every joint.
    private static void DrawSegments(UnitShapes shapes, Hand hand, Vector3 a, Vector3 b, Vector3 c, float radius, Color skin)
    {
        Vector3 pa = hand.Wrist + ToRoom(a, hand.Turn);
        Vector3 pb = hand.Wrist + ToRoom(b, hand.Turn);
        Vector3 pc = hand.Wrist + ToRoom(c, hand.Turn);
        float r = radius * HandScale;

        shapes.CylinderAlong(pa, pb - pa, r, Vector3.Distance(pa, pb), skin);
        shapes.CylinderAlong(pb, pc - pb, r * 0.95f, Vector3.Distance(pb, pc), skin);
        shapes.Ball(pa, new Vector3(r), skin);
        shapes.Ball(pb, new Vector3(r), skin);
        shapes.Ball(pc, new Vector3(r * 0.95f), skin);
    }
}
