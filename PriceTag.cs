using System.Numerics;
using Raylib_cs;

// The price tag for the store item you clicked: a see-through tag with a punched hole,
// tied back to the item by a glowing string. It stays where it was hung until
// you press Buy or Cancel (or click something else in the store).
// The item it describes is SeedStore.SelectedItem; this class only places and draws the tag and handles its buttons.
public class PriceTag
{
    private const int Width = 220, Height = 132, Pad = 10;
    private const int ButtonHeight = 26, ButtonGap = 6;
    private const int SmallText = 12, BigText = 20;
    private static readonly Color Warning = new Color(232, 128, 104, 255);

    private Rectangle rect, buyRect, cancelRect;

    // Hangs the tag just to the right of where the player clicked, kept on screen.
    public void HangAt(Vector2 point)
    {
        float x = Math.Clamp(point.X + 24, 8, Raylib.GetScreenWidth() - Width - 8);
        float y = Math.Clamp(point.Y - Height / 2f, 8, Raylib.GetScreenHeight() - Height - 8);
        rect = new Rectangle(x, y, Width, Height);

        float buttonWidth = (Width - Pad * 2 - ButtonGap) / 2f;
        float buttonY = y + Height - Pad - ButtonHeight;
        buyRect = new Rectangle(x + Pad, buttonY, buttonWidth, ButtonHeight);
        cancelRect = new Rectangle(x + Pad + buttonWidth + ButtonGap, buttonY, buttonWidth, ButtonHeight);
    }

    public bool IsMouseOver(SeedStore store)
    {
        return store.SelectedItem != null && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
    }

    // ---------------- UPDATE (no drawing) ----------------

    public void Update(SeedStore store)
    {
        ShelfItem? item = store.SelectedItem;
        if (item == null || !Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        Vector2 mouse = Raylib.GetMousePosition();
        if (Raylib.CheckCollisionPointRec(mouse, buyRect))
        {
            store.TryBuy(item);
        }
        else if (Raylib.CheckCollisionPointRec(mouse, cancelRect))
        {
            store.SelectedItem = null;
        }
    }

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(SeedStore store, Camera3D camera, Color accent)
    {
        ShelfItem? item = store.SelectedItem;
        if (item == null) return;

        int coins = GameEngine.Instance.Coins;
        int x = (int)rect.X, y = (int)rect.Y;

        // The string runs from the item on the shelf to the tag's hole, so it follows the item if the store turns
        Vector2 itemOnScreen = Raylib.GetWorldToScreen(store.ToScene(item.Center), camera);
        Vector2 hole = new Vector2(x + 14, y + 14);
        Raylib.DrawLineEx(itemOnScreen, hole, 1.5f, Raylib.Fade(accent, 0.7f));
        Raylib.DrawCircleV(itemOnScreen, 3f, accent);

        UiDraw.Panel(rect, accent);
        Raylib.DrawCircleV(hole, 5f, Palette.PanelDark);
        Raylib.DrawCircleLinesV(hole, 5f, accent);

        // What it is, and what you get
        if (item.IsFeeder)
        {
            FeederInfo feeder = Feeders.Get(item.Feeder);
            UiText.Draw("FEEDER", x + 26, y + Pad, SmallText, accent);
            UiText.Draw(feeder.Name, x + Pad, y + Pad + 18, BigText, Palette.BrassLight);
            UiText.Draw($"Holds {feeder.Capacity:0} seed", x + Pad, y + Pad + 48, SmallText, Palette.DimText);
        }
        else
        {
            SeedInfo info = Seeds.Get(item.Seed);
            UiText.Draw($"{item.KindName.ToUpper()} OF {info.Name.ToUpper()}", x + 26, y + Pad, SmallText, accent);
            UiText.Draw(info.ProductName, x + Pad, y + Pad + 18, BigText, info.Color);
            UiText.Draw($"{item.Amount:0} seed", x + Pad, y + Pad + 48, SmallText, Palette.DimText);
        }

        string price = $"{item.Price} coin{(item.Price == 1 ? "" : "s")}";
        UiText.Draw(price, x + Width - Pad - UiText.Measure(price, BigText), y + Pad + 44, BigText, Palette.Text);

        if (coins < item.Price)
        {
            UiText.Draw($"You have {coins}.", x + Pad, y + Pad + 66, SmallText, Warning);
        }
        else if (item.IsFeeder)
        {
            UiText.Draw(Feeders.Get(item.Feeder).Label, x + Pad, y + Pad + 66, SmallText, Palette.DimText);
        }
        else if (item.IsSack)
        {
            string deal = $"{ShelfItem.SackAmount / ShelfItem.JarAmount:0} jars' worth for the price of {ShelfItem.SackPriceInJars}";
            UiText.Draw(deal, x + Pad, y + Pad + 66, SmallText, Palette.DimText);
        }

        UiDraw.Button(buyRect, "Buy", accent);
        UiDraw.Button(cancelRect, "Cancel", accent);
    }
}
