using System.Numerics;
using Raylib_cs;

// The birb minding the Gilded Granary's counter, standing on a stool so it can just see over.
// A random common birb (or a Frostcrest, once one has been spotted) takes the counter, and shifts change
// every few minutes. It idles and looks around, watches the item under your mouse, and hops when you buy.
//
// This is the keeper's state and motion; StoreRenderer draws it with BirbBuilder.
public class Shopkeeper
{
    public BirdType Type { get; private set; }
    public BirbShape Shape { get; private set; } = null!;
    public string Name { get; private set; } = "";
    public float Size => Birds.Get(Type).Size;         // scene units per birb unit, the same as in the bird world

    // The box around the birb's shape, in birb units. Measured the first time it's asked for, not when the shift
    // starts: the first shift starts before the game window opens, and the part meshes it needs only load after that.
    public BoundingBox Bounds => bounds ??= Shape.GetBounds();
    private BoundingBox? bounds;

    // Store units, tall enough that a bit over half the birb shows above the counter
    public float StoolHeight
    {
        get
        {
            float height = (Bounds.Max.Y - Bounds.Min.Y) * Size / SeedStore.Scale;
            return MathF.Max(0.15f, SeedStore.CounterTop - height * 0.55f);
        }
    }

    // MOTION: what BirbBuilder draws this frame
    public BirbMotion Motion { get; private set; }
    public float Turn { get; private set; }            // radians the body is turned away from facing the customer (+Z)

    private const float ShiftLength = 180f;    // seconds
    private float shiftTimer;

    // Idle life: the keeper moves between standing, looking around and pecking at the ledger
    private BirdPose pose = BirdPose.Idle;
    private float poseTimer;
    private float clock;

    private float happyTimer;                  // counts down after a sale
    private const float HappyTime = 0.9f;

    private static readonly string[] Titles = { "Sir", "Dame", "Master", "Goodwife", "Brother", "Lady" };
    private static readonly string[] GivenNames = { "Aldric", "Maud", "Osric", "Elspeth", "Godwin", "Hild", "Wystan", "Bertilde" };

    public Shopkeeper()
    {
        StartShift();
    }

    // Returns true if a new keeper took over this frame.
    // lookAt: the hovered item, relative to where the keeper stands (store space), or null.
    public bool Update(float dt, Vector3? lookAt)
    {
        clock += dt;
        bool newShift = false;
        shiftTimer -= dt;
        if (shiftTimer <= 0f)
        {
            StartShift();
            newShift = true;
        }

        UpdateIdlePose(dt);
        BirbMotion target = BirbAnimator.PoseMotion(pose, clock, clock * 5f, Shape.Style);

        // Watching: the body turns part of the way toward the item, and the head does the rest
        float bodyTarget = 0f;
        if (lookAt != null)
        {
            float toItem = MathF.Atan2(lookAt.Value.X, lookAt.Value.Z);
            bodyTarget = Math.Clamp(toItem, -2.2f, 2.2f);
            target.HeadYaw = Math.Clamp((toItem - Turn) * Raylib.RAD2DEG, -60f, 60f);
            target.HeadPitch = -5f;
        }
        Turn += (bodyTarget - Turn) * MathF.Min(1f, dt * 4f);

        // A sale: a little hop with the wings fluttering
        if (happyTimer > 0f)
        {
            happyTimer -= dt;
            float done = 1f - happyTimer / HappyTime;
            target.Bounce = MathF.Sin(done * MathF.PI) * 0.25f;
            target.WingSpread = 45f;
            target.WingFlap = MathF.Sin(clock * 30f) * 35f;
            target.TailPitch = 15f;
        }

        Motion = BirbMotion.Lerp(Motion, target, MathF.Min(1f, dt * 10f));
        return newShift;
    }

    public void Celebrate()
    {
        happyTimer = HappyTime;
    }

    // Every few seconds, a new idle pose: mostly standing or looking around, sometimes a quick peck.
    private void UpdateIdlePose(float dt)
    {
        poseTimer -= dt;
        if (poseTimer > 0f) return;

        float roll = Rng.Range(0f, 1f);
        if (roll < 0.45f) { pose = BirdPose.Idle; poseTimer = Rng.Range(2f, 4f); }
        else if (roll < 0.85f) { pose = BirdPose.LookAround; poseTimer = Rng.Range(2f, 3f); }
        else { pose = BirdPose.Peck; poseTimer = Rng.Range(0.6f, 1.2f); }
    }

    // Picks a different birb than last time if there's a choice, and names it.
    private void StartShift()
    {
        List<BirdType> choices = Birds.Common.ToList();
        if (Seeds.FrostcrestSeen) choices.Add(BirdType.Frostcrest);
        if (choices.Count > 1 && Shape != null) choices.Remove(Type);

        Type = choices[Rng.Next(choices.Count)];
        BirbShape[] shapes = Birds.Get(Type).Shapes;
        Shape = shapes[Rng.Next(shapes.Length)];
        Name = $"{Titles[Rng.Next(Titles.Length)]} {GivenNames[Rng.Next(GivenNames.Length)]} the {Birds.Get(Type).Name}";
        bounds = null;    // a new shape, so it gets measured again

        shiftTimer = ShiftLength;
        Turn = 0f;
        Motion = new BirbMotion();
    }
}
