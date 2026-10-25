using System.Numerics;

// A rock lying in the garden. Placed when a new world is made and saved with it; it never moves.
// Seen from above it's a circle (Radius); from the side a low dome (Height), so HeightAt gives the top of it anywhere.
// Birds walk around rocks and sometimes hop up to sit on one (BirdMover). Seed that lands on a rock rolls off (Food).
// The grass under a rock never wears, and grows a little lusher in a ring around it (GroundWear).
public class Rock : Entity
{
    // TUNING
    private const int BigRocks = 2;
    private const int SmallRocksMin = 3, SmallRocksMax = 5;
    private const float RockGap = 1.2f;            // space kept between rocks' edges, so birds can pass between
    private const float FeederGap = 0.8f;          // space kept between a rock and a feeder
    private const float NestleChance = 0.5f;       // share of small rocks placed right beside a big one
    private const float EdgeMargin = 4f;           // rocks stay this far in from the world edge

    public float Radius { get; private init; }     // footprint, seen from above
    public float Height { get; private init; }
    public int Look { get; private init; }         // picks the lumps and shade when drawn (WorldDrawer)

    // How many birds can sit on it at once.
    public int PerchRoom => Math.Max(1, (int)(Radius * 2.2f));

    public Rock(Vector3 position, float radius, float height, int look)
    {
        Position = new Vector3(position.X, 0f, position.Z);
        Radius = radius;
        Height = height;
        Look = look;
    }

    public override void Update(float dt) { }

    public Vector2 Flat => new Vector2(Position.X, Position.Z);

    // The top of the rock at a world position (a dome), or 0 off the rock.
    public float HeightAt(Vector3 pos)
    {
        float d = Vector2.Distance(new Vector2(pos.X, pos.Z), Flat) / Radius;
        return d >= 1f ? 0f : Height * MathF.Sqrt(1f - d * d);
    }

    // Saving (see SaveGame.cs)
    public RockSave ToSave() => new() { Position = Position, Radius = Radius, Height = Height, Look = Look };
    public static Rock FromSave(RockSave save) => new Rock(save.Position, save.Radius, save.Height, save.Look);

    // ---------------- LOOKING UP ROCKS ----------------

    // The rock whose footprint (grown by margin) covers this spot, or null.
    public static Rock? At(Vector3 pos, float margin = 0f)
    {
        foreach (Rock rock in World.Instance.GetAllEntities<Rock>())
        {
            if (Vector2.Distance(new Vector2(pos.X, pos.Z), rock.Flat) < rock.Radius + margin) return rock;
        }
        return null;
    }

    // The same spot moved just outside any rock it's on (at least margin from the rock's edge). Height is kept.
    public static Vector3 PushOut(Vector3 pos, float margin = 0.15f)
    {
        foreach (Rock rock in World.Instance.GetAllEntities<Rock>())
        {
            Vector2 away = new Vector2(pos.X, pos.Z) - rock.Flat;
            float dist = away.Length();
            float min = rock.Radius + margin;
            if (dist >= min) continue;

            Vector2 dir = dist > 0.001f ? away / dist : new Vector2(1f, 0f);
            Vector2 flat = rock.Flat + dir * min;
            pos = new Vector3(flat.X, pos.Y, flat.Y);
        }
        return pos;
    }

    // ---------------- A NEW GARDEN ----------------

    // Lays out the rocks for a new world: two big ones toward the middle and a few small ones,
    // some nestled beside a big rock. Keeps clear of the feeders already standing.
    public static void PlaceNew(World world)
    {
        List<Rock> placed = new();
        for (int i = 0; i < BigRocks; i++)
        {
            TryPlace(world, placed, Rng.Range(0.95f, 1.3f), Rng.Range(0.5f, 0.75f), nestle: false);
        }

        int small = SmallRocksMin + Rng.Next(SmallRocksMax - SmallRocksMin + 1);
        for (int i = 0; i < small; i++)
        {
            TryPlace(world, placed, Rng.Range(0.35f, 0.55f), Rng.Range(0.18f, 0.3f), nestle: Rng.Chance(NestleChance));
        }

        foreach (Rock rock in placed) world.AddEntity(rock);
        world.FlushChanges();
    }

    // Tries a number of random spots and keeps the first that's clear. Gives up quietly if none is.
    private static void TryPlace(World world, List<Rock> placed, float radius, float height, bool nestle)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            Vector3 spot;
            float gap = RockGap;
            if (nestle && placed.Count > 0 && attempt < 30)
            {
                // Right beside one of the big rocks, nearly touching
                Rock big = placed[Rng.Next(Math.Min(BigRocks, placed.Count))];
                float angle = Rng.Range(0f, MathF.PI * 2f);
                float dist = big.Radius + radius + Rng.Range(0.05f, 0.35f);
                spot = big.Position + new Vector3(MathF.Cos(angle) * dist, 0f, MathF.Sin(angle) * dist);
                gap = 0f;
            }
            else
            {
                spot = world.RandomGroundPointNearMiddle(EdgeMargin);
            }

            if (!world.IsInside(spot, -EdgeMargin) || !IsClear(world, placed, spot, radius, gap)) continue;

            placed.Add(new Rock(spot, radius, height, Rng.Next(100000)));
            return;
        }
    }

    // Clear of the other rocks (by gap; a nestled rock only needs to not overlap) and of every feeder.
    private static bool IsClear(World world, List<Rock> placed, Vector3 spot, float radius, float gap)
    {
        Vector2 flat = new Vector2(spot.X, spot.Z);
        foreach (Rock other in placed)
        {
            float need = other.Radius + radius + gap;
            if (Vector2.Distance(flat, other.Flat) < need) return false;
        }
        foreach (Feeder feeder in world.GetAllEntities<Feeder>())
        {
            float need = feeder.Info.FootRadius + radius + FeederGap;
            if (Vector2.Distance(flat, new Vector2(feeder.Position.X, feeder.Position.Z)) < need) return false;
        }
        return true;
    }
}
