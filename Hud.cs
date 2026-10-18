using System.Numerics;
using Raylib_cs;

// The fixed on-screen HUD, drawn with plain Raylib on top of the hologram.
// Shows stats, the selected bird, a minimap, and "has entered the chat" messages.
// Display only, except the minimap, which can be clicked or dragged to move the view.
public class Hud
{
    // LAYOUT
    private const int Margin = 12;
    private const int Pad = 10;
    private const int MapSize = 200;
    private const int HeaderSize = 10;
    private const int StatsWidth = 200, StatsHeight = 124;
    private const int CardWidth = 260, CardHeight = 132;

    // COLORS
    private static readonly Color PanelFill = new Color(10, 14, 20, 200);
    private static readonly Color TextColor = new Color(220, 232, 236, 255);
    private static readonly Color DimText = new Color(130, 150, 158, 255);

    // JOIN MESSAGES
    private const float MessageLifetime = 6f;
    private const float MessageFadeIn = 0.25f;
    private const float MessageFadeOut = 1.5f;
    private const int MaxMessages = 6;
    private const string JoinText = " has entered the chat";

    private class JoinMessage
    {
        public string Name;
        public Color Color;
        public float Age;

        public JoinMessage(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }

    private readonly List<JoinMessage> messages = new();
    private readonly HashSet<int> announcedBirds = new();
    private bool firstUpdate = true;

    // Screen rectangles, recalculated every frame so they follow window resizes.
    private Rectangle statsRect, mapRect, mapArea, cardRect;
    private bool cardVisible;
    private bool draggingMap = false;

    // ---------------- UPDATE (no drawing) ----------------

    public void Update(GameEngine engine, float dt, bool mouseBlockedByImGui)
    {
        Layout(engine);
        HandleMinimapInput(engine, mouseBlockedByImGui);
        UpdateJoinMessages(engine, dt);
    }

    // True when the mouse is over a HUD panel, so clicks don't fall through to the hologram.
    public bool IsMouseOver(GameEngine engine)
    {
        Layout(engine);
        Vector2 mouse = Raylib.GetMousePosition();
        return Raylib.CheckCollisionPointRec(mouse, statsRect)
            || Raylib.CheckCollisionPointRec(mouse, mapRect)
            || (cardVisible && Raylib.CheckCollisionPointRec(mouse, cardRect));
    }

    private void Layout(GameEngine engine)
    {
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();

        statsRect = new Rectangle(Margin, Margin, StatsWidth, StatsHeight);

        float mapPanelWidth = MapSize + Pad * 2;
        mapRect = new Rectangle(width - Margin - mapPanelWidth, Margin,
                                mapPanelWidth, MapSize + Pad * 3 + HeaderSize);
        mapArea = new Rectangle(mapRect.X + Pad, mapRect.Y + Pad * 2 + HeaderSize, MapSize, MapSize);

        cardVisible = engine.SelectedBird != null;
        cardRect = new Rectangle(Margin, height - Margin - CardHeight, CardWidth, CardHeight);
    }

    private void HandleMinimapInput(GameEngine engine, bool mouseBlockedByImGui)
    {
        Vector2 mouse = Raylib.GetMousePosition();

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !mouseBlockedByImGui
            && Raylib.CheckCollisionPointRec(mouse, mapArea))
        {
            draggingMap = true;
        }
        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            draggingMap = false;
        }

        if (draggingMap)
        {
            float worldHalf = World.Instance.HalfSize;
            float scale = MapSize / (worldHalf * 2f);
            Vector2 onMap = mouse - new Vector2(mapArea.X, mapArea.Y);
            engine.View.JumpTo(onMap / scale - new Vector2(worldHalf));
            engine.FollowSelected = false;
        }
    }

    // Announce birds that arrived after the game started, the first time they enter the view.
    private void UpdateJoinMessages(GameEngine engine, float dt)
    {
        List<Bird> birds = World.Instance.GetAllEntities<Bird>();

        // Birds already here on the first frame are residents: mark them as seen without a message.
        if (firstUpdate)
        {
            foreach (Bird bird in birds) announcedBirds.Add(bird.EntityID);
            firstUpdate = false;
        }

        foreach (Bird bird in birds)
        {
            if (announcedBirds.Contains(bird.EntityID)) continue;
            if (!engine.View.Contains(bird.Position)) continue;

            announcedBirds.Add(bird.EntityID);
            messages.Add(new JoinMessage(bird.Name, BirdColor(bird.Type)));
        }

        foreach (JoinMessage msg in messages) msg.Age += dt;
        messages.RemoveAll(msg => msg.Age >= MessageLifetime);
        while (messages.Count > MaxMessages) messages.RemoveAt(0);
    }

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(GameEngine engine)
    {
        Layout(engine);
        Color accent = ToColor(engine.Renderer.HoloColor);

        DrawStats(engine, accent);
        DrawMinimap(engine, accent);
        if (cardVisible) DrawSelectedCard(engine.SelectedBird!, accent);
        DrawJoinMessages();
    }

    private void DrawStats(GameEngine engine, Color accent)
    {
        DrawPanel(statsRect, accent);
        int x = (int)statsRect.X + Pad;
        int y = (int)statsRect.Y + Pad;

        Raylib.DrawText("BIRB BOX", x, y, HeaderSize, accent);
        y += HeaderSize + 8;

        int birdCount = World.Instance.GetAllEntities<Bird>().Count;
        int foodCount = World.Instance.GetAllEntities<Food>().Count;
        DrawRow("Birds", birdCount.ToString(), x, y);
        y += 24;
        DrawRow("Food", foodCount.ToString(), x, y);
        y += 24;

        float t = engine.SimTime;
        DrawRow("Time", $"{(int)(t / 60):00}:{(int)(t % 60):00}", x, y);
        string status = engine.Paused ? "PAUSED" : (engine.SimSpeed != 1f ? $"{engine.SimSpeed:0.00}x" : "");
        Raylib.DrawText(status, x + 130, y + 5, HeaderSize, accent);
        y += 28;

        Raylib.DrawText("Tab: tools", x, y, HeaderSize, DimText);
    }

    private void DrawRow(string label, string value, int x, int y)
    {
        Raylib.DrawText(label, x, y, 20, DimText);
        Raylib.DrawText(value, x + 70, y, 20, TextColor);
    }

    private void DrawSelectedCard(Bird bird, Color accent)
    {
        DrawPanel(cardRect, accent);
        int x = (int)cardRect.X + Pad;
        int y = (int)cardRect.Y + Pad;
        int innerWidth = CardWidth - Pad * 2;

        Raylib.DrawText("SELECTED", x, y, HeaderSize, accent);
        y += HeaderSize + 6;
        Raylib.DrawText(bird.Name, x, y, 20, BirdColor(bird.Type));
        y += 24;
        Raylib.DrawText(bird.State.ToString(), x, y, 20, DimText);
        y += 28;

        // Hunger bar: teal normally, orange when the bird is hungry
        Color barColor = bird.IsHungry() ? new Color(240, 158, 38, 255) : accent;
        Raylib.DrawRectangle(x, y, innerWidth, 16, new Color(30, 38, 46, 255));
        Raylib.DrawRectangle(x, y, (int)(innerWidth * bird.Hunger), 16, Raylib.Fade(barColor, 0.8f));
        Raylib.DrawText($"Hunger {bird.Hunger * 100:0}%", x + 6, y + 3, HeaderSize, TextColor);
        y += 24;

        Raylib.DrawText($"Age {bird.FormatAge()}", x, y, 20, DimText);
    }

    private void DrawMinimap(GameEngine engine, Color accent)
    {
        DrawPanel(mapRect, accent);
        Raylib.DrawText("WORLD MAP", (int)mapRect.X + Pad, (int)mapRect.Y + Pad, HeaderSize, accent);

        float worldHalf = World.Instance.HalfSize;
        float scale = MapSize / (worldHalf * 2f);

        // World X maps to map X, world Z maps to map Y.
        Vector2 ToMap(Vector3 worldPos) =>
            new Vector2(mapArea.X, mapArea.Y) + (new Vector2(worldPos.X, worldPos.Z) + new Vector2(worldHalf)) * scale;

        // Scissor mode clips drawing to the map area, so the view box can't spill past the edge.
        Raylib.BeginScissorMode((int)mapArea.X, (int)mapArea.Y, MapSize, MapSize);
        Raylib.DrawRectangleRec(mapArea, new Color(51, 38, 26, 255));

        Color foodColor = new Color(214, 178, 112, 255);
        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            Raylib.DrawRectangleV(ToMap(food.Position) - Vector2.One, new Vector2(2, 2), foodColor);
        }

        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (!World.Instance.IsInside(bird.Position)) continue;
            Raylib.DrawCircleV(ToMap(bird.Position), 2f, BirdColor(bird.Type));
        }

        if (engine.SelectedBird != null)
        {
            Vector2 p = ToMap(engine.SelectedBird.Position);
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, 5f, accent);
        }

        // The view window
        ViewWindow view = engine.View;
        Vector2 topLeft = ToMap(new Vector3(view.Center.X - view.HalfSize, 0, view.Center.Y - view.HalfSize));
        float boxSize = view.HalfSize * 2f * scale;
        Raylib.DrawRectangleLinesEx(new Rectangle(topLeft.X, topLeft.Y, boxSize, boxSize), 1.5f, accent);
        Raylib.EndScissorMode();
    }

    // Newest message at the bottom-right corner, older ones stacked above it.
    private void DrawJoinMessages()
    {
        int width = Raylib.GetScreenWidth();
        int y = Raylib.GetScreenHeight() - Margin - 24;

        for (int i = messages.Count - 1; i >= 0; i--)
        {
            JoinMessage msg = messages[i];

            float alpha = 1f;
            if (msg.Age < MessageFadeIn) alpha = msg.Age / MessageFadeIn;
            else if (msg.Age > MessageLifetime - MessageFadeOut) alpha = (MessageLifetime - msg.Age) / MessageFadeOut;
            float slide = msg.Age < MessageFadeIn ? (1f - msg.Age / MessageFadeIn) * 40f : 0f;

            int nameWidth = Raylib.MeasureText(msg.Name, 20);
            int totalWidth = nameWidth + Raylib.MeasureText(JoinText, 20);
            int x = (int)(width - Margin - 8 - totalWidth + slide);

            Raylib.DrawRectangle(x - 8, y - 4, totalWidth + 16, 28, Raylib.Fade(PanelFill, alpha));
            Raylib.DrawRectangle(x - 8, y - 4, 2, 28, Raylib.Fade(msg.Color, alpha));
            Raylib.DrawText(msg.Name, x, y, 20, Raylib.Fade(msg.Color, alpha));
            Raylib.DrawText(JoinText, x + nameWidth, y, 20, Raylib.Fade(TextColor, alpha));

            y -= 32;
        }
    }

    // ---------------- HELPERS ----------------

    // Dark see-through box with a faint border and brighter corner ticks.
    private static void DrawPanel(Rectangle r, Color accent)
    {
        Raylib.DrawRectangleRec(r, PanelFill);
        Raylib.DrawRectangleLinesEx(r, 1f, Raylib.Fade(accent, 0.3f));

        int x = (int)r.X, y = (int)r.Y, w = (int)r.Width, h = (int)r.Height;
        const int tick = 10;
        Raylib.DrawRectangle(x, y, tick, 2, accent);
        Raylib.DrawRectangle(x, y, 2, tick, accent);
        Raylib.DrawRectangle(x + w - tick, y, tick, 2, accent);
        Raylib.DrawRectangle(x + w - 2, y, 2, tick, accent);
        Raylib.DrawRectangle(x, y + h - 2, tick, 2, accent);
        Raylib.DrawRectangle(x, y + h - tick, 2, tick, accent);
        Raylib.DrawRectangle(x + w - tick, y + h - 2, tick, 2, accent);
        Raylib.DrawRectangle(x + w - 2, y + h - tick, 2, tick, accent);
    }

    private static Color ToColor(Vector3 rgb)
    {
        return new Color(
            (int)Math.Clamp(rgb.X * 255f, 0, 255),
            (int)Math.Clamp(rgb.Y * 255f, 0, 255),
            (int)Math.Clamp(rgb.Z * 255f, 0, 255),
            255);
    }

    private static Color BirdColor(BirdType type)
    {
        switch (type)
        {
            case BirdType.Cardinal: return new Color(227, 74, 74, 255);
            case BirdType.Dove: return new Color(212, 209, 199, 255);
            default: return new Color(240, 158, 38, 255);
        }
    }
}
