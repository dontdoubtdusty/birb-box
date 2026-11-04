using System.Numerics;
using Raylib_cs;

// The shopkeeper's ledger: click the book on the counter and a panel opens where you swap feathers,
// FeatherPurse.ExchangeRate of one kind for one of another. Pick what to give (a kind you hold enough of) and what to
// get (any kind you've collected), then press Trade. It stays until you close it or click something else in the store.
// Like the price tag it is a stand-in for something in the room; the book itself is on the counter (StoreRenderer).
// This class holds the panel's state, places it and handles its clicks; Draw only draws.
public class Ledger
{
    private const int Width = 360, Pad = 10;
    private const int RowHeight = 26, MaxRows = 8;
    private const int HeaderHeight = 92, FooterHeight = 46;
    private const int ButtonHeight = 26, ButtonGap = 6;
    private const int SmallText = 12, BigText = 20, RowText = 14;

    public bool IsOpen { get; private set; }

    private Species? give, get;
    private Vector2 anchor;                    // where the panel hangs: to the right of where the book was clicked
    private int scroll;                        // the first row shown, when there are more kinds than fit

    private Rectangle rect, tradeRect, closeRect, giveColumn, getColumn;

    // Opens the panel near the click and starts a fresh trade.
    public void Open(Vector2 point)
    {
        IsOpen = true;
        anchor = point;
        give = null;
        get = null;
        scroll = 0;
        Layout();
    }

    public void Close()
    {
        IsOpen = false;
    }

    public bool IsMouseOver()
    {
        return IsOpen && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
    }

    // Works out where everything is, for the number of kinds held. Called before every click and draw.
    private void Layout()
    {
        int rows = Math.Clamp(GameEngine.Instance.Feathers.CollectedCount, 1, MaxRows);
        int height = HeaderHeight + rows * RowHeight + FooterHeight;

        float x = Math.Clamp(anchor.X + 24, 8, Raylib.GetScreenWidth() - Width - 8);
        float y = Math.Clamp(anchor.Y - height / 2f, 8, Raylib.GetScreenHeight() - height - 8);
        rect = new Rectangle(x, y, Width, height);

        float columnWidth = (Width - Pad * 3) / 2f;
        giveColumn = new Rectangle(x + Pad, y + HeaderHeight, columnWidth, rows * RowHeight);
        getColumn = new Rectangle(x + Pad * 2 + columnWidth, y + HeaderHeight, columnWidth, rows * RowHeight);

        float buttonWidth = (Width - Pad * 2 - ButtonGap) / 2f;
        float buttonY = y + height - Pad - ButtonHeight;
        tradeRect = new Rectangle(x + Pad, buttonY, buttonWidth, ButtonHeight);
        closeRect = new Rectangle(x + Pad + buttonWidth + ButtonGap, buttonY, buttonWidth, ButtonHeight);
    }

    // ---------------- UPDATE (no drawing) ----------------

    public void Update(SeedStore store)
    {
        if (!IsOpen) return;
        Layout();

        List<Species> kinds = GameEngine.Instance.Feathers.Collected();
        if (IsMouseOver() && kinds.Count > MaxRows)
        {
            scroll = Math.Clamp(scroll - (int)Raylib.GetMouseWheelMove(), 0, kinds.Count - MaxRows);
        }
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        Vector2 mouse = Raylib.GetMousePosition();
        FeatherPurse purse = GameEngine.Instance.Feathers;

        if (Raylib.CheckCollisionPointRec(mouse, closeRect))
        {
            Close();
        }
        else if (Raylib.CheckCollisionPointRec(mouse, tradeRect))
        {
            Trade(store);
        }
        else if (Raylib.CheckCollisionPointRec(mouse, giveColumn))
        {
            Species? picked = RowAt(kinds, giveColumn, mouse);
            if (picked != null && purse.CountOf(picked) >= FeatherPurse.ExchangeRate)
            {
                give = picked;
                if (ReferenceEquals(get, give)) get = null;
            }
        }
        else if (Raylib.CheckCollisionPointRec(mouse, getColumn))
        {
            Species? picked = RowAt(kinds, getColumn, mouse);
            if (picked != null && !ReferenceEquals(picked, give)) get = picked;
        }
    }

    // The kind in the row under the mouse, or null.
    private Species? RowAt(List<Species> kinds, Rectangle column, Vector2 mouse)
    {
        int index = scroll + (int)((mouse.Y - column.Y) / RowHeight);
        return index >= 0 && index < kinds.Count ? kinds[index] : null;
    }

    private bool CanTrade()
    {
        return give != null && get != null && !ReferenceEquals(give, get)
            && GameEngine.Instance.Feathers.CountOf(give) >= FeatherPurse.ExchangeRate;
    }

    // Swaps the feathers, tells the log, and makes the keeper happy.
    private void Trade(SeedStore store)
    {
        if (!CanTrade()) return;
        Species giving = give!, getting = get!;
        if (!GameEngine.Instance.Feathers.Exchange(giving, getting)) return;

        EventLog.Add($"Traded {FeatherPurse.ExchangeRate} {giving.FeatherName} for 1 {getting.FeatherName}.");
        store.Keeper.Celebrate();
        if (GameEngine.Instance.Feathers.CountOf(giving) < FeatherPurse.ExchangeRate) give = null;
    }

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(SeedStore store, Camera3D camera, Color accent)
    {
        if (!IsOpen) return;
        Layout();

        FeatherPurse purse = GameEngine.Instance.Feathers;
        List<Species> kinds = purse.Collected();
        int x = (int)rect.X, y = (int)rect.Y;

        // The string runs from the book on the counter to the panel's hole
        Vector2 bookOnScreen = Raylib.GetWorldToScreen(store.ToScene(store.LedgerBook.Center), camera);
        Vector2 hole = new Vector2(x + 14, y + 14);
        Raylib.DrawLineEx(bookOnScreen, hole, 1.5f, Raylib.Fade(accent, 0.7f));
        Raylib.DrawCircleV(bookOnScreen, 3f, accent);

        UiDraw.Panel(rect, accent);
        Raylib.DrawCircleV(hole, 5f, Palette.PanelDark);
        Raylib.DrawCircleLinesV(hole, 5f, accent);

        UiText.Draw("THE LEDGER", x + 26, y + Pad, SmallText, accent);
        UiText.Draw("Trade feathers", x + Pad, y + Pad + 18, BigText, Palette.BrassLight);
        UiText.Draw($"{FeatherPurse.ExchangeRate} of one kind for 1 of another.", x + Pad, y + Pad + 44, SmallText, Palette.DimText);

        if (kinds.Count == 0)
        {
            UiText.Draw("You hold no feathers yet.", x + Pad, y + HeaderHeight + 4, RowText, Palette.DimText);
        }
        else
        {
            UiText.Draw($"GIVE {FeatherPurse.ExchangeRate}", (int)giveColumn.X, (int)giveColumn.Y - 16, SmallText, accent);
            UiText.Draw("GET 1", (int)getColumn.X, (int)getColumn.Y - 16, SmallText, accent);
            DrawColumn(kinds, giveColumn, accent, purse, forGiving: true);
            DrawColumn(kinds, getColumn, accent, purse, forGiving: false);
        }

        DrawAction(tradeRect, "Trade", CanTrade(), accent);
        UiDraw.Button(closeRect, "Close", accent);
    }

    // One list of kinds: a dot in the species' color, the feather's name and how many are held.
    private void DrawColumn(List<Species> kinds, Rectangle column, Color accent, FeatherPurse purse, bool forGiving)
    {
        Vector2 mouse = Raylib.GetMousePosition();
        int shown = Math.Min(MaxRows, kinds.Count);
        for (int row = 0; row < shown; row++)
        {
            Species species = kinds[scroll + row];
            Rectangle r = new Rectangle(column.X, column.Y + row * RowHeight, column.Width, RowHeight - 2);

            int count = purse.CountOf(species);
            bool usable = forGiving ? count >= FeatherPurse.ExchangeRate : !ReferenceEquals(species, give);
            bool picked = ReferenceEquals(species, forGiving ? give : get);

            if (picked) Raylib.DrawRectangleRec(r, Raylib.Fade(accent, 0.35f));
            else if (usable && Raylib.CheckCollisionPointRec(mouse, r)) Raylib.DrawRectangleRec(r, Raylib.Fade(accent, 0.18f));

            float fade = usable ? 1f : 0.4f;
            Raylib.DrawCircleV(new Vector2(r.X + 11, r.Y + r.Height / 2f), 5f, Raylib.Fade(species.Color, fade));

            string number = count.ToString();
            int numberWidth = UiText.Measure(number, RowText);
            string name = Fit(species.FeatherName, RowText, (int)r.Width - 26 - numberWidth - 8);
            UiText.Draw(name, (int)r.X + 24, (int)r.Y + 4, RowText, Raylib.Fade(Palette.Text, fade));
            UiText.Draw(number, (int)(r.X + r.Width - numberWidth - 6), (int)r.Y + 4, RowText, Raylib.Fade(Palette.DimText, fade));
        }
    }

    // A flat button that dims when it can't be used.
    private static void DrawAction(Rectangle r, string label, bool enabled, Color accent)
    {
        if (enabled)
        {
            UiDraw.Button(r, label, accent);
            return;
        }
        Raylib.DrawRectangleRec(r, Raylib.Fade(accent, 0.08f));
        int width = UiText.Measure(label, 20);
        UiText.Draw(label, (int)(r.X + (r.Width - width) / 2), (int)(r.Y + (r.Height - 20) / 2), 20, Raylib.Fade(Palette.DimText, 0.6f));
    }

    // The text cut short with ".." so it fits in this many pixels.
    private static string Fit(string text, int size, int maxWidth)
    {
        if (UiText.Measure(text, size) <= maxWidth) return text;
        while (text.Length > 1 && UiText.Measure(text + "..", size) > maxWidth) text = text[..^1];
        return text + "..";
    }
}
