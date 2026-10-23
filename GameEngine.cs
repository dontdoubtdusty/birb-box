using System.Numerics;
using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;

public class GameEngine
{
    public static GameEngine Instance { get; private set; } = null!;

    // Shown in the window title and printed on top of the pedestal's bezel ring. Bump it as the build changes.
    public const string Version = "Birb Box 4.48 Alpha";

    // SYSTEMS
    public World World { get; private set; }
    public BirdSpawner BirdSpawner { get; private set; }
    public FoodSpawner FoodSpawner { get; private set; }
    public HoloCamera Camera { get; private set; }
    public Dial Dial { get; private set; }
    public ViewWindow View { get; private set; }
    public HologramRenderer Renderer { get; private set; }
    public Hud Hud { get; private set; }
    public MiniMap Map { get; private set; }
    public BucketPanel Bucket { get; private set; }
    public SidePanels Sides { get; private set; }
    public UIHandler UI { get; private set; }
    public PauseMenu Menu { get; private set; }
    public BoxMode Mode { get; private set; }
    public SeedStore Store { get; private set; }
    public PriceTag Tag { get; private set; }
    public BirbDesigner Designer { get; private set; }
    public FeederPlacer Placer { get; private set; }
    public Radio Radio { get; private set; }
    public Projector Projector { get; private set; }

    // SIMULATION STATE
    public bool Paused = false;
    public float SimSpeed = 1f;
    public float SimTime { get; private set; }

    // OFF-SCREEN BIRDS update less often, with a bigger time step, to save work.
    private const int OffscreenStep = 3;           // update every 3rd frame
    private const float OffscreenMargin = 2f;      // past the hologram's edge plus this much fog
    private long simFrame = 0;

    // PERFORMANCE (shown in the tools panel)
    public float UpdateMs { get; private set; }    // time spent in Update(), smoothed
    public float DrawMs { get; private set; }      // time spent issuing Draw() on the CPU, smoothed (the wait for the screen is left out)
    private readonly System.Diagnostics.Stopwatch updateClock = new();
    private readonly System.Diagnostics.Stopwatch drawClock = new();

    // ECONOMY
    public int Coins = 0;
    public bool HasScraper = false;    // bought once at the store; lets clicks scrape up gunk

    // POURING (hold right mouse over the floor)
    private const float PileSpacing = 0.35f;     // cursor travel between piles
    private const float PileInterval = 0.25f;    // seconds between piles while holding still
    private Vector3? lastPilePoint;              // box space, null when not pouring
    private float pileTimer = 0f;
    private bool warnedEmptyBucket = false;

    // SELECTION. Choosing a different bird (or none) always switches following off.
    private Bird? selectedBird;
    public Bird? SelectedBird
    {
        get => selectedBird;
        set
        {
            if (value != selectedBird) FollowSelected = false;
            selectedBird = value;
        }
    }
    public Bird? HoveredBird;          // set by the UI when hovering a name in the tracker

    // PLACING FEEDERS: where a carried feeder would go (box space), or null when the mouse isn't over clear floor
    public Vector3? PlacingSpot { get; private set; }
    public bool FollowSelected = false;

    // WINDOW
    private const int StartWidth = 1280;
    private const int StartHeight = 720;
    public bool IsFullscreen { get; private set; }
    // True fullscreen takes over the monitor, which keeps Windows popups (like the volume bar) off the game.
    // Off means a borderless window the size of the monitor, which Alt-Tabs faster.
    public bool TrueFullscreen { get; private set; } = false;
    private Vector2 windowedPos;     // where the window was before going fullscreen,
    private Vector2 windowedSize;    // so it can go back there

    // SCREENSHOTS (F9). Update asks for one, Draw takes it once the frame is finished.
    // Not F12: Raylib has its own built-in F12 screenshot that can't be switched off.
    private bool screenshotRequested = false;

    public GameEngine()
    {
        Instance = this;
        World = new World();
        BirdSpawner = new BirdSpawner();
        FoodSpawner = new FoodSpawner();
        Camera = new HoloCamera();
        Dial = new Dial();
        View = new ViewWindow();
        Renderer = new HologramRenderer();
        Hud = new Hud();
        Map = new MiniMap();
        Bucket = new BucketPanel();
        Sides = new SidePanels();
        UI = new UIHandler();
        Menu = new PauseMenu();
        Mode = new BoxMode();
        Store = new SeedStore();
        Tag = new PriceTag();
        Designer = new BirbDesigner();
        Placer = new FeederPlacer();
        Radio = new Radio();
        Projector = new Projector();
    }

    public void Run()
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(StartWidth, StartHeight, Version);
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);    // Esc opens the menu instead of quitting

        Image icon = Raylib.LoadImage(Assets.PathTo("icon.png"));
        Raylib.SetWindowIcon(icon);
        Raylib.UnloadImage(icon);

        Sounds.Load();
        Radio.Load();
        Renderer.Load(View);
        BirbBuilder.Load(Renderer.HoloShader);

        // Fonts added between Begin and End come before ImGui's built-in one, so the first becomes the default.
        rlImGui.BeginInitImGui();
        ImGui.StyleColorsDark();
        UIHandler.LoadFont();
        rlImGui.EndInitImGui();
        UI.ApplyStyle();

        SeedStartingWorld();

        while (!Raylib.WindowShouldClose() && !Menu.QuitRequested)
        {
            float dt = Raylib.GetFrameTime();
            Update(dt);
            Draw();
        }

        rlImGui.Shutdown();
        Bucket.Unload();
        Renderer.Unload();
        BirbBuilder.Unload();
        UiText.Unload();
        Radio.Unload();
        Sounds.Unload();
        Raylib.CloseWindow();
    }

    // The world starts with wild seed and no birds; every bird arrives because of seed.
    private void SeedStartingWorld()
    {
        FoodSpawner.ScatterAcrossWorld(40);
        FoodSpawner.ScatterInView(6, View);
        World.FlushChanges();
    }

    // ---------------- UPDATE (no drawing) ----------------

    private void Update(float dt)
    {
        updateClock.Restart();

        // The hum runs even while paused; it just ducks quieter behind the menu or the Designer, and follows the power.
        // Bird sounds fade out while the store is showing or the box is off.
        Sounds.Update(dt, Menu.IsOpen || Designer.IsOpen, Mode.Showing == BoxScene.Birds && Projector.IsOn, Projector.Lens);
        Radio.Update(dt, ducked: Menu.IsOpen);    // keeps playing, quieter, behind the Esc menu
        Projector.Update(dt);                     // keeps starting up or shutting down behind the menu too

        // The mouse is "blocked" for the hologram when it's over an ImGui window, the bird card, or a side column.
        // The keyboard is blocked while typing, either a bird's name or in an ImGui box.
        Sides.Layout();
        bool typingName = Hud.IsEditingName;
        Hud.UpdateTyping(this);
        bool mouseOverImGui = ImGui.GetIO().WantCaptureMouse;
        bool mouseBlocked = mouseOverImGui || Hud.IsMouseOver(this) || Sides.IsMouseOver() || Tag.IsMouseOver(Store);
        bool keyboardBlocked = typingName || ImGui.GetIO().WantCaptureKeyboard;

        if (!keyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.F))
        {
            ToggleFullscreen();
        }
        // Only typing blocks screenshots, not a focused slider or panel
        bool typingAnywhere = typingName || ImGui.GetIO().WantTextInput;
        if (!typingAnywhere && Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            screenshotRequested = true;
        }

        // While the menu is open it takes all input and the rest of the box waits.
        // (Skipped while typing a name, so the Esc that cancels the name doesn't also open the menu.)
        if (!typingName) Menu.Update(this);
        if (Menu.IsOpen) return;

        // The radio on the table has the mouse while it's over the radio or turning one of its knobs
        Radio.HandleInput(MouseRayInRoom(), mouseBlocked);
        mouseBlocked |= Radio.UsingMouse;

        // The logo on the pedestal's front is the power button
        bool mouseOverLogo = !mouseBlocked && Pedestal.RayHitsLogo(MouseRayInRoom(), View);
        Projector.LogoHovered = mouseOverLogo;

        // The Birb Designer takes over the hologram while it's open (see BirbDesigner.cs).
        // Its birb is shown in the bird scene, so if it's opened from the store (B or the Esc menu),
        // the box fogs back to the birbs first.
        if (!keyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.B))
        {
            Designer.Toggle(this);
        }
        if (Designer.IsOpen)
        {
            Projector.TurnOn();    // the Designer shows its birb in the box, so the box has to be on
            Mode.SwitchTo(BoxScene.Birds);
            if (Mode.Update(dt)) OnSceneSwapped();
            UpdateDesigning(dt, mouseBlocked);
            return;
        }

        if (!keyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            Paused = !Paused;
        }
        if (!keyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            UI.ShowTools = !UI.ShowTools;
        }
        // The box shows either the bird world or the store; the sign floating above it switches.
        // While the store is showing, the birds ignore the mouse and WASD (see ClickInStore for its clicks).
        // Nothing in the box takes the mouse or WASD until the projector is fully on.
        bool boxReady = Projector.Ready;
        bool mouseOverSign = !mouseBlocked && boxReady && BoxMode.RayHitsSign(MouseRayInHologram(), View);
        Mode.SignHovered = mouseOverSign;
        if (Mode.Update(dt)) OnSceneSwapped();
        bool inStore = Mode.InStore;
        bool birdsShowing = Mode.Showing == BoxScene.Birds && !Mode.IsSwitching && boxReady;
        if (!Projector.IsOn) Store.SelectedItem = null;    // no price tag hanging off a switched-off box

        Store.Update(dt, showing: inStore);
        Store.UpdateHover(MouseRayInHologram(), active: inStore && boxReady && !Mode.IsSwitching && !mouseBlocked && !mouseOverSign);
        Tag.Update(Store);

        // The wheel zooms the hologram (and map) when the mouse is over the birds, otherwise the outer camera.
        bool mouseOverHologram = !mouseBlocked && birdsShowing && !mouseOverSign && View.RayHitsHologram(MouseRayInBox());
        Camera.Update(mouseBlocked, mouseOverHologram);
        if (mouseOverHologram) View.ZoomBy(Raylib.GetMouseWheelMove());

        // A sideways drag turns the bezel ring, and with it whatever the box is showing (see Dial)
        if (inStore) Dial.Turn(ref Store.Yaw, dt, Camera.TurnThisFrame, Camera.IsHolding);
        else Dial.Turn(ref View.Yaw, dt, Camera.TurnThisFrame, Camera.IsHolding);

        if (!keyboardBlocked && birdsShowing && View.HandlePanInput(dt))
        {
            FollowSelected = false;    // manual panning takes over from following
        }

        Placer.Update(birdsShowing);
        PlacingSpot = Placer.Carried != null && birdsShowing && !mouseBlocked && !mouseOverSign ? FloorPointUnderMouse() : null;

        if (Camera.ClickedThisFrame && !mouseBlocked)
        {
            if (mouseOverLogo) Projector.Toggle();
            else if (mouseOverSign) Mode.Toggle();
            else if (inStore && boxReady) ClickInStore();
            else if (birdsShowing) HandleClick();
        }
        UpdatePouring(dt, mouseBlocked || !birdsShowing || mouseOverSign);

        if (!Paused)
        {
            float simDt = dt * SimSpeed;
            SimTime += simDt;

            UpdateEntities(simDt);
            CheckFrostcrestSeen();
            BirdSpawner.Update(simDt);
            FoodSpawner.Update(simDt);
        }

        World.FlushChanges();

        // Forget birds that have left the world
        if (SelectedBird != null && !World.Contains(SelectedBird)) SelectedBird = null;
        if (HoveredBird != null && !World.Contains(HoveredBird)) HoveredBird = null;

        Sides.Update(this, mouseOverImGui);
        Hud.Update(this, mouseOverImGui);
        Map.Update(this, mouseOverImGui);
        Renderer.ChatMessages.Update(View, dt);
        Bucket.Update(dt, mouseOverImGui);

        if (FollowSelected && SelectedBird != null && !inStore)
        {
            View.Follow(SelectedBird.Position, dt);
        }

        // Smoothed so the number is readable instead of flickering
        float ms = (float)updateClock.Elapsed.TotalMilliseconds;
        UpdateMs += (ms - UpdateMs) * 0.05f;
    }

    // While designing, dragging still turns and tilts the hologram and the wheel moves the camera.
    // Nothing else in the box reacts: no panning, clicking, pouring or simulation.
    private void UpdateDesigning(float dt, bool mouseBlocked)
    {
        Camera.Update(mouseBlocked, wheelUsedElsewhere: false);
        Dial.Turn(ref View.Yaw, dt, Camera.TurnThisFrame, Camera.IsHolding);
        Designer.Update(dt);
    }

    // The first Frostcrest to enter the view unlocks their other tastes (see Seeds.FrostcrestSeen).
    private void CheckFrostcrestSeen()
    {
        if (Seeds.FrostcrestSeen) return;

        foreach (Bird bird in World.GetAllEntities<Bird>())
        {
            if (bird.Type == BirdType.Frostcrest && bird.State != BirdState.Leaving && View.Contains(bird.Position))
            {
                Seeds.FrostcrestSeen = true;
                EventLog.Add("A Frostcrest has been spotted! Now that they know this place, corn may draw them in too.");
                return;
            }
        }
    }

    // Birds far outside the view take turns: each one updates every OffscreenStep frames
    // with OffscreenStep times the time step, so it still covers the same ground.
    private void UpdateEntities(float simDt)
    {
        simFrame++;
        foreach (Entity entity in World.Entities)
        {
            bool farOffscreen = entity is Bird && !View.Contains(entity.Position, OffscreenMargin);
            if (!farOffscreen)
            {
                entity.Update(simDt);
            }
            else if ((simFrame + entity.EntityID) % OffscreenStep == 0)
            {
                entity.Update(simDt * OffscreenStep);
            }
        }
    }

    // Called on the frame the fog is thickest and the box changes scene.
    private void OnSceneSwapped()
    {
        if (Mode.InStore) Store.OnEnter();
        else Store.OnLeave();
    }

    // In the store, clicking a jar or sack hangs its price tag where you clicked.
    // Clicking anything else just closes an open tag; only the sign leaves the store.
    private void ClickInStore()
    {
        if (Store.HoveredItem != null)
        {
            Store.SelectedItem = Store.HoveredItem;
            Tag.HangAt(Raylib.GetMousePosition());
        }
        else
        {
            Store.SelectedItem = null;
        }
    }

    // A click sets down a carried feeder. Otherwise it picks up a coin, selects a bird, picks up a feeder
    // to move it, or scrapes up gunk. Clicking empty floor deselects.
    private void HandleClick()
    {
        if (Placer.Carried != null)
        {
            if (PlacingSpot != null) Placer.TryPlace(View.ToWorld(PlacingSpot.Value));
            return;
        }

        Ray ray = MouseRayInBox();

        Coin? hitCoin = PickCoin(ray);
        if (hitCoin != null)
        {
            hitCoin.Collect();
            return;
        }

        Bird? hitBird = PickBird(ray);
        if (hitBird != null)
        {
            SelectedBird = hitBird;
            hitBird.Call();
            return;
        }

        Feeder? hitFeeder = PickFeeder(ray);
        if (hitFeeder != null)
        {
            Placer.PickUp(hitFeeder);
            return;
        }

        Gunk? hitGunk = PickGunk();
        if (hitGunk != null)
        {
            if (HasScraper) hitGunk.ScrapeUp();
            else EventLog.Add("That gunk won't come up by hand. The Gilded Granary sells a scraper.");
            return;
        }

        SelectedBird = null;
    }

    // Holding right mouse pours the bucket onto the floor under the cursor.
    // A pile drops each time the cursor moves a little, or on a timer while it holds still.
    private void UpdatePouring(float dt, bool mouseBlocked)
    {
        if (!Raylib.IsMouseButtonDown(MouseButton.Right) || mouseBlocked)
        {
            lastPilePoint = null;
            warnedEmptyBucket = false;
            return;
        }

        Vector3? point = FloorPointUnderMouse();
        if (point == null) return;

        pileTimer -= dt;
        bool movedFar = lastPilePoint == null || Vector3.Distance(point.Value, lastPilePoint.Value) >= PileSpacing;
        if (!movedFar && pileTimer > 0f) return;

        lastPilePoint = point;
        pileTimer = PileInterval;

        if (Bucket.Bucket.IsEmpty)
        {
            if (!warnedEmptyBucket) EventLog.Add("The seed bucket is empty. Click a seed bag on the bucket panel to fill it.");
            warnedEmptyBucket = true;
            return;
        }
        if (!FoodSpawner.HasRoom) return;

        Dictionary<SeedType, float> scoop = Bucket.Bucket.Take(FoodSpawner.PileAmount);
        FoodSpawner.ThrowFood(View.ToWorld(point.Value), View.WorldHeight, scoop);
    }

    // Where the mouse ray hits the floor, in box space.
    // Only the clear middle of the cube counts, not the foggy edges and corners.
    private Vector3? FloorPointUnderMouse()
    {
        Ray ray = MouseRayInBox();
        if (MathF.Abs(ray.Direction.Y) < 0.0001f) return null;

        float t = -ray.Position.Y / ray.Direction.Y;
        if (t < 0) return null;
        Vector3 localPoint = ray.Position + ray.Direction * t;

        float limit = MathF.Max(0.85f, View.FogStart);
        if (ViewWindow.EdgeAmount(localPoint.X, localPoint.Z, View.HalfWidth) > limit) return null;
        return localPoint;
    }

    // The ray from the camera through the mouse, turned back into box space,
    // so it can be tested against things where they sit in the un-turned cube.
    private Ray MouseRayInBox()
    {
        Ray sceneRay = MouseRayInHologram();
        return new Ray(View.SceneToBox(sceneRay.Position), View.SceneToBox(sceneRay.Direction));
    }

    // The ray from the camera through the mouse, among the things in the room (the radio, the pedestal).
    private Ray MouseRayInRoom()
    {
        return Raylib.GetScreenToWorldRay(Raylib.GetMousePosition(), Camera.Camera);
    }

    // The same ray in the hologram's own space, where its floor is at height 0 (see HoloCamera.HoloLift).
    private Ray MouseRayInHologram()
    {
        return Raylib.GetScreenToWorldRay(Raylib.GetMousePosition(), Camera.HologramCamera);
    }

    private Coin? PickCoin(Ray ray)
    {
        Coin? best = null;
        float bestDist = float.MaxValue;

        foreach (Coin coin in World.GetAllEntities<Coin>())
        {
            if (!View.Contains(coin.Position)) continue;

            // A bigger target than the coin itself, so it's easy to click
            Vector3 localCenter = View.ToLocal(coin.Position);
            RayCollision hit = Raylib.GetRayCollisionSphere(ray, localCenter, Coin.Radius * 3f * View.Zoom);
            if (hit.Hit && hit.Distance < bestDist)
            {
                best = coin;
                bestDist = hit.Distance;
            }
        }
        return best;
    }

    // The nearest feeder the mouse ray passes through, tested against a box from its foot up to its top.
    private Feeder? PickFeeder(Ray ray)
    {
        Feeder? best = null;
        float bestDist = float.MaxValue;

        foreach (Feeder feeder in World.GetAllEntities<Feeder>())
        {
            if (!View.Contains(feeder.Position)) continue;

            Vector3 local = View.ToLocal(feeder.Position);
            float r = feeder.Info.FootRadius;
            Vector3 min = local + new Vector3(-r, 0f, -r) * View.Zoom;
            Vector3 max = local + new Vector3(r, feeder.Info.CatchHeight, r) * View.Zoom;
            RayCollision hit = Raylib.GetRayCollisionBox(ray, new BoundingBox(min, max));
            if (hit.Hit && hit.Distance < bestDist)
            {
                best = feeder;
                bestDist = hit.Distance;
            }
        }
        return best;
    }

    // The gunk under the mouse, found from where the mouse points on the floor.
    private Gunk? PickGunk()
    {
        Vector3? point = FloorPointUnderMouse();
        if (point == null) return null;

        Vector3 world = View.ToWorld(point.Value);
        foreach (Gunk gunk in World.GetAllEntities<Gunk>())
        {
            float dist = Vector2.Distance(new Vector2(world.X, world.Z), new Vector2(gunk.Position.X, gunk.Position.Z));
            if (dist <= gunk.Radius + 0.1f) return gunk;
        }
        return null;
    }

    private Bird? PickBird(Ray ray)
    {
        Bird? best = null;
        float bestDist = float.MaxValue;

        foreach (Bird bird in World.GetAllEntities<Bird>())
        {
            if (!View.Contains(bird.Position, -0.5f)) continue;

            float? dist = RayHitsBird(ray, bird);
            if (dist != null && dist < bestDist)
            {
                best = bird;
                bestDist = dist.Value;
            }
        }
        return best;
    }

    // Tests the click against the bird's box. The ray is moved into the bird's own space
    // (unturned, in birb units), where the box from its shape fits it exactly.
    // Returns how far along the box-space ray the hit is, or null for a miss.
    private float? RayHitsBird(Ray ray, Bird bird)
    {
        float scale = bird.Size * View.Zoom;
        Matrix4x4 unturn = Matrix4x4.CreateRotationY(-bird.Heading);

        Vector3 origin = Vector3.Transform(ray.Position - View.ToLocal(bird.Position), unturn) / scale;
        Vector3 direction = Vector3.Transform(ray.Direction, unturn);
        RayCollision hit = Raylib.GetRayCollisionBox(new Ray(origin, direction), bird.Bounds);
        return hit.Hit ? hit.Distance * scale : null;
    }

    // ---------------- DRAW (no simulation changes) ----------------

    private void Draw()
    {
        drawClock.Restart();
        Raylib.BeginDrawing();
            Color accent = Palette.ToColor(Renderer.HoloColor);
            Renderer.Draw(Camera, View, Mode, Store, SelectedBird, HoveredBird, Designer, Radio, Projector);
            Sides.Draw(this, accent);
            Hud.Draw(this, accent);
            Map.Draw(this, accent);
            Bucket.Draw(accent);
            Tag.Draw(Store, Camera.HologramCamera, accent);    // the tag's string runs to an item in the hologram
            Radio.DrawLabel(Camera.Camera, accent);

            rlImGui.Begin();
                if (!Menu.IsOpen && !Designer.IsOpen) UI.Draw(this);
                if (!Menu.IsOpen) Designer.DrawWindow(this);
            rlImGui.End();
            Menu.Draw(this, accent);

            // Measured before EndDrawing, which waits for the screen to be ready.
            float ms = (float)drawClock.Elapsed.TotalMilliseconds;
            DrawMs += (ms - DrawMs) * 0.05f;
            DrawStats.EndFrame();

            if (screenshotRequested)
            {
                SaveScreenshot();
                screenshotRequested = false;
            }
        Raylib.EndDrawing();
    }

    // Reads the frame that was just drawn, before EndDrawing hands it to Windows,
    // so the picture is exactly this frame and not one still waiting in the display queue.
    private void SaveScreenshot()
    {
        Rlgl.DrawRenderBatchActive();    // raylib batches draw calls; push any leftovers to the screen first

        // While developing, screenshots go in the project folder; with no project around, next to the .exe.
        string folder = Path.Combine(Assets.ProjectFolder ?? AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        string name = $"birbbox_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";

        Image shot = Raylib.LoadImageFromScreen();
        bool saved = Raylib.ExportImage(shot, Path.Combine(folder, name));
        Raylib.UnloadImage(shot);

        EventLog.Add(saved ? $"Screenshot saved: {name}" : "Screenshot failed to save.");
    }

    // A plain window with no border, stretched over the monitor. Raylib's own ToggleBorderlessWindowed
    // switches to real monitor fullscreen, which stays on top and blocks alt-tab.
    public void ToggleFullscreen()
    {
        if (!IsFullscreen)
        {
            if (Raylib.IsWindowMaximized()) Raylib.RestoreWindow();
            windowedPos = Raylib.GetWindowPosition();
            windowedSize = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

            int monitor = Raylib.GetCurrentMonitor();
            if (TrueFullscreen)
            {
                // Raylib's fullscreen uses the window's size as the screen resolution, so grow it to the monitor first
                Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
                Raylib.ToggleFullscreen();
            }
            else
            {
                Vector2 monitorPos = Raylib.GetMonitorPosition(monitor);
                Raylib.SetWindowState(ConfigFlags.UndecoratedWindow);
                Raylib.SetWindowPosition((int)monitorPos.X, (int)monitorPos.Y);
                Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
            }
        }
        else
        {
            if (Raylib.IsWindowFullscreen()) Raylib.ToggleFullscreen();
            else Raylib.ClearWindowState(ConfigFlags.UndecoratedWindow);
            Raylib.SetWindowSize((int)windowedSize.X, (int)windowedSize.Y);
            Raylib.SetWindowPosition((int)windowedPos.X, (int)windowedPos.Y);
        }
        IsFullscreen = !IsFullscreen;
    }

    // Switching type while fullscreen steps out to a window and back in the new way.
    public void SetTrueFullscreen(bool on)
    {
        if (on == TrueFullscreen) return;
        bool wasFullscreen = IsFullscreen;
        if (wasFullscreen) ToggleFullscreen();
        TrueFullscreen = on;
        if (wasFullscreen) ToggleFullscreen();
    }

    public void JumpToBird(Bird bird)
    {
        View.JumpTo(new Vector2(bird.Position.X, bird.Position.Z));
    }
}
