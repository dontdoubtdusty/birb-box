using System.Numerics;
using Raylib_cs;

// The sun and the moon under the cube's ceiling in the room view (5.06). They ride one wheel that turns once a day,
// across from each other, around the whole cube along the garden's east-west line (box X, east is +X), so it turns
// with the ring and the sun rises on the side the morning light comes from. The wheel is wider than the cube: the
// sun comes in through the east wall at sunrise, arcs over under the ceiling and leaves through the west wall at
// sunset, fading at the glass like a bird walking into the fog; as it leaves, the moon comes in through the east
// wall, crosses through the night, and leaves in the west just as the sun comes back in through the east wall.
// The rest of the wheel runs outside the cube and under it, out of sight. So in summer the sun crawls and the moon hurries,
// and in winter the other way round. Only one is up at a time; the moon shows its real phase (faint at new moon,
// so the arc is never empty). Not where the real moon is: the Hologram View's sky still uses that (WorldClock).
//
// Drawn in the hologram pass right after the floor, without writing depth, so birds draw over them: they read as a
// sky behind the garden, not things in it. Fades out as the camera steps into the garden (the Hologram View has its
// own sky), during start-up and scene switches, and under cloud. Not shown in the store or the Designer.
// Nothing in here changes the world.
public class SkyOrbs
{
    // THE WHEEL (heights are shares of the cube's height)
    private const float ArcTop = 0.9f;         // the top of the arc
    private const float WallCross = 0.5f;      // where they pass through the side walls
    private static readonly float CrossAngle = 35f * MathF.PI / 180f;    // how steeply they cross the walls (0 level, 90 straight up)
    private const float WallFade = 0.12f;      // they fade over this share of the cube's half-width, centered on the wall (half bright at the glass)

    // SIZES (box units; the square drawn for each is its disc radius times its spread)
    private const float SunRadius = 0.3f;
    private const float SunSpread = 4.5f;      // the halo reaches this many disc radii out
    private const float MoonRadius = 0.28f;
    private const float MoonSpread = 3f;

    // LOOK
    private static readonly Vector3 SunHigh = new Vector3(1f, 0.95f, 0.85f);    // the sun's disc high in the sky
    private static readonly Vector3 SunLow = new Vector3(1f, 0.6f, 0.32f);      // ...and near the horizon
    private static readonly Vector3 MoonColor = new Vector3(0.78f, 0.82f, 0.9f);
    private const float SunHalo = 0.8f;        // the sun's halo, as a share of its disc color
    private const float MoonHalo = 0.22f;      // the moon's
    private const float HoloShare = 0.15f;     // how far both lean toward the hologram's color
    private const float CloudHide = 0.9f;      // how much a full overcast hides them
    private const float Earthshine = 0.1f;     // the moon's dark side
    private const float NewMoonGlow = 0.3f;    // ...near new moon, so a faint disc still crosses the sky

    // This frame's state (Refresh)
    private Vector3 sunSpot, moonSpot;         // box space, where each is on the arc
    private Vector3 sunColor, sunHaloColor, moonColor, moonHaloColor;
    private float sunShows, moonShows, moonLit, earthshine;
    private bool waxing;                       // lit on its west side (before full), else on its east side

    private Shader shader;
    private int locIsMoon, locDiscShare, locDiscColor, locHaloColor, locHaloReach, locStrength, locMoonLight, locEarthshine;

    public void Load()
    {
        shader = Raylib.LoadShader(null!, Assets.PathTo("shaders/skyorb.fs"));    // raylib's own vertex shader
        locIsMoon = Raylib.GetShaderLocation(shader, "isMoon");
        locDiscShare = Raylib.GetShaderLocation(shader, "discShare");
        locDiscColor = Raylib.GetShaderLocation(shader, "discColor");
        locHaloColor = Raylib.GetShaderLocation(shader, "haloColor");
        locHaloReach = Raylib.GetShaderLocation(shader, "haloReach");
        locStrength = Raylib.GetShaderLocation(shader, "strength");
        locMoonLight = Raylib.GetShaderLocation(shader, "moonLight");
        locEarthshine = Raylib.GetShaderLocation(shader, "earthshine");
    }

    public void Unload()
    {
        Raylib.UnloadShader(shader);
    }

    // Works out where the sun and moon are on the wheel this frame, their colors, and how much each shows.
    // cloud: 0 clear to 1 overcast.
    public void Refresh(WorldClock clock, float cloud, ViewWindow view, Vector3 holoColor)
    {
        float clear = 1f - CloudHide * cloud;

        // How far through the day or the night it is; the sun is up by day, the moon by night
        float dayLength = clock.Sunset - clock.Sunrise;
        bool isDay = clock.Hour >= clock.Sunrise && clock.Hour < clock.Sunset;
        float through = isDay ? (clock.Hour - clock.Sunrise) / dayLength
                              : ((clock.Hour - clock.Sunset + 24f) % 24f) / (24f - dayLength);
        float sunAngle = WheelAngle(isDay, through);
        float moonAngle = WheelAngle(!isDay, through);

        // The sun: warmer near the walls (low in the sky)
        sunSpot = OnWheel(sunAngle, view);
        float low = 1f - MathEx.Smooth(MathF.Sin(CrossAngle), MathF.Sin(CrossAngle) + 0.3f, MathF.Sin(sunAngle));
        sunColor = TowardHolo(Vector3.Lerp(SunHigh, SunLow, low), holoColor);
        sunHaloColor = sunColor * SunHalo;
        sunShows = InsideShare(sunSpot, sunAngle, view) * clear;

        // The moon: its real phase, with the dark side glowing faintly near new moon
        moonSpot = OnWheel(moonAngle, view);
        moonLit = clock.MoonLit;
        waxing = clock.MoonPhase < 0.5f;
        earthshine = float.Lerp(NewMoonGlow, Earthshine, MathEx.Smooth(0f, 0.3f, moonLit));
        moonColor = TowardHolo(MoonColor, holoColor);
        moonHaloColor = moonColor * MoonHalo * MathEx.Smooth(0f, 0.3f, moonLit);
        moonShows = InsideShare(moonSpot, moonAngle, view) * clear;
    }

    // How far round the wheel a body is (radians; 0 east, pi / 2 overhead, pi west). up: whether it's its time to be
    // up; through: 0 to 1 through that time. While up it crosses from the east wall to the west wall; while down it
    // goes the rest of the way round, outside and under the cube, back to the east wall.
    private static float WheelAngle(bool up, float through) =>
        up ? CrossAngle + through * (MathF.PI - 2f * CrossAngle)
           : MathF.PI - CrossAngle + through * (MathF.PI + 2f * CrossAngle);

    // How much of a body shows: fading as it nears a side wall and gone past it, and never on the wheel's lower half
    // (where it passes under the floor, in case that ever comes inside the walls).
    private static float InsideShare(Vector3 spot, float angle, ViewWindow view)
    {
        float wall = 1f - MathEx.Smooth(view.HalfWidth * (1f - WallFade * 0.5f), view.HalfWidth * (1f + WallFade * 0.5f), MathF.Abs(spot.X));
        return wall * MathEx.Smooth(0f, 0.05f, MathF.Sin(angle));
    }

    // Draws both through the hologram camera (call inside its BeginMode3D). level: how much of them shows overall
    // (the projector's start-up, the scene switch, the dive into the garden).
    public void Draw(Camera3D camera, ViewWindow view, float level)
    {
        if (level <= 0.001f || (sunShows <= 0.001f && moonShows <= 0.001f)) return;

        // The camera's own sideways and up directions, so each square faces it
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, camera.Up));
        Vector3 up = Vector3.Cross(right, forward);

        Vector3 sun = view.BoxToScene(sunSpot);
        Vector3 moon = view.BoxToScene(moonSpot);

        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableDepthMask();
        Rlgl.SetBlendFactors(GlOne, GlOne, GlFuncAdd);    // color adds; alpha adds how much the room is hidden
        Raylib.BeginBlendMode(BlendMode.Custom);
        Raylib.BeginShaderMode(shader);

            if (moonShows * level > 0.001f)
            {
                // The lit side: west before full, east after (as seen on the screen); how much is lit comes from the real phase
                Vector3 litWay = view.BoxToScene(waxing ? -Vector3.UnitX : Vector3.UnitX);
                Vector2 toSun = new Vector2(Vector3.Dot(litWay, right), Vector3.Dot(litWay, up));
                toSun = toSun.LengthSquared() > 0.0001f ? Vector2.Normalize(toSun) : Vector2.UnitY;
                float cosPhase = 2f * moonLit - 1f;
                float sinPhase = MathF.Sqrt(MathF.Max(0f, 1f - cosPhase * cosPhase));
                Vector3 light = new Vector3(toSun.X * sinPhase, toSun.Y * sinPhase, cosPhase);
                Raylib.SetShaderValue(shader, locMoonLight, light, ShaderUniformDataType.Vec3);
                Raylib.SetShaderValue(shader, locEarthshine, earthshine, ShaderUniformDataType.Float);
                DrawOrb(moon, right, up, isMoon: 1, MoonRadius, MoonSpread, moonColor, moonHaloColor, moonShows * level);
            }
            if (sunShows * level > 0.001f)
            {
                DrawOrb(sun, right, up, isMoon: 0, SunRadius, SunSpread, sunColor, sunHaloColor, sunShows * level);
            }

        Raylib.EndShaderMode();
        Raylib.EndBlendMode();
        Rlgl.EnableDepthMask();
    }

    // One body: its uniforms, then a square facing the camera.
    private void DrawOrb(Vector3 center, Vector3 right, Vector3 up, int isMoon, float radius, float spread,
                         Vector3 disc, Vector3 halo, float strength)
    {
        Raylib.SetShaderValue(shader, locIsMoon, isMoon, ShaderUniformDataType.Int);
        Raylib.SetShaderValue(shader, locDiscShare, 1f / spread, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locDiscColor, disc, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locHaloColor, halo, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locHaloReach, 1f, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locStrength, strength, ShaderUniformDataType.Float);

        float size = radius * spread;
        Vector3 r = right * size, u = up * size;
        Vector3 topLeft = center - r + u, bottomLeft = center - r - u;
        Vector3 bottomRight = center + r - u, topRight = center + r + u;
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(255, 255, 255, 255);
            Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(topLeft.X, topLeft.Y, topLeft.Z);
            Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
            Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(bottomRight.X, bottomRight.Y, bottomRight.Z);
            Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(topRight.X, topRight.Y, topRight.Z);
        Rlgl.End();
        Rlgl.DrawRenderBatchActive();    // each body has its own uniforms, so draw it before the next one changes them
    }

    // Where a point at this angle round the wheel lands, in box space. The wheel is sized so it passes through the
    // side walls at WallCross, at CrossAngle round from level, and peaks at ArcTop.
    private static Vector3 OnWheel(float angle, ViewWindow view)
    {
        float sinCross = MathF.Sin(CrossAngle);
        float across = view.HalfWidth / MathF.Cos(CrossAngle);                       // the wheel's half-width
        float tall = view.Height * (ArcTop - WallCross) / (1f - sinCross);            // ...and half-height
        float middleY = view.Height * WallCross - tall * sinCross;                   // ...and its middle's height
        return new Vector3(MathF.Cos(angle) * across, middleY + MathF.Sin(angle) * tall, 0f);
    }

    // ---------------- HELPERS ----------------

    private const int GlOne = 1, GlFuncAdd = 0x8006;    // OpenGL's names for "times one" and "add"

    // Leans a color a little toward the hologram's hue, keeping its brightness.
    private static Vector3 TowardHolo(Vector3 color, Vector3 holoColor)
    {
        float holoLum = MathF.Max(Luminance(holoColor), 0.001f);
        return Vector3.Lerp(color, holoColor * (Luminance(color) / holoLum), HoloShare);
    }

    private static float Luminance(Vector3 c) => Vector3.Dot(c, new Vector3(0.299f, 0.587f, 0.114f));
}
