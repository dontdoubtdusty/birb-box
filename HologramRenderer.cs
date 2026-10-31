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
// The Hologram View (the observer mode, see HoloCamera): as the camera steps into the garden, the cube's edges fade out,
// the hologram's fog widens and turns round (observer in hologram.fs), and the room behind it fades into a sky
// (HorizonSky: the time of day's sky with a misty treeline), whose horizon color the fog takes on, so the garden melts
// into it at every edge. Once the room is gone it isn't drawn.
//
// The pieces live in their own files: WorldDrawer (birds, food, floor...), FieldEdges (the cube's glowing edges and beams),
// StoreRenderer, Pedestal and Backdrop. This file sets up the passes and the shaders they share.
//
// Smooth edges: the window's anti-aliasing (MSAA) doesn't reach an off-screen texture, so instead the scene
// is drawn bigger than the screen (Quality: 2x on High, 1.5x on Medium, 1x on Low) and shrunk back down,
// which blends neighboring pixels together. Lower quality draws far fewer pixels.
public class HologramRenderer
{
    // LOOK SETTINGS (changed from the ImGui panel)
    public Vector3 HoloColor = new Vector3(0.36f, 0.86f, 0.82f);
    public float GlowStrength = 0.7f;          // the soft bloom post.fs spreads around bright things
    public float RimGlow = 0.45f;              // the hologram-colored rim on the edges of birds, rocks and feeders (hologram.fs)
    public float ScanlineStrength = 0.12f;
    public bool ShowProjector = true;          // the pedestal, its lens and the beams
    public float Quality = 2f;         // the hologram is drawn at this many times the screen size (1, 1.5 or 2; Esc menu Settings)
    public float WallStrength = 1f;    // the scanline walls of the field; 0 hides them
    public float EdgeWidth = 0.8f;     // things this close to a wall (box units) start turning into plain hologram (hologram.fs edge look)
    public float EdgeStrength = 1f;    // how strong that edge look is: 0 off, 1 normal, 2 strong

    private const int MaxSceneSize = 8192;    // biggest texture to ask the graphics card for
    private const float CurtainHeight = 1.7f; // how high the curtain of light rises up the cube's sides
    private const float HoloLightHeight = 0.3f;    // the hologram's light on the room comes from this far above its floor
    private static readonly Vector3 NightHaze = new Vector3(0.03f, 0.06f, 0.15f);    // the fog's haze at full night
    private const float NightHazeShare = 0.85f;                                       // how far toward it the haze goes
    private const float TwilightSkyHaze = 0.55f;    // at dawn, dusk and the blue hour the room view's haze takes the sky's horizon color, this bright (5.04)
    private const float TwilightSkyShare = 0.85f;   // how far toward it the haze goes at the peak of golden hour or the blue hour
    private const float CloudShadowDepth = 0.75f;    // how much of the sun a drifting cloud's shadow takes away (5.05)
    private const float NightShadowFade = 0.85f;    // at full night the shadows fade by this share (moonlight casts faint ones)

    public Shader HoloShader { get; private set; }
    private Shader grainShader;    // seed grains, many in one draw (grain.vs with hologram.fs); gets the same uniforms as HoloShader
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
    private readonly HorizonSky sky = new HorizonSky();
    private readonly SkyOrbs skyOrbs = new SkyOrbs();    // the sun and moon under the cube's ceiling in the room view (5.06)
    public Backdrop Backdrop { get; } = new Backdrop();
    public ChatMessages ChatMessages { get; } = new ChatMessages();

    // Shader uniform locations
    private HoloUniforms holoUniforms = null!, grainUniforms = null!;
    private DayLight light = DayLight.Noon;    // this frame's light inside the hologram
    private Vector3 haze;                      // this frame's fog haze (SetHologramUniforms); the sky's horizon in the Hologram View
    private bool weatherShows;                 // false while the Designer is open (its birb is always seen at a clear midday)
    private int locResolution, locPostTime, locGlow, locScanlines, locAlphaFix;

    public void Load(ViewWindow view)
    {
        HoloShader = Raylib.LoadShader(Assets.PathTo("shaders/hologram.vs"), Assets.PathTo("shaders/hologram.fs"));
        holoUniforms = new HoloUniforms(HoloShader);

        // Raylib finds grain.vs's per-grain transform (instanceTransform) by its name on its own
        grainShader = Raylib.LoadShader(Assets.PathTo("shaders/grain.vs"), Assets.PathTo("shaders/hologram.fs"));
        grainUniforms = new HoloUniforms(grainShader);
        Raylib.SetShaderValue(grainShader, Raylib.GetShaderLocation(grainShader, "useVertexColor"), 1, ShaderUniformDataType.Int);

        postShader = Raylib.LoadShader(Assets.PathTo("shaders/post.vs"), Assets.PathTo("shaders/post.fs"));
        locResolution = Raylib.GetShaderLocation(postShader, "resolution");
        locPostTime = Raylib.GetShaderLocation(postShader, "time");
        locGlow = Raylib.GetShaderLocation(postShader, "glowStrength");
        locScanlines = Raylib.GetShaderLocation(postShader, "scanlineStrength");
        locAlphaFix = Raylib.GetShaderLocation(postShader, "alphaFix");

        fieldEdges.Load();
        sky.Load();
        skyOrbs.Load();
        worldDrawer.Load(HoloShader, grainShader, view);
        storeRenderer.Load(HoloShader);
        Backdrop.Load();
        pedestal.Load(Backdrop.RoomShader, view);    // after the Backdrop, whose room shader it uses
        FeedTexture = Raylib.LoadRenderTexture(FeedSize, FeedSize);
    }

    public void Unload()
    {
        worldDrawer.Unload();
        fieldEdges.Unload();
        sky.Unload();
        skyOrbs.Unload();
        pedestal.Unload();
        storeRenderer.Unload();
        Backdrop.Unload();
        Raylib.UnloadRenderTexture(FeedTexture);
        ChatMessages.Unload();
        Raylib.UnloadShader(HoloShader);
        Raylib.UnloadShader(grainShader);
        Raylib.UnloadShader(postShader);
        if (sceneTarget.Id != 0) Raylib.UnloadRenderTexture(sceneTarget);
    }

    // While the Birb Designer is open, its birb is drawn in place of the world's food, coins and birds.
    public void Draw(HoloCamera holoCamera, ViewWindow view, BoxMode mode, SeedStore store, Bird? selected, Bird? hovered,
                     BirbDesigner designer, Radio radio, Projector projector, Hands hands, Rocker rocker)
    {
        int width = Raylib.GetRenderWidth();
        int height = Raylib.GetRenderHeight();
        float scale = Quality;
        while (scale > 1f && (width * scale > MaxSceneSize || height * scale > MaxSceneSize)) scale -= 0.5f;
        Camera3D roomCamera = holoCamera.Camera;
        Camera3D camera = holoCamera.HologramCamera;    // everything in the hologram pass is seen through this one

        // The birds and the store follow the time of day, dimmed under cloud; the Birb Designer always shows its birb at midday
        weatherShows = !designer.IsOpen;
        light = weatherShows ? Weather.Instance.Overcast(WorldClock.Instance.Light) : DayLight.Noon;
        if (weatherShows)
        {
            WorldClock clock = WorldClock.Instance;
            Weather weather = Weather.Instance;
            sky.Refresh(clock.SunElevation, clock.SunDirection, clock.MoonSkyDirection, clock.MoonLit, light, weather.Cloud, HoloColor);
            sky.SetClouds(weather.Cover, weather.CloudDrift, view.Center);
            skyOrbs.Refresh(clock, weather.Cloud, view, HoloColor);
        }
        else
        {
            sky.Refresh(45f, DayLight.Noon.Direction, -DayLight.Noon.Direction, 1f, light, cloud: 0f, HoloColor);
            sky.SetClouds(0f, Vector2.Zero, view.Center);
        }
        EnsureTargetSize((int)(width * scale), (int)(height * scale));

        // These draw into their own small textures, so they must happen before the scene pass.
        if (ShowProjector) pedestal.RefreshReadouts(view, GameEngine.Instance.Coins);
        ChatMessages.PrepareTextures();
        if (mode.InStore && projector.Showing) DrawZooCamera(view, selected);

        float immersion = holoCamera.Immersion;
        SetHologramUniforms(camera, view, mode.Swallow, projector.Cube, edges: !mode.InStore, immersion, clouds: !mode.InStore);    // the store has no edge look, and a roof
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
                        SetHologramUniforms(camera, view, mode.Swallow, projector.Cube, edges: false, immersion, clouds: false);    // the store changed the turn to its own
                    }
                    else
                    {
                        worldDrawer.DrawFloor(view);
                        // The sun and moon: only in the room view, fading as the camera dives in and while the scene switches
                        if (!designer.IsOpen) skyOrbs.Draw(camera, view, projector.Cube * (1f - immersion) * (1f - mode.Swallow));
                        if (designer.IsOpen) designer.DrawPreview(view);
                        else worldDrawer.DrawContents(view, view.SceneToBox(camera.Position), selected, hovered, Palette.ToColor(HoloColor));
                    }
                    fieldEdges.Draw(view.HalfWidth, view.Height, ringYaw, CurtainHeight, projector.Cube, HoloColor, WallStrength, camera.Position,
                                    shown: 1f - immersion);
                    if (!designer.IsOpen && !mode.InStore) ChatMessages.Draw(camera, view, HoloColor);
                Raylib.EndMode3D();
                Rlgl.SetLineWidth(1f);
            }
        Raylib.EndTextureMode();

        // The box's light on the room: from the hologram (as far as the cube is up) and from the lens
        Vector3 lens = Pedestal.LensSpot(view);
        Vector3 holoLight = new Vector3(0f, holoCamera.HoloFloorY + HoloLightHeight * holoCamera.HoloScale, 0f);
        Backdrop.SetProjectorLights(holoLight, HoloColor * projector.Cube, lens, HoloColor * projector.Beams, Pedestal.WellRadius(view));

        // Standing in the garden the room has faded away completely, so only its haze is drawn
        if (immersion >= 0.999f) Raylib.ClearBackground(Color.Blank);
        else Backdrop.Draw(roomCamera, view.HalfSize, radio);
        if (ShowProjector && immersion < 0.999f)
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

        if (immersion > 0f) sky.Draw(camera, view.Yaw, HoloColor, immersion);

        // The scene's colors are already multiplied by their alpha, so it's laid over the backdrop
        // with premultiplied blending. The post shader is given the screen size, not the scene texture's,
        // so the glow spreads the same distance either way.
        SetPostUniforms(width, height, immersion);
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
        SetHologramUniforms(feedCamera, view, swallow: 0f, reveal: 1f, observer: 0f);
        Raylib.BeginTextureMode(FeedTexture);
            Raylib.ClearBackground(new Color(12, 16, 22, 255));
            Raylib.BeginMode3D(feedCamera);
                worldDrawer.DrawFloor(view);
                worldDrawer.DrawContents(view, view.SceneToBox(feedCamera.Position), selected, hovered: null, Palette.ToColor(HoloColor));
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
    // Both hologram shaders get the same values, so grains fog and tint like everything else.
    // The light comes from this frame's DayLight: its direction is turned with the hologram, since the sun stays put
    // in the bird world while the ring turns it.
    // edges: whether things near the walls get the edge look (off for the store, whose floor runs into the fog).
    // observer: how far the camera has stepped into the garden (the Hologram View). The edge look fades away with it
    // (no wall should show), the fog turns round (see hologram.fs), and its haze becomes the sky's horizon color.
    // clouds: whether the drifting clouds' shadows fall here (not in the store, which has a roof, nor in the Designer).
    private void SetHologramUniforms(Camera3D camera, ViewWindow view, float swallow, float reveal, bool edges = true, float observer = 0f,
                                     bool clouds = true)
    {
        float time = (float)Raylib.GetTime();
        haze = Vector3.Lerp(HoloColor * 0.3f, NightHaze, light.Darkness * NightHazeShare);
        // Dawn, dusk and the blue hour: the fog takes the sky's own color (rose, gold, violet), dimmed, so a warm
        // morning isn't framed in teal. Midday and full night keep their haze. twilight peaks at golden hour and halfway into the dark.
        float twilight = MathF.Max(light.Golden, 4f * light.Darkness * (1f - light.Darkness));
        haze = Vector3.Lerp(haze, sky.Horizon * TwilightSkyHaze, twilight * TwilightSkyShare);
        if (weatherShows) haze = Weather.Instance.Haze(haze, light);    // grayer under rain clouds
        haze = Vector3.Lerp(haze, sky.Horizon, observer);               // standing in the garden, things fade into the sky
        Vector3 lightDir = Vector3.Transform(light.Direction, Matrix4x4.CreateRotationY(view.Yaw));
        float edgeStrength = (edges ? EdgeStrength : 0f) * (1f - observer);

        // The clouds' shadows: fading as the deck closes over (the whole sky is shade then, already in the light)
        // and mostly gone at night
        float cover = 0f, shadow = 0f;
        Vector2 drift = Vector2.Zero;
        if (clouds && weatherShows)
        {
            Weather weather = Weather.Instance;
            cover = weather.Cover;
            drift = weather.CloudDrift;
            shadow = CloudShadowDepth * (1f - weather.Cloud) * (1f - NightShadowFade * light.Darkness);
        }
        var shared = new SharedValues(light, lightDir, haze, edgeStrength, observer, cover, drift, shadow);
        holoUniforms.Set(camera, view, swallow, reveal, HoloColor, RimGlow, time, EdgeWidth, shared);
        grainUniforms.Set(camera, view, swallow, reveal, HoloColor, RimGlow, time, EdgeWidth, shared);
    }

    // This frame's values that depend on the time of day, the weather and where the camera is, handed to each hologram shader
    private readonly record struct SharedValues(DayLight Light, Vector3 SceneLightDir, Vector3 Haze, float EdgeStrength, float Observer,
                                                float CloudCover, Vector2 CloudDrift, float CloudShadow);

    // The uniforms every hologram shader shares, found once per shader
    private class HoloUniforms
    {
        private readonly Shader shader;
        private readonly int origin, boxYaw, halfWidth, fogStart, swallow, reveal, cubeHeight, viewCenter, viewZoom, viewPos, holoColor, rimGlow, time, isFloor;
        private readonly int lightDir, ambientLight, directLight, hazeColor, edgeWidth, edgeStrength, observer, lampColor, selfLit;
        private readonly int shadeColor, golden, nightLevel, cloudCover, cloudDrift, cloudShadow;

        public HoloUniforms(Shader shader)
        {
            this.shader = shader;
            origin = Raylib.GetShaderLocation(shader, "holoOrigin");
            boxYaw = Raylib.GetShaderLocation(shader, "boxYaw");
            halfWidth = Raylib.GetShaderLocation(shader, "holoHalfWidth");
            fogStart = Raylib.GetShaderLocation(shader, "fogStart");
            swallow = Raylib.GetShaderLocation(shader, "swallow");
            reveal = Raylib.GetShaderLocation(shader, "reveal");
            cubeHeight = Raylib.GetShaderLocation(shader, "cubeHeight");
            viewCenter = Raylib.GetShaderLocation(shader, "viewCenter");
            viewZoom = Raylib.GetShaderLocation(shader, "viewZoom");
            viewPos = Raylib.GetShaderLocation(shader, "viewPos");
            holoColor = Raylib.GetShaderLocation(shader, "holoColor");
            rimGlow = Raylib.GetShaderLocation(shader, "rimGlow");
            time =Raylib.GetShaderLocation(shader, "time");
            isFloor = Raylib.GetShaderLocation(shader, "isFloor");
            lightDir = Raylib.GetShaderLocation(shader, "lightDir");
            ambientLight = Raylib.GetShaderLocation(shader, "ambientLight");
            directLight = Raylib.GetShaderLocation(shader, "directLight");
            hazeColor = Raylib.GetShaderLocation(shader, "hazeColor");
            edgeWidth = Raylib.GetShaderLocation(shader, "edgeWidth");
            edgeStrength = Raylib.GetShaderLocation(shader, "edgeStrength");
            observer = Raylib.GetShaderLocation(shader, "observer");
            lampColor = Raylib.GetShaderLocation(shader, "lampColor");
            selfLit = Raylib.GetShaderLocation(shader, "selfLit");
            shadeColor = Raylib.GetShaderLocation(shader, "shadeColor");
            golden = Raylib.GetShaderLocation(shader, "golden");
            nightLevel = Raylib.GetShaderLocation(shader, "nightLevel");
            cloudCover = Raylib.GetShaderLocation(shader, "cloudCover");
            cloudDrift = Raylib.GetShaderLocation(shader, "cloudDrift");
            cloudShadow = Raylib.GetShaderLocation(shader, "cloudShadow");
        }

        public void Set(Camera3D camera, ViewWindow view, float swallowAmount, float revealAmount, Vector3 color, float rimGlowAmount, float seconds,
                        float edgeWidthAmount, SharedValues v)
        {
            DayLight light = v.Light;
            Raylib.SetShaderValue(shader, origin, Vector3.Zero, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader, boxYaw, view.Yaw, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, halfWidth, view.HalfWidth, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, fogStart, view.FogStartShown, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, swallow, swallowAmount, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, reveal, revealAmount, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, cubeHeight, view.Height, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, viewCenter, view.Center, ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(shader, viewZoom, view.Zoom, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, viewPos, camera.Position, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader, holoColor, color, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader, rimGlow, rimGlowAmount, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, time, seconds, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, isFloor, 0, ShaderUniformDataType.Int);
            Raylib.SetShaderValue(shader, lightDir, v.SceneLightDir, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader, ambientLight, light.Ambient, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader, directLight, light.Direct, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader, hazeColor, v.Haze, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader, edgeWidth, edgeWidthAmount, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, edgeStrength, v.EdgeStrength, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, observer, v.Observer, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, lampColor, Vector3.Zero, ShaderUniformDataType.Vec3);    // only the store lights its lamp
            Raylib.SetShaderValue(shader, selfLit, 0f, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, shadeColor, light.Shade, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader, golden, light.Golden, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, nightLevel, light.Darkness, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, cloudCover, v.CloudCover, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(shader, cloudDrift, v.CloudDrift, ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(shader, cloudShadow, v.CloudShadow, ShaderUniformDataType.Float);
        }
    }

    // alphaFix: how far the camera has stepped into the garden (see post.fs)
    private void SetPostUniforms(int width, int height, float alphaFix)
    {
        Raylib.SetShaderValue(postShader, locResolution, new Vector2(width, height), ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(postShader, locPostTime, (float)Raylib.GetTime(), ShaderUniformDataType.Float);
        Raylib.SetShaderValue(postShader, locGlow, GlowStrength, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(postShader, locScanlines, ScanlineStrength, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(postShader, locAlphaFix, alphaFix, ShaderUniformDataType.Float);
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
