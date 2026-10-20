using System.Numerics;
using Raylib_cs;

// The Gilded Granary: a little self-serve general store the Birb Box can project in place of the birds
// (see BoxMode). The people who built it studied old shops closely and got most of it right,
// but it's still a ship's store underneath.
//
// This class is the store's state: how it's turned, what's on the shelves, which item the mouse is on,
// the shopkeeper, and buying. StoreRenderer draws it; PriceTag shows the selected item's price.
//
// Store space: (0, 0, 0) is the middle of the box's floor, before the store is turned by Yaw.
// The shop was laid out small, so store space is scaled up by Scale to fill the box.
//
// The room is round: a horseshoe wall with its opening at the front (+Z), shelves curving along the back,
// and the counter in the middle facing the opening. Angles around the room are measured from the front (+Z)
// toward +X, in radians (see OnCircle).
public class SeedStore
{
    public const string Name = "The Gilded Granary";

    public const float Scale = 2.4f;     // scene units per store unit
    public float Yaw = 0f;               // how far the store is turned, in radians (separate from the birds' turn)

    // ROOM LAYOUT (store units)
    public const float WallRadius = 1.75f;
    public const float WallOpening = MathF.PI / 4f;        // the wall leaves this much open either side of the front

    // SHELVES: the top of each board, and the arc they curve along at the back of the room
    public static readonly float[] ShelfHeights = { 0.5f, 0.95f, 1.4f };
    public const float ShelfRadius = 1.55f;                // middle of the shelf boards, just inside the wall
    public const float ShelfStart = 95f * MathF.PI / 180f;
    public const float ShelfEnd = 245f * MathF.PI / 180f;
    public List<ShelfItem> Stock { get; } = new();

    // COUNTER AND KEEPER: the counter faces the opening; the keeper stands behind it on a stool
    public static readonly Vector3 CounterSpot = new Vector3(0f, 0f, 0.2f);
    public const float CounterTop = 0.76f;
    public static readonly Vector3 KeeperSpot = new Vector3(0f, 0f, -0.3f);
    public Shopkeeper Keeper { get; } = new Shopkeeper();

    // SHOPPING
    public ShelfItem? HoveredItem { get; private set; }    // the jar or sack under the mouse
    public ShelfItem? SelectedItem;                        // the one whose price tag is showing
    public const float RestockTime = 60f;                  // seconds before a bought item is replaced

    // The air recycler's blades turn slowly. It's ship machinery, so it keeps going when the birds are paused.
    public float FanAngle { get; private set; }
    private const float FanSpeed = 1.2f;    // radians per second

    private bool visitedBefore = false;

    public SeedStore()
    {
        StockShelves();
    }

    // showing: the box is showing the store, so a shift change gets a line in the log.
    public void Update(float dt, bool showing)
    {
        FanAngle += FanSpeed * dt;

        Vector3? lookAt = HoveredItem != null ? HoveredItem.Center - KeeperSpot : null;
        string outgoing = Keeper.Name;
        if (Keeper.Update(dt, lookAt) && showing)
        {
            EventLog.Add($"{outgoing} hands the counter over to {Keeper.Name}.");
        }

        foreach (ShelfItem item in Stock)
        {
            if (!item.OnShelf) item.RestockTimer -= dt;
        }
    }

    // Called when the box finishes switching to the store, and back.
    public void OnEnter()
    {
        SelectedItem = null;
        if (!visitedBefore)
        {
            EventLog.Add($"Welcome to {Name}, purveyors of fine seed since the year 1312. Click a jar or sack to see its price.");
            visitedBefore = true;
        }
        EventLog.Add($"{Keeper.Name} is minding the counter today.");
    }

    public void OnLeave()
    {
        SelectedItem = null;
        HoveredItem = null;
    }

    // Finds the jar or sack under the mouse, only items still on the shelf.
    // active: the store is showing and the mouse isn't over something else.
    public void UpdateHover(Ray sceneRay, bool active)
    {
        HoveredItem = null;
        if (!active) return;

        float bestDistance = float.MaxValue;
        foreach (ShelfItem item in Stock)
        {
            if (!item.OnShelf) continue;
            RayCollision hit = Raylib.GetRayCollisionSphere(sceneRay, ToScene(item.Center), item.PickRadius * Scale);
            if (hit.Hit && hit.Distance < bestDistance)
            {
                HoveredItem = item;
                bestDistance = hit.Distance;
            }
        }
    }

    // Pays for the item. Seed goes into that seed's bag on the bucket panel; the scraper is kept for good;
    // a feeder waits until the box shows the birds, then follows the mouse to be set down (FeederPlacer).
    // Returns false (and says why) if there aren't enough coins.
    public bool TryBuy(ShelfItem item)
    {
        GameEngine engine = GameEngine.Instance;

        if (engine.Coins < item.Price)
        {
            EventLog.Add($"Not enough coins for {item.Title}. It costs {item.Price}.");
            return false;
        }

        engine.Coins -= item.Price;
        if (SelectedItem == item) SelectedItem = null;
        Keeper.Celebrate();

        if (item.IsTool)
        {
            engine.HasScraper = true;
            item.RestockTimer = float.PositiveInfinity;    // there's only one, and it's yours to keep
            EventLog.Add("Bought the Muck Scraper. Click gunk in the box to scrape it up.");
        }
        else if (item.IsFeeder)
        {
            engine.Placer.AddBought(item.Feeder);
            item.RestockTimer = RestockTime;
            EventLog.Add($"Bought {item.Title}. Head back to the birbs to set it down.");
        }
        else
        {
            engine.Bucket.AddToBag(item.Seed, item.Amount);
            item.RestockTimer = RestockTime;
            EventLog.Add($"Bought {item.Title}: {item.Amount:0} seed into the bag.");
        }
        return true;
    }

    // Store space to scene space: scale up to fill the box, then turn by Yaw.
    public Vector3 ToScene(Vector3 storePos)
    {
        return Vector3.Transform(storePos * Scale, Matrix4x4.CreateRotationY(Yaw));
    }

    // The point at this angle around the room (0 = the front, +Z), this far from the middle, at height y.
    public static Vector3 OnCircle(float angle, float radius, float y)
    {
        return new Vector3(MathF.Sin(angle) * radius, y, MathF.Cos(angle) * radius);
    }

    // Jars on the top two shelves, sacks on the bottom one, spaced evenly along the curve,
    // cycling through the bagged seeds. The Muck Scraper stands on the floor by the corn barrel,
    // the Almoner's Tray between the barrel and the shelves, and the Turret Feeder past the other end of the shelves.
    private void StockShelves()
    {
        SeedType[] seeds = Seeds.Bags;
        float arcLength = (ShelfEnd - ShelfStart) * ShelfRadius;
        int n = 0;
        for (int row = 1; row < ShelfHeights.Length; row++)
        {
            AddRow(ShelfHeights[row], (int)(arcLength / 0.32f), ItemKind.Jar, seeds, ref n);
        }
        AddRow(ShelfHeights[0], (int)(arcLength / 0.45f), ItemKind.Sack, seeds, ref n);
        Stock.Add(new ShelfItem(SeedType.Wild, OnCircle(55f * MathF.PI / 180f, 1.3f, 0f), ItemKind.Scraper));
        Stock.Add(new ShelfItem(SeedType.Wild, OnCircle(88f * MathF.PI / 180f, 1.15f, 0f), ItemKind.Feeder, FeederStyle.Tray));
        Stock.Add(new ShelfItem(SeedType.Wild, OnCircle(262f * MathF.PI / 180f, 1.28f, 0f), ItemKind.Feeder, FeederStyle.Tube));
    }

    private void AddRow(float height, int count, ItemKind kind, SeedType[] seeds, ref int n)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = ShelfStart + (ShelfEnd - ShelfStart) * (i + 0.5f) / count;
            Stock.Add(new ShelfItem(seeds[n++ % seeds.Length], OnCircle(angle, ShelfRadius, height), kind));
        }
    }
}

public enum ItemKind { Jar, Sack, Scraper, Feeder }

// One thing for sale: a jar or sack of seed, the Muck Scraper (a tool, bought once and kept), or a feeder.
// A sack holds five jars' worth of seed for the price of four.
public class ShelfItem
{
    public const float JarAmount = 10f;
    public const float SackAmount = 50f;
    public const int SackPriceInJars = 4;
    public const int ScraperPrice = 5;

    public SeedType Seed;       // only used by jars and sacks
    public FeederStyle Feeder;  // only used by feeders
    public Vector3 Position;    // bottom middle of the item, in store space
    public ItemKind Kind;
    public float RestockTimer;  // counts down after it's bought; back on the shelf at 0

    public ShelfItem(SeedType seed, Vector3 position, ItemKind kind, FeederStyle feeder = FeederStyle.Tray)
    {
        Seed = seed;
        Position = position;
        Kind = kind;
        Feeder = feeder;
    }

    public bool IsSack => Kind == ItemKind.Sack;
    public bool IsTool => Kind == ItemKind.Scraper;
    public bool IsFeeder => Kind == ItemKind.Feeder;
    public bool OnShelf => RestockTimer <= 0f;
    public float Amount => IsSack ? SackAmount : JarAmount;
    public string KindName => Kind.ToString();

    public int Price => Kind switch
    {
        ItemKind.Scraper => ScraperPrice,
        ItemKind.Feeder => Feeders.Get(Feeder).Price,
        _ => Seeds.Get(Seed).JarPrice * (IsSack ? SackPriceInJars : 1),
    };

    // For the event log, e.g. "a jar of Millet", "the Muck Scraper" or "the Turret Feeder"
    public string Title => Kind switch
    {
        ItemKind.Scraper => "the Muck Scraper",
        ItemKind.Feeder => $"the {Feeders.Get(Feeder).Name}",
        _ => $"a {KindName.ToLower()} of {Seeds.Get(Seed).Name}",
    };

    // For clicking: a sphere around the middle of the item, in store space
    public Vector3 Center => Position + new Vector3(0, CenterHeight, 0);
    private float CenterHeight => Kind switch
    {
        ItemKind.Sack => 0.17f,
        ItemKind.Scraper => 0.3f,
        ItemKind.Feeder => Feeder == FeederStyle.Tube ? 0.22f : 0.05f,
        _ => 0.12f,
    };
    public float PickRadius => Kind switch
    {
        ItemKind.Sack => 0.18f,
        ItemKind.Scraper => 0.2f,
        ItemKind.Feeder => Feeder == FeederStyle.Tube ? 0.22f : 0.17f,
        _ => 0.13f,
    };
}
