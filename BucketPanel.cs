using System.Numerics;
using Raylib_cs;

// The seed bucket screen in the left side column. Top to bottom: seed bags (hold to pour),
// Stir and Empty, the bucket, then the hovered bag's label and which birds the current mix would attract.
// Each bag holds a limited supply of seed. Pouring uses it up; buying at the seed store adds more.
public class BucketPanel
{
    public SeedBucket Bucket { get; } = new();

    // LAYOUT
    private const int Pad = 10;
    private const int HeaderSize = 12;
    private const int InnerWidth = 200;
    private const int BagHeight = 30, BagGap = 6;
    private const int ButtonHeight = 24;
    private const int BucketHeight = 100;
    private const int InfoHeight = 170;
    private const int SectionGap = 12;
    private static readonly int BagsHeight = Seeds.Bags.Length * (BagHeight + BagGap) - BagGap;
    public const int PanelWidth = Pad * 2 + InnerWidth;
    public static readonly int PanelHeight = Pad * 3 + HeaderSize + BagsHeight + ButtonHeight
                                             + BucketHeight + InfoHeight + SectionGap * 3;

    private const float PourRate = 25f;    // seed per second while a bag is held

    private Rectangle panelRect, bucketRect, infoRect, stirRect, emptyRect;
    private readonly Rectangle[] bagRects = new Rectangle[Seeds.Bags.Length];

    private SeedType? hoveredSeed;
    private SeedType? pouringSeed;    // the bag the mouse was pressed on, while it's held

    // How much seed is left in each bag
    public Dictionary<SeedType, float> BagSupply { get; } = new();
    private bool warnedEmptyBag = false;

    // The speckles and the ATTRACTS list only change when the bucket does, so they're worked out once per change.
    private RenderTexture2D speckleTexture;
    private int speckleVersion = -1;
    private List<KeyValuePair<BirdType, float>> shares = new();
    private int sharesVersion = -1;
    private bool sharesFrostcrestSeen;

    public BucketPanel()
    {
        foreach (SeedType seed in Seeds.Bags)
        {
            BagSupply[seed] = Seeds.Get(seed).StartingSupply;
        }
    }

    public void AddToBag(SeedType seed, float amount)
    {
        BagSupply[seed] += amount;
    }

    // ---------------- UPDATE (no drawing) ----------------

    public void Update(float dt, bool mouseBlockedByImGui)
    {
        hoveredSeed = null;
        Layout();
        Vector2 mouse = Raylib.GetMousePosition();
        bool pressed = Raylib.IsMouseButtonPressed(MouseButton.Left) && !mouseBlockedByImGui;

        if (!mouseBlockedByImGui)
        {
            for (int i = 0; i < bagRects.Length; i++)
            {
                if (Raylib.CheckCollisionPointRec(mouse, bagRects[i])) hoveredSeed = Seeds.Bags[i];
            }
        }

        if (pressed && hoveredSeed != null) pouringSeed = hoveredSeed;
        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            pouringSeed = null;
            warnedEmptyBag = false;
        }

        // Pours only while the mouse stays on the bag it pressed
        if (pouringSeed != null && pouringSeed == hoveredSeed)
        {
            PourFromBag(pouringSeed.Value, PourRate * dt);
        }

        if (pressed && Raylib.CheckCollisionPointRec(mouse, stirRect)) Bucket.Stir();
        if (pressed && Raylib.CheckCollisionPointRec(mouse, emptyRect)) Bucket.Empty();
    }

    // Pours from the bag's supply. When the bag runs dry, it says so once per press.
    private void PourFromBag(SeedType seed, float amount)
    {
        float poured = Bucket.Pour(seed, MathF.Min(amount, BagSupply[seed]));
        BagSupply[seed] = MathF.Max(0f, BagSupply[seed] - poured);

        if (BagSupply[seed] <= 0.01f && !warnedEmptyBag)
        {
            EventLog.Add($"The {Seeds.Get(seed).Name} bag is empty. {SeedStore.Name} sells more.");
            warnedEmptyBag = true;
        }
    }

    // The side column decides where the panel goes; everything inside is stacked down from its top.
    private void Layout()
    {
        panelRect = GameEngine.Instance.Sides.BucketSlot;

        float x = panelRect.X + Pad;
        float y = panelRect.Y + Pad * 2 + HeaderSize;

        for (int i = 0; i < bagRects.Length; i++)
        {
            bagRects[i] = new Rectangle(x, y + i * (BagHeight + BagGap), InnerWidth, BagHeight);
        }
        y += BagsHeight + SectionGap;

        float buttonWidth = (InnerWidth - BagGap) / 2f;
        stirRect = new Rectangle(x, y, buttonWidth, ButtonHeight);
        emptyRect = new Rectangle(x + buttonWidth + BagGap, y, buttonWidth, ButtonHeight);
        y += ButtonHeight + SectionGap;

        bucketRect = new Rectangle(x, y, InnerWidth, BucketHeight);
        y += BucketHeight + SectionGap;

        infoRect = new Rectangle(x, y, InnerWidth, InfoHeight);
    }

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(Color accent)
    {
        Layout();

        UiDraw.Panel(panelRect, accent);
        UiText.Draw("SEED BUCKET", (int)panelRect.X + Pad, (int)panelRect.Y + Pad, HeaderSize, accent);

        DrawBags(accent);
        UiDraw.Button(stirRect, "Stir", accent);
        UiDraw.Button(emptyRect, "Empty", accent);
        DrawBucket(accent);
        DrawInfo(accent);
    }

    private void DrawBags(Color accent)
    {
        for (int i = 0; i < bagRects.Length; i++)
        {
            SeedType seed = Seeds.Bags[i];
            SeedInfo info = Seeds.Get(seed);
            Rectangle r = bagRects[i];

            Color fill = Palette.PanelDark;
            if (pouringSeed == seed && hoveredSeed == seed) fill = Raylib.Fade(accent, 0.35f);
            else if (hoveredSeed == seed) fill = Raylib.Fade(accent, 0.25f);

            // An empty bag's name goes dim; the amount left sits on the right
            float supply = BagSupply[seed];
            Color nameColor = supply > 0.01f ? Palette.Text : Palette.DimText;
            string left = $"{supply:0}";

            Raylib.DrawRectangleRec(r, fill);
            Raylib.DrawRectangle((int)r.X, (int)r.Y, 6, (int)r.Height, info.Color);
            UiText.Draw(info.Name, (int)r.X + 14, (int)r.Y + 5, 20, nameColor);
            UiText.Draw(left, (int)(r.X + r.Width - 8 - UiText.Measure(left, 20)), (int)r.Y + 5, 20, Palette.DimText);
        }
    }

    // Stirred seed fills the bottom as speckles; unstirred layers stack above it as bands.
    private void DrawBucket(Color accent)
    {
        Rectangle r = bucketRect;
        Color wall = Raylib.Fade(accent, 0.6f);

        // Walls and floor, open at the top, with a small rim
        Raylib.DrawRectangle((int)r.X, (int)r.Y, 2, (int)r.Height, wall);
        Raylib.DrawRectangle((int)(r.X + r.Width - 2), (int)r.Y, 2, (int)r.Height, wall);
        Raylib.DrawRectangle((int)r.X, (int)(r.Y + r.Height - 2), (int)r.Width, 2, wall);
        Raylib.DrawRectangle((int)r.X - 3, (int)r.Y, 8, 2, wall);
        Raylib.DrawRectangle((int)(r.X + r.Width - 5), (int)r.Y, 8, 2, wall);

        float innerX = r.X + 2, innerWidth = r.Width - 4;
        float innerHeight = r.Height - 2;
        float floorY = r.Y + innerHeight;
        float pxPerSeed = innerHeight / SeedBucket.Capacity;

        float mixedHeight = Bucket.MixedTotal * pxPerSeed;
        if (mixedHeight >= 1f)
        {
            DrawSpeckles(new Rectangle(innerX, floorY - mixedHeight, innerWidth, mixedHeight));
        }

        // Stack the layers using a running float so there are no gaps from rounding
        float fillTop = floorY - mixedHeight;
        foreach (SeedBucket.Layer layer in Bucket.Layers)
        {
            float bandHeight = layer.Amount * pxPerSeed;
            int y0 = (int)MathF.Round(fillTop - bandHeight);
            int y1 = (int)MathF.Round(fillTop);
            Raylib.DrawRectangle((int)innerX, y0, (int)innerWidth, y1 - y0, Seeds.Get(layer.Seed).Color);
            fillTop -= bandHeight;
        }

        string fullness = Bucket.IsFull ? "FULL" : $"{Bucket.Total / SeedBucket.Capacity * 100f:0}%";
        int textWidth = UiText.Measure(fullness, HeaderSize);
        UiText.Draw(fullness, (int)(r.X + r.Width - textWidth), (int)panelRect.Y + Pad, HeaderSize, Palette.DimText);
    }

    // The speckles are drawn into a texture as tall as the bucket, sitting on its floor,
    // and only redrawn when the bucket changes. Every other frame just shows the texture.
    private void DrawSpeckles(Rectangle area)
    {
        if (speckleTexture.Id == 0)
        {
            speckleTexture = Raylib.LoadRenderTexture((int)area.Width, BucketHeight);
        }
        int textureHeight = speckleTexture.Texture.Height;

        if (speckleVersion != Bucket.Version)
        {
            Raylib.BeginTextureMode(speckleTexture);
                Raylib.ClearBackground(Color.Blank);
                RenderSpeckles(new Rectangle(0, textureHeight - area.Height, area.Width, area.Height));
            Raylib.EndTextureMode();
            speckleVersion = Bucket.Version;
        }

        // Render textures are stored upside down, so the source height is negative.
        Rectangle source = new Rectangle(0, 0, speckleTexture.Texture.Width, -textureHeight);
        Vector2 topLeft = new Vector2(area.X, area.Y + area.Height - textureHeight);
        Raylib.DrawTextureRec(speckleTexture.Texture, source, topLeft, Color.White);
    }

    // A dark base with dots in each seed's color, in proportion to how much of it is mixed in.
    private void RenderSpeckles(Rectangle area)
    {
        Raylib.DrawRectangleRec(area, new Color(40, 32, 24, 255));

        float mixedTotal = Bucket.MixedTotal;
        int dots = (int)(area.Width * area.Height / 6f);
        float floorY = area.Y + area.Height;

        for (int i = 0; i < dots; i++)
        {
            // Fixed random numbers per dot, so the speckles stay put from frame to frame
            float px = Rng.Fixed(i, 0);
            float py = Rng.Fixed(i, 1);
            SeedType seed = PickMixedSeed(Rng.Fixed(i, 2) * mixedTotal);

            int x = (int)(area.X + px * (area.Width - 2));
            int y = (int)(floorY - 2 - py * (area.Height - 2));
            Raylib.DrawRectangle(x, y, 2, 2, Seeds.Get(seed).Color);
        }
    }

    // Walks through the mixed amounts until it passes "value", so bigger amounts get picked more often.
    private SeedType PickMixedSeed(float value)
    {
        SeedType last = Seeds.All[0];
        foreach ((SeedType seed, float amount) in Bucket.Mixed)
        {
            last = seed;
            value -= amount;
            if (value <= 0f) return seed;
        }
        return last;
    }

    private void DrawInfo(Color accent)
    {
        int x = (int)infoRect.X;
        int y = (int)infoRect.Y;
        int width = (int)infoRect.Width;

        UiText.Draw("BAG LABEL", x, y, HeaderSize, accent);
        if (hoveredSeed != null)
        {
            SeedInfo info = Seeds.Get(hoveredSeed.Value);
            string price = $"{info.JarPrice} coin{(info.JarPrice == 1 ? "" : "s")} a jar";
            UiText.Draw(price, x + width - UiText.Measure(price, HeaderSize), y, HeaderSize, Palette.DimText);
            UiText.Draw(info.ProductName, x, y + 16, HeaderSize, info.Color);
            DrawWrapped(info.Label, x, y + 30, width, HeaderSize, Palette.Text);
        }
        else
        {
            UiText.Draw("Hover a bag to read it.", x, y + 16, HeaderSize, Palette.DimText);
        }

        y += 78;
        UiText.Draw("ATTRACTS", x, y, HeaderSize, accent);
        y += 16;

        if (sharesVersion != Bucket.Version || sharesFrostcrestSeen != Seeds.FrostcrestSeen)
        {
            shares = Bucket.PredictBirds()
                .Where(pair => pair.Value >= 0.01f)
                .OrderByDescending(pair => pair.Value)
                .ToList();
            sharesVersion = Bucket.Version;
            sharesFrostcrestSeen = Seeds.FrostcrestSeen;
        }

        if (shares.Count == 0)
        {
            UiText.Draw("Nothing yet.", x, y, HeaderSize, Palette.DimText);
            return;
        }

        foreach ((BirdType bird, float share) in shares)
        {
            string percent = $"{share * 100f:0}%";
            BirdInfo birdInfo = Birds.Get(bird);
            UiText.Draw(birdInfo.Name, x, y, 20, birdInfo.Color);
            UiText.Draw(percent, x + width - UiText.Measure(percent, 20), y, 20, Palette.DimText);
            y += 20;
        }
    }

    public void Unload()
    {
        if (speckleTexture.Id != 0) Raylib.UnloadRenderTexture(speckleTexture);
    }

    // ---------------- HELPERS ----------------

    // Raylib doesn't wrap text, so this breaks it into lines that fit maxWidth.
    private static void DrawWrapped(string text, int x, int y, int maxWidth, int size, Color color)
    {
        string line = "";
        foreach (string word in text.Split(' '))
        {
            string attempt = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && UiText.Measure(attempt, size) > maxWidth)
            {
                UiText.Draw(line, x, y, size, color);
                y += size + 4;
                line = word;
            }
            else
            {
                line = attempt;
            }
        }
        if (line.Length > 0) UiText.Draw(line, x, y, size, color);
    }
}
