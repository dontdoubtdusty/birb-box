using System.Numerics;
using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;

public class GameEngine
{
    public static GameEngine Instance { get; private set; } = null!;

    // Shown in the window title and printed on top of the pedestal's bezel ring. Bump it as the build changes.
    public const string Version = "Birb Cube 4.95 Alpha";

    // SYSTEMS
    public World World { get; private set; }
    public BirdSpawner BirdSpawner { get; private set; }
    public FoodSpawner FoodSpawner { get; private set; }
    public GroundWear Wear { get; private set; }
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
    public SaveGame Saves { get; private set; }
    public Hands Hands { get; private set; }
    public Rocker Rocker { get; private set; }
    public FramePacing Pacing { get; private set; }
    public GameWindow Window { get; private set; }
    public MousePicking Mouse { get; private set; }
    public Pouring Pour { get; private set; }
    public WorldClock Clock { get; private set; }

    // SIMULATION STATE
    public bool Paused = false;
    public float SimSpeed = 1f;
    public float SimTime { get; set; }    // set directly only when loading a save

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

    // SELECTION. A bird or a feeder can be selected, never both: picking one clears the other.
    // Following is separate from selecting (see FollowedBird), so clicking away keeps the camera on its bird.
    private Bird? selectedBird;
    public Bird? SelectedBird
    {
        get => selectedBird;
        set
        {
            selectedBird = value;
            if (value != null) selectedFeeder = null;
        }
    }
    private Feeder? selectedFeeder;
    public Feeder? SelectedFeeder
    {
        get => selectedFeeder;
        set
        {
            selectedFeeder = value;
            if (value != null) SelectedBird = null;
        }
    }
    public Bird? HoveredBird;          // set by the UI when hovering a name in the tracker

    // PLACING FEEDERS: where a carried feeder would go (box space), or null when the mouse isn't over clear floor
    public Vector3? PlacingSpot { get; private set; }
    // FOLLOWING: the bird the view slides along with, selected or not. Only a manual move of the view
    // (WASD, dragging the world map, Center on something else) or the bird leaving the world stops it.
    public Bird? FollowedBird;

    // HOLOGRAM VIEW: the bird world's zoom when it was entered; the wheel zooms the world back out only this far
    private float holoBaseZoom = 1f;

    // What this frame's input is free to do and what the mouse is on. Filled in as Update goes, phase by phase,
    // so each phase can see what the ones before it found.
    private struct FrameInput
    {
        public bool TypingName;        // editing a bird's name on the card
        public bool MouseOverImGui;
        public bool MouseBlocked;      // over an ImGui window, the bird card, a side column, the price tag, or the radio
        public bool KeyboardBlocked;   // typing, in the name field or an ImGui box
        public bool HandsFree;         // false while a drag turns the ring: the hands stay on it
        public bool MouseOverLogo;
        public BoxScene? RockerHalf;   // the rocker half under the mouse, if any
        public bool BoxReady;          // the projector is fully on
        public bool InStore;
        public bool BirdsShowing;      // the bird scene is up, not mid-switch, and the box is ready
        public bool MouseOverBox;      // over the hologram itself, not a control in front of it
        public bool DoubleClickFollowed;   // this frame's double-click picked a bird to follow

        public readonly bool MouseOverRocker => RockerHalf != null;
    }

    public GameEngine()
    {
        Instance = this;
        World = new World();
        BirdSpawner = new BirdSpawner();
        FoodSpawner = new FoodSpawner();
        Wear = new GroundWear();
        Wear.Reset(World.HalfSize);
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
        Saves = new SaveGame();
        Hands = new Hands();
        Rocker = new Rocker();
        Pacing = new FramePacing();
        Window = new GameWindow();
        Mouse = new MousePicking(Camera, View, World);
        Pour = new Pouring();
        Clock = new WorldClock();
    }

    public void Run()
    {
        Window.Open(Version);
        Pacing.Start();    // vsync paces the frames; no timer on top (see FramePacing)

        Sounds.Load();
        Radio.Load();
        Renderer.Load(View);
        Rocker.Load();
        BirbBuilder.Load(Renderer.HoloShader);
        Birds.LoadDesigns();    // Designer saves join their species' looks before the world (and its birds) loads

        // Fonts added between Begin and End come before ImGui's built-in one, so the first becomes the default.
        rlImGui.BeginInitImGui();
        ImGui.StyleColorsDark();
        UIHandler.LoadFont();
        rlImGui.EndInitImGui();
        UI.ApplyStyle();

        Saves.LoadOrStartNew(this);
        Settings.Load(this);    // after the window is up, since it may go fullscreen

        while (!Raylib.WindowShouldClose() && !Menu.QuitRequested)
        {
            float dt = Raylib.GetFrameTime();
            Pacing.Update(dt);
            Update(dt);
            Draw();
        }

        Saves.Save(this);    // closing the window or quitting from the menu both end up here
        Settings.Save(this);
        rlImGui.Shutdown();
        Bucket.Unload();
        Renderer.Unload();
        Rocker.Unload();
        BirbBuilder.Unload();
        UiText.Unload();
        Radio.Unload();
        Sounds.Unload();
        Raylib.CloseWindow();
    }

    // A new world starts with wild seed and no birds; every bird arrives because of seed.
    public void SeedStartingWorld()
    {
        FoodSpawner.ScatterAcrossWorld(40);
        FoodSpawner.ScatterInView(6, View);
        World.FlushChanges();
    }

    // ---------------- UPDATE (no drawing) ----------------

    // Everything, then the hands, which go wherever this frame's update asked them to (see Hands).
    private void Update(float dt)
    {
        updateClock.Restart();

        UpdateSystems(dt);
        Hands.Update(dt, Camera.Camera, showColor: Menu.ShowingSettings);

        // Smoothed so the number is readable instead of flickering
        float ms = (float)updateClock.Elapsed.TotalMilliseconds;
        UpdateMs += (ms - UpdateMs) * 0.05f;
    }

    // The frame in phases, in order. The menu or the Designer, while open, stop it partway.
    private void UpdateSystems(float dt)
    {
        UpdateBackground(dt);
        FrameInput input = ReadInput();

        // While the menu is open it takes all input and the rest of the box waits.
        // (Skipped while typing a name, so the Esc that cancels the name doesn't also open the menu.)
        if (!input.TypingName) Menu.Update(this);
        if (Menu.IsOpen) return;

        UpdateRoomControls(ref input);

        // The Birb Designer takes over the hologram while it's open (see BirbDesigner.cs).
        if (!input.KeyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.B))
        {
            Designer.Toggle(this);
        }
        if (Designer.IsOpen)
        {
            UpdateDesigning(dt, input.MouseBlocked);
            return;
        }

        if (!input.KeyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            Paused = !Paused;
        }
        if (!input.KeyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            UI.ShowTools = !UI.ShowTools;
        }

        UpdateScene(dt, ref input);
        UpdateCameraAndRing(dt, ref input);
        UpdateBoxClicks(dt, input);
        UpdateSimulation(dt);
        UpdatePanels(dt, input);
    }

    // What keeps going no matter what: sounds, the radio, the projector and the autosave.
    // The hum runs even while paused; it just ducks quieter behind the menu or the Designer, and follows the power.
    // Bird sounds fade out while the store is showing or the box is off.
    private void UpdateBackground(float dt)
    {
        Sounds.Update(dt, Menu.IsOpen || Designer.IsOpen, Mode.Showing == BoxScene.Birds && Projector.IsOn, Projector.Lens);
        Radio.Update(dt, ducked: Menu.IsOpen);    // keeps playing, quieter, behind the Esc menu
        Projector.Update(dt);                     // keeps starting up or shutting down behind the menu too
        Saves.Update(dt, this);                   // autosave, on real time
    }

    // Who has the mouse and keyboard this frame, plus the keys that work everywhere (F fullscreen, F9 screenshot).
    // The mouse is "blocked" for the hologram when it's over an ImGui window, the bird card, a side column or the price tag.
    // The keyboard is blocked while typing, either a bird's name or in an ImGui box.
    private FrameInput ReadInput()
    {
        FrameInput input = new FrameInput();
        Sides.Layout();
        input.TypingName = Hud.IsEditingName;
        Hud.UpdateTyping(this);
        input.MouseOverImGui = ImGui.GetIO().WantCaptureMouse;
        input.MouseBlocked = input.MouseOverImGui || Hud.IsMouseOver(this) || Sides.IsMouseOver() || Tag.IsMouseOver(Store);
        input.KeyboardBlocked = input.TypingName || ImGui.GetIO().WantCaptureKeyboard;

        if (!input.KeyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.F))
        {
            Window.ToggleFullscreen();
        }
        // Only typing blocks screenshots, not a focused slider or panel
        bool typingAnywhere = input.TypingName || ImGui.GetIO().WantTextInput;
        if (!typingAnywhere && Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            Window.RequestScreenshot();
        }
        return input;
    }

    // The controls in the room: the radio on the table and the power button (the logo on the pedestal's front).
    // The right hand comes up to whichever one the mouse is on.
    private void UpdateRoomControls(ref FrameInput input)
    {
        // While a drag is turning the ring, the hands stay on the ring: none reaches for a button or knob the mouse passes
        input.HandsFree = !Camera.IsTurning;

        // The radio has the mouse while it's over the radio or turning one of its knobs
        Radio.HandleInput(Mouse.RayInRoom(), input.MouseBlocked);
        input.MouseBlocked |= Radio.UsingMouse;
        if (input.HandsFree) ReachForRadio();

        input.MouseOverLogo = !input.MouseBlocked && Pedestal.RayHitsLogo(Mouse.RayInRoom(), View);
        Projector.LogoHovered = input.MouseOverLogo;
        if (input.MouseOverLogo && input.HandsFree) Hands.Press(HandSide.Right, Pedestal.LogoFace(View), Vector3.UnitZ, pushed: Camera.IsHolding);
    }

    // The box shows either the bird world or the store; the rocker on the pedestal's front switches.
    // The right hand comes up to press whichever half the mouse is on.
    // While the store is showing, the birds ignore the mouse and WASD (see ClickInStore for its clicks).
    // Nothing in the box takes the mouse or WASD until the projector is fully on (the rocker won't budge either).
    private void UpdateScene(float dt, ref FrameInput input)
    {
        input.BoxReady = Projector.Ready;
        if (!input.MouseBlocked && Rocker.RayHits(Mouse.RayInRoom(), View, out BoxScene half)) input.RockerHalf = half;
        if (input.RockerHalf != null && input.HandsFree)
        {
            Vector3 face = Rocker.FacePoint(View, input.RockerHalf.Value, out Vector3 facing);
            Hands.Press(HandSide.Right, face, facing, pushed: Camera.IsHolding);
        }
        if (Mode.Update(dt)) OnSceneSwapped();
        Rocker.Update(dt, Mode.Heading, input.RockerHalf, holding: Camera.IsHolding && input.BoxReady && !Mode.IsSwitching);
        input.InStore = Mode.InStore;
        input.BirdsShowing = Mode.Showing == BoxScene.Birds && !Mode.IsSwitching && input.BoxReady;
        if (!Projector.IsOn) Store.SelectedItem = null;    // no price tag hanging off a switched-off box

        Store.Update(dt, showing: input.InStore);
        bool storeActive = input.InStore && input.BoxReady && !Mode.IsSwitching && !input.MouseBlocked && !input.MouseOverRocker;
        Store.UpdateHover(Mouse.RayInHologram(), active: storeActive);
        Tag.Update(Store);
    }

    // The camera, the wheel, double-clicks, the ring and panning.
    // Room view: the wheel zooms the hologram (and map) when the mouse is over the birds, otherwise the outer camera.
    // Hologram View: see HologramWheelZoomsWorld.
    private void UpdateCameraAndRing(float dt, ref FrameInput input)
    {
        bool overControl = input.MouseBlocked || input.MouseOverRocker || input.MouseOverLogo;
        input.MouseOverBox = !overControl && View.RayHitsHologram(Mouse.RayInBox());
        float wheel = Raylib.GetMouseWheelMove();
        bool wheelZoomsWorld = input.BirdsShowing
            && (Camera.InHologramView ? !input.MouseBlocked && HologramWheelZoomsWorld(wheel) : input.MouseOverBox);
        Camera.Update(input.MouseBlocked, wheelZoomsWorld);
        if (wheelZoomsWorld)
        {
            View.ZoomBy(wheel);
            if (Camera.InHologramView && wheel < 0f) View.Zoom = MathF.Max(View.Zoom, holoBaseZoom);
        }
        // A double-click on a bird follows it; anywhere else on the box it enters or leaves the Hologram View
        input.DoubleClickFollowed = input.BirdsShowing && !overControl && FollowBirdOnDoubleClick();
        UpdateHologramView(overControl || input.DoubleClickFollowed, input.MouseOverBox && input.BoxReady && !Mode.IsSwitching);
        // Both hands part as the cube grows, and turn around to press it back together as it shrinks
        if (Camera.IsChangingView) Hands.Spread(Camera.Growth, closing: !Camera.InHologramView);

        // A sideways drag turns the bezel ring, and with it whatever the box is showing (see Dial).
        // The birds and the store share one turn, so the ring never jumps when the rocker switches scenes.
        Dial.Turn(ref View.Yaw, dt, Camera.TurnThisFrame, Camera.IsHolding);
        Store.Yaw = View.Yaw;
        if (Camera.IsTurning) Hands.HoldRing(RingHand(), View, Camera.TurnThisFrame);

        if (!input.KeyboardBlocked && input.BirdsShowing && View.HandlePanInput(dt))
        {
            FollowedBird = null;    // manual panning takes over from following
        }
    }

    // Clicks and pouring: carrying a feeder, pressing the logo or the rocker, the store's shelves,
    // and clicking or pouring seed on the birds' floor.
    private void UpdateBoxClicks(float dt, FrameInput input)
    {
        Placer.Update(input.BirdsShowing);
        PlacingSpot = Placer.Carried != null && input.BirdsShowing && !input.MouseBlocked && !input.MouseOverRocker ? Mouse.FloorPoint() : null;

        if (Camera.ClickedThisFrame && !input.MouseBlocked)
        {
            if (input.MouseOverLogo)
            {
                Projector.PressButton();
                Hands.Tap(HandSide.Right);
            }
            else if (input.RockerHalf != null)
            {
                Hands.Tap(HandSide.Right);
                PressRocker(input.RockerHalf.Value, input.BoxReady);
            }
            else if (input.InStore && input.BoxReady) ClickInStore();
            else if (input.BirdsShowing && !input.DoubleClickFollowed) HandleClick();    // the double-click's second click already picked its bird
        }
        bool pourBlocked = input.MouseBlocked || !input.BirdsShowing || input.MouseOverRocker;
        Pour.Update(dt, pourBlocked, Mouse, Bucket.Bucket, FoodSpawner, View);
    }

    // The bird world and its clock move on (unless paused), then birds that left are forgotten by the selection.
    private void UpdateSimulation(float dt)
    {
        if (!Paused)
        {
            float simDt = dt * SimSpeed;
            SimTime += simDt;
            Clock.Advance(simDt);
            Clock.Refresh();

            UpdateEntities(simDt);
            BirdSpawner.CheckFrostcrestSeen(View);
            BirdSpawner.Update(simDt);
            FoodSpawner.Update(simDt);
            Wear.Update(simDt, World);
        }

        World.FlushChanges();
        Clock.Refresh();    // also while paused, so the tools panel's hour slider shows at once

        if (SelectedBird != null && !World.Contains(SelectedBird)) SelectedBird = null;
        if (FollowedBird != null && !World.Contains(FollowedBird)) FollowedBird = null;
        if (SelectedFeeder != null && !SelectedFeeder.InWorld) SelectedFeeder = null;
        if (HoveredBird != null && !World.Contains(HoveredBird)) HoveredBird = null;
    }

    // The screen panels and labels, and the view following its bird.
    private void UpdatePanels(float dt, FrameInput input)
    {
        Sides.Update(this, input.MouseOverImGui);
        Hud.Update(this, input.MouseOverImGui);
        Map.Update(this, input.MouseOverImGui);
        Renderer.ChatMessages.Update(View, dt);
        Bucket.Update(dt, input.MouseOverImGui);

        if (FollowedBird != null && !input.InStore)
        {
            View.Follow(FollowedBird.Position, dt);
        }
    }

    // Pressing the raised half of the rocker switches to its scene with a click. The half already pushed in,
    // a box that isn't fully on, or one already mid-switch doesn't budge.
    private void PressRocker(BoxScene half, bool boxReady)
    {
        if (!boxReady || Mode.IsSwitching || half == Mode.Showing) return;
        Mode.SwitchTo(half);
        Sounds.PlayRocker();
    }

    // Double-clicking the box enters the Hologram View; double-clicking anywhere (but a control) leaves it.
    // Switching the box off leaves it too, since there's nothing left to look at.
    private void UpdateHologramView(bool mouseOnSomethingElse, bool mouseOverReadyBox)
    {
        if (Camera.InHologramView && !Projector.IsOn)
        {
            Camera.LeaveHologramView();
            return;
        }
        if (!Camera.DoubleClickedThisFrame || mouseOnSomethingElse) return;

        if (Camera.InHologramView) Camera.LeaveHologramView();
        else if (mouseOverReadyBox)
        {
            Camera.EnterHologramView(View);
            holoBaseZoom = View.Zoom;
        }
    }

    // On a double-click, the bird under the mouse (if any) is selected and followed. Not while carrying a feeder.
    private bool FollowBirdOnDoubleClick()
    {
        if (!Camera.DoubleClickedThisFrame || Placer.Carried != null) return false;

        Bird? bird = Mouse.PickBird(Mouse.RayInBox());
        if (bird == null) return false;
        SelectedBird = bird;
        FollowedBird = bird;
        return true;
    }

    // In the Hologram View the wheel is one continuous zoom. In: the camera first, down to where the whole
    // cube just fits, then the bird world. Out: the bird world back to its zoom on entering, then the camera.
    private bool HologramWheelZoomsWorld(float wheel)
    {
        if (wheel > 0f) return Camera.AtClosestFit;
        if (wheel < 0f) return View.Zoom > holoBaseZoom + 0.0001f;
        return false;
    }

    // The hand that turns the ring: the right one for a drag that started toward the right, else the left.
    private HandSide RingHand() => Camera.TurnStartedRight ? HandSide.Right : HandSide.Left;

    // The right hand reaches for whichever radio control the mouse is on: a finger for the buttons, a grip for the knobs.
    private void ReachForRadio()
    {
        if (!Radio.HandTarget(out Vector3 point, out Vector3 facing, out bool isKnob, out bool holding, out float twist)) return;
        if (isKnob) Hands.GripKnob(HandSide.Right, point, facing, holding, twist);
        else Hands.Press(HandSide.Right, point, facing, holding);
    }

    // While designing, the box stays on and shows the birds (fogging back from the store if the Designer was
    // opened there), and the rocker follows it back. Dragging still turns and tilts the hologram and the wheel
    // moves the camera. Nothing else in the box reacts: no panning, clicking, pouring or simulation.
    private void UpdateDesigning(float dt, bool mouseBlocked)
    {
        Projector.TurnOn();
        Mode.SwitchTo(BoxScene.Birds);
        if (Mode.Update(dt)) OnSceneSwapped();
        Rocker.Update(dt, Mode.Heading, hovered: null, holding: false);

        Camera.Update(mouseBlocked, wheelUsedElsewhere: false);
        Dial.Turn(ref View.Yaw, dt, Camera.TurnThisFrame, Camera.IsHolding);
        if (Camera.IsTurning) Hands.HoldRing(RingHand(), View, Camera.TurnThisFrame);
        Designer.Update(dt);
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
    // Clicking anything else just closes an open tag; only the rocker leaves the store.
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

    // A click sets down a carried feeder. Otherwise it picks up a coin, selects a bird, or selects a feeder
    // (its card has a Move button). Clicking empty floor deselects.
    private void HandleClick()
    {
        if (Placer.Carried != null)
        {
            if (PlacingSpot != null) Placer.TryPlace(View.ToWorld(PlacingSpot.Value));
            return;
        }

        Ray ray = Mouse.RayInBox();

        Coin? hitCoin = Mouse.PickCoin(ray);
        if (hitCoin != null)
        {
            hitCoin.Collect();
            return;
        }

        Bird? hitBird = Mouse.PickBird(ray);
        if (hitBird != null)
        {
            SelectedBird = hitBird;
            hitBird.Call();
            return;
        }

        Feeder? hitFeeder = Mouse.PickFeeder(ray);
        if (hitFeeder != null)
        {
            SelectedFeeder = hitFeeder;
            return;
        }

        SelectedBird = null;
        SelectedFeeder = null;
    }

    // ---------------- DRAW (no simulation changes) ----------------

    private void Draw()
    {
        drawClock.Restart();
        Raylib.BeginDrawing();
            Color accent = Palette.ToColor(Renderer.HoloColor);
            Renderer.Draw(Camera, View, Mode, Store, SelectedBird, HoveredBird, Designer, Radio, Projector, Hands, Rocker);
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

            Window.TakeRequestedScreenshot();
        Raylib.EndDrawing();
    }

    // ---------------- ASKED FOR BY THE PANELS ----------------

    // Center on a bird. Centering on any bird but the followed one stops following.
    public void JumpToBird(Bird bird)
    {
        if (bird != FollowedBird) FollowedBird = null;
        View.JumpTo(new Vector2(bird.Position.X, bird.Position.Z));
    }

    // Center on a spot (the feeder card's Center). Stops following, like any manual move of the view.
    public void JumpToSpot(Vector3 worldPos)
    {
        FollowedBird = null;
        View.JumpTo(new Vector2(worldPos.X, worldPos.Z));
    }

    // The feeder card's Move button: lift the selected feeder so the next click on the dirt sets it down.
    public void MoveSelectedFeeder()
    {
        if (SelectedFeeder == null || Placer.Carried != null) return;
        Placer.PickUp(SelectedFeeder);
        SelectedFeeder = null;
    }
}
