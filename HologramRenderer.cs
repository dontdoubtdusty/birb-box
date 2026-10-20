using System.Numerics;
using Raylib_cs;

// Draws the whole scene. Nothing in here changes the simulation.
//
// Three steps:
//  1. The 3D scene is drawn into an off-screen texture (sceneTarget) with a clear background.
//     Things inside the hologram use hologram.fs for tint, fog and scan bands.
//     The hologram's contents are turned by view.Yaw (or the store's Yaw); the pedestal never turns.
//     The box shows either the bird world or the store (see BoxMode). While it shows the store,
//     the bird world is also drawn into a small "zoo camera" texture for the world map panel.
//  2. The backdrop (the cabin, the dining table and the things on it) is drawn straight to the screen.
//  3. The scene texture is laid over the backdrop through post.fs for glow and scanlines.
//
// Smooth edges: the window's anti-aliasing (MSAA) doesn't reach an off-screen texture, so instead the scene
// is drawn at twice the screen size. Shrinking it back down blends each 2x2 block of pixels into one.
public class HologramRenderer
{
    // LOOK SETTINGS (changed from the ImGui panel)
    public Vector3 HoloColor = new Vector3(0.36f, 0.86f, 0.82f);
    public float GlowStrength = 0.7f;
    public float ScanlineStrength = 0.12f;
    public bool ShowProjector = true;
    public bool SmoothEdges = true;

    private const int MaxSceneSize = 8192;    // biggest texture to ask the graphics card for

    public Shader HoloShader { get; private set; }
    private Shader postShader;
    private RenderTexture2D sceneTarget;

    // ZOO CAMERA: a fixed camera looking down into the bird world, used while the box shows the store
    public const int FeedSize = 200;
    public RenderTexture2D FeedTexture { get; private set; }
    private Camera3D feedCamera = new Camera3D
    {
        Position = new Vector3(0f, 8f, 10f),
        Target = new Vector3(0f, 0.5f, 0f),
        Up = Vector3.UnitY,
        FovY = 42f,
        Projection = CameraProjection.Perspective
    };

    private readonly HoloSign sign = new HoloSign();

    private Model floorModel;
    private Model grainRound;    // unit-sized; each seed type stretches these into its own grain shape
    private Model grainBlock;
    private Model coinModel;
    private readonly Pedestal pedestal = new Pedestal();
    private readonly BirbAnimator animator = new BirbAnimator();
    private readonly StoreRenderer storeRenderer = new StoreRenderer();
    private readonly UnitShapes shapes = new UnitShapes();    // for feeders
    public Backdrop Backdrop { get; } = new Backdrop();
    public JoinMessages JoinMessages { get; } = new JoinMessages();

    // Shader uniform locations
    private int locHoloOrigin, locBoxYaw, locHoloRadius, locFogStart, locSwallow, locCubeHeight, locViewCenter, locViewZoom, locViewPos, locHoloColor, locTime, locIsFloor;
    private int locResolution, locPostTime, locGlow, locScanlines;

    public void Load(ViewWindow view)
    {
        HoloShader = Raylib.LoadShader(Assets.PathTo("shaders/hologram.vs"), Assets.PathTo("shaders/hologram.fs"));
        locHoloOrigin = Raylib.GetShaderLocation(HoloShader, "holoOrigin");
        locBoxYaw = Raylib.GetShaderLocation(HoloShader, "boxYaw");
        locHoloRadius = Raylib.GetShaderLocation(HoloShader, "holoRadius");
        locFogStart = Raylib.GetShaderLocation(HoloShader, "fogStart");
        locSwallow = Raylib.GetShaderLocation(HoloShader, "swallow");
        locCubeHeight = Raylib.GetShaderLocation(HoloShader, "cubeHeight");
        locViewCenter = Raylib.GetShaderLocation(HoloShader, "viewCenter");
        locViewZoom = Raylib.GetShaderLocation(HoloShader, "viewZoom");
        locViewPos = Raylib.GetShaderLocation(HoloShader, "viewPos");
        locHoloColor = Raylib.GetShaderLocation(HoloShader, "holoColor");
        locTime = Raylib.GetShaderLocation(HoloShader, "time");
        locIsFloor = Raylib.GetShaderLocation(HoloShader, "isFloor");

        postShader = Raylib.LoadShader(Assets.PathTo("shaders/post.vs"), Assets.PathTo("shaders/post.fs"));
        locResolution = Raylib.GetShaderLocation(postShader, "resolution");
        locPostTime = Raylib.GetShaderLocation(postShader, "time");
        locGlow = Raylib.GetShaderLocation(postShader, "glowStrength");
        locScanlines = Raylib.GetShaderLocation(postShader, "scanlineStrength");

        // A square floor big enough for the widest the circle can be; the shader's fog cuts off the rest.
        float floorSize = view.HalfSize * ViewWindow.MaxRadiusScale * 2f;
        floorModel = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(floorSize, floorSize, 1, 1));
        grainRound = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(0.5f, 6, 8));
        grainBlock = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
        coinModel = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(Coin.Radius, 0.03f, 20));
        pedestal.Load();
        storeRenderer.Load(HoloShader);
        shapes.Load(HoloShader);
        Backdrop.Load();
        FeedTexture = Raylib.LoadRenderTexture(FeedSize, FeedSize);
        sign.Load();

        Shader holo = HoloShader;
        Raylib.SetMaterialShader(ref floorModel, 0, ref holo);
        Raylib.SetMaterialShader(ref grainRound, 0, ref holo);
        Raylib.SetMaterialShader(ref grainBlock, 0, ref holo);
        Raylib.SetMaterialShader(ref coinModel, 0, ref holo);
    }

    public void Unload()
    {
        Raylib.UnloadModel(floorModel);
        Raylib.UnloadModel(grainRound);
        Raylib.UnloadModel(grainBlock);
        Raylib.UnloadModel(coinModel);
        pedestal.Unload();
        storeRenderer.Unload();
        shapes.Unload();
        Backdrop.Unload();
        Raylib.UnloadRenderTexture(FeedTexture);
        sign.Unload();
        JoinMessages.Unload();
        Raylib.UnloadShader(HoloShader);
        Raylib.UnloadShader(postShader);
        if (sceneTarget.Id != 0) Raylib.UnloadRenderTexture(sceneTarget);
    }

    // While the Birb Designer is open, its birb is drawn in place of the world's food, coins and birds.
    public void Draw(HoloCamera holoCamera, ViewWindow view, BoxMode mode, SeedStore store, Bird? selected, Bird? hovered, BirbDesigner designer)
    {
        int width = Raylib.GetRenderWidth();
        int height = Raylib.GetRenderHeight();
        bool fits = width * 2 <= MaxSceneSize && height * 2 <= MaxSceneSize;
        int scale = SmoothEdges && fits ? 2 : 1;
        Camera3D camera = holoCamera.Camera;
        EnsureTargetSize(width * scale, height * scale);

        // These draw into their own small textures, so they must happen before the scene pass.
        if (ShowProjector) pedestal.RefreshReadouts(view, GameEngine.Instance.Coins);
        JoinMessages.PrepareTextures();
        if (mode.InStore) DrawZooCamera(view, selected);

        SetHologramUniforms(camera, view, mode.Swallow);

        Raylib.BeginTextureMode(sceneTarget);
            Raylib.ClearBackground(Color.Blank);
            Rlgl.SetLineWidth(scale);    // so wire outlines aren't thinner when the scene is drawn bigger
            Raylib.BeginMode3D(camera);
                if (ShowProjector) pedestal.Draw(view, Palette.ToColor(HoloColor));
                if (mode.InStore)
                {
                    storeRenderer.Draw(store, view);
                    SetHologramUniforms(camera, view, mode.Swallow);    // the store changed the turn to its own
                }
                else
                {
                    DrawFloor(view);
                    if (designer.IsOpen)
                    {
                        designer.DrawPreview(view);
                    }
                    else
                    {
                        DrawFeeders(view);
                        DrawFood(view);
                        DrawGunk(view);
                        DrawCoins(view);
                        DrawBirds(view, selected, hovered);
                    }
                }
                DrawFieldEdges(Vector3.Zero, view.Radius, view.Height, 1.4f, 1f);
                if (!designer.IsOpen) sign.Draw(view, mode, HoloColor);
                if (!designer.IsOpen && !mode.InStore) JoinMessages.Draw(camera, view);
            Raylib.EndMode3D();
            Rlgl.SetLineWidth(1f);
        Raylib.EndTextureMode();

        Backdrop.Draw(camera, view.HalfSize);

        // The scene's colors are already multiplied by their alpha, so it's laid over the backdrop
        // with premultiplied blending. The post shader is given the screen size, not the scene texture's,
        // so the glow spreads the same distance either way.
        SetPostUniforms(width, height);
        Raylib.BeginBlendMode(BlendMode.AlphaPremultiply);
        Raylib.BeginShaderMode(postShader);
            // Render textures are stored upside down, so flip the source height.
            Rectangle source = new Rectangle(0, 0, sceneTarget.Texture.Width, -sceneTarget.Texture.Height);
            Rectangle dest = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            Raylib.DrawTexturePro(sceneTarget.Texture, source, dest, Vector2.Zero, 0f, Color.White);
        Raylib.EndShaderMode();
        Raylib.EndBlendMode();
    }

    // ---------------- SCENE PIECES ----------------

    // The bird world as seen by the fixed zoo camera, drawn into FeedTexture.
    // Clear background, the same hologram shader and fog, no glowing edges.
    private void DrawZooCamera(ViewWindow view, Bird? selected)
    {
        SetHologramUniforms(feedCamera, view, swallow: 0f);
        Raylib.BeginTextureMode(FeedTexture);
            Raylib.ClearBackground(new Color(12, 16, 22, 255));
            Raylib.BeginMode3D(feedCamera);
                DrawFloor(view);
                DrawFeeders(view);
                DrawFood(view);
                DrawGunk(view);
                DrawCoins(view);
                DrawBirds(view, selected, hovered: null);
            Raylib.EndMode3D();
        Raylib.EndTextureMode();
    }

    private void DrawFloor(ViewWindow view)
    {
        Raylib.SetShaderValue(HoloShader, locIsFloor, 1, ShaderUniformDataType.Int);
        Raylib.DrawModelEx(floorModel, Vector3.Zero, Vector3.UnitY, view.YawDegrees, Vector3.One, Color.White);
        Raylib.SetShaderValue(HoloShader, locIsFloor, 0, ShaderUniformDataType.Int);
    }

    // Each pile is drawn as a scatter of small grains, shaped and spread by its seed type (GrainLook in Seeds.cs).
    // The grains are only for show: each bite eaten removes GrainsPerBite of them.
    // A grain's spot comes from its pile, seed and number, so the rest stay put while others disappear.
    // As seed goes bad its grains darken toward gunk and slump flatter and closer together.
    // Seed in a tray stays inside its rim; seed in a tube is shown by the tube itself (FeederModel).
    private const int GrainsPerBite = 6;
    private const float TraySpread = 0.24f;

    private void DrawFood(ViewWindow view)
    {
        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (!view.Contains(food.Position, 0.5f)) continue;
            if (food.Feeder?.Style == FeederStyle.Tube) continue;

            Vector3 local = view.ToLocal(food.Position);
            foreach ((SeedType seed, float amount) in food.Contents)
            {
                SeedInfo info = Seeds.Get(seed);
                GrainLook look = info.Grain;
                float spread = food.Feeder != null ? MathF.Min(look.Spread, TraySpread) : look.Spread;
                Model model = look.Mesh == GrainMesh.Block ? grainBlock : grainRound;
                float gone = Math.Clamp((food.Rot(seed) - 0.5f) / (Food.GunkAfter - 0.5f), 0f, 1f);    // 0 fresh, 1 gunk
                Vector3 size = look.Size * new Vector3(1f, 1f - 0.5f * gone, 1f);
                Color color = Palette.Mix(info.Color, Gunk.Color, gone);
                int grains = (int)MathF.Ceiling(amount / Food.BiteSize * GrainsPerBite - 0.01f);

                for (int g = 0; g < grains; g++)
                {
                    // The square root spreads grains evenly over the patch instead of bunching at the middle.
                    float angle = Rng.Fixed(food.EntityID, (int)seed, g, 1) * MathF.PI * 2f;
                    float distance = spread * (1f - 0.3f * gone) * MathF.Sqrt(Rng.Fixed(food.EntityID, (int)seed, g, 2));
                    float turn = Rng.Fixed(food.EntityID, (int)seed, g, 3) * 360f;
                    float shade = Rng.Fixed(food.EntityID, (int)seed, g, 4) * 0.3f - 0.15f;

                    Vector3 offset = new Vector3(MathF.Cos(angle) * distance, size.Y * 0.5f, MathF.Sin(angle) * distance);
                    Raylib.DrawModelEx(model, view.BoxToScene(local + offset * view.Zoom), Vector3.UnitY,
                                       turn + view.YawDegrees, size * view.Zoom, Raylib.ColorBrightness(color, shade));
                }
            }
        }
    }

    // Gunk is a flat dark blob with a few lumps around it.
    private void DrawGunk(ViewWindow view)
    {
        const int Lumps = 5;
        foreach (Gunk gunk in World.Instance.GetAllEntities<Gunk>())
        {
            if (!view.Contains(gunk.Position, 0.5f)) continue;

            Vector3 local = view.ToLocal(gunk.Position);
            float r = gunk.Radius;
            Vector3 blob = new Vector3(r * 2f, 0.05f, r * 1.7f);
            float blobTurn = Rng.Fixed(gunk.EntityID, 0, 0, 1) * 360f;
            Raylib.DrawModelEx(grainRound, view.BoxToScene(local + new Vector3(0, 0.02f, 0) * view.Zoom), Vector3.UnitY,
                               blobTurn + view.YawDegrees, blob * view.Zoom, Gunk.Color);

            for (int i = 0; i < Lumps; i++)
            {
                float angle = Rng.Fixed(gunk.EntityID, 1, i, 1) * MathF.PI * 2f;
                float distance = r * 0.7f * Rng.Fixed(gunk.EntityID, 1, i, 2);
                float lumpSize = r * (0.3f + 0.3f * Rng.Fixed(gunk.EntityID, 1, i, 3));
                float shade = Rng.Fixed(gunk.EntityID, 1, i, 4) * 0.3f - 0.2f;

                Vector3 offset = new Vector3(MathF.Cos(angle) * distance, 0.04f, MathF.Sin(angle) * distance);
                Vector3 lump = new Vector3(lumpSize, lumpSize * 0.5f, lumpSize);
                Raylib.DrawModelEx(grainRound, view.BoxToScene(local + offset * view.Zoom), Vector3.UnitY,
                                   view.YawDegrees, lump * view.Zoom, Raylib.ColorBrightness(Gunk.Color, shade));
            }
        }
    }

    // Feeders standing in view, plus the see-through one following the mouse while placing.
    private void DrawFeeders(ViewWindow view)
    {
        foreach (Feeder feeder in World.Instance.GetAllEntities<Feeder>())
        {
            if (!view.Contains(feeder.Position, 0.5f)) continue;

            float fill = feeder.Held / feeder.Info.Capacity;
            Color seedColor = feeder.Seed != null ? Seeds.Get(feeder.Seed.MainSeed).Color : Color.Blank;
            FeederModel.Draw(shapes, view.BoxToScene(view.ToLocal(feeder.Position)), view.Zoom, view.YawDegrees,
                             feeder.Style, fill, seedColor, Color.White);
        }

        // The engine works out where the mouse points on the floor; null when it isn't over the clear floor.
        GameEngine engine = GameEngine.Instance;
        Feeder? carried = engine.Placer.Carried;
        if (carried != null && engine.PlacingSpot != null)
        {
            Color ghost = new Color(160, 255, 240, 130);
            FeederModel.Draw(shapes, view.BoxToScene(engine.PlacingSpot.Value), view.Zoom, view.YawDegrees,
                             carried.Style, carried.Held / carried.Info.Capacity, Color.White, ghost);
        }
    }

    // Coins stand on their edge and spin, so they catch the eye from any angle.
    private void DrawCoins(ViewWindow view)
    {
        foreach (Coin coin in World.Instance.GetAllEntities<Coin>())
        {
            if (!view.Contains(coin.Position, 0.5f)) continue;

            float spin = (float)Raylib.GetTime() * 3f + coin.EntityID + view.Yaw;
            coinModel.Transform = Raymath.MatrixMultiply(Raymath.MatrixRotateX(MathF.PI / 2f), Raymath.MatrixRotateY(spin));

            Color gold = new Color(255, 200, 60, (int)(255 * coin.Alpha));
            Raylib.DrawModel(coinModel, view.BoxToScene(view.ToLocal(coin.Position)), view.Zoom, gold);
        }
    }

    private void DrawBirds(ViewWindow view, Bird? selected, Bird? hovered)
    {
        // Animation follows simulation time, so it freezes when paused and speeds up with the sim.
        GameEngine engine = GameEngine.Instance;
        float animDt = engine.Paused ? 0f : Raylib.GetFrameTime() * engine.SimSpeed;

        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (!view.Contains(bird.Position, 0.5f)) continue;

            BirbMotion motion = animator.Animate(bird, animDt);
            Vector3 scenePos = view.BoxToScene(view.ToLocal(bird.Position)) + new Vector3(0f, motion.Bounce * view.Zoom, 0f);
            Color tint = Color.White;
            if (bird == selected) tint = new Color(150, 200, 255, 255);
            else if (bird == hovered) tint = new Color(200, 230, 255, 255);

            BirbBuilder.Draw(bird.Shape, scenePos, bird.Heading + view.Yaw, bird.Size * view.Zoom, tint, motion);

            if (bird == selected)
            {
                Vector3 ringPos = new Vector3(scenePos.X, 0.02f, scenePos.Z);
                Raylib.DrawCircle3D(ringPos, bird.FootprintRadius * 1.1f * view.Zoom, Vector3.UnitX, 90f, Palette.ToColor(HoloColor));
            }
        }
        animator.ForgetUnseen();
    }

    // The glowing edges of a hologram whose floor middle is at 'origin': two rings, one at the floor and one
    // at the ceiling, each a thin bright core plus a wider faint halo, and a curtain of light rising from the floor.
    // Brightness 1 is normal; higher makes it glow more.
    // A circle looks the same however it's turned, so no yaw is needed.
    private const int EdgeSegments = 72;

    public void DrawFieldEdges(Vector3 origin, float radius, float height, float curtainHeight, float brightness)
    {
        Color core = Palette.ToColor(HoloColor, Palette.GlowAlpha(200, brightness));
        Color halo = Palette.ToColor(HoloColor, Palette.GlowAlpha(16, brightness));

        BeginGlowDrawing();
        foreach (float y in new[] { 0f, height })
        {
            for (int i = 0; i < EdgeSegments; i++)
            {
                Vector3 a = origin + PointOnCircle(radius, i, y);
                Vector3 b = origin + PointOnCircle(radius, i + 1, y);
                Raylib.DrawCylinderEx(a, b, 0.08f, 0.08f, 8, halo);
                Raylib.DrawCylinderEx(a, b, 0.018f, 0.018f, 6, core);
            }
        }
        DrawFieldCurtain(origin, radius, curtainHeight, Palette.GlowAlpha(40, brightness));
        EndGlowDrawing();
    }

    // A faint sheet of light rising from the bottom ring, like the edge of a projected field.
    // Bright at the floor, fading to nothing a little way up.
    private void DrawFieldCurtain(Vector3 origin, float radius, float curtainHeight, int bottomAlpha)
    {
        Color bottom = Palette.ToColor(HoloColor, bottomAlpha);
        Color top = Palette.ToColor(HoloColor, 0);

        Rlgl.DisableBackfaceCulling();
        Rlgl.Begin(DrawMode.Quads);
        for (int i = 0; i < EdgeSegments; i++)
        {
            Vector3 a = origin + PointOnCircle(radius, i, 0f);
            Vector3 b = origin + PointOnCircle(radius, i + 1, 0f);
            Rlgl.Color4ub(bottom.R, bottom.G, bottom.B, bottom.A);
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
            Rlgl.Color4ub(top.R, top.G, top.B, top.A);
            Rlgl.Vertex3f(b.X, b.Y + curtainHeight, b.Z);
            Rlgl.Vertex3f(a.X, a.Y + curtainHeight, a.Z);
        }
        Rlgl.End();
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableBackfaceCulling();
    }

    // The point at step i of EdgeSegments around a circle of this radius, at height y.
    private static Vector3 PointOnCircle(float radius, int i, float y)
    {
        float angle = i * MathF.PI * 2f / EdgeSegments;
        return new Vector3(MathF.Cos(angle) * radius, y, MathF.Sin(angle) * radius);
    }

    // Additive blending makes overlapping glow brighter; skipping depth writes
    // keeps the see-through glow from hiding things behind it.
    public static void BeginGlowDrawing()
    {
        Rlgl.DrawRenderBatchActive();
        Raylib.BeginBlendMode(BlendMode.Additive);
        Rlgl.DisableDepthMask();
    }

    public static void EndGlowDrawing()
    {
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableDepthMask();
        Raylib.EndBlendMode();
    }

    // ---------------- SETUP HELPERS ----------------

    private void SetHologramUniforms(Camera3D camera, ViewWindow view, float swallow)
    {
        float time = (float)Raylib.GetTime();
        Raylib.SetShaderValue(HoloShader, locHoloOrigin, Vector3.Zero, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(HoloShader, locBoxYaw, view.Yaw, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locHoloRadius, view.Radius, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locFogStart, view.FogStart, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locSwallow, swallow, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locCubeHeight, view.Height, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locViewCenter, view.Center, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(HoloShader, locViewZoom, view.Zoom, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locViewPos, camera.Position, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(HoloShader, locHoloColor, HoloColor, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(HoloShader, locTime, time, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locIsFloor, 0, ShaderUniformDataType.Int);
    }

    private void SetPostUniforms(int width, int height)
    {
        Raylib.SetShaderValue(postShader, locResolution, new Vector2(width, height), ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(postShader, locPostTime, (float)Raylib.GetTime(), ShaderUniformDataType.Float);
        Raylib.SetShaderValue(postShader, locGlow, GlowStrength, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(postShader, locScanlines, ScanlineStrength, ShaderUniformDataType.Float);
    }

    // Recreate the off-screen texture when the window size changes (resize or fullscreen).
    private void EnsureTargetSize(int width, int height)
    {
        if (sceneTarget.Id != 0 && sceneTarget.Texture.Width == width && sceneTarget.Texture.Height == height)
        {
            return;
        }
        if (sceneTarget.Id != 0) Raylib.UnloadRenderTexture(sceneTarget);
        sceneTarget = Raylib.LoadRenderTexture(width, height);
        Raylib.SetTextureFilter(sceneTarget.Texture, TextureFilter.Bilinear);
    }
}
