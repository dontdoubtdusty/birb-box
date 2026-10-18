using System.Numerics;
using Raylib_cs;

// Draws the whole scene. Nothing in here changes the simulation.
//
// Two passes:
//  1. The 3D scene is drawn into an off-screen texture (sceneTarget).
//     Things inside the cube use hologram.fs for tint, fog and scan bands.
//  2. That texture is drawn to the screen through post.fs for glow and scanlines.
public class HologramRenderer
{
    // LOOK SETTINGS (changed from the ImGui panel)
    public Color BackgroundColor = new Color(18, 22, 30, 255);
    public Vector3 HoloColor = new Vector3(0.36f, 0.86f, 0.82f);
    public float GlowStrength = 0.7f;
    public float ScanlineStrength = 0.12f;
    public bool ShowProjector = true;

    public Shader HoloShader { get; private set; }
    private Shader postShader;
    private RenderTexture2D sceneTarget;

    private Model floorModel;
    private Model seedModel;

    // Shader uniform locations
    private int locCubeHalf, locCubeHeight, locViewCenter, locViewPos, locHoloColor, locTime, locIsFloor;
    private int locResolution, locBackground, locPostTime, locGlow, locScanlines;

    public void Load(ViewWindow view)
    {
        HoloShader = Raylib.LoadShader("resources/shaders/hologram.vs", "resources/shaders/hologram.fs");
        locCubeHalf = Raylib.GetShaderLocation(HoloShader, "cubeHalf");
        locCubeHeight = Raylib.GetShaderLocation(HoloShader, "cubeHeight");
        locViewCenter = Raylib.GetShaderLocation(HoloShader, "viewCenter");
        locViewPos = Raylib.GetShaderLocation(HoloShader, "viewPos");
        locHoloColor = Raylib.GetShaderLocation(HoloShader, "holoColor");
        locTime = Raylib.GetShaderLocation(HoloShader, "time");
        locIsFloor = Raylib.GetShaderLocation(HoloShader, "isFloor");

        postShader = Raylib.LoadShader("resources/shaders/post.vs", "resources/shaders/post.fs");
        locResolution = Raylib.GetShaderLocation(postShader, "resolution");
        locBackground = Raylib.GetShaderLocation(postShader, "backgroundColor");
        locPostTime = Raylib.GetShaderLocation(postShader, "time");
        locGlow = Raylib.GetShaderLocation(postShader, "glowStrength");
        locScanlines = Raylib.GetShaderLocation(postShader, "scanlineStrength");

        // The floor is exactly the size of the cube; fog fades it out near the edges.
        floorModel = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(view.HalfSize * 2, view.HalfSize * 2, 1, 1));
        seedModel = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(1f, 8, 8));
        Shader holo = HoloShader;
        Raylib.SetMaterialShader(ref floorModel, 0, ref holo);
        Raylib.SetMaterialShader(ref seedModel, 0, ref holo);
    }

    public void Unload()
    {
        Raylib.UnloadModel(floorModel);
        Raylib.UnloadModel(seedModel);
        Raylib.UnloadShader(HoloShader);
        Raylib.UnloadShader(postShader);
        if (sceneTarget.Id != 0) Raylib.UnloadRenderTexture(sceneTarget);
    }

    public void Draw(OrbitCamera orbit, ViewWindow view, Bird? selected, Bird? hovered)
    {
        int width = Raylib.GetRenderWidth();
        int height = Raylib.GetRenderHeight();
        EnsureTargetSize(width, height);
        SetHologramUniforms(orbit, view);

        Raylib.BeginTextureMode(sceneTarget);
            Raylib.ClearBackground(BackgroundColor);
            Raylib.BeginMode3D(orbit.Camera);
                if (ShowProjector) DrawProjectorBase(view);
                DrawFloor();
                DrawFood(view);
                DrawBirds(view, selected, hovered);
                DrawCubeEdges(view);
            Raylib.EndMode3D();
        Raylib.EndTextureMode();

        SetPostUniforms(width, height);
        Raylib.BeginShaderMode(postShader);
            // Render textures are stored upside down, so flip the source height.
            Rectangle source = new Rectangle(0, 0, sceneTarget.Texture.Width, -sceneTarget.Texture.Height);
            Rectangle dest = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            Raylib.DrawTexturePro(sceneTarget.Texture, source, dest, Vector2.Zero, 0f, Color.White);
        Raylib.EndShaderMode();
    }

    // ---------------- SCENE PIECES ----------------

    private void DrawFloor()
    {
        Raylib.SetShaderValue(HoloShader, locIsFloor, 1, ShaderUniformDataType.Int);
        Raylib.DrawModel(floorModel, Vector3.Zero, 1f, Color.White);
        Raylib.SetShaderValue(HoloShader, locIsFloor, 0, ShaderUniformDataType.Int);
    }

    private void DrawFood(ViewWindow view)
    {
        Color seedColor = new Color(214, 178, 112, 255);
        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (!view.Contains(food.Position, 0.5f)) continue;

            Vector3 local = view.ToLocal(food.Position);
            // One little seed ball per bite left, arranged in a small cluster
            for (int i = 0; i < food.Bites; i++)
            {
                float angle = i * 2.1f + food.EntityID;
                Vector3 offset = new Vector3(MathF.Cos(angle) * 0.1f, 0.07f, MathF.Sin(angle) * 0.1f);
                Raylib.DrawModel(seedModel, local + offset, 0.08f, seedColor);
            }
        }
    }

    private void DrawBirds(ViewWindow view, Bird? selected, Bird? hovered)
    {
        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (!view.Contains(bird.Position, 0.5f)) continue;

            Vector3 local = view.ToLocal(bird.Position);
            Color tint = Color.White;
            if (bird == selected) tint = new Color(150, 200, 255, 255);
            else if (bird == hovered) tint = new Color(200, 230, 255, 255);

            float angleDegrees = bird.Heading * Raylib.RAD2DEG;
            Model model = ModelLibrary.Get(bird.ModelKey);
            Raylib.DrawModelEx(model, local, Vector3.UnitY, angleDegrees, new Vector3(bird.Size), tint);

            if (bird == selected)
            {
                Vector3 ringPos = new Vector3(local.X, 0.02f, local.Z);
                Raylib.DrawCircle3D(ringPos, bird.PickRadius * 1.3f, Vector3.UnitX, 90f, ToColor(HoloColor, 255));
            }
        }
    }

    // Glowing cube frame: a thin bright core plus a wider faint halo on each edge.
    private void DrawCubeEdges(ViewWindow view)
    {
        float h = view.HalfSize;
        float top = view.Height;
        Vector3[] c =
        {
            new(-h, 0, -h), new(h, 0, -h), new(h, 0, h), new(-h, 0, h),
            new(-h, top, -h), new(h, top, -h), new(h, top, h), new(-h, top, h)
        };
        int[,] edges = { {0,1},{1,2},{2,3},{3,0}, {4,5},{5,6},{6,7},{7,4}, {0,4},{1,5},{2,6},{3,7} };

        Color core = ToColor(HoloColor, 200);
        Color halo = ToColor(HoloColor, 16);

        BeginGlowDrawing();
        for (int i = 0; i < edges.GetLength(0); i++)
        {
            Vector3 a = c[edges[i, 0]];
            Vector3 b = c[edges[i, 1]];
            Raylib.DrawCylinderEx(a, b, 0.08f, 0.08f, 8, halo);
            Raylib.DrawCylinderEx(a, b, 0.018f, 0.018f, 6, core);
        }
        foreach (Vector3 corner in c)
        {
            Raylib.DrawSphereEx(corner, 0.05f, 6, 6, core);
        }
        DrawFieldCurtain(view);
        EndGlowDrawing();
    }

    // A faint sheet of light rising from the bottom edges, like the edge of a projected field.
    // Bright at the floor, fading to nothing a little way up.
    private void DrawFieldCurtain(ViewWindow view)
    {
        float h = view.HalfSize;
        float curtainHeight = 1.4f;
        Vector3[] corners = { new(-h, 0, -h), new(h, 0, -h), new(h, 0, h), new(-h, 0, h) };
        Color bottom = ToColor(HoloColor, 40);
        Color top = ToColor(HoloColor, 0);

        Rlgl.DisableBackfaceCulling();
        Rlgl.Begin(DrawMode.Quads);
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = corners[i];
            Vector3 b = corners[(i + 1) % 4];
            Rlgl.Color4ub(bottom.R, bottom.G, bottom.B, bottom.A);
            Rlgl.Vertex3f(a.X, 0, a.Z);
            Rlgl.Vertex3f(b.X, 0, b.Z);
            Rlgl.Color4ub(top.R, top.G, top.B, top.A);
            Rlgl.Vertex3f(b.X, curtainHeight, b.Z);
            Rlgl.Vertex3f(a.X, curtainHeight, a.Z);
        }
        Rlgl.End();
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableBackfaceCulling();
    }

    // A dark puck under the cube with a soft cone of light shining up into the floor.
    private void DrawProjectorBase(ViewWindow view)
    {
        Vector3 baseTop = new Vector3(0, -1.3f, 0);
        Raylib.DrawCylinder(new Vector3(0, -1.65f, 0), 1.3f, 1.6f, 0.35f, 32, new Color(28, 34, 44, 255));

        BeginGlowDrawing();
            Raylib.DrawCylinderWires(baseTop, 0.9f, 0.9f, 0.01f, 32, ToColor(HoloColor, 160));
            Raylib.DrawCylinderEx(baseTop, new Vector3(0, -0.02f, 0), 0.5f, view.HalfSize * 0.95f, 32,
                                  ToColor(HoloColor, 14));
        EndGlowDrawing();
    }

    // Additive blending makes overlapping glow brighter; skipping depth writes
    // keeps the see-through glow from hiding things behind it.
    private static void BeginGlowDrawing()
    {
        Rlgl.DrawRenderBatchActive();
        Raylib.BeginBlendMode(BlendMode.Additive);
        Rlgl.DisableDepthMask();
    }

    private static void EndGlowDrawing()
    {
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableDepthMask();
        Raylib.EndBlendMode();
    }

    // ---------------- SETUP HELPERS ----------------

    private void SetHologramUniforms(OrbitCamera orbit, ViewWindow view)
    {
        float time = (float)Raylib.GetTime();
        Raylib.SetShaderValue(HoloShader, locCubeHalf, view.HalfSize, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locCubeHeight, view.Height, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locViewCenter, view.Center, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(HoloShader, locViewPos, orbit.Camera.Position, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(HoloShader, locHoloColor, HoloColor, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(HoloShader, locTime, time, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locIsFloor, 0, ShaderUniformDataType.Int);
    }

    private void SetPostUniforms(int width, int height)
    {
        Vector3 bg = new Vector3(BackgroundColor.R, BackgroundColor.G, BackgroundColor.B) / 255f;
        Raylib.SetShaderValue(postShader, locResolution, new Vector2(width, height), ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(postShader, locBackground, bg, ShaderUniformDataType.Vec3);
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

    private static Color ToColor(Vector3 rgb, int alpha)
    {
        return new Color(
            (int)Math.Clamp(rgb.X * 255f, 0, 255),
            (int)Math.Clamp(rgb.Y * 255f, 0, 255),
            (int)Math.Clamp(rgb.Z * 255f, 0, 255),
            alpha);
    }
}
