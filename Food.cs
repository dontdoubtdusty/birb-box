using System.Numerics;

// A pile of seed. It can fall from above, then gets eaten a bite at a time.
// A pile can hold several kinds of seed. Each bird only eats the kinds it likes,
// so a mixed pile can outlast its first visitors.
//
// The seed lies in layers, in the order it arrived (newest on top), so pouring corn onto millet
// heaps corn over the millet. Each layer keeps its own age. Birds eat from the top layer of the kind they want.
// Seed landing near a pile on the dirt joins it as a new top layer, so pouring in one spot grows one heap.
//
// Seed left on the dirt goes bad. After its seed's SpoilTime (Seeds.cs) birds won't eat it,
// and once a layer has sat there FadeAfter times that long, it fades into the dirt.
// Wild seed fades sooner, as soon as it has spoiled. A pile with no layers left is gone.
// Each seed that rots away has a small chance to come up as a sprout there instead, so heaps sprout far more
// often than a lone seed (TrySprout, Sprout.cs).
//
// Seed falling onto a feeder is caught by it (LandOn) instead of reaching the dirt.
// The seed a feeder holds is a Food too (Feeder is set): it never spoils, and birds perch to eat it.
// Seed falling onto a rock rolls down its side and comes to rest just past its edge.
// The seed on a sprout's heads is a Food too (Plant is set): it never spoils, falls or joins a pile, and the sprout
// fills it, sheds from it and removes it.
public class Food : Entity
{
    public const float BiteSize = 0.5f;           // seed removed per bite
    public const float NutritionPerBite = 0.35f;
    public const float FadeAfter = 2f;            // times the spoil time
    public const float SettledAge = 10f;          // a layer this old has long finished landing (for seed loaded from a save)

    // Landing seed joins a pile on the dirt this close (or within most of a bigger heap's width).
    // Kept under the pour spacing (Pouring.PileSpacing), so a dragged pour still leaves a trail.
    private const float JoinReach = 0.2f, JoinHeapShare = 0.8f;
    private const int MaxLayers = 40;             // past this, neighboring layers of the same seed are combined

    // The layers, bottom first. Changed only here, through AddLayer, TakeBite and the fading in Update.
    public List<SeedLayer> Layers { get; } = new();

    // Amount of each seed left, added up from the layers (read only; worked out again whenever a layer changes).
    public Dictionary<SeedType, float> Contents { get; } = new();

    public Feeder? Feeder { get; }              // the feeder holding this seed, or null for seed on the dirt
    public Feeder? LandOn { get; init; }        // while falling: the feeder below that will catch it
    public Sprout? Plant { get; private set; }  // the sprout whose seedheads this is, or null

    private float fallSpeed = 0f;
    private Rock? rollingOff;                   // the rock it landed on and is rolling down, or null
    private float rollSpeed = 0f;
    private const float RollStartSpeed = 0.6f, RollSpeedUp = 5f, RestPastEdge = 0.15f;
    private bool removed = false;
    private bool needsLanding = false;          // placed straight on the dirt: lands (and maybe joins a pile) on its first update

    public bool IsLanded { get; private set; }
    public bool IsRolling => rollingOff != null;
    public float FallSpeed => fallSpeed;
    public float Total { get; private set; }
    public bool IsFullyEaten => Total <= 0.01f;
    public bool IsGone => removed;

    // How wide the heap is, in world units (birds stand just outside it to peck)
    public float HeapRadius { get; private set; }

    // The seed there is most of, used for map dots.
    public SeedType MainSeed { get; private set; }

    // impactSpeed: how fast the seed was falling when it landed (for the landing bounce in WorldDrawer).
    // age: how long ago it landed (SettledAge for seed loaded from a save).
    public Food(Vector3 position, Dictionary<SeedType, float> contents, Feeder? feeder = null, float impactSpeed = 0f, float age = 0f)
    {
        Position = position;
        Feeder = feeder;
        IsLanded = feeder != null || position.Y <= 0f;
        needsLanding = feeder == null && IsLanded;
        foreach ((SeedType seed, float amount) in contents) AddLayer(new SeedLayer(seed, amount, age) { ImpactSpeed = impactSpeed });
        RecountContents();
    }

    public override void Update(float dt)
    {
        if (Plant != null) return;    // seedheads: the sprout looks after them

        if (needsLanding)
        {
            needsLanding = false;
            Land(0f);
            if (removed) return;
        }

        if (rollingOff != null)
        {
            RollOff(dt);
        }
        else if (!IsLanded)
        {
            // A feeder that was picked up while the seed fell can't catch it anymore.
            bool caught = LandOn != null && LandOn.InWorld;
            float floor = caught ? LandOn!.Info.CatchHeight : 0f;

            fallSpeed += World.Gravity * dt;
            float y = MathF.Max(floor, Position.Y - fallSpeed * dt);
            Position = new Vector3(Position.X, y, Position.Z);

            // Hitting the top of a rock starts it rolling instead of landing
            Rock? rock = caught ? null : Rock.At(Position);
            if (rock != null && y <= rock.HeightAt(Position))
            {
                Position = new Vector3(Position.X, rock.HeightAt(Position), Position.Z);
                rollingOff = rock;
                rollSpeed = RollStartSpeed;
                fallSpeed = 0f;
                return;
            }

            if (y <= floor)
            {
                IsLanded = true;
                if (caught) DropInto(LandOn!);
                else Land(fallSpeed);
                if (removed) return;
            }
        }
        else
        {
            AgeLayers(dt);
        }

        if (IsFullyEaten && !removed)
        {
            removed = true;
            World.Instance.RemoveEntity(this);
        }
    }

    // Layers grow older; on the dirt, a layer that has gone bad long enough fades away (and may sprout).
    private void AgeLayers(float dt)
    {
        bool faded = false;
        for (int i = Layers.Count - 1; i >= 0; i--)
        {
            SeedLayer layer = Layers[i];
            layer.Age += dt;
            if (Feeder == null && HasFaded(layer))
            {
                Layers.RemoveAt(i);
                faded = true;
                TrySprout(layer);
            }
        }
        if (faded) RecountContents();
    }

    // A layer has just rotted away: each of its seed has its own small chance (SimTuning.SproutChancePerSeed)
    // to come up as a sprout of that seed here, and one is enough. So a lone seed rarely sprouts, a heap
    // (many layers, each rolling as it rots) quite often, even with fresh seed still on top.
    private void TrySprout(SeedLayer layer)
    {
        float chance = 1f - MathF.Pow(1f - SimTuning.Now.SproutChancePerSeed, layer.Amount);
        if (Rng.Chance(chance)) Sprout.TryPlant(Position, layer.Seed);
    }

    // Just came to rest on the dirt. If another pile lies close by, this seed joins it as its new top layers
    // and this pile is gone; otherwise it starts a heap of its own here.
    private void Land(float speed)
    {
        Food? heap = PileToJoin();
        if (heap == null)
        {
            foreach (SeedLayer layer in Layers) layer.ImpactSpeed = speed;
            return;
        }

        Vector2 offset = new Vector2(Position.X - heap.Position.X, Position.Z - heap.Position.Z);
        foreach (SeedLayer layer in Layers)
        {
            layer.ImpactOffset = offset;
            layer.ImpactSpeed = speed;
            heap.AddLayer(layer);
        }
        heap.RecountContents();
        Layers.Clear();
        RecountContents();
        removed = true;
        World.Instance.RemoveEntity(this);
    }

    // The nearest pile on the dirt close enough to join, or null.
    private Food? PileToJoin()
    {
        Food? best = null;
        float bestDistance = float.MaxValue;
        Vector2 here = new Vector2(Position.X, Position.Z);
        foreach (Food other in World.Instance.GetAllEntities<Food>())
        {
            if (other == this || other.Feeder != null || other.Plant != null || !other.IsLanded || other.IsGone || other.needsLanding) continue;

            float distance = Vector2.Distance(here, new Vector2(other.Position.X, other.Position.Z));
            float reach = MathF.Max(JoinReach, other.HeapRadius * JoinHeapShare);
            if (distance <= reach && distance < bestDistance)
            {
                best = other;
                bestDistance = distance;
            }
        }
        return best;
    }

    // Rolls outward from the rock's middle along its top, getting faster, and lands on the ground once past the edge.
    private void RollOff(float dt)
    {
        Rock rock = rollingOff!;
        Vector2 away = new Vector2(Position.X, Position.Z) - rock.Flat;
        float dist = away.Length();
        Vector2 dir;
        if (dist > 0.001f) dir = away / dist;
        else
        {
            float angle = Rng.Fixed(EntityID, 5) * MathF.PI * 2f;    // dead center: any way will do, but always the same one
            dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        rollSpeed += RollSpeedUp * dt;
        dist += rollSpeed * dt;
        Vector2 flat = rock.Flat + dir * dist;

        if (dist >= rock.Radius + RestPastEdge)
        {
            flat = rock.Flat + dir * (rock.Radius + RestPastEdge);
            Position = Rock.PushOut(new Vector3(flat.X, 0f, flat.Y));    // clear of a rock right beside it too
            rollingOff = null;
            IsLanded = true;
            Land(0f);
            return;
        }
        Vector3 onTop = new Vector3(flat.X, 0f, flat.Y);
        Position = new Vector3(flat.X, rock.HeightAt(onTop), flat.Y);
    }

    // Hands this seed to the feeder. Whatever doesn't fit carries on falling to the dirt beside it.
    private void DropInto(Feeder feeder)
    {
        Dictionary<SeedType, float> spilled = feeder.Catch(Contents, fallSpeed);
        if (spilled.Count > 0)
        {
            Vector3 beside = feeder.Position + new Vector3(feeder.Info.FootRadius + 0.1f, feeder.Info.CatchHeight, 0f);
            World.Instance.AddEntity(new Food(beside, spilled));
        }
        removed = true;
        World.Instance.RemoveEntity(this);
    }

    // ---------------- SAVING (see SaveGame.cs) ----------------
    // Seed in a feeder is saved by its feeder, so this is only for seed on the dirt or still falling.

    public FoodSave ToSave() => new()
    {
        Position = Position,
        Contents = new(Contents),
        FallSpeed = fallSpeed,
        Layers = Layers.Select(layer => new SeedLayerSave { Seed = layer.Seed, Amount = layer.Amount, Age = layer.Age }).ToList(),
    };

    // Seed still falling looks for the feeder under it again, so feeders have to be loaded first.
    // A save from before layers has one layer per seed, all as old as the pile was.
    public static Food FromSave(FoodSave save)
    {
        Feeder? below = save.Position.Y > 0f ? FoodSpawner.FeederBelow(save.Position) : null;
        Food food = new Food(save.Position, new()) { LandOn = below };
        food.fallSpeed = save.FallSpeed;
        food.needsLanding = false;    // it already landed (or joined a pile) before it was saved

        if (save.Layers != null)
        {
            foreach (SeedLayerSave layer in save.Layers) food.AddLayer(new SeedLayer(layer.Seed, layer.Amount, layer.Age));
        }
        else
        {
            foreach ((SeedType seed, float amount) in save.Contents) food.AddLayer(new SeedLayer(seed, amount, save.Age));
        }
        food.RecountContents();
        return food;
    }

    // Seed added on top of a feeder's pile.
    public void AddSeed(Dictionary<SeedType, float> more, float impactSpeed = 0f)
    {
        foreach ((SeedType seed, float amount) in more) AddLayer(new SeedLayer(seed, amount, 0f) { ImpactSpeed = impactSpeed });
        RecountContents();
    }

    // Puts a layer on top. Too many layers: neighboring layers of the same seed are combined, from the bottom up
    // (their age becomes the average, weighted by amount). The caller recounts.
    private void AddLayer(SeedLayer layer)
    {
        if (layer.Amount <= 0.01f) return;
        Layers.Add(layer);

        for (int i = 0; Layers.Count > MaxLayers && i < Layers.Count - 1; i++)
        {
            SeedLayer lower = Layers[i];
            SeedLayer upper = Layers[i + 1];
            if (lower.Seed != upper.Seed) continue;

            float amount = lower.Amount + upper.Amount;
            lower.Age = (lower.Age * lower.Amount + upper.Age * upper.Amount) / amount;
            lower.Amount = amount;
            Layers.RemoveAt(i + 1);
            i--;
        }
    }

    // How far gone a layer is: 0 fresh, 1 spoiled, FadeAfter rotted through.
    // Always 0 for seed that never spoils, for seed in a feeder and for seedheads. SimTuning.SpoilSpeed speeds it up or slows it down.
    public float Rot(SeedLayer layer)
    {
        float? spoilTime = Seeds.Get(layer.Seed).SpoilTime;
        return spoilTime == null || Feeder != null || Plant != null ? 0f : layer.Age * SimTuning.Now.SpoilSpeed / spoilTime.Value;
    }

    public bool IsSpoiled(SeedLayer layer) => Rot(layer) >= 1f;

    // Rotted through, or wild seed that has spoiled
    private bool HasFaded(SeedLayer layer)
    {
        if (layer.Seed == SeedType.Wild) return IsSpoiled(layer);
        return Seeds.Get(layer.Seed).SpoilTime != null && Rot(layer) >= FadeAfter;
    }

    // Empties the pile at once (tools panel cheat). Birds stop wanting it, and it removes itself next update.
    public void ClearAway()
    {
        Layers.Clear();
        RecountContents();
    }

    // ---------------- SEEDHEADS (see Sprout.cs) ----------------

    // The seed on a sprout's heads, at the sprout's foot. It starts empty; the sprout fills it.
    public static Food OnSprout(Sprout sprout)
    {
        Food heads = new Food(new Vector3(sprout.Position.X, 0f, sprout.Position.Z), new());
        heads.Plant = sprout;
        heads.needsLanding = false;
        heads.RecountContents();
        return heads;
    }

    // Takes up to this much seed off the top, whatever its kind (a sprout shedding). Returns how much came off.
    public float TakeFromTop(float amount)
    {
        float taken = 0f;
        for (int i = Layers.Count - 1; i >= 0 && taken < amount - 0.0001f; i--)
        {
            SeedLayer layer = Layers[i];
            float bit = MathF.Min(layer.Amount, amount - taken);
            layer.Amount -= bit;
            taken += bit;
            if (layer.Amount <= 0.01f) Layers.RemoveAt(i);
        }
        RecountContents();
        return taken;
    }

    // Empties it and takes it out of the world at once (a sprout's heads when it dies). Birds going for it stop.
    public void RemoveNow()
    {
        Layers.Clear();
        RecountContents();
        if (removed) return;
        removed = true;
        World.Instance.RemoveEntity(this);
    }

    // How much the bird likes eating here: 1 on the dirt, the feeder's appeal in a feeder.
    public float AppealFor(BirdType bird) => Feeder?.Info.AppealFor(bird) ?? 1f;

    // The fresh seed left in this pile that the bird likes most, or null if there's nothing here for it
    // (including when it's in a feeder the bird can't use).
    public SeedType? FavoriteFor(BirdType bird)
    {
        if (AppealFor(bird) <= 0f) return null;

        SeedType? best = null;
        float bestTaste = 0f;

        foreach (SeedLayer layer in Layers)
        {
            if (layer.Amount <= 0.01f || IsSpoiled(layer)) continue;

            Seeds.Get(layer.Seed).Tastes.TryGetValue(bird, out float taste);
            if (taste > bestTaste)
            {
                best = layer.Seed;
                bestTaste = taste;
            }
        }
        return best;
    }

    // True if this pile holds some fresh seed of this kind.
    public bool HasFresh(SeedType seed)
    {
        foreach (SeedLayer layer in Layers)
        {
            if (layer.Seed == seed && layer.Amount > 0.01f && !IsSpoiled(layer)) return true;
        }
        return false;
    }

    // Eats one bite of seed here: the bird's own favorite seed (see Bird.FavoriteSeed) if the pile has some,
    // otherwise the seed here its kind likes most. The bite comes off the topmost fresh layers of that seed.
    // Returns false if there was nothing it wanted.
    public bool TakeBite(BirdType bird, SeedType? ownFavorite = null)
    {
        SeedType? seed = FavoriteFor(bird);
        if (seed == null) return false;
        if (ownFavorite is SeedType own && HasFresh(own) && Seeds.Eats(bird, own)) seed = own;

        float bite = BiteSize;
        for (int i = Layers.Count - 1; i >= 0 && bite > 0.0001f; i--)
        {
            SeedLayer layer = Layers[i];
            if (layer.Seed != seed.Value || IsSpoiled(layer)) continue;

            float taken = MathF.Min(layer.Amount, bite);
            layer.Amount -= taken;
            bite -= taken;
            if (layer.Amount <= 0.01f) Layers.RemoveAt(i);
        }
        RecountContents();
        return true;
    }

    // Adds up the layers into Contents, Total, MainSeed and the heap's width (for seedheads, how far the heads reach).
    private void RecountContents()
    {
        Contents.Clear();
        float volume = 0f;
        foreach (SeedLayer layer in Layers)
        {
            Contents.TryGetValue(layer.Seed, out float current);
            Contents[layer.Seed] = current + layer.Amount;
            volume += SeedHeap.GrainCount(layer.Amount) * SeedHeap.GrainVolume(layer.Seed) * (1f - SeedHeap.StragglerShare(layer.Seed));
        }

        Total = 0f;
        MainSeed = SeedType.Wild;
        float most = 0f;
        foreach ((SeedType seed, float amount) in Contents)
        {
            Total += amount;
            if (amount > most)
            {
                most = amount;
                MainSeed = seed;
            }
        }
        HeapRadius = Plant != null ? Sprout.Reach : SeedHeap.Radius(volume);
    }
}
