using System.Numerics;
using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;

public class GameEngine
{
    public static GameEngine Instance { get; private set; } = null!;

    // SYSTEMS
    public World World { get; private set; }
    public BirdSpawner BirdSpawner { get; private set; }
    public FoodSpawner FoodSpawner { get; private set; }
    public OrbitCamera Camera { get; private set; }
    public ViewWindow View { get; private set; }
    public HologramRenderer Renderer { get; private set; }
    public Hud Hud { get; private set; }
    public UIHandler UI { get; private set; }

    // SIMULATION STATE
    public bool Paused = false;
    public float SimSpeed = 1f;
    public float SimTime { get; private set; }

    // SELECTION
    public Bird? SelectedBird;
    public Bird? HoveredBird;          // set by the UI when hovering a name in the tracker
    public bool FollowSelected = false;

    // WINDOW
    private const int StartWidth = 1280;
    private const int StartHeight = 720;

    public GameEngine()
    {
        Instance = this;
        World = new World();
        BirdSpawner = new BirdSpawner();
        FoodSpawner = new FoodSpawner();
        Camera = new OrbitCamera();
        View = new ViewWindow();
        Renderer = new HologramRenderer();
        Hud = new Hud();
        UI = new UIHandler();
    }

    public void Run()
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(StartWidth, StartHeight, "Birb Box Alpha 4.0");
        Raylib.SetTargetFPS(60);

        ModelLibrary.Load();
        Renderer.Load(View);
        ModelLibrary.ApplyShader(Renderer.HoloShader);

        rlImGui.Setup(true);
        UI.ApplyStyle();

        SeedStartingWorld();

        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();
            Update(dt);
            Draw();
        }

        rlImGui.Shutdown();
        Renderer.Unload();
        ModelLibrary.Unload();
        Raylib.CloseWindow();
    }

    private void SeedStartingWorld()
    {
        FoodSpawner.ScatterAcrossWorld(40);
        FoodSpawner.ScatterInView(6, View);
        BirdSpawner.SpawnResidents(8, View.Center, View.HalfSize * 0.7f);
        BirdSpawner.SpawnResidents(25, Vector2.Zero, World.HalfSize * 0.8f);
        World.FlushChanges();
    }

    // ---------------- UPDATE (no drawing) ----------------

    private void Update(float dt)
    {
        // The mouse is "blocked" for the hologram when it's over an ImGui window or a HUD panel.
        bool mouseOverImGui = ImGui.GetIO().WantCaptureMouse;
        bool mouseBlocked = mouseOverImGui || Hud.IsMouseOver(this);
        bool keyboardBlocked = ImGui.GetIO().WantCaptureKeyboard;

        if (Raylib.IsKeyPressed(KeyboardKey.F11))
        {
            ToggleFullscreen();
        }
        if (!keyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            Paused = !Paused;
        }
        if (!keyboardBlocked && Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            UI.ShowTools = !UI.ShowTools;
        }

        Camera.Update(mouseBlocked);

        if (!keyboardBlocked && View.HandlePanInput(dt, Camera.Yaw))
        {
            FollowSelected = false;    // manual panning takes over from following
        }

        if (Camera.ClickedThisFrame && !mouseBlocked)
        {
            HandleClick();
        }

        if (!Paused)
        {
            float simDt = dt * SimSpeed;
            SimTime += simDt;

            foreach (Entity entity in World.Entities)
            {
                entity.Update(simDt);
            }
            BirdSpawner.Update(simDt);
            FoodSpawner.Update(simDt);
        }

        World.FlushChanges();

        // Forget birds that have left the world
        if (SelectedBird != null && !World.Contains(SelectedBird)) SelectedBird = null;
        if (HoveredBird != null && !World.Contains(HoveredBird)) HoveredBird = null;

        Hud.Update(this, dt, mouseOverImGui);

        if (FollowSelected && SelectedBird != null)
        {
            View.Follow(SelectedBird.Position, dt);
        }
    }

    // A click either selects the bird under the mouse or throws food onto the floor.
    private void HandleClick()
    {
        Ray ray = Raylib.GetScreenToWorldRay(Raylib.GetMousePosition(), Camera.Camera);

        Bird? hitBird = PickBird(ray);
        if (hitBird != null)
        {
            SelectedBird = hitBird;
            return;
        }

        // Find where the ray crosses the floor (Y = 0) in hologram-local space.
        if (MathF.Abs(ray.Direction.Y) < 0.0001f) return;
        float t = -ray.Position.Y / ray.Direction.Y;
        if (t < 0) return;
        Vector3 localPoint = ray.Position + ray.Direction * t;

        // Only the clear middle of the cube accepts food, not the foggy edges.
        float limit = View.HalfSize * 0.85f;
        if (MathF.Abs(localPoint.X) > limit || MathF.Abs(localPoint.Z) > limit)
        {
            SelectedBird = null;
            return;
        }

        FoodSpawner.ThrowFood(View.ToWorld(localPoint), View.Height);
    }

    private Bird? PickBird(Ray ray)
    {
        Bird? best = null;
        float bestDist = float.MaxValue;

        foreach (Bird bird in World.GetAllEntities<Bird>())
        {
            if (!View.Contains(bird.Position, -0.5f)) continue;

            Vector3 localCenter = View.ToLocal(bird.PickCenter);
            RayCollision hit = Raylib.GetRayCollisionSphere(ray, localCenter, bird.PickRadius);
            if (hit.Hit && hit.Distance < bestDist)
            {
                best = bird;
                bestDist = hit.Distance;
            }
        }
        return best;
    }

    // ---------------- DRAW (no simulation changes) ----------------

    private void Draw()
    {
        Raylib.BeginDrawing();
            Renderer.Draw(Camera, View, SelectedBird, HoveredBird);
            Hud.Draw(this);

            rlImGui.Begin();
                UI.Draw(this);
            rlImGui.End();
        Raylib.EndDrawing();
    }

    public void ToggleFullscreen()
    {
        // Borderless fullscreen behaves better than exclusive fullscreen with resizing and alt-tab.
        Raylib.ToggleBorderlessWindowed();
    }

    public void JumpToBird(Bird bird)
    {
        View.JumpTo(new Vector2(bird.Position.X, bird.Position.Z));
    }
}
