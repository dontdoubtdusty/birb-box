using System.Numerics;

// The garden's pool: a small pond, one per world, placed when a new world is made and saved with it.
// Its shape is a few overlapping circles (Lobes) blended smoothly into one rounded, lobed outline.
// DistanceAt gives how far a spot is from the shore (below 0 is in the water), and the floor shader
// (hologram.fs, waterDistance) runs the very same math, so what you see and where birds wade always match.
// Birds walk around it, sip at the shore and bathe in the shallows (BirdMover). Seed that lands in it sinks (Food).
// It also keeps the ripples and splash droplets, which only the drawing reads.
public class Pool : Entity
{
    // TUNING
    public const int MaxLobes = 3;                 // the shader has room for this many circles
    private const float Blend = 0.9f;              // how softly the lobes melt into each other (world units)
    private const float EdgeMargin = 7f;           // the pool stays this far in from the world edge
    private const float FeederGap = 1f;            // space kept between the water and a feeder
    public const float RockGap = 0.8f;             // space kept between the water and a rock
    public const float Shallows = 0.45f;           // how far in from the shore the water is shallow enough to wade
    public const int MaxRipples = 8;               // the shader has room for this many
    public const float RippleLife = 2.2f;          // seconds a ripple ring spreads before it's gone
    private const int MaxDroplets = 80;

    public readonly struct Lobe
    {
        public Vector2 Center { get; init; }
        public float Radius { get; init; }
    }

    // One ripple ring spreading from where something touched the water. Strength 0 to 1.
    public struct Ripple
    {
        public Vector2 Spot;
        public float Age;
        public float Strength;
    }

    // One splash droplet flying up out of the water and falling back.
    public struct Droplet
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Size;
    }

    public IReadOnlyList<Lobe> Lobes => lobes;
    private readonly List<Lobe> lobes;
    public IReadOnlyList<Ripple> Ripples => ripples;
    private readonly List<Ripple> ripples = new();
    public IReadOnlyList<Droplet> Droplets => droplets;
    private readonly List<Droplet> droplets = new();

    public Pool(IEnumerable<Lobe> shape)
    {
        lobes = shape.Take(MaxLobes).ToList();
        Vector2 middle = Vector2.Zero;
        foreach (Lobe lobe in lobes) middle += lobe.Center;
        middle /= Math.Max(1, lobes.Count);
        Position = new Vector3(middle.X, 0f, middle.Y);
    }

    // The pool in this world, or null (a world always has one, but a tools panel or test world may not).
    public static Pool? Current
    {
        get
        {
            IReadOnlyList<Pool> pools = World.Instance.GetAllEntities<Pool>();
            return pools.Count > 0 ? pools[0] : null;
        }
    }

    // Ages the ripples and moves the droplets along. Nothing here changes what the birds do.
    public override void Update(float dt)
    {
        for (int i = ripples.Count - 1; i >= 0; i--)
        {
            Ripple r = ripples[i];
            r.Age += dt;
            if (r.Age >= RippleLife) ripples.RemoveAt(i);
            else ripples[i] = r;
        }

        for (int i = droplets.Count - 1; i >= 0; i--)
        {
            Droplet d = droplets[i];
            d.Velocity.Y -= World.Gravity * dt;
            d.Position += d.Velocity * dt;
            if (d.Position.Y <= 0f) droplets.RemoveAt(i);
            else droplets[i] = d;
        }
    }

    // ---------------- THE SHAPE ----------------

    // How far a spot is from the shore, in world units: below 0 is in the water (how deep in), above 0 on land.
    // A smooth union of the lobes' circles; hologram.fs's waterDistance is the same math.
    public float DistanceAt(Vector2 spot)
    {
        float d = float.MaxValue;
        foreach (Lobe lobe in lobes)
        {
            float here = Vector2.Distance(spot, lobe.Center) - lobe.Radius;
            d = d == float.MaxValue ? here : SmoothMin(d, here, Blend);
        }
        return d;
    }

    public float DistanceAt(Vector3 pos) => DistanceAt(new Vector2(pos.X, pos.Z));

    // The way straight out of the water at this spot (toward land), one unit long.
    public Vector2 OutwardAt(Vector2 spot)
    {
        Vector2 slope = SlopeAt(spot);
        return slope.LengthSquared() > 1e-8f ? Vector2.Normalize(slope) : new Vector2(1f, 0f);
    }

    // How fast DistanceAt grows along X and Z here (about one unit per unit, a little less where the lobes blend).
    private Vector2 SlopeAt(Vector2 spot)
    {
        const float Step = 0.02f;
        return new Vector2(DistanceAt(spot + new Vector2(Step, 0f)) - DistanceAt(spot - new Vector2(Step, 0f)),
                           DistanceAt(spot + new Vector2(0f, Step)) - DistanceAt(spot - new Vector2(0f, Step))) / (2f * Step);
    }

    // The spot moved along the slope until it's exactly `distance` from the shore (negative: that far into the water).
    // A few small corrections (Newton's method), since the blended shore isn't a perfect circle.
    public Vector2 MoveTo(Vector2 spot, float distance)
    {
        for (int i = 0; i < 8; i++)
        {
            float off = DistanceAt(spot) - distance;
            if (MathF.Abs(off) < 0.003f) break;
            Vector2 slope = SlopeAt(spot);
            float steep = slope.LengthSquared();
            if (steep < 1e-6f) slope = new Vector2(1f, 0f);    // dead center of the pool: any way out will do
            spot -= slope * off / MathF.Max(steep, 0.25f);
        }
        return spot;
    }

    // Polynomial smooth minimum: like min(a, b), but rounding off the corner where the two meet.
    private static float SmoothMin(float a, float b, float k)
    {
        float h = MathF.Max(k - MathF.Abs(a - b), 0f) / k;
        return MathF.Min(a, b) - h * h * k * 0.25f;
    }

    // ---------------- LOOKING UP THE WATER (safe with no pool) ----------------

    // How far from the shore (below 0 in the water), or a big number when there's no pool.
    public static float Distance(Vector3 pos) => Current?.DistanceAt(pos) ?? float.MaxValue;

    // True if this spot is in the water, or within margin of it.
    public static bool IsWater(Vector3 pos, float margin = 0f) => Distance(pos) < margin;

    // The same spot moved out onto the shore if it's in the water (at least margin from the waterline;
    // a negative margin allows that far into the shallows). Height is kept.
    public static Vector3 PushOut(Vector3 pos, float margin = 0.15f)
    {
        Pool? pool = Current;
        if (pool == null) return pos;

        Vector2 flat = new Vector2(pos.X, pos.Z);
        if (pool.DistanceAt(flat) >= margin) return pos;
        flat = pool.MoveTo(flat, margin);
        return new Vector3(flat.X, pos.Y, flat.Y);
    }

    // ---------------- RIPPLES AND SPLASHES ----------------

    // A ripple ring spreading from here. When all rings are taken, the oldest one makes way.
    public void AddRipple(Vector3 pos, float strength)
    {
        if (ripples.Count >= MaxRipples)
        {
            int oldest = 0;
            for (int i = 1; i < ripples.Count; i++) if (ripples[i].Age > ripples[oldest].Age) oldest = i;
            ripples.RemoveAt(oldest);
        }
        ripples.Add(new Ripple { Spot = new Vector2(pos.X, pos.Z), Age = 0f, Strength = Math.Clamp(strength, 0f, 1f) });
    }

    // A few droplets thrown up from here (a bathing bird's splash).
    public void Splash(Vector3 pos, int count, float power)
    {
        for (int i = 0; i < count && droplets.Count < MaxDroplets; i++)
        {
            float angle = Rng.Range(0f, MathF.Tau);
            float reach = Rng.Range(0.3f, 1f) * power;
            droplets.Add(new Droplet
            {
                Position = new Vector3(pos.X, 0.05f, pos.Z),
                Velocity = new Vector3(MathF.Cos(angle) * reach, Rng.Range(1.2f, 2.2f) * power, MathF.Sin(angle) * reach),
                Size = Rng.Range(0.025f, 0.045f),
            });
        }
    }

    // ---------------- SAVING (see SaveGame.cs) ----------------

    public PoolSave ToSave() => new() { Lobes = lobes.Select(l => new LobeSave { Center = l.Center, Radius = l.Radius }).ToList() };

    public static Pool FromSave(PoolSave save) =>
        new Pool(save.Lobes.Select(l => new Lobe { Center = l.Center, Radius = l.Radius }));

    // ---------------- A NEW POOL ----------------

    // Lays out a pool: one big lobe toward the middle of the world and two smaller ones off its sides,
    // so it reads as a pond, not a circle. Keeps clear of feeders, rocks and sprouts already there,
    // and of any other spots in avoid (an old save's sprouts, which are loaded after the pool).
    // Tries a number of spots and gives up quietly if none is clear (the world then has no pool).
    public static void PlaceNew(World world, IEnumerable<Vector3>? avoid = null)
    {
        List<Vector3> keepClear = avoid?.ToList() ?? new();
        for (int attempt = 0; attempt < 80; attempt++)
        {
            Vector3 middle = world.RandomGroundPointNearMiddle(EdgeMargin);
            Vector2 c = new Vector2(middle.X, middle.Z);

            float mainRadius = Rng.Range(1.6f, 2f);
            float angle = Rng.Range(0f, MathF.Tau);
            List<Lobe> shape = new() { new Lobe { Center = c, Radius = mainRadius } };
            for (int i = 0; i < 2; i++)
            {
                float a = angle + i * MathF.PI + Rng.Range(-0.7f, 0.7f);
                float dist = mainRadius * Rng.Range(0.8f, 1.1f);
                shape.Add(new Lobe { Center = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * dist, Radius = Rng.Range(0.8f, 1.25f) });
            }

            Pool pool = new Pool(shape);
            if (!pool.IsClear(world) || keepClear.Any(spot => pool.DistanceAt(spot) < Sprout.Reach + 0.3f)) continue;

            world.AddEntity(pool);
            world.FlushChanges();
            return;
        }
    }

    // Inside the world (by EdgeMargin), and clear of every feeder, rock and sprout.
    private bool IsClear(World world)
    {
        foreach (Lobe lobe in lobes)
        {
            Vector3 c = new Vector3(lobe.Center.X, 0f, lobe.Center.Y);
            if (!world.IsInside(c, -(lobe.Radius + 3f))) return false;    // the whole lobe, plus room to walk around it
        }
        foreach (Feeder feeder in world.GetAllEntities<Feeder>())
        {
            if (DistanceAt(feeder.Position) < feeder.Info.FootRadius + FeederGap) return false;
        }
        foreach (Rock rock in world.GetAllEntities<Rock>())
        {
            if (DistanceAt(rock.Position) < rock.Radius + RockGap) return false;
        }
        foreach (Sprout sprout in world.GetAllEntities<Sprout>())
        {
            if (DistanceAt(sprout.Position) < Sprout.Reach + 0.3f) return false;
        }
        return true;
    }
}
