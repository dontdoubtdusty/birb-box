using System.Numerics;
using Raylib_cs;

// The fixed on-screen HUD, drawn with plain Raylib on top of the hologram:
// the stats screen in the right side column (see SidePanels; with the time of day: a 24-hour dial, the word for it,
// the day and the season), and the selected bird or feeder card
// floating at the top of the middle area. The world map is its own class (MiniMap).
// Clickable parts of the bird card: the name (click to rename), and the Follow and Center buttons.
// The bird card also shows its personality as five sliders, one per trait scale (hover one for its trait's flavor line),
// and its favorite seed.
// The feeder card shows the seed inside and has Move and Center buttons.
public class Hud
{
    // LAYOUT
    private const int Margin = 12;
    private const int Pad = 10;
    private const int HeaderSize = 12;
    private const int NameSize = 20;
    private const int CardWidth = 300, CardHeight = 302;
    private const int TraitSize = 16;                                         // the "Fond of" line
    private const int FlavorSize = 14, FlavorLineHeight = 17;

    // TRAIT SLIDERS: one row per scale (name, brass track, the trait's word), a glowing pip on the bird's spot.
    private const int SliderRowHeight = 22;
    private const int SliderWordSize = 14;
    private const int SliderGap = 10;               // between the name, the track and the word
    private const float PipRadius = 4f;
    private const float PipGlow = 9f;               // outer glow radius
    private const float PipSlide = 8f;              // how quickly pips slide to their spot (higher is quicker)
    private const float PipPulseSpeed = 2.5f;       // gentle breathing of the glow
    private readonly float[] pipSpots = new float[Traits.Scales.Length];    // 0..1 along each track, eased in Update
    private Bird? pipBird;                                                  // whose spots they are
    private const int SeedLineHeight = 20;
    private const int CardButtonHeight = 24, CardButtonGap = 6;

    // THE CLOCK DIAL: 24 hours around, midnight at the bottom and noon at the top. The lit arc runs from sunrise to sunset,
    // so it grows in summer and shrinks in winter; a dimmer band on either side is the first and last glow.
    private const float DialRadius = 30f;
    private const int DialTimeSize = 12;
    private static readonly Color DialNight = new Color(18, 26, 48, 255);
    private static readonly Color DialGlow = new Color(120, 84, 110, 255);
    private static readonly Color DialDay = new Color(196, 158, 92, 255);

    // Screen rectangles, recalculated every frame so they follow window resizes.
    private Rectangle statsRect;
    private Rectangle cardRect, nameRect, followButton, centerButton;
    private bool cardVisible;
    private Feeder? feeder;          // the selected feeder this frame, when its card is showing instead of a bird's

    // NAME EDITING: while this is on, the keyboard types into the name instead of steering the game.
    public bool IsEditingName { get; private set; }
    private string nameDraft = "";
    private Bird? editingBird;    // stops editing if a different bird gets selected
    private const int NameMaxWidth = CardWidth - Pad * 2 - 12;    // leaves room for the cursor

    // ---------------- UPDATE (no drawing) ----------------

    // Runs at the very start of the frame, before anything else reads the keyboard,
    // so Esc, Space, WASD and the rest go into the name instead of the game.
    public void UpdateTyping(GameEngine engine)
    {
        if (!IsEditingName) return;
        Layout(engine);

        Bird? bird = engine.SelectedBird;
        if (bird == null || bird != editingBird)
        {
            IsEditingName = false;
            return;
        }

        // GetCharPressed hands back each typed character once. Only plain ASCII letters and symbols are kept.
        int c = Raylib.GetCharPressed();
        while (c > 0)
        {
            string longer = nameDraft + (char)c;
            if (c >= 32 && c <= 126 && UiText.Measure(longer, NameSize) <= NameMaxWidth) nameDraft = longer;
            c = Raylib.GetCharPressed();
        }

        bool backspace = Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace);
        if (backspace && nameDraft.Length > 0) nameDraft = nameDraft[..^1];

        bool enter = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter);
        bool clickedAway = Raylib.IsMouseButtonPressed(MouseButton.Left)
                           && !Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), nameRect);

        if (enter || clickedAway)
        {
            bird.Rename(nameDraft);
            IsEditingName = false;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            IsEditingName = false;
        }
    }

    public void Update(GameEngine engine, bool mouseBlockedByImGui)
    {
        Layout(engine);
        HandleCardClicks(engine, mouseBlockedByImGui);
        UpdatePips(engine.SelectedBird);
    }

    // Slides the slider pips toward the bird's spots. A newly selected bird's pips start in the middle and slide out.
    private void UpdatePips(Bird? bird)
    {
        if (bird == null)
        {
            pipBird = null;
            return;
        }
        if (bird != pipBird)
        {
            pipBird = bird;
            for (int i = 0; i < pipSpots.Length; i++) pipSpots[i] = AverageSpot(Traits.Scales[i]);
        }

        float ease = MathF.Min(1f, Raylib.GetFrameTime() * PipSlide);
        for (int i = 0; i < pipSpots.Length; i++)
        {
            pipSpots[i] += (SpotOf(bird, Traits.Scales[i]) - pipSpots[i]) * ease;
        }
    }

    // The trait a bird holds on a scale, or null when it's average there.
    private static Trait? TraitOn(Bird bird, TraitScale scale)
    {
        foreach (Trait trait in bird.Personality)
        {
            if (Traits.Get(trait).Scale == scale) return trait;
        }
        return null;
    }

    // Where a bird sits along a scale's track, 0 (left end) to 1 (right end).
    private static float SpotOf(Bird bird, TraitScale scale) => StopSpot(scale, TraitOn(bird, scale));

    private static float AverageSpot(TraitScale scale) => StopSpot(scale, null);

    private static float StopSpot(TraitScale scale, Trait? trait)
    {
        IReadOnlyList<Trait?> stops = Traits.Stops(scale);
        int index = Math.Max(0, stops.ToList().IndexOf(trait));
        return stops.Count > 1 ? index / (float)(stops.Count - 1) : 0.5f;
    }

    // True when the mouse is over the selected bird card. The side columns are covered by SidePanels.IsMouseOver.
    public bool IsMouseOver(GameEngine engine)
    {
        Layout(engine);
        return cardVisible && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), cardRect);
    }

    // The stats rectangle comes from the side column, which lays itself out first each frame.
    // The card sits in the top-left corner of the middle area, just right of the left column.
    private void Layout(GameEngine engine)
    {
        statsRect = engine.Sides.StatsSlot;

        feeder = engine.SelectedFeeder;
        cardVisible = engine.SelectedBird != null || feeder != null;
        Rectangle left = engine.Sides.LeftColumn;
        cardRect = new Rectangle(left.X + left.Width + Margin, Margin, CardWidth, feeder != null ? FeederCardHeight(feeder) : CardHeight);

        float x = cardRect.X + Pad;
        float innerWidth = CardWidth - Pad * 2;
        nameRect = new Rectangle(x - 4, cardRect.Y + Pad + HeaderSize + 3, innerWidth + 8, NameSize + 6);

        float buttonWidth = (innerWidth - CardButtonGap) / 2f;
        float buttonTop = cardRect.Y + cardRect.Height - Pad - CardButtonHeight;
        followButton = new Rectangle(x, buttonTop, buttonWidth, CardButtonHeight);
        centerButton = new Rectangle(x + buttonWidth + CardButtonGap, buttonTop, buttonWidth, CardButtonHeight);
    }

    private void HandleCardClicks(GameEngine engine, bool mouseBlockedByImGui)
    {
        if (feeder != null)
        {
            HandleFeederCardClicks(engine, feeder, mouseBlockedByImGui);
            return;
        }

        Bird? bird = engine.SelectedBird;
        if (bird == null || mouseBlockedByImGui || !Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        Vector2 mouse = Raylib.GetMousePosition();
        if (Raylib.CheckCollisionPointRec(mouse, followButton))
        {
            engine.FollowedBird = engine.FollowedBird == bird ? null : bird;
        }
        else if (Raylib.CheckCollisionPointRec(mouse, centerButton))
        {
            engine.JumpToBird(bird);
        }
        else if (Raylib.CheckCollisionPointRec(mouse, nameRect) && !IsEditingName)
        {
            IsEditingName = true;
            editingBird = bird;
            nameDraft = bird.Name;
        }
    }

    // The feeder card's buttons share the bird card's spots: Move on the left, Center on the right.
    private void HandleFeederCardClicks(GameEngine engine, Feeder selected, bool mouseBlockedByImGui)
    {
        if (mouseBlockedByImGui || !Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        Vector2 mouse = Raylib.GetMousePosition();
        if (Raylib.CheckCollisionPointRec(mouse, followButton)) engine.MoveSelectedFeeder();
        else if (Raylib.CheckCollisionPointRec(mouse, centerButton)) engine.JumpToSpot(selected.Position);
    }

    // Header, name, label, fill bar, one line per seed (at least one, for "Empty"), the birds eating there, buttons.
    private static int FeederCardHeight(Feeder selected)
    {
        int seedLines = Math.Max(1, SeedsIn(selected).Count);
        return Pad + HeaderSize + 6 + 24 + 22 + 24 + seedLines * SeedLineHeight + 24 + CardButtonHeight + Pad;
    }

    // The seed in a feeder that's worth listing, most first.
    private static List<(SeedType Seed, float Amount)> SeedsIn(Feeder selected)
    {
        if (selected.Seed == null) return new();
        return selected.Seed.Contents.Where(pair => pair.Value > 0.01f)
                                     .OrderByDescending(pair => pair.Value)
                                     .Select(pair => (pair.Key, pair.Value)).ToList();
    }

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(GameEngine engine, Color accent)
    {
        Layout(engine);
        DrawStats(engine, accent);
        if (feeder != null) DrawFeederCard(feeder, accent);
        else if (cardVisible) DrawSelectedCard(engine, engine.SelectedBird!, accent);
    }

    private void DrawFeederCard(Feeder selected, Color accent)
    {
        UiDraw.Panel(cardRect, accent);
        int x = (int)cardRect.X + Pad;
        int y = (int)cardRect.Y + Pad;
        int innerWidth = CardWidth - Pad * 2;
        FeederInfo info = selected.Info;

        UiText.Draw("FEEDER", x, y, HeaderSize, accent);
        y += HeaderSize + 6;
        UiText.Draw(info.Name, x, y, NameSize, Palette.BrassLight);
        y += 24;
        UiText.Draw(info.Label, x, y, HeaderSize, Palette.DimText);
        y += 22;

        // How full it is
        float full = Math.Clamp(selected.Held / info.Capacity, 0f, 1f);
        Raylib.DrawRectangle(x, y, innerWidth, 16, Palette.PanelDark);
        Raylib.DrawRectangle(x, y, (int)(innerWidth * full), 16, Raylib.Fade(accent, 0.8f));
        UiText.Draw($"Holds {selected.Held:0.0} of {info.Capacity:0}", x + 6, y + 3, HeaderSize, Palette.Text);
        y += 24;

        // What's inside, with a swatch of each seed's color
        List<(SeedType Seed, float Amount)> inside = SeedsIn(selected);
        if (inside.Count == 0)
        {
            UiText.Draw("Empty", x, y, 18, Palette.DimText);
            y += SeedLineHeight;
        }
        foreach ((SeedType seed, float amount) in inside)
        {
            SeedInfo seedInfo = Seeds.Get(seed);
            Raylib.DrawRectangle(x, y + 4, 4, 12, seedInfo.Color);
            UiText.Draw(seedInfo.Name, x + 10, y, 18, Palette.Text);
            string value = amount.ToString("0.0");
            UiText.Draw(value, x + innerWidth - UiText.Measure(value, 18), y, 18, Palette.DimText);
            y += SeedLineHeight;
        }

        int eating = World.Instance.GetAllEntities<Bird>().Count(b => b.State == BirdState.Eating && b.TargetFood != null && b.TargetFood == selected.Seed);
        UiText.Draw(eating == 1 ? "1 bird eating here" : $"{eating} birds eating here", x, y + 2, HeaderSize, Palette.DimText);

        UiDraw.Button(followButton, "Move", accent);
        UiDraw.Button(centerButton, "Center", accent);
    }

    private void DrawStats(GameEngine engine, Color accent)
    {
        UiDraw.Panel(statsRect, accent);
        int x = (int)statsRect.X + Pad;
        int y = (int)statsRect.Y + Pad;

        UiText.Draw("BIRB CUBE", x, y, HeaderSize, accent);
        string help = "Tab: tools  RMB: pour";
        UiText.Draw(help, (int)(statsRect.X + statsRect.Width) - Pad - UiText.Measure(help, HeaderSize), y, HeaderSize, Palette.DimText);
        y += HeaderSize + 8;

        int birdCount = World.Instance.GetAllEntities<Bird>().Count;
        int foodCount = World.Instance.GetAllEntities<Food>().Count;
        DrawRow("Birds", birdCount.ToString(), x, y);
        y += 22;
        DrawRow("Food", foodCount.ToString(), x, y);
        y += 22;
        DrawRow("Coins", engine.Coins.ToString(), x, y);
        y += 24;

        // The time of day: the word in the hologram color, then the day and season
        WorldClock clock = engine.Clock;
        UiText.Draw(clock.Phase.ToString(), x, y, 20, accent);
        string date = clock.DateText;
        int dateX = x + UiText.Measure(clock.Phase.ToString(), 20) + 10;
        UiText.Draw(date, dateX, y + 4, 16, Palette.DimText);

        Vector2 center = new Vector2(statsRect.X + statsRect.Width - Pad - DialRadius, statsRect.Y + Pad + HeaderSize + 8 + DialRadius + 4);
        DrawDial(clock, center, accent);
    }

    // The 24-hour dial: night all round, the glow and the daylight as arcs, an hour hand, and the time in the middle.
    private static void DrawDial(WorldClock clock, Vector2 center, Color accent)
    {
        Raylib.DrawCircleV(center, DialRadius, DialNight);
        DrawDialArc(center, clock.FirstGlow, clock.LastGlow, DialGlow);
        DrawDialArc(center, clock.Sunrise, clock.Sunset, DialDay);
        Raylib.DrawCircleV(center, DialRadius - 7f, Palette.PanelFill);
        Raylib.DrawCircleLines((int)center.X, (int)center.Y, DialRadius, Palette.Brass);

        // A notch every 6 hours
        for (int h = 0; h < 24; h += 6)
        {
            Vector2 dir = DialDirection(h);
            Raylib.DrawLineEx(center + dir * (DialRadius - 4f), center + dir * DialRadius, 2f, Palette.BrassLight);
        }

        Vector2 hand = DialDirection(clock.Hour);
        Raylib.DrawLineEx(center + hand * 4f, center + hand * (DialRadius - 2f), 2f, accent);
        Raylib.DrawCircleV(center + hand * (DialRadius - 3.5f), 3f, accent);

        string time = clock.TimeText;
        UiText.Draw(time, (int)(center.X - UiText.Measure(time, DialTimeSize) / 2f), (int)center.Y - DialTimeSize / 2, DialTimeSize, Palette.Text);
    }

    // An arc of the dial's ring from one hour to another, going clockwise.
    private static void DrawDialArc(Vector2 center, float fromHour, float toHour, Color color)
    {
        float start = DialAngle(fromHour);
        float end = DialAngle(toHour);
        if (end < start) end += 360f;
        Raylib.DrawRing(center, DialRadius - 7f, DialRadius, start, end, 32, color);
    }

    // Raylib's angles: 0 points right and they grow clockwise on screen. Midnight is at the bottom (90), noon at the top.
    private static float DialAngle(float hour) => 90f + hour / 24f * 360f;

    private static Vector2 DialDirection(float hour)
    {
        float radians = DialAngle(hour) * MathF.PI / 180f;
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }

    private void DrawRow(string label, string value, int x, int y)
    {
        UiText.Draw(label, x, y, 20, Palette.DimText);
        UiText.Draw(value, x + 70, y, 20, Palette.Text);
    }

    private void DrawSelectedCard(GameEngine engine, Bird bird, Color accent)
    {
        UiDraw.Panel(cardRect, accent);
        int x = (int)cardRect.X + Pad;
        int y = (int)cardRect.Y + Pad;
        int innerWidth = CardWidth - Pad * 2;
        bool nameHovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), nameRect);

        UiText.Draw("SELECTED", x, y, HeaderSize, accent);
        string hint = IsEditingName ? "ENTER: SAVE   ESC: CANCEL" : nameHovered ? "CLICK TO RENAME" : "";
        UiText.Draw(hint, x + innerWidth - UiText.Measure(hint, HeaderSize), y, HeaderSize, Palette.DimText);
        y += HeaderSize + 6;

        DrawNameField(bird, x, y, nameHovered, accent);
        y += 24;
        UiText.Draw(bird.Roosting ? "Roosting" : bird.State.ToString(), x, y, 20, Palette.DimText);
        if (bird.Visits > 1)
        {
            string visit = $"Visit {bird.Visits}";
            UiText.Draw(visit, x + innerWidth - UiText.Measure(visit, 20), y, 20, Palette.DimText);
        }
        y += 28;

        // Hunger bar: hologram color normally, orange when the bird is hungry
        Color barColor = bird.IsHungry() ? new Color(240, 158, 38, 255) : accent;
        Raylib.DrawRectangle(x, y, innerWidth, 16, Palette.PanelDark);
        Raylib.DrawRectangle(x, y, (int)(innerWidth * bird.Hunger), 16, Raylib.Fade(barColor, 0.8f));
        UiText.Draw($"Hunger {bird.Hunger * 100:0}%", x + 6, y + 3, HeaderSize, Palette.Text);
        y += 24;

        UiText.Draw($"Age {bird.FormatAge()}", x, y, 20, Palette.DimText);
        y += 26;

        Trait? hoveredTrait = DrawTraitSliders(bird, x, y, innerWidth);
        y += Traits.Scales.Length * SliderRowHeight + 4;

        SeedInfo favorite = Seeds.Get(bird.FavoriteSeed);
        Raylib.DrawRectangle(x, y + 3, 4, 12, favorite.Color);
        UiText.Draw($"Fond of {favorite.Name}", x + 10, y, TraitSize, Palette.DimText);

        bool following = engine.FollowedBird == bird;
        UiDraw.Button(followButton, following ? "Following" : "Follow", accent, lit: following);
        UiDraw.Button(centerButton, "Center", accent);

        if (hoveredTrait is Trait trait) DrawFlavor(trait, accent);
    }

    // One row per scale: its name, a brass track with a tick per stop (a small ring marks average),
    // the bird's glowing pip, and its trait's word (or "average"). Returns the trait under the mouse, if any.
    private Trait? DrawTraitSliders(Bird bird, int x, int y, int innerWidth)
    {
        int nameColumn = Traits.Scales.Max(scale => UiText.Measure(scale.ToString().ToUpper(), HeaderSize));
        int wordColumn = Traits.All.Max(trait => UiText.Measure(Traits.Get(trait).Name, SliderWordSize));
        float trackLeft = x + nameColumn + SliderGap + PipGlow / 2f;
        float trackRight = x + innerWidth - wordColumn - SliderGap - PipGlow / 2f;
        Vector2 mouse = Raylib.GetMousePosition();
        float time = (float)Raylib.GetTime();
        Trait? hovered = null;

        for (int i = 0; i < Traits.Scales.Length; i++)
        {
            TraitScale scale = Traits.Scales[i];
            Trait? trait = TraitOn(bird, scale);
            int rowY = y + i * SliderRowHeight;
            float midY = rowY + SliderRowHeight / 2f - 1f;

            Rectangle row = new Rectangle(x - 3, rowY, innerWidth + 6, SliderRowHeight);
            bool over = trait != null && Raylib.CheckCollisionPointRec(mouse, row);
            if (over)
            {
                hovered = trait;
                Raylib.DrawRectangleRec(row, Palette.PanelDark);
            }

            UiText.Draw(scale.ToString().ToUpper(), x, (int)midY - HeaderSize / 2, HeaderSize, Palette.DimText);
            DrawTrack(scale, trackLeft, trackRight, midY);

            float pipX = trackLeft + (trackRight - trackLeft) * pipSpots[i];
            if (trait != null) DrawPip(new Vector2(pipX, midY), bird.Info.Color, time * PipPulseSpeed + i * 1.7f);
            else Raylib.DrawCircleLines((int)pipX, (int)midY, PipRadius, Palette.DimText);

            string word = trait is Trait held ? Traits.Get(held).Name : "average";
            Color wordColor = trait == null ? Raylib.Fade(Palette.DimText, 0.7f) : over ? Palette.Text : Palette.BrassLight;
            UiText.Draw(word, x + innerWidth - wordColumn, (int)midY - SliderWordSize / 2, SliderWordSize, wordColor);
        }
        return hovered;
    }

    // The brass line with a notch at each stop. The average stop is a small open ring instead of a notch.
    private static void DrawTrack(TraitScale scale, float left, float right, float midY)
    {
        Raylib.DrawLineEx(new Vector2(left, midY), new Vector2(right, midY), 2f, Palette.Brass);

        IReadOnlyList<Trait?> stops = Traits.Stops(scale);
        for (int s = 0; s < stops.Count; s++)
        {
            float stopX = left + (right - left) * (stops.Count > 1 ? s / (float)(stops.Count - 1) : 0.5f);
            if (stops[s] == null)
            {
                Raylib.DrawCircleV(new Vector2(stopX, midY), 3f, Palette.PanelFill);
                Raylib.DrawCircleLines((int)stopX, (int)midY, 3f, Palette.Brass);
            }
            else
            {
                Raylib.DrawRectangle((int)stopX - 1, (int)midY - 4, 2, 8, Raylib.Fade(Palette.BrassLight, 0.7f));
            }
        }
    }

    // A softly breathing glow in the bird's color, a solid core, and a small bright glint.
    private static void DrawPip(Vector2 at, Color color, float phase)
    {
        float breathe = 0.5f + 0.5f * MathF.Sin(phase);
        Raylib.DrawCircleV(at, PipGlow + breathe * 1.5f, Raylib.Fade(color, 0.10f + 0.08f * breathe));
        Raylib.DrawCircleV(at, PipRadius + 2f, Raylib.Fade(color, 0.35f));
        Raylib.DrawCircleV(at, PipRadius, color);
        Raylib.DrawCircleV(at + new Vector2(-1.2f, -1.2f), 1.3f, Palette.Lighten(color, 0.6f));
    }

    // A small panel just under the card with the hovered trait's flavor line, wrapped to the card's width.
    private void DrawFlavor(Trait trait, Color accent)
    {
        int innerWidth = CardWidth - Pad * 2;
        List<string> lines = Wrap(Traits.Get(trait).Flavor, FlavorSize, innerWidth);
        Rectangle panel = new Rectangle(cardRect.X, cardRect.Y + cardRect.Height + 4, CardWidth, Pad * 2 + lines.Count * FlavorLineHeight);
        UiDraw.Panel(panel, accent);

        int y = (int)panel.Y + Pad;
        foreach (string line in lines)
        {
            UiText.Draw(line, (int)panel.X + Pad, y, FlavorSize, Palette.Text);
            y += FlavorLineHeight;
        }
    }

    // Splits text into lines no wider than maxWidth, breaking between words.
    private static List<string> Wrap(string text, int size, int maxWidth)
    {
        List<string> lines = new();
        string line = "";
        foreach (string word in text.Split(' '))
        {
            string longer = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && UiText.Measure(longer, size) > maxWidth)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = longer;
            }
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    // The name, or while renaming, a text box with a blinking cursor.
    private void DrawNameField(Bird bird, int x, int y, bool hovered, Color accent)
    {
        Color nameColor = bird.Info.Color;

        if (!IsEditingName)
        {
            if (hovered) Raylib.DrawRectangleLinesEx(nameRect, 1f, Raylib.Fade(accent, 0.4f));
            UiText.Draw(bird.Name, x, y, NameSize, nameColor);
            return;
        }

        Raylib.DrawRectangleRec(nameRect, Palette.PanelDark);
        Raylib.DrawRectangleLinesEx(nameRect, 1f, accent);
        UiText.Draw(nameDraft, x, y, NameSize, nameColor);

        bool cursorOn = (int)(Raylib.GetTime() * 2.0) % 2 == 0;
        if (cursorOn)
        {
            int cursorX = x + UiText.Measure(nameDraft, NameSize) + 2;
            Raylib.DrawRectangle(cursorX, y, 2, NameSize, Palette.Text);
        }
    }
}
