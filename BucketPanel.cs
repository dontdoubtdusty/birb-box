using System.Numerics;
using Raylib_cs;

// The seed bucket screen in the left side column. Top to bottom: seed bags (click for a scoop,
// Shift-click to fill, right-click to put a scoop back), Stir and Empty, the bucket, then the hovered bag's label and which birds the current mix would attract.
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

    private const float ScoopSize = 10f;    // seed per click on a bag

    private Rectangle panelRect, bucketRect, infoRect, stirRect, emptyRect;
    private readonly Rectangle[] bagRects = new Rectangle[Seeds.Bags.Length];

    private SeedType? hoveredSeed;

    // How much seed is left in each bag
    public Dictionary<SeedType, float> BagSupply { get; } = new();

    // The bucket's contents and the ATTRACTS list only change when the bucket does, so they're worked out once per change.
    private RenderTexture2D contentsTexture;
    private int contentsVersion = -1;
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
        bool leftClick = Raylib.IsMouseButtonPressed(MouseButton.Left) && !mouseBlockedByImGui;
        bool rightClick = Raylib.IsMouseButtonPressed(MouseButton.Right) && !mouseBlockedByImGui;

        if (!mouseBlockedByImGui)
        {
            for (int i = 0; i < bagRects.Length; i++)
            {
                if (Raylib.CheckCollisionPointRec(mouse, bagRects[i])) hoveredSeed = Seeds.Bags[i];
            }
        }

        if (hoveredSeed != null)
        {
            SeedType seed = hoveredSeed.Value;
            bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);

            if (leftClick) PourFromBag(seed, shift ? SeedBucket.Capacity : ScoopSize);
            if (rightClick) ReturnToBag(seed, ScoopSize);
        }

        if (leftClick && Raylib.CheckCollisionPointRec(mouse, stirRect)) Bucket.Stir();
        if (leftClick && Raylib.CheckCollisionPointRec(mouse, emptyRect)) Bucket.Empty();
    }

    // Pours from the bag's supply, as much as the bag has and the bucket has room for.
    private void PourFromBag(SeedType seed, float amount)
    {
        if (BagSupply[seed] <= 0.01f)
        {
            EventLog.Add($"The {Seeds.Get(seed).Name} bag is empty. {SeedStore.Name} sells more.");
            return;
        }

        float poured = Bucket.Pour(seed, MathF.Min(amount, BagSupply[seed]));
        BagSupply[seed] = MathF.Max(0f, BagSupply[seed] - poured);
    }

    // Takes unstirred seed back out of the bucket and returns it to its bag.
    private void ReturnToBag(SeedType seed, float amount)
    {
        float returned = Bucket.TakeBack(seed, amount);
        if (returned > 0f)
        {
            BagSupply[seed] += returned;
        }
        else if (Bucket.AmountOf(seed) > 0f)
        {
            EventLog.Add($"The {Seeds.Get(seed).Name} is stirred in. Stirred seed can't be picked back out.");
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
            if (hoveredSeed == seed) fill = Raylib.Fade(accent, 0.25f);

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

    // ---------------- THE BUCKET ----------------

    // The bucket's outline in screen pixels, worked out from the box it's drawn in: a rim band with an oval opening,
    // sides that narrow toward a rounded bottom, and a handle that loops out on the left to a grip,
    // then crosses the front up to the right side.
    private readonly struct BucketShape
    {
        public const float TopHalf = 44f, BottomHalf = 36f;    // half widths of the body at its top and bottom
        public const float RimHalf = 48f, RimDepth = 6f;       // the rim's oval: half width, and half height (the tilt)
        public const float BandHeight = 9f;
        public const float BottomDepth = 5f;                   // how far the rounded bottom dips below the sides
        public const float GripRadius = 4f;

        public readonly float Cx, RimY, BodyTop, BodyBottom;

        // Shifted right of the box's middle, to make room for the handle on the left
        public BucketShape(Rectangle box)
        {
            Cx = box.X + box.Width / 2f + 10f;
            RimY = box.Y + RimDepth;
            BodyTop = RimY + BandHeight;
            BodyBottom = box.Y + box.Height - 2f - BottomDepth;
        }

        public float Floor => BodyBottom + BottomDepth;      // the lowest point inside
        public float FillTop => RimY + 2f;                   // a full bucket reaches just under the rim
        public Vector2 GripLeft => new Vector2(Cx - TopHalf - 8f, BodyTop + 46f);
        public Vector2 GripRight => new Vector2(Cx - TopHalf + 20f, BodyTop + 46f);

        // Half the inside width at a height: straight under the rim, narrowing down the body, rounding off at the bottom.
        public float HalfWidthAt(float y)
        {
            if (y < RimY) return 0f;
            if (y <= BodyTop) return TopHalf + 2f;
            if (y <= BodyBottom) return TopHalf + (BottomHalf - TopHalf) * (y - BodyTop) / (BodyBottom - BodyTop);
            float t = (y - BodyBottom) / BottomDepth;
            return t >= 1f ? 0f : BottomHalf * MathF.Sqrt(1f - t * t);
        }
    }

    // A see-through hologram bucket: a faint body with scanlines, the seed inside, and a glowing outline
    // (a wide faint stroke under a thin bright one).
    private void DrawBucket(Color accent)
    {
        Rectangle r = bucketRect;
        BucketShape shape = new BucketShape(r);

        for (int y = (int)shape.RimY; y < (int)MathF.Ceiling(shape.Floor); y++)
        {
            float halfWidth = shape.HalfWidthAt(y + 0.5f);
            if (halfWidth <= 0f) continue;
            float alpha = y % 3 == 0 ? 0.12f : 0.06f;
            Raylib.DrawRectangle((int)(shape.Cx - halfWidth), y, (int)MathF.Ceiling(halfWidth * 2f), 1, Raylib.Fade(accent, alpha));
        }

        if (Bucket.Total > 0.01f) DrawContents(r);

        Vector2 gripLeft = shape.GripLeft, gripRight = shape.GripRight;
        Color gripFill = Raylib.Fade(accent, 0.15f);
        Raylib.DrawRectangleRec(new Rectangle(gripLeft.X, gripLeft.Y - BucketShape.GripRadius, gripRight.X - gripLeft.X, BucketShape.GripRadius * 2f), gripFill);
        Raylib.DrawCircleV(gripLeft, BucketShape.GripRadius, gripFill);
        Raylib.DrawCircleV(gripRight, BucketShape.GripRadius, gripFill);

        DrawOutline(shape, 4f, Raylib.Fade(accent, 0.18f));
        DrawOutline(shape, 1.6f, Raylib.Fade(accent, 0.9f));

        string fullness = Bucket.IsFull ? "FULL" : $"{Bucket.Total / SeedBucket.Capacity * 100f:0}%";
        int textWidth = UiText.Measure(fullness, HeaderSize);
        UiText.Draw(fullness, (int)(r.X + r.Width - textWidth), (int)panelRect.Y + Pad, HeaderSize, Palette.DimText);
    }

    // Every line of the bucket, at one thickness and color.
    private static void DrawOutline(BucketShape s, float thick, Color color)
    {
        float r = BucketShape.RimHalf, d = BucketShape.RimDepth;

        // Rim: the oval opening, the band's sides, and the band's lower edge (front half only)
        Arc(s.Cx, s.RimY, r, d, 0f, 360f, thick, color);
        Raylib.DrawLineEx(new Vector2(s.Cx - r, s.RimY), new Vector2(s.Cx - r, s.BodyTop), thick, color);
        Raylib.DrawLineEx(new Vector2(s.Cx + r, s.RimY), new Vector2(s.Cx + r, s.BodyTop), thick, color);
        Arc(s.Cx, s.BodyTop, r, d, 0f, 180f, thick, color);

        // Sides and the rounded bottom
        float sideTop = s.BodyTop + 2f;
        Raylib.DrawLineEx(new Vector2(s.Cx - BucketShape.TopHalf, sideTop), new Vector2(s.Cx - BucketShape.BottomHalf, s.BodyBottom), thick, color);
        Raylib.DrawLineEx(new Vector2(s.Cx + BucketShape.TopHalf, sideTop), new Vector2(s.Cx + BucketShape.BottomHalf, s.BodyBottom), thick, color);
        Arc(s.Cx, s.BodyBottom, BucketShape.BottomHalf, BucketShape.BottomDepth, 0f, 180f, thick, color);

        // Handle: out from the left ear and down to the grip, then from the grip across the front up to the right ear
        Vector2 gripLeft = s.GripLeft, gripRight = s.GripRight;
        float g = BucketShape.GripRadius;
        Bezier(new Vector2(s.Cx - BucketShape.TopHalf + 1f, s.BodyTop + 8f),
               new Vector2(s.Cx - BucketShape.TopHalf - 26f, s.BodyTop + 18f),
               gripLeft - new Vector2(g, 0f), thick, color);
        Bezier(gripRight + new Vector2(g, 0f),
               new Vector2(s.Cx + BucketShape.TopHalf * 0.3f, s.BodyTop + 52f),
               new Vector2(s.Cx + BucketShape.TopHalf - 1f, s.BodyTop + 8f), thick, color);

        // The grip: a short capsule
        Raylib.DrawLineEx(gripLeft - new Vector2(0f, g), gripRight - new Vector2(0f, g), thick, color);
        Raylib.DrawLineEx(gripLeft + new Vector2(0f, g), gripRight + new Vector2(0f, g), thick, color);
        Arc(gripLeft.X, gripLeft.Y, g, g, 90f, 270f, thick, color);
        Arc(gripRight.X, gripRight.Y, g, g, -90f, 90f, thick, color);
    }

    // The seed inside is drawn into a texture the size of the bucket's box, only redrawn when the bucket changes.
    private void DrawContents(Rectangle area)
    {
        if (contentsTexture.Id == 0)
        {
            contentsTexture = Raylib.LoadRenderTexture((int)area.Width, (int)area.Height);
        }

        if (contentsVersion != Bucket.Version)
        {
            Raylib.BeginTextureMode(contentsTexture);
                Raylib.ClearBackground(Color.Blank);
                RenderContents(new BucketShape(new Rectangle(0, 0, area.Width, area.Height)));
            Raylib.EndTextureMode();
            contentsVersion = Bucket.Version;
        }

        // Render textures are stored upside down, so the source height is negative. Drawn a little see-through.
        Rectangle source = new Rectangle(0, 0, contentsTexture.Texture.Width, -contentsTexture.Texture.Height);
        Raylib.DrawTextureRec(contentsTexture.Texture, source, new Vector2(area.X, area.Y), Raylib.Fade(Color.White, 0.8f));
    }

    // Row by row from the bottom up, following the bucket's shape: stirred seed first (a dark base with speckles
    // in each seed's color), then the unstirred layers as bands, and a lighter oval for the top of the seed.
    private void RenderContents(BucketShape shape)
    {
        float pxPerSeed = (shape.Floor - shape.FillTop) / SeedBucket.Capacity;
        float mixedHeight = Bucket.MixedTotal * pxPerSeed;
        float fillHeight = Bucket.Total * pxPerSeed;
        float topY = shape.Floor - fillHeight;

        for (int y = (int)MathF.Floor(topY); y < (int)MathF.Ceiling(shape.Floor); y++)
        {
            float above = shape.Floor - (y + 0.5f);
            float halfWidth = shape.HalfWidthAt(y + 0.5f);
            if (above > fillHeight || halfWidth <= 0f) continue;
            Color color = ColorAtHeight(above, mixedHeight, pxPerSeed);
            Raylib.DrawRectangle((int)(shape.Cx - halfWidth), y, (int)MathF.Ceiling(halfWidth * 2f), 1, color);
        }

        if (mixedHeight >= 1f) RenderSpeckles(shape, mixedHeight);

        Color top = Palette.Lighten(ColorAtHeight(fillHeight - 0.5f, mixedHeight, pxPerSeed), 0.15f);
        Raylib.DrawEllipse((int)shape.Cx, (int)topY, shape.HalfWidthAt(topY), 3.5f, top);
    }

    // Which seed shows at this height above the bottom: the stirred base, or one of the layers stacked on it.
    private Color ColorAtHeight(float above, float mixedHeight, float pxPerSeed)
    {
        if (above < mixedHeight) return MixedBase;

        float layerTop = mixedHeight;
        Color last = MixedBase;
        foreach (SeedBucket.Layer layer in Bucket.Layers)
        {
            layerTop += layer.Amount * pxPerSeed;
            last = Seeds.Get(layer.Seed).Color;
            if (above < layerTop) return last;
        }
        return last;
    }

    private static readonly Color MixedBase = new Color(40, 32, 24, 255);

    // Dots in each seed's color over the stirred part, in proportion to how much of it is mixed in.
    private void RenderSpeckles(BucketShape shape, float mixedHeight)
    {
        float mixedTotal = Bucket.MixedTotal;
        int dots = (int)(mixedHeight * BucketShape.TopHalf * 2f / 6f);

        for (int i = 0; i < dots; i++)
        {
            // Fixed random numbers per dot, so the speckles stay put from frame to frame
            float y = shape.Floor - 1f - Rng.Fixed(i, 1) * (mixedHeight - 1f);
            float halfWidth = shape.HalfWidthAt(y) - 1.5f;
            if (halfWidth <= 1f) continue;
            float x = shape.Cx + (Rng.Fixed(i, 0) * 2f - 1f) * halfWidth;
            SeedType seed = PickMixedSeed(Rng.Fixed(i, 2) * mixedTotal);
            Raylib.DrawRectangle((int)x, (int)y - 1, 2, 2, Seeds.Get(seed).Color);
        }
    }

    // A piece of an oval as short straight strokes. Angles in degrees; 0 is right, 90 is down.
    private static void Arc(float cx, float cy, float rx, float ry, float fromDegrees, float toDegrees, float thick, Color color)
    {
        const int Steps = 28;
        Vector2 previous = Vector2.Zero;
        for (int i = 0; i <= Steps; i++)
        {
            float a = (fromDegrees + (toDegrees - fromDegrees) * i / Steps) * Raylib.DEG2RAD;
            Vector2 point = new Vector2(cx + MathF.Cos(a) * rx, cy + MathF.Sin(a) * ry);
            if (i > 0) Raylib.DrawLineEx(previous, point, thick, color);
            previous = point;
        }
    }

    // A curve from a to b, bent toward control (a quadratic Bezier), as short straight strokes.
    private static void Bezier(Vector2 a, Vector2 control, Vector2 b, float thick, Color color)
    {
        const int Steps = 16;
        Vector2 previous = a;
        for (int i = 1; i <= Steps; i++)
        {
            float t = (float)i / Steps;
            Vector2 point = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * control + t * t * b;
            Raylib.DrawLineEx(previous, point, thick, color);
            previous = point;
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
        if (contentsTexture.Id != 0) Raylib.UnloadRenderTexture(contentsTexture);
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
