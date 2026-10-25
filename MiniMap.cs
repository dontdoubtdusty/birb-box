using System.Numerics;
using Raylib_cs;

// The world map screen at the bottom of the right side column (SidePanels gives it MapSlot).
// It shows food, feeders, gunk, birds and the view square. Click or drag on it to move the view,
// scroll on it to zoom, or use the +/- buttons to jump between the fixed zoom levels.
// While the box shows the store, it becomes a zoo camera: a live look at the birds. Clicking it goes back to them.
public class MiniMap
{
    // LAYOUT
    private const int Pad = 10;
    private const int HeaderSize = 12;
    private const int MapSize = 200;
    private const int ButtonSize = 16;
    public const int PanelWidth = MapSize + Pad * 2;
    public const int PanelHeight = MapSize + Pad * 3 + HeaderSize;

    // Screen rectangles, recalculated every frame so they follow window resizes.
    private Rectangle panelRect, mapArea, zoomOutButton, zoomInButton;
    private bool dragging = false;
    private bool zooCamHovered = false;

    // Map zoom: 1 shows the whole world. It follows the hologram's zoom so the map
    // always shows about MapSpan times the hologram's width (see FollowHologramZoom).
    private const float MapSpan = 3f;
    private float mapZoom = 1f;
    private Vector2 mapFocus = Vector2.Zero;    // world X, Z shown at the middle of the map

    // ---------------- UPDATE (no drawing) ----------------

    public void Update(GameEngine engine, bool mouseBlockedByImGui)
    {
        Layout(engine);
        if (engine.Mode.InStore)
        {
            dragging = false;
            HandleZooCamClick(engine, mouseBlockedByImGui);
            return;
        }
        zooCamHovered = false;
        HandleDragging(engine, mouseBlockedByImGui);
        HandleZoomButtons(engine, mouseBlockedByImGui);
        HandleWheel(engine, mouseBlockedByImGui);
        FollowHologramZoom(engine);
        if (!dragging) KeepViewOnMap(engine.View);
    }

    private void Layout(GameEngine engine)
    {
        panelRect = engine.Sides.MapSlot;
        mapArea = new Rectangle(panelRect.X + Pad, panelRect.Y + Pad * 2 + HeaderSize, MapSize, MapSize);

        // The - and + buttons sit at the right end of the header line.
        float buttonY = panelRect.Y + Pad - 3;
        float right = panelRect.X + panelRect.Width - Pad;
        zoomInButton = new Rectangle(right - ButtonSize, buttonY, ButtonSize, ButtonSize);
        zoomOutButton = new Rectangle(right - ButtonSize * 2 - 4, buttonY, ButtonSize, ButtonSize);
    }

    // In the store, clicking the zoo camera fogs the box back to the birds.
    private void HandleZooCamClick(GameEngine engine, bool mouseBlockedByImGui)
    {
        Vector2 mouse = Raylib.GetMousePosition();
        zooCamHovered = !mouseBlockedByImGui && !engine.Mode.IsSwitching && !engine.Tag.IsMouseOver(engine.Store)
                        && Raylib.CheckCollisionPointRec(mouse, mapArea);

        if (zooCamHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            engine.Mode.SwitchTo(BoxScene.Birds);
        }
    }

    private void HandleDragging(GameEngine engine, bool mouseBlockedByImGui)
    {
        Vector2 mouse = Raylib.GetMousePosition();

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !mouseBlockedByImGui
            && Raylib.CheckCollisionPointRec(mouse, mapArea))
        {
            dragging = true;
        }
        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            dragging = false;
        }

        if (dragging)
        {
            engine.View.JumpTo(MapToWorld(mouse));
            engine.FollowSelected = false;
        }
    }

    // The +/- buttons jump the hologram between its fixed zoom levels.
    private void HandleZoomButtons(GameEngine engine, bool mouseBlockedByImGui)
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left) || mouseBlockedByImGui) return;

        Vector2 mouse = Raylib.GetMousePosition();
        if (Raylib.CheckCollisionPointRec(mouse, zoomInButton)) engine.View.StepZoom(1);
        if (Raylib.CheckCollisionPointRec(mouse, zoomOutButton)) engine.View.StepZoom(-1);
    }

    // Scrolling on the map zooms the hologram, and the map zooms toward the cursor.
    private void HandleWheel(GameEngine engine, bool mouseBlockedByImGui)
    {
        Vector2 mouse = Raylib.GetMousePosition();
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel == 0f || mouseBlockedByImGui || !Raylib.CheckCollisionPointRec(mouse, mapArea)) return;

        Vector2 underCursor = MapToWorld(mouse);
        engine.View.ZoomBy(wheel);
        SetMapZoom(HologramToMapZoom(engine.View), underCursor);
    }

    // When the hologram zoomed some other way (scrolling on it, or the buttons), the map zooms toward the view square.
    private void FollowHologramZoom(GameEngine engine)
    {
        float target = HologramToMapZoom(engine.View);
        if (target != mapZoom) SetMapZoom(target, engine.View.Center);
    }

    // The map shows MapSpan hologram-widths across, but never zooms out past the whole world.
    private static float HologramToMapZoom(ViewWindow view)
    {
        float worldWidth = World.Instance.HalfSize * 2f;
        float shownWidth = view.WorldHalfWidth * 2f * MapSpan;
        return MathF.Max(1f, worldWidth / shownWidth);
    }

    // Changes the zoom while keeping the anchor's world spot at the same place on screen.
    private void SetMapZoom(float newZoom, Vector2 anchorWorld)
    {
        Vector2 anchorOnScreen = WorldToMap(anchorWorld);
        mapZoom = newZoom;
        mapFocus = anchorWorld - (anchorOnScreen - MapAreaCenter) / MapScale;
        ClampMapFocus();
    }

    // When zoomed in, slide the map only as far as needed to keep the whole view square on it.
    private void KeepViewOnMap(ViewWindow view)
    {
        float visibleHalf = World.Instance.HalfSize / mapZoom;
        Vector2 slack = new Vector2(MathF.Max(0f, visibleHalf - view.WorldHalfWidth));
        mapFocus = Vector2.Clamp(mapFocus, view.Center - slack, view.Center + slack);
        ClampMapFocus();
    }

    // Stops the map from scrolling past the edge of the world.
    private void ClampMapFocus()
    {
        float limit = World.Instance.HalfSize - World.Instance.HalfSize / mapZoom;
        mapFocus = Vector2.Clamp(mapFocus, new Vector2(-limit), new Vector2(limit));
    }

    // Converting between world X, Z and screen pixels on the map.
    private float MapScale => MapSize / (World.Instance.HalfSize * 2f) * mapZoom;
    private Vector2 MapAreaCenter => new Vector2(mapArea.X + MapSize / 2f, mapArea.Y + MapSize / 2f);
    private Vector2 WorldToMap(Vector2 worldXZ) => MapAreaCenter + (worldXZ - mapFocus) * MapScale;
    private Vector2 MapToWorld(Vector2 screenPos) => mapFocus + (screenPos - MapAreaCenter) / MapScale;

    // ---------------- DRAW (no simulation changes) ----------------

    public void Draw(GameEngine engine, Color accent)
    {
        Layout(engine);
        UiDraw.Panel(panelRect, accent);
        if (engine.Mode.InStore)
        {
            DrawZooCamera(engine, accent);
            return;
        }
        UiText.Draw("WORLD MAP", (int)panelRect.X + Pad, (int)panelRect.Y + Pad, HeaderSize, accent);

        float zoom = engine.View.Zoom;
        DrawZoomButton(zoomOutButton, false, zoom > ViewWindow.ZoomLevels[0], accent);
        DrawZoomButton(zoomInButton, true, zoom < ViewWindow.ZoomLevels[^1], accent);

        // World X maps to map X, world Z maps to map Y.
        Vector2 ToMap(Vector3 worldPos) => WorldToMap(new Vector2(worldPos.X, worldPos.Z));

        // Scissor mode clips drawing to the map area, so the view square can't spill past the edge.
        Raylib.BeginScissorMode((int)mapArea.X, (int)mapArea.Y, MapSize, MapSize);
        Raylib.DrawRectangleRec(mapArea, new Color(51, 38, 26, 255));

        // Rocks at their real size, under everything else
        foreach (Rock rock in World.Instance.GetAllEntities<Rock>())
        {
            Vector2 center = ToMap(rock.Position);
            float radius = MathF.Max(1.5f, Vector2.Distance(center, ToMap(rock.Position + new Vector3(rock.Radius, 0f, 0f))));
            Raylib.DrawCircleV(center, radius, new Color(120, 114, 104, 255));
        }

        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            Color c = Seeds.Get(food.MainSeed).Color;
            Raylib.DrawRectangleV(ToMap(food.Position) - Vector2.One, new Vector2(2, 2), c);
        }

        foreach (Feeder feeder in World.Instance.GetAllEntities<Feeder>())
        {
            Raylib.DrawRectangleV(ToMap(feeder.Position) - new Vector2(3, 3), new Vector2(6, 6), Palette.BrassLight);
        }

        foreach (Gunk gunk in World.Instance.GetAllEntities<Gunk>())
        {
            Raylib.DrawCircleV(ToMap(gunk.Position), 2f, Gunk.Color);
        }

        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (!World.Instance.IsInside(bird.Position)) continue;
            Raylib.DrawCircleV(ToMap(bird.Position), 2f, bird.Info.Color);
        }

        if (engine.SelectedBird != null)
        {
            Vector2 p = ToMap(engine.SelectedBird.Position);
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, 5f, accent);
        }

        // The view window: the cube's sides run along world X and Z, so it's a plain square on the map
        ViewWindow view = engine.View;
        Vector2 mapCenter = ToMap(new Vector3(view.Center.X, 0, view.Center.Y));
        float mapHalf = view.WorldHalfWidth * MapScale;
        Rectangle square = new Rectangle(mapCenter.X - mapHalf, mapCenter.Y - mapHalf, mapHalf * 2f, mapHalf * 2f);
        Raylib.DrawRectangleLinesEx(square, 1.5f, accent);
        DrawLookNotch(mapCenter, mapHalf, view.LookDirection, accent);
        Raylib.EndScissorMode();
    }

    // The renderer's zoo camera texture, with a blinking REC light and a LIVE label over it.
    private void DrawZooCamera(GameEngine engine, Color accent)
    {
        UiText.Draw("ZOO CAM", (int)panelRect.X + Pad, (int)panelRect.Y + Pad, HeaderSize, accent);

        // Render textures are stored upside down, so the source height is negative.
        Texture2D feed = engine.Renderer.FeedTexture.Texture;
        Rectangle source = new Rectangle(0, 0, feed.Width, -feed.Height);
        Raylib.DrawTexturePro(feed, source, mapArea, Vector2.Zero, 0f, Color.White);
        Raylib.DrawRectangleLinesEx(mapArea, zooCamHovered ? 2f : 1f, Raylib.Fade(accent, zooCamHovered ? 1f : 0.4f));

        int x = (int)mapArea.X + 8, y = (int)mapArea.Y + 8;
        bool blinkOn = (int)(Raylib.GetTime() * 2) % 2 == 0;
        if (blinkOn) Raylib.DrawCircle(x + 4, y + 6, 4f, new Color(230, 60, 50, 255));
        UiText.Draw("REC", x + 12, y, HeaderSize, Palette.Text);
        int bottom = (int)(mapArea.Y + mapArea.Height);
        if (zooCamHovered)
        {
            const string Hint = "CLICK TO RETURN";
            Raylib.DrawRectangle((int)mapArea.X, bottom - 24, (int)mapArea.Width, 24, Raylib.Fade(Palette.PanelDark, 0.85f));
            UiText.Draw(Hint, (int)(mapArea.X + (mapArea.Width - UiText.Measure(Hint, HeaderSize)) / 2), bottom - 18, HeaderSize, accent);
        }
        else
        {
            UiText.Draw("LIVE", x, bottom - 20, HeaderSize, Palette.DimText);
        }
    }

    // A small triangle on the view square's edge, pointing the way the camera is looking.
    // World X, Z map straight to map X, Y, so the world direction works on the map as-is.
    private static void DrawLookNotch(Vector2 center, float halfSide, Vector2 direction, Color accent)
    {
        const float Length = 7f, HalfWidth = 4.5f;
        // How far from the middle the square's edge is in this direction
        float radius = halfSide / MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
        Vector2 side = new Vector2(-direction.Y, direction.X);
        Vector2 tip = center + direction * (radius + Length);
        Vector2 baseMid = center + direction * (radius - 1f);

        // Raylib only fills triangles listed counter-clockwise on screen: tip, then left, then right.
        Raylib.DrawTriangle(tip, baseMid - side * HalfWidth, baseMid + side * HalfWidth, accent);
    }

    // A small square button with a + or - drawn from bars. Dimmed when there's no further level that way.
    private static void DrawZoomButton(Rectangle r, bool isPlus, bool enabled, Color accent)
    {
        bool hovered = enabled && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
        Color color = enabled ? accent : Raylib.Fade(accent, 0.3f);

        if (hovered) Raylib.DrawRectangleRec(r, Raylib.Fade(accent, 0.2f));
        Raylib.DrawRectangleLinesEx(r, 1f, color);

        int cx = (int)(r.X + r.Width / 2), cy = (int)(r.Y + r.Height / 2);
        Raylib.DrawRectangle(cx - 4, cy - 1, 8, 2, color);
        if (isPlus) Raylib.DrawRectangle(cx - 1, cy - 4, 2, 8, color);
    }
}
