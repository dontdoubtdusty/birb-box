using System.Numerics;
using Raylib_cs;

// The Gilded Granary: a little self-serve general store the Birb Cube can project in place of the birds
// (see BoxMode). The people who built it studied old shops closely and got most of it right,
// but it's still a ship's store underneath.
//
// This class is the store's state: how it's turned, what's on the shelves, which item the mouse is on,
// the shopkeeper, and buying. StoreRenderer draws it; PriceTag shows the selected item's price; Ledger swaps feathers.
//
// Store space: (0, 0, 0) is the middle of the box's floor, before the store is turned by Yaw.
// The shop was laid out small, so store space is scaled up by Scale to fill the box.
//
// The room is round: a horseshoe wall with its opening at the front (+Z), shelves curving along the back,
// and the counter in the middle facing the opening. Angles around the room are measured from the front (+Z)
// toward +X, in radians (see OnCircle).
//
// SHELVES: each species whose feather you hold gets a section of the shelf (four to a page, the first on the left)
// stocked with the seeds it eats, priced in its own feathers. A rare species' lure seed has no section until you
// hold that species' feather, so it sits on the crate by the door and takes feathers of any kind until then.
public class SeedStore
{
    public const string Name = "The Gilded Granary";

    public const float Scale = 2.4f;     // scene units per store unit
    public float Yaw = 0f;               // how far the store is turned, in radians (always the same as the birds' turn, see GameEngine)

    // ROOM LAYOUT (store units)
    public const float WallRadius = 1.75f;
    public const float WallOpening = MathF.PI / 4f;        // the wall leaves this much open either side of the front

    // SHELVES: the top of each board, and the arc they curve along at the back of the room
    public static readonly float[] ShelfHeights = { 0.5f, 0.95f, 1.4f };
    public const float ShelfRadius = 1.55f;                // middle of the shelf boards, just inside the wall
    public const float ShelfStart = 95f * MathF.PI / 180f;
    public const float ShelfEnd = 245f * MathF.PI / 180f;

    // SECTIONS: the shelf arc is cut into equal sections, one per species; slot 0 is on the left (the ShelfEnd side).
    public const int SectionsPerPage = 4;
    private const float SectionGap = 2f * MathF.PI / 180f;    // empty board either side of a section
    private const int JarsPerRow = 3;                          // jars on each of the top two boards of a section
    private const int SacksPerSection = 2;                     // sacks on the bottom board of a section
    private const float MinShelfTaste = 0.2f;                  // a species' shelf sells seed it likes at least this much (the same cut as a favorite seed)

    public static float SectionWidth => (ShelfEnd - ShelfStart) / SectionsPerPage;

    // The angle at the middle of a section's shelf, for slot 0 (left) to 3 (right).
    public static float SlotAngle(int slot) => ShelfEnd - SectionWidth * (slot + 0.5f);

    // What's for sale: the jars and sacks on this page, the lure seed on the crate, and the two feeders.
    public List<ShelfItem> Stock { get; } = new();

    // Things you click that aren't for sale: the ledger on the counter and the shelf's page arrows.
    public List<ShelfItem> Controls { get; } = new();
    public ShelfItem LedgerBook { get; }

    // The species with a section on this page, left to right.
    public List<Species> PageSpecies { get; } = new();
    public int Page { get; private set; }
    public int PageCount { get; private set; } = 1;
    private List<Species> shelfSpecies = new();            // every species with a section, over all pages

    // The crate by the door holds lure seed for rare species not yet collected.
    private const float CrateAngle = 305f * MathF.PI / 180f;
    private const float CrateRadius = 1.25f;
    private const float CrateTop = 0.36f;
    private const int MaxLureJars = 2;

    // COUNTER AND KEEPER: the counter faces the opening; the keeper stands behind it on a stool
    public static readonly Vector3 CounterSpot = new Vector3(0f, 0f, 0.2f);
    public const float CounterTop = 0.76f;
    public static readonly Vector3 KeeperSpot = new Vector3(0f, 0f, -0.3f);
    public Shopkeeper Keeper { get; } = new Shopkeeper();

    // FEEDERS: the two feeders stand on the floor near the shelf ends, drawn this many scene units per model unit
    // (the bird world draws them at Feeders.WorldScale, the shop's other things at Scale).
    public const float FeederShopScale = 2.2f;

    // SHOPPING
    public ShelfItem? HoveredItem { get; private set; }    // the jar, sack or control under the mouse
    public ShelfItem? SelectedItem;                        // the one whose price tag is showing
    public const float RestockTime = 60f;                  // seconds before a bought item is replaced

    // Restock timers of bought items that aren't on a visible shelf (another page, or the species not shown yet), by item key.
    // They are not saved: a reloaded game finds everything back on the shelf.
    private readonly Dictionary<string, float> waiting = new();
    private int seenPurseVersion = -1, seenBirdsVersion = -1;

    // The air recycler's blades turn slowly. It's ship machinery, so it keeps going when the birds are paused.
    public float FanAngle { get; private set; }
    private const float FanSpeed = 1.2f;    // radians per second

    // THE LAMP under the air recycler: the keeper switches it on as the bird world gets dark, like a shop lit up at night.
    // It follows the world's clock even while the box shows the birds, so it's already on when you walk in after dark.
    public float LampLevel { get; private set; }     // how bright it is right now: 0 off, 1 fully on
    private const float LampOnDarkness = 0.35f;      // switched on once the world is this dark (DayLight.Darkness)
    private const float LampOffDarkness = 0.2f;      // and off again once it's lighter than this, so it never flickers at the line
    private const float LampWarmUp = 0.8f;           // seconds for the filament to warm up to full
    private const float LampCoolDown = 0.4f;         // seconds to go dark
    private const float LampFlickerTime = 0.35f;     // an old bulb stutters this long as it comes on
    private bool lampOn;
    private float lampFlicker;                       // seconds of stutter left
    private float lampSteady;                        // the brightness without the stutter

    public bool VisitedBefore { get; set; } = false;    // the welcome line is only said on the first visit

    public SeedStore()
    {
        LedgerBook = new ShelfItem(SeedType.Wild, CounterSpot + new Vector3(0f, CounterTop, 0.08f), ItemKind.Ledger) { Key = "ledger" };
    }

    // showing: the box is showing the store, so a shift change gets a line in the log.
    public void Update(float dt, bool showing)
    {
        FanAngle += FanSpeed * dt;
        UpdateLamp(dt, WorldClock.Instance.Light.Darkness);

        // New shelves open when a species is collected for the first time (or a species changes)
        FeatherPurse purse = GameEngine.Instance.Feathers;
        if (purse.Version != seenPurseVersion || Birds.Version != seenBirdsVersion) RebuildShelves();

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
        foreach (string key in waiting.Keys.ToList())
        {
            waiting[key] -= dt;
            if (waiting[key] <= 0f) waiting.Remove(key);
        }
    }

    // Switches the lamp by how dark the world is and eases its brightness, with a short stutter when it comes on.
    private void UpdateLamp(float dt, float darkness)
    {
        if (!lampOn && darkness >= LampOnDarkness)
        {
            lampOn = true;
            lampFlicker = LampFlickerTime;
        }
        else if (lampOn && darkness <= LampOffDarkness)
        {
            lampOn = false;
        }

        float target = lampOn ? 1f : 0f;
        float rate = lampOn ? 1f / LampWarmUp : 1f / LampCoolDown;
        lampSteady += Math.Clamp(target - lampSteady, -rate * dt, rate * dt);
        LampLevel = lampSteady;

        if (lampFlicker > 0f)
        {
            lampFlicker -= dt;
            float stutter = MathF.Sin(lampFlicker * 90f) * MathF.Sin(lampFlicker * 37f);
            if (lampFlicker > 0f && stutter > 0.2f) LampLevel = lampSteady * 0.25f;
        }
    }

    // Called when the box finishes switching to the store, and back.
    public void OnEnter()
    {
        SelectedItem = null;
        if (!VisitedBefore)
        {
            EventLog.Add($"Welcome to {Name}, purveyors of fine seed since the year 1312. Each bird's shelf opens once you hold one of its feathers. Click a jar or sack to see its price.");
            VisitedBefore = true;
        }
        EventLog.Add($"{Keeper.Name} is minding the counter today.");
    }

    public void OnLeave()
    {
        SelectedItem = null;
        HoveredItem = null;
    }

    // Finds the item under the mouse, only things still on the shelf and arrows that lead somewhere.
    // active: the store is showing and the mouse isn't over something else.
    public void UpdateHover(Ray sceneRay, bool active)
    {
        HoveredItem = null;
        if (!active) return;

        float bestDistance = float.MaxValue;
        foreach (ShelfItem item in Stock.Concat(Controls))
        {
            if (!item.OnShelf || !IsEnabled(item)) continue;
            RayCollision hit = Raylib.GetRayCollisionSphere(sceneRay, ToScene(item.Center), item.PickRadius * Scale);
            if (hit.Hit && hit.Distance < bestDistance)
            {
                HoveredItem = item;
                bestDistance = hit.Distance;
            }
        }
    }

    // A page arrow does nothing past the first or last page; everything else always works.
    public bool IsEnabled(ShelfItem item) => item.Kind switch
    {
        ItemKind.PageBack => Page > 0,
        ItemKind.PageNext => Page < PageCount - 1,
        _ => true,
    };

    // Turns the shelf one page (-1 back toward the first species, +1 on to later ones).
    public void TurnPage(int direction)
    {
        int page = Math.Clamp(Page + direction, 0, PageCount - 1);
        if (page == Page) return;
        Page = page;
        SelectedItem = null;
        RebuildShelves();
    }

    // Pays for the item. Seed goes into that seed's bag on the bucket panel;
    // a feeder waits until the box shows the birds, then follows the mouse to be set down (FeederPlacer).
    // Returns false (and says why) if there aren't enough feathers. The shelf's species pays; the lure seed on the crate takes any kind.
    public bool TryBuy(ShelfItem item)
    {
        GameEngine engine = GameEngine.Instance;
        if (item.IsControl) return false;

        bool paid = item.PaidWith != null
            ? engine.Feathers.TrySpend(item.PaidWith, item.Price)
            : engine.Feathers.TrySpendAny(item.Price);
        if (!paid)
        {
            EventLog.Add($"Not enough feathers for {item.Title}. It costs {item.PriceText}.");
            return false;
        }

        if (SelectedItem == item) SelectedItem = null;
        Keeper.Celebrate();

        if (item.IsFeeder)
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

    // ---------------- STOCKING THE SHELVES ----------------

    // The seeds a species' section sells, best liked first. Wild seed is never sold.
    public static List<SeedType> SellableSeeds(Species species)
    {
        return Seeds.Bags
            .Where(seed => species.TasteFor(seed) >= MinShelfTaste)
            .OrderByDescending(seed => species.TasteFor(seed))
            .ToList();
    }

    // Works out who has a section and puts the right things on the shelves for the current page. Bought items keep their
    // restock timers across the change. Called when a species is first collected, a species changes, or the page turns.
    private void RebuildShelves()
    {
        FeatherPurse purse = GameEngine.Instance.Feathers;
        seenPurseVersion = purse.Version;
        seenBirdsVersion = Birds.Version;

        // Remember which item was selected and how long bought ones have left
        string? selectedKey = SelectedItem?.Key;
        foreach (ShelfItem old in Stock)
        {
            if (old.RestockTimer > 0f) waiting[old.Key] = old.RestockTimer;
        }
        Stock.Clear();

        shelfSpecies = purse.Collected().Where(species => SellableSeeds(species).Count > 0).ToList();
        PageCount = Math.Max(1, (shelfSpecies.Count + SectionsPerPage - 1) / SectionsPerPage);
        Page = Math.Clamp(Page, 0, PageCount - 1);

        PageSpecies.Clear();
        PageSpecies.AddRange(shelfSpecies.Skip(Page * SectionsPerPage).Take(SectionsPerPage));
        for (int slot = 0; slot < PageSpecies.Count; slot++) StockSection(slot, PageSpecies[slot]);

        StockLureCrate(purse);
        StockFeeders();

        foreach (ShelfItem item in Stock)
        {
            if (waiting.Remove(item.Key, out float left)) item.RestockTimer = left;
        }
        SelectedItem = selectedKey == null ? null : Stock.Find(item => item.Key == selectedKey);

        Controls.Clear();
        Controls.Add(LedgerBook);
        if (PageCount > 1)
        {
            float rightEnd = ShelfStart - 4f * MathF.PI / 180f, leftEnd = ShelfEnd + 4f * MathF.PI / 180f;
            Controls.Add(new ShelfItem(SeedType.Wild, OnCircle(leftEnd, PageArrowRadius, ShelfHeights[1]), ItemKind.PageBack) { Key = "back", Angle = leftEnd });
            Controls.Add(new ShelfItem(SeedType.Wild, OnCircle(rightEnd, PageArrowRadius, ShelfHeights[1]), ItemKind.PageNext) { Key = "next", Angle = rightEnd });
        }
    }

    public const float PageArrowRadius = 1.64f;    // the arrows sit on the wall just past the ends of the shelf

    // One species' section: jars on the top two boards and sacks on the bottom, cycling through the seeds it eats.
    private void StockSection(int slot, Species species)
    {
        List<SeedType> seeds = SellableSeeds(species);
        int n = 0;
        for (int row = 1; row < ShelfHeights.Length; row++)
        {
            AddRow(slot, species, seeds, row, JarsPerRow, ItemKind.Jar, ref n);
        }
        AddRow(slot, species, seeds, 0, SacksPerSection, ItemKind.Sack, ref n);
    }

    private void AddRow(int slot, Species species, List<SeedType> seeds, int row, int count, ItemKind kind, ref int n)
    {
        float usable = SectionWidth - 2f * SectionGap;
        for (int i = 0; i < count; i++)
        {
            float angle = SlotAngle(slot) + usable * (0.5f - (i + 0.5f) / count);
            Vector3 position = OnCircle(angle, ShelfRadius, ShelfHeights[row]);
            Stock.Add(new ShelfItem(seeds[n++ % seeds.Count], position, kind) { PaidWith = species, Key = $"{species.Key}|{row}|{i}" });
        }
    }

    // A jar of each lure seed no shelf sells yet, standing on the crate. Any feather pays for it, so a rare bird
    // can be called in before it has left a feather of its own.
    private void StockLureCrate(FeatherPurse purse)
    {
        HashSet<SeedType> onShelves = shelfSpecies.SelectMany(SellableSeeds).ToHashSet();
        List<SeedType> lures = Birds.All
            .Where(species => !species.Common && species.Lure != null && !purse.Has(species))
            .Select(species => species.Lure!.Value)
            .Where(seed => seed != SeedType.Wild && !onShelves.Contains(seed))
            .Distinct()
            .Take(MaxLureJars)
            .ToList();

        Vector3 along = new Vector3(MathF.Cos(CrateAngle), 0f, -MathF.Sin(CrateAngle));    // the crate's long side
        for (int i = 0; i < lures.Count; i++)
        {
            float offset = lures.Count == 1 ? 0f : (i == 0 ? -0.085f : 0.085f);
            Vector3 position = OnCircle(CrateAngle, CrateRadius, CrateTop) + along * offset;
            Stock.Add(new ShelfItem(lures[i], position, ItemKind.Jar) { Key = $"lure|{lures[i]}" });
        }
    }

    // The two feeders stand on the floor at the ends of the shelf, paid in the feathers of the species that likes each best.
    private void StockFeeders()
    {
        Stock.Add(MakeFeeder(FeederStyle.Tray, OnCircle(94f * MathF.PI / 180f, 1.08f, 0f)));
        Stock.Add(MakeFeeder(FeederStyle.Tube, OnCircle(262f * MathF.PI / 180f, 1.28f, 0f)));
    }

    private static ShelfItem MakeFeeder(FeederStyle style, Vector3 position)
    {
        FeederInfo info = Feeders.Get(style);
        return new ShelfItem(SeedType.Wild, position, ItemKind.Feeder, style) { PaidWith = Birds.Find(info.PaidIn), Key = $"feeder|{style}" };
    }
}

public enum ItemKind { Jar, Sack, Feeder, Ledger, PageBack, PageNext }

// One thing in the store you can click: a jar or sack of seed, a feeder, or a control (the ledger, a page arrow).
// A sack holds five jars' worth of seed for the price of four.
public class ShelfItem
{
    public const float JarAmount = 10f;
    public const float SackAmount = 50f;
    public const int SackPriceInJars = 4;

    public SeedType Seed;       // only used by jars and sacks
    public FeederStyle Feeder;  // only used by feeders
    public Vector3 Position;    // bottom middle of the item, in store space
    public ItemKind Kind;
    public float RestockTimer;  // counts down after it's bought; back on the shelf at 0
    public Species? PaidWith;   // the species whose feathers pay for it; null means feathers of any kind
    public string Key = "";     // the same item gets the same key whenever the shelves are rebuilt
    public float Angle;         // for page arrows: where they sit around the wall

    public ShelfItem(SeedType seed, Vector3 position, ItemKind kind, FeederStyle feeder = FeederStyle.Tray)
    {
        Seed = seed;
        Position = position;
        Kind = kind;
        Feeder = feeder;
    }

    public bool IsSack => Kind == ItemKind.Sack;
    public bool IsFeeder => Kind == ItemKind.Feeder;
    public bool IsControl => Kind >= ItemKind.Ledger;
    public bool OnShelf => RestockTimer <= 0f;
    public float Amount => IsSack ? SackAmount : JarAmount;
    public string KindName => Kind.ToString();

    public int Price => Kind switch
    {
        ItemKind.Feeder => Feeders.Get(Feeder).Price,
        ItemKind.Jar or ItemKind.Sack => Seeds.Get(Seed).JarPrice * (IsSack ? SackPriceInJars : 1),
        _ => 0,
    };

    // "2 Dusk Down", or "5 feathers (any kind)" for the lure seed.
    public string PriceText => PaidWith != null
        ? $"{Price} {PaidWith.FeatherName}"
        : $"{Price} feather{(Price == 1 ? "" : "s")} (any kind)";

    // For the event log, e.g. "a jar of Millet" or "the Turret Feeder"
    public string Title => Kind switch
    {
        ItemKind.Feeder => $"the {Feeders.Get(Feeder).Name}",
        ItemKind.Ledger => "the ledger",
        ItemKind.PageBack or ItemKind.PageNext => "the page arrow",
        _ => $"a {KindName.ToLower()} of {Seeds.Get(Seed).Name}",
    };

    // For clicking: a sphere around the middle of the item, in store space
    public Vector3 Center => Position + new Vector3(0, CenterHeight, 0);
    private float CenterHeight => Kind switch
    {
        ItemKind.Sack => 0.17f,
        ItemKind.Feeder => (Feeder == FeederStyle.Tube ? 0.5f : 0.2f) * ModelToStore,
        ItemKind.Ledger => 0.03f,
        ItemKind.PageBack or ItemKind.PageNext => 0f,
        _ => 0.12f,
    };
    public float PickRadius => Kind switch
    {
        ItemKind.Sack => 0.18f,
        ItemKind.Feeder => (Feeder == FeederStyle.Tube ? 0.26f : 0.4f) * ModelToStore,
        ItemKind.Ledger => 0.16f,
        ItemKind.PageBack or ItemKind.PageNext => 0.1f,
        _ => 0.13f,
    };

    // A feeder's model is in world units; this turns its sizes into store units (see SeedStore.FeederShopScale).
    private static float ModelToStore => SeedStore.FeederShopScale / SeedStore.Scale;
}
