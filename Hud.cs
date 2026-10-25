using System.Numerics;
using Raylib_cs;

// The fixed on-screen HUD, drawn with plain Raylib on top of the hologram:
// the stats screen in the right side column (see SidePanels), and the selected bird or feeder card
// floating at the top of the middle area. The world map is its own class (MiniMap).
// Clickable parts of the bird card: the name (click to rename), and the Follow and Center buttons.
// The feeder card shows the seed inside and has Move and Center buttons.
public class Hud
{
    // LAYOUT
    private const int Margin = 12;
    private const int Pad = 10;
    private const int HeaderSize = 12;
    private const int NameSize = 20;
    private const int CardWidth = 260, CardHeight = 164;
    private const int SeedLineHeight = 20;
    private const int CardButtonHeight = 24, CardButtonGap = 6;

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
            engine.FollowSelected = !engine.FollowSelected;
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
        else if (Raylib.CheckCollisionPointRec(mouse, centerButton)) engine.View.JumpTo(new Vector2(selected.Position.X, selected.Position.Z));
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

        UiText.Draw("BIRB BOX", x, y, HeaderSize, accent);
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
        y += 22;

        DrawRow("Time", Clock.Format(engine.SimTime), x, y);
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
        UiText.Draw(bird.State.ToString(), x, y, 20, Palette.DimText);
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

        UiDraw.Button(followButton, engine.FollowSelected ? "Following" : "Follow", accent, lit: engine.FollowSelected);
        UiDraw.Button(centerButton, "Center", accent);
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
