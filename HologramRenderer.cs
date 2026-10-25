using System.Numerics;
using Raylib_cs;

// Draws the whole scene. Nothing in here changes the simulation.
//
// Four steps:
//  1. The hologram is drawn into an off-screen texture (sceneTarget) with a clear background, through the
//     lifted HologramCamera so it floats above the pedestal, and grows in the Hologram View (see HoloCamera).
//     Things inside it use hologram.fs for tint, fog and scan bands. Its contents and the cube's edges
//     are turned by view.Yaw (or the store's Yaw). Nothing is drawn while the projector is off,
//     and while it starts up the picture rises from the floor ('reveal' in hologram.fs).
//     The box shows either the bird world or the store (see BoxMode). While it shows the store,
//     the bird world is also drawn into a small "zoo camera" texture for the world map panel.
//  2. The room is drawn straight to the screen: the backdrop (the cabin, the table, the radio and the other
//     things on it), then the pedestal, its rocker switch and the projector's beams. They're lit by room.fs, which also gets
//     the box's own light while it's on. No glow or scanlines here.
//  3. The hologram texture is laid over the room through post.fs for glow and scanlines.
//     The camera always looks down on the pedestal, so nothing of the pedestal is ever in front of the hologram.
//  4. The player's hands (see Hands), in the room's 3D space again, on top of the hologram.
//
// The pieces live in their own files: WorldDrawer (birds, food, floor...), FieldEdges (the cube's glowing edges and beams),
// StoreRenderer, Pedestal and Backdrop. This file sets up the passes and the shaders they share.
//
// Smooth edges: the window's anti-aliasing (MSAA) doesn't reach an off-screen texture, so instead the scene
// is drawn at twice the screen size. Shrinking it back down blends each 2x2 block of pixels into one.
public class HologramRenderer
{
    // LOOK SETTINGS (changed from the ImGui panel)
    public Vector3 HoloColor = new Vector3(0.36f, 0.86f, 0.82f);
    public float GlowStrength = 0.7f;
    public float ScanlineStrength = 0.12f;
    public bool ShowProjector = true;          // the pedestal, its lens and the beams
    public bool SmoothEdges = true;
    public float WallStrength = 1f;    // the scanline walls of the field; 0 hides them

    private const int MaxSceneSize = 8192;    // biggest texture to ask the graphics card for
    private const float CurtainHeight = 1.7f; // how high the curtain of light rises up the cube's sides
    private const float HoloLightHeight = 0.3f;    // the hologram's light on the room comes from this far above its floor

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

    private readonly Pedestal pedestal = new Pedestal();
    private readonly StoreRenderer storeRenderer = new StoreRenderer();
    private readonly WorldDrawer worldDrawer = new WorldDrawer();
    private readonly FieldEdges fieldEdges = new FieldEdges();
    public Backdrop Backdrop { get; } = new Backdrop();
    public ChatMessages ChatMessages { get; } = new ChatMessages();

    // Shader uniform locations
    private int locHoloOrigin, locBoxYaw, locHoloHalfWidth, locFogStart, locSwallow, locReveal, locCubeHeight, locViewCenter, locViewZoom, locViewPos, locHoloColor, locTime, locIsFloor;
    private int locResolution, locPostTime, locGlow, locScanlines;

    public void Load(ViewWindow view)
    {
        HoloShader = Raylib.LoadShader(Assets.PathTo("shaders/hologram.vs"), Assets.PathTo("shaders/hologram.fs"));
        locHoloOrigin = Raylib.GetShaderLocation(HoloShader, "holoOrigin");
        locBoxYaw = Raylib.GetShaderLocation(HoloShader, "boxYaw");
        locHoloHalfWidth = Raylib.GetShaderLocation(HoloShader, "holoHalfWidth");
        locFogStart = Raylib.GetShaderLocation(HoloShader, "fogStart");
        locSwallow = Raylib.GetShaderLocation(HoloShader, "swallow");
        locReveal = Raylib.GetShaderLocation(HoloShader, "reveal");
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

        fieldEdges.Load();
        worldDrawer.Load(HoloShader, view);
        storeRenderer.Load(HoloShader);
        Backdrop.Load();
        pedestal.Load(Backdrop.RoomShader, view);    // after the Backdrop, whose room shader it uses
        FeedTexture = Raylib.LoadRenderTexture(FeedSize, FeedSize);
    }

    public void Unload()
    {
        worldDrawer.Unload();
        fieldEdges.Unload();
        pedestal.Unload();
        storeRenderer.Unload();
        Backdrop.Unload();
        Raylib.UnloadRenderTexture(FeedTexture);
        ChatMessages.Unload();
        Raylib.UnloadShader(HoloShader);
        Raylib.UnloadShader(postShader);
        if (sceneTarget.Id != 0) Raylib.UnloadRenderTexture(sceneTarget);
    }

    // While the Birb Designer is open, its birb is drawn in place of the world's food, coins and birds.
    public void Draw(HoloCamera holoCamera, ViewWindow view, BoxMode mode, SeedStore store, Bird? selected, Bird? hovered,
                     BirbDesigner designer, Radio radio, Projector projector, Hands hands, Rocker rocker)
    {
        int width = Raylib.GetRenderWidth();
        int height = Raylib.GetRenderHeight();
        bool fits = width * 2 <= MaxSceneSize && height * 2 <= MaxSceneSize;
        int scale = SmoothEdges && fits ? 2 : 1;
        Camera3D roomCamera = holoCamera.Camera;
        Camera3D camera = holoCamera.HologramCamera;    // everything in the hologram pass is seen through this one
        EnsureTargetSize(width * scale, height * scale);

        // These draw into their own small textures, so they must happen before the scene pass.
        if (ShowProjector) pedestal.RefreshReadouts(view, GameEngine.Instance.Coins);
        ChatMessages.PrepareTextures();
        if (mode.InStore && projector.Showing) DrawZooCamera(view, selected);

        SetHologramUniforms(camera, view, mode.Swallow, projector.Cube);
        fieldEdges.SetUniforms(camera, view.Height);

        // The bezel ring, the cube and its beams all turn with the box (the birds and the store share one turn)
        float ringYaw = view.Yaw;

        Raylib.BeginTextureMode(sceneTarget);
            Raylib.ClearBackground(Color.Blank);
            if (projector.Showing)
            {
                Rlgl.SetLineWidth(scale);    // so wire outlines aren't thinner when the scene is drawn bigger
                Raylib.BeginMode3D(camera);
                    if (mode.InStore)
                    {
                        storeRenderer.Draw(store, view);
                        SetHologramUniforms(camera, view, mode.Swallow, projector.Cube);    // the store changed the turn to its own
                    }
                    else
                    {
                        worldDrawer.DrawFloor(view);
                        if (designer.IsOpen) designer.DrawPreview(view);
                        else worldDrawer.DrawContents(view, selected, hovered, Palette.ToColor(HoloColor));
                    }
                    fieldEdges.Draw(view.HalfWidth, view.Height, ringYaw, CurtainHeight, projector.Cube, HoloColor, WallStrength, camera.Position);
                    if (!designer.IsOpen && !mode.InStore) ChatMessages.Draw(camera, view);
                Raylib.EndMode3D();
                Rlgl.SetLineWidth(1f);
            }
        Raylib.EndTextureMode();

        // The box's light on the room: from the hologram (as far as the cube is up) and from the lens
        Vector3 lens = Pedestal.LensSpot(view);
        Vector3 holoLight = new Vector3(0f, holoCamera.HoloFloorY + HoloLightHeight * holoCamera.HoloScale, 0f);
        Backdrop.SetProjectorLights(holoLight, HoloColor * projector.Cube, lens, HoloColor * projector.Beams, Pedestal.WellRadius(view));

        Backdrop.Draw(roomCamera, view.HalfSize, radio);
        if (ShowProjector)
        {
            Raylib.BeginMode3D(roomCamera);
                pedestal.Draw(Backdrop, view, ringYaw, Palette.ToColor(HoloColor), projector);
                rocker.Draw(Backdrop, view, mode.Heading, projector.Lens);
                if (projector.Beams > 0f)
                {
                    // The beams reach the cube's floor corners as it is right now, grown or on the pedestal
                    FieldEdges.DrawProjection(lens, view.HalfWidth * holoCamera.HoloScale, ringYaw, holoCamera.HoloFloorY,
                                              HoloColor, projector.Beams, roomCamera.Position);
                }
            Raylib.EndMode3D();
        }

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

        // 4. The hands, in front of the hologram. Laying the hologram over the room didn't touch the room's depth,
        //    so the radio and the pedestal still hide whatever part of a hand is behind them.
        Raylib.BeginMode3D(roomCamera);
            hands.Draw(Backdrop);
        Raylib.EndMode3D();
    }

    // The bird world as seen by the fixed zoo camera, drawn into FeedTexture.
    // Clear background, the same hologram shader and fog, no glowing edges.
    private void DrawZooCamera(ViewWindow view, Bird? selected)
    {
        SetHologramUniforms(feedCamera, view, swallow: 0f, reveal: 1f);
        Raylib.BeginTextureMode(FeedTexture);
            Raylib.ClearBackground(new Color(12, 16, 22, 255));
            Raylib.BeginMode3D(feedCamera);
                worldDrawer.DrawFloor(view);
                worldDrawer.DrawContents(view, selected, hovered: null, Palette.ToColor(HoloColor));
            Raylib.EndMode3D();
        Raylib.EndTextureMode();
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

    // reveal: how far the projector has the picture up (see hologram.fs); 1 when fully on.
    private void SetHologramUniforms(Camera3D camera, ViewWindow view, float swallow, float reveal)
    {
        float time = (float)Raylib.GetTime();
        Raylib.SetShaderValue(HoloShader, locHoloOrigin, Vector3.Zero, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(HoloShader, locBoxYaw, view.Yaw, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locHoloHalfWidth, view.HalfWidth, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locFogStart, view.FogStart, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locSwallow, swallow, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(HoloShader, locReveal, reveal, ShaderUniformDataType.Float);
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
