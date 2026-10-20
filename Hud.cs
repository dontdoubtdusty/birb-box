using System.Numerics;
using Raylib_cs;

// The fixed on-screen HUD, drawn with plain Raylib on top of the hologram:
// the stats screen in the right side column (see SidePanels), and the selected bird card
// floating at the top of the middle area. The world map is its own class (MiniMap).
// Clickable parts of the card: the name (click to rename), and the Follow and Center buttons.
public class Hud
{
    // LAYOUT
    private const int Margin = 12;
    private const int Pad = 10;
    private const int HeaderSize = 12;
    private const int NameSize = 20;
    private const int CardWidth = 260, CardHeight = 164;
    private const int CardButtonHeight = 24, CardButtonGap = 6;

    // Screen rectangles, recalculated every frame so they follow window resizes.
    private Rectangle statsRect;
    private Rectangle cardRect, nameRect, followButton, centerButton;
    private bool cardVisible;

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

        cardVisible = engine.SelectedBird != null;
        Rectangle left = engine.Sides.LeftColumn;
        cardRect = new Rectangle(left.X + left.Width + Margin, Margin, CardWidth, CardHeight);

        float x = cardRect.X + Pad;
        float innerWidth = CardWidth - Pad * 2;
        nameRect = new Rectangle(x - 4, cardRect.Y + Pad + HeaderSize + 3, innerWidth + 8, NameSize + 6);

        float buttonWidth = (innerWidth - CardButtonGap) / 2f;
        float buttonTop = cardRect.Y + CardHeight - Pad - CardButtonHeight;
        followButton = new Rectangle(x, buttonTop, buttonWidth, CardButtonHeight);
        centerButton = new Rectangle(x + buttonWidth + CardButtonGap, buttonTop, buttonWidth, CardButtonHeight);
    }

    private void HandleCardClicks(GameEngine engine, bool mouseBlockedByImGui)
    {
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

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(GameEngine engine, Color accent)
    {
        Layout(engine);
        DrawStats(engine, accent);
        if (cardVisible) DrawSelectedCard(engine, engine.SelectedBird!, accent);
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
