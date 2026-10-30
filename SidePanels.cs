using System.Numerics;
using Raylib_cs;

// The two brass side columns that frame the hologram. Both are the same width,
// so the Birb Cube stays in the middle of the screen.
//  - left:  the seed bucket (BucketPanel draws into BucketSlot)
//  - right: stats, the round pause/speed buttons, and the world map (Hud draws into StatsSlot, MiniMap into MapSlot)
// This class owns the column shapes, the slots, and the buttons.
// In the Hologram View each column slides off its side of the screen and peeks back while the mouse is near that edge
// (or on the column), so nothing but the garden is in view (UpdateSlide). Everything inside a column is laid out from
// its slots, so it all slides along and clicks still land where things are drawn.
public class SidePanels
{
    // LAYOUT
    public const int Margin = 12;           // space between a column and the window edge
    private const int RimPad = 12;          // brass showing around each screen
    private const int SlotGap = 14;         // between screens in the same column
    public const int ColumnWidth = BucketPanel.PanelWidth + RimPad * 2;
    public const int StatsHeight = 146;     // room for the time of day and the weather under the counts
    private const int ButtonRadius = 15;
    private const int ButtonSpacing = 40;
    private const int ButtonRowGap = 10;    // between the stats screen and the buttons under it

    // SLIDING AWAY (the Hologram View)
    private const float PeekBand = 28f;     // pixels from the screen's edge that bring a column back
    private const float KeepReach = 24f;    // a shown column stays while the mouse is this close to it
    private const float SlideTime = 0.25f;  // seconds to slide all the way in or out

    // The speed buttons. Pause is its own button, to the left of these.
    private static readonly float[] Speeds = { 0.5f, 1f, 2f, 4f };
    private static readonly string[] SpeedLabels = { ".5x", "1x", "2x", "4x" };

    public Rectangle LeftColumn { get; private set; }
    public Rectangle RightColumn { get; private set; }
    public Rectangle BucketSlot { get; private set; }
    public Rectangle StatsSlot { get; private set; }
    public Rectangle MapSlot { get; private set; }

    private readonly Vector2[] buttonCenters = new Vector2[Speeds.Length + 1];   // [0] is pause
    private float leftShown = 1f, rightShown = 1f;    // 1 in place, 0 slid off the screen

    // ---------------- UPDATE (no drawing) ----------------

    public void Update(GameEngine engine, bool mouseBlockedByImGui)
    {
        Layout();
        if (mouseBlockedByImGui || !Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        Vector2 mouse = Raylib.GetMousePosition();
        for (int i = 0; i < buttonCenters.Length; i++)
        {
            if (Vector2.Distance(mouse, buttonCenters[i]) > ButtonRadius) continue;

            if (i == 0)
            {
                engine.Paused = !engine.Paused;
            }
            else
            {
                engine.SimSpeed = Speeds[i - 1];
                engine.Paused = false;
            }
        }
    }

    // Slides each column in or out. hideable: the Hologram View is showing, so a column only stays while the mouse
    // is at its edge of the screen or on it (or holding the button down on it, so a drag on the map isn't cut short).
    public void UpdateSlide(float dt, bool hideable)
    {
        Vector2 mouse = Raylib.GetMousePosition();
        bool held = Raylib.IsMouseButtonDown(MouseButton.Left);
        bool wantLeft = !hideable || mouse.X <= PeekBand || (leftShown > 0.5f && (held || Near(mouse, LeftColumn)));
        bool wantRight = !hideable || mouse.X >= Raylib.GetScreenWidth() - PeekBand
                         || (rightShown > 0.5f && (held || Near(mouse, RightColumn)));
        float step = dt / SlideTime;
        leftShown = Math.Clamp(leftShown + (wantLeft ? step : -step), 0f, 1f);
        rightShown = Math.Clamp(rightShown + (wantRight ? step : -step), 0f, 1f);
    }

    private static bool Near(Vector2 mouse, Rectangle r)
    {
        Rectangle grown = new Rectangle(r.X - KeepReach, r.Y - KeepReach, r.Width + KeepReach * 2, r.Height + KeepReach * 2);
        return Raylib.CheckCollisionPointRec(mouse, grown);
    }

    // How far a column is pushed toward its edge: none when shown, all the way off the screen when hidden (eased).
    private static float SlideOffset(float shown)
    {
        float hidden = 1f - shown;
        return (Margin + ColumnWidth + 8f) * hidden * hidden * (3f - 2f * hidden);
    }

    // True when the mouse is over either column, so clicks and the wheel don't fall through to the hologram.
    public bool IsMouseOver()
    {
        Vector2 mouse = Raylib.GetMousePosition();
        return Raylib.CheckCollisionPointRec(mouse, LeftColumn) || Raylib.CheckCollisionPointRec(mouse, RightColumn);
    }

    // Recalculated every frame so it follows window resizes.
    public void Layout()
    {
        int width = Raylib.GetScreenWidth();
        float leftX = Margin - SlideOffset(leftShown);

        // Left column: just the bucket
        BucketSlot = new Rectangle(leftX + RimPad, Margin + RimPad, BucketPanel.PanelWidth, BucketPanel.PanelHeight);
        LeftColumn = new Rectangle(leftX, Margin, ColumnWidth, BucketPanel.PanelHeight + RimPad * 2);

        // Right column: stats, buttons, map, top to bottom
        float rightX = width - Margin - ColumnWidth + SlideOffset(rightShown);
        float y = Margin + RimPad;
        StatsSlot = new Rectangle(rightX + RimPad, y, BucketPanel.PanelWidth, StatsHeight);
        y += StatsHeight + ButtonRowGap + ButtonRadius;

        for (int i = 0; i < buttonCenters.Length; i++)
        {
            buttonCenters[i] = new Vector2(StatsSlot.X + ButtonRadius + 4 + i * ButtonSpacing, y);
        }
        y += ButtonRadius + SlotGap;

        MapSlot = new Rectangle(rightX + RimPad, y, MiniMap.PanelWidth, MiniMap.PanelHeight);
        y += MiniMap.PanelHeight + RimPad;
        RightColumn = new Rectangle(rightX, Margin, ColumnWidth, y - Margin);
    }

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(GameEngine engine, Color accent)
    {
        DrawColumn(LeftColumn, accent);
        DrawColumn(RightColumn, accent);

        DrawRecess(BucketSlot);
        DrawRecess(StatsSlot);
        DrawRecess(MapSlot);

        DrawButtons(engine, accent);
    }

    // A brass slab with a bright edge and a thin glowing line just inside it.
    private static void DrawColumn(Rectangle r, Color accent)
    {
        Raylib.DrawRectangleRec(r, Palette.Brass);
        Raylib.DrawRectangleLinesEx(r, 3f, Palette.BrassLight);
        Rectangle inner = new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, r.Height - 12);
        Raylib.DrawRectangleLinesEx(inner, 1f, Raylib.Fade(accent, 0.5f));
    }

    private static void DrawRecess(Rectangle slot)
    {
        Raylib.DrawRectangleRec(new Rectangle(slot.X - 4, slot.Y - 4, slot.Width + 8, slot.Height + 8), Palette.BrassDark);
    }

    // Round brass buttons. The active one is lit with the hologram color.
    private void DrawButtons(GameEngine engine, Color accent)
    {
        Vector2 mouse = Raylib.GetMousePosition();

        for (int i = 0; i < buttonCenters.Length; i++)
        {
            Vector2 c = buttonCenters[i];
            bool lit = i == 0 ? engine.Paused : (!engine.Paused && engine.SimSpeed == Speeds[i - 1]);
            bool hovered = Vector2.Distance(mouse, c) <= ButtonRadius;

            Raylib.DrawCircleV(c, ButtonRadius + 2, Palette.BrassDark);
            Raylib.DrawCircleV(c, ButtonRadius, lit ? Raylib.Fade(accent, 0.8f) : (hovered ? Palette.BrassLight : Palette.Brass));

            string label = i == 0 ? (engine.Paused ? ">" : "II") : SpeedLabels[i - 1];
            int textWidth = UiText.Measure(label, 10);
            Color textColor = lit ? Palette.BrassDark : Palette.Text;
            UiText.Draw(label, (int)(c.X - textWidth / 2f), (int)c.Y - 5, 10, textColor);
        }
    }
}
