using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

// Keeps the terrarium between runs. There is one save file: it's written every couple of minutes and on quit,
// and read back when the game starts. "New world" in the Esc menu starts over.
//
// The file is plain JSON next to the game (the project folder while developing, beside the .exe in a Release copy),
// so it can be opened and read. Everything saved goes into the plain "...Save" classes at the bottom of this file.
// Each game class turns itself into one (ToSave) and back (FromSave), since only it can reach its private parts.
public class SaveGame
{
    public const string FileName = "birbbox-save.json";
    private const float AutosaveInterval = 120f;    // seconds of real time
    private const int FormatVersion = 1;            // goes up if the file's layout changes in a way old saves can't follow

    public static string FilePath => Path.Combine(Assets.ProjectFolder ?? AppContext.BaseDirectory, FileName);

    private float autosaveTimer = AutosaveInterval;
    private bool warnedFailure = false;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IncludeFields = true,                                                  // Vector2 and Vector3 keep X, Y, Z in fields
        Converters = { new JsonStringEnumConverter() },                         // "Emberwing" instead of 0
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,    // the sold scraper restocks at "Infinity"
    };

    // Counts down real time, paused or not, and saves when it runs out.
    public void Update(float dt, GameEngine engine)
    {
        autosaveTimer -= dt;
        if (autosaveTimer > 0f) return;

        autosaveTimer = AutosaveInterval;
        Save(engine);
    }

    // Writes a temporary file first and then swaps it in, so a crash halfway through never leaves a broken save.
    public void Save(GameEngine engine)
    {
        try
        {
            string json = JsonSerializer.Serialize(Capture(engine), Options);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FilePath, overwrite: true);
            warnedFailure = false;
        }
        catch (Exception e)
        {
            if (!warnedFailure) EventLog.Add($"The terrarium couldn't be saved: {e.Message}");
            warnedFailure = true;
        }
    }

    // On start: the saved world if there is one, otherwise a fresh one.
    // A save that can't be read is set aside under another name instead of being overwritten.
    public void LoadOrStartNew(GameEngine engine)
    {
        if (!File.Exists(FilePath))
        {
            StartNewWorld(engine);
            return;
        }

        try
        {
            SaveData data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath), Options)
                            ?? throw new InvalidDataException("the file is empty");
            Apply(data, engine);
            EventLog.Add("The terrarium flickers back to life, just as you left it.");
        }
        catch (Exception e)
        {
            string brokenPath = Path.ChangeExtension(FilePath, ".broken.json");
            try { File.Move(FilePath, brokenPath, overwrite: true); } catch (Exception) { }

            StartNewWorld(engine);
            EventLog.Add($"The old save couldn't be read ({e.Message}). It was set aside as {Path.GetFileName(brokenPath)}.");
        }
    }

    // Everything back to how a first run starts: an empty SaveData is a brand-new world, then wild seed is scattered.
    public void StartNewWorld(GameEngine engine)
    {
        Apply(new SaveData(), engine);
        engine.SeedStartingWorld();
    }

    // ---------------- GAME -> SAVE ----------------

    private static SaveData Capture(GameEngine engine)
    {
        engine.World.FlushChanges();    // so nothing waiting to be added is missed

        SaveData data = new()
        {
            FormatVersion = FormatVersion,
            GameVersion = GameEngine.Version,
            SavedAt = DateTime.Now,
            SimTime = engine.SimTime,
            Coins = engine.Coins,
            HasScraper = engine.HasScraper,
            FrostcrestSeen = Seeds.FrostcrestSeen,
            VisitedStore = engine.Store.VisitedBefore,
            View = new ViewSave
            {
                Center = engine.View.Center,
                Zoom = engine.View.Zoom,
                Yaw = engine.View.Yaw,
                StoreYaw = engine.Store.Yaw,
            },
            BagSupply = new(engine.Bucket.BagSupply),
            Bucket = engine.Bucket.Bucket.ToSave(),
            NameCounters = engine.BirdSpawner.SaveNameCounters(),
            Regulars = engine.BirdSpawner.SaveRegulars(),
            RestockTimers = engine.Store.Stock.Select(item => item.RestockTimer).ToList(),
            BoughtFeeders = engine.Placer.Bought.ToList(),
            CarriedFeeder = engine.Placer.Carried?.ToSave(),
            Log = EventLog.Entries.ToList(),
            GroundWear = engine.Wear.ToSave(),
            Rocks = new(),
        };

        // Seed sitting in a feeder is saved with its feeder.
        foreach (Entity entity in engine.World.Entities)
        {
            switch (entity)
            {
                case Bird bird: data.Birds.Add(bird.ToSave()); break;
                case Food food when food.Feeder == null: data.Seed.Add(food.ToSave()); break;
                case Feeder feeder: data.Feeders.Add(feeder.ToSave()); break;
                case Gunk gunk: data.Gunk.Add(gunk.ToSave()); break;
                case Coin coin: data.LooseCoins.Add(coin.ToSave()); break;
                case Rock rock: data.Rocks.Add(rock.ToSave()); break;
            }
        }
        return data;
    }

    // ---------------- SAVE -> GAME ----------------

    // Replaces the whole world and the player's things with what's in the save.
    private static void Apply(SaveData data, GameEngine engine)
    {
        World world = engine.World;
        world.Clear();
        engine.SelectedBird = null;
        engine.SelectedFeeder = null;
        engine.HoveredBird = null;

        engine.SimTime = data.SimTime;
        engine.Coins = data.Coins;
        engine.HasScraper = data.HasScraper;
        Seeds.FrostcrestSeen = data.FrostcrestSeen;
        engine.Store.VisitedBefore = data.VisitedStore;

        engine.View.Zoom = Math.Clamp(data.View.Zoom, ViewWindow.ZoomLevels[0], ViewWindow.ZoomLevels[^1]);
        engine.View.JumpTo(data.View.Center);    // after the zoom, so the center is kept inside the world for it
        engine.View.Yaw = data.View.Yaw;
        engine.Store.Yaw = data.View.StoreYaw;

        // A bag missing from the save (a seed added since) starts with its usual supply.
        foreach (SeedType seed in Seeds.Bags)
        {
            engine.Bucket.BagSupply[seed] = data.BagSupply.TryGetValue(seed, out float left) ? left : Seeds.Get(seed).StartingSupply;
        }
        engine.Bucket.Bucket.Restore(data.Bucket);
        engine.BirdSpawner.RestoreNameCounters(data.NameCounters);
        engine.BirdSpawner.RestoreRegulars(data.Regulars);
        engine.Store.RestoreTimers(data.RestockTimers, data.HasScraper);
        engine.Placer.Restore(data.BoughtFeeders, data.CarriedFeeder != null ? Feeder.FromSave(data.CarriedFeeder) : null);
        EventLog.Restore(data.Log);
        engine.Wear.Restore(data.GroundWear, world.HalfSize);

        // Feeders go in first, so seed that was still falling can find the feeder below it.
        // A save from a bigger world can have things past the edge: they're pulled back inside (see PullInside).
        foreach (FeederSave feeder in data.Feeders) Feeder.FromSave(feeder).PlaceAt(world.PullInside(feeder.Position));
        world.FlushChanges();

        // Rocks next, so nothing else ends up inside one. A new world, or a save from before rocks, gets a fresh set
        // (placed clear of the feeders). The lush grass around them is worked out from where they are.
        if (data.Rocks != null) foreach (RockSave rock in data.Rocks) world.AddEntity(Rock.FromSave(rock));
        world.FlushChanges();
        if (data.Rocks == null) Rock.PlaceNew(world);
        engine.Wear.RebuildLush(world);

        // Seed lying on the ground is moved off any rock; seed still falling rolls off by itself.
        foreach (FoodSave food in data.Seed)
        {
            food.Position = world.PullInside(food.Position);
            if (food.Position.Y <= 0f) food.Position = Rock.PushOut(food.Position);
            world.AddEntity(Food.FromSave(food));
        }
        foreach (GunkSave gunk in data.Gunk) { gunk.Position = Rock.PushOut(world.PullInside(gunk.Position), gunk.Radius); world.AddEntity(Gunk.FromSave(gunk)); }
        foreach (CoinSave coin in data.LooseCoins) { coin.Position = Rock.PushOut(world.PullInside(coin.Position), Coin.Radius); world.AddEntity(Coin.FromSave(coin)); }
        foreach (BirdSave save in data.Birds)
        {
            Bird bird = Bird.FromSave(save);
            BringBirdInside(bird, world);
            world.AddEntity(bird);
        }
        world.FlushChanges();
    }

    // A bird from a bigger world that's now past the edge walks back in from just outside it, like a new arrival.
    // Flying arrivals land inside instead; leaving birds just carry on flying off.
    private static void BringBirdInside(Bird bird, World world)
    {
        if (bird.State == BirdState.Leaving) return;

        if (bird.State == BirdState.Arriving && bird.ArrivingByAir)
        {
            bird.FlightTarget = world.PullInside(bird.FlightTarget, 2f);
            return;
        }

        if (world.IsInside(bird.Position, -1f)) return;

        // Just outside the edge, on the side the bird was on, heading straight in.
        float edge = world.HalfSize + 2f;
        Vector3 start = new Vector3(Math.Clamp(bird.Position.X, -edge, edge), bird.GroundY, Math.Clamp(bird.Position.Z, -edge, edge));
        Vector3 inward = Vector3.Normalize(new Vector3(-start.X, 0f, -start.Z));
        bird.Position = start;
        bird.FlightTarget = start + inward * 8f;
        bird.ArrivingByAir = false;
        bird.State = BirdState.Arriving;
    }
}

// ---------------- WHAT'S IN THE FILE ----------------
// Plain values only. Every field has a default, so a save from an older version still loads:
// anything it doesn't have yet just starts at the default.

public class SaveData
{
    public int FormatVersion { get; set; }
    public string GameVersion { get; set; } = "";
    public DateTime SavedAt { get; set; }

    // THE PLAYER
    public float SimTime { get; set; }
    public int Coins { get; set; }
    public bool HasScraper { get; set; }
    public bool FrostcrestSeen { get; set; }
    public bool VisitedStore { get; set; }
    public ViewSave View { get; set; } = new();
    public Dictionary<SeedType, float> BagSupply { get; set; } = new();
    public BucketSave Bucket { get; set; } = new();
    public List<FeederStyle> BoughtFeeders { get; set; } = new();    // bought, not yet handed over
    public FeederSave? CarriedFeeder { get; set; }                    // in hand, waiting to be set down

    // THE STORE AND THE LOG
    public Dictionary<BirdType, int> NameCounters { get; set; } = new();
    public List<float> RestockTimers { get; set; } = new();          // one per shelf item, in shelf order
    public List<string> Log { get; set; } = new();

    // THE WORLD
    public List<BirdSave> Birds { get; set; } = new();
    public List<FoodSave> Seed { get; set; } = new();                 // piles on the dirt, or still falling
    public List<FeederSave> Feeders { get; set; } = new();
    public List<GunkSave> Gunk { get; set; } = new();
    public List<CoinSave> LooseCoins { get; set; } = new();
    public string GroundWear { get; set; } = "";                      // worn grass, one byte per cell (GroundWear.ToSave)
    public List<RockSave>? Rocks { get; set; }                        // null in a new world or a save from before rocks: new ones are placed
    public List<BirdSave> Regulars { get; set; } = new();             // birds away for now that may come back (BirdSpawner)
}

public class ViewSave
{
    public Vector2 Center { get; set; }
    public float Zoom { get; set; } = 1f;
    public float Yaw { get; set; }
    public float StoreYaw { get; set; }
}

public class BucketSave
{
    public List<LayerSave> Layers { get; set; } = new();
    public Dictionary<SeedType, float> Mixed { get; set; } = new();
}

public class LayerSave
{
    public SeedType Seed { get; set; }
    public float Amount { get; set; }
}

public class BirdSave
{
    public BirdType Type { get; set; }
    public string Name { get; set; } = "";
    public int ShapeIndex { get; set; }
    public Vector3 Position { get; set; }
    public float Speed { get; set; }
    public float Heading { get; set; }
    public float HopPhase { get; set; }
    public float Age { get; set; }
    public float Lifespan { get; set; }
    public float Hunger { get; set; }
    public BirdState State { get; set; }
    public Vector3 FlightTarget { get; set; }
    public bool ArrivingByAir { get; set; }
    public float VoicePitch { get; set; } = 1f;
    public bool IsNamed { get; set; }
    public int Visits { get; set; } = 1;
    public bool HasHome { get; set; }
    public Vector3 Home { get; set; }
}

public class FoodSave
{
    public Vector3 Position { get; set; }
    public Dictionary<SeedType, float> Contents { get; set; } = new();
    public float FallSpeed { get; set; }
    public float Age { get; set; }
}

public class FeederSave
{
    public FeederStyle Style { get; set; }
    public Vector3 Position { get; set; }
    public Dictionary<SeedType, float>? Seed { get; set; }    // null when empty
}

public class GunkSave
{
    public Vector3 Position { get; set; }
    public float Radius { get; set; }
}

public class RockSave
{
    public Vector3 Position { get; set; }
    public float Radius { get; set; }
    public float Height { get; set; }
    public int Look { get; set; }
}

public class CoinSave
{
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float LifeLeft { get; set; }
}
