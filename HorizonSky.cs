using System.Numerics;
using Raylib_cs;

// The Hologram View's sky: what you see past the garden's far fog while standing in it (see HoloCamera.Immersion).
// A sky that follows the time of day (blue by day, a warm glow on the sun's side at dawn and dusk, stars and the moon
// at night, grayer under rain clouds), with three faint rows of hills and trees on the horizon, rising out of the mist.
// It turns with the garden, so it reads as the land around it. The shapes are drawn by horizon.fs; this class works
// out the colors for this moment and hands them over. Horizon is also what the garden's fog fades into in this view,
// so the ground melts into the sky at the far edge.
//
// Nothing in here changes the world. Drawn over the whole screen behind the hologram, faded in as the camera steps
// into the garden (replacing the room).
public class HorizonSky
{
    // SKY KEYS: sun elevation (degrees) -> the color straight up, at the horizon, and the warm glow on the sun's side.
    // In between two keys the colors blend, like WorldClock's LIGHT KEYS.
    private static readonly (float Elevation, Vector3 Zenith, Vector3 Horizon, Vector3 Glow)[] SkyKeys =
    {
        (-14f, new Vector3(0.035f, 0.050f, 0.110f), new Vector3(0.110f, 0.140f, 0.240f), new Vector3(0f)),                 // night: a moonlit blue-gray at the horizon (5.04, was near-black)
        (-7f,  new Vector3(0.050f, 0.075f, 0.180f), new Vector3(0.200f, 0.190f, 0.330f), new Vector3(0.20f, 0.10f, 0.12f)), // the blue hour
        (-1f,  new Vector3(0.120f, 0.180f, 0.360f), new Vector3(0.520f, 0.380f, 0.400f), new Vector3(0.60f, 0.30f, 0.16f)), // rose glow
        (4f,   new Vector3(0.180f, 0.320f, 0.550f), new Vector3(0.640f, 0.560f, 0.480f), new Vector3(0.55f, 0.36f, 0.18f)), // golden low sun
        (14f,  new Vector3(0.200f, 0.380f, 0.620f), new Vector3(0.560f, 0.640f, 0.680f), new Vector3(0.25f, 0.20f, 0.12f)), // morning or afternoon
        (30f,  new Vector3(0.200f, 0.400f, 0.660f), new Vector3(0.540f, 0.660f, 0.720f), new Vector3(0.12f, 0.11f, 0.08f)), // full day
    };

    // LOOK
    private const float HoloShare = 0.15f;          // how far the sky leans toward the hologram's own color
    private static readonly Vector3 Foliage = new Vector3(0.12f, 0.20f, 0.14f);    // the trees' own color before light and mist
    private const float NearMist = 0.45f;           // how far the nearest trees fade toward the horizon color (0 none, 1 gone)
    private const float FarMist = 0.82f;            // ...and the farthest hills
    private const float CloudGray = 0.85f;          // how gray the sky goes under full cloud
    private const float CloudDarken = 0.8f;         // how bright that gray is, as a share of the clear sky's brightness
    private const float DayMoon = 0.35f;            // how much the moon shows by day (a pale ghost of it)

    // THE DRIFTING CLOUDS (5.05; their shapes come from cloudField in horizon.fs, the same as their shadows on the ground)
    private const float CloudSunLight = 0.8f;       // how much of the sun's (or moon's) light their sunny side catches
    private const float CloudSkyLight = 0.7f;       // ...and of the light from all around
    private const float CloudNightDim = 0.72f;      // at full night they're this much dimmer (moonlit silver, not glowing white)
    private const float CloudUnderside = 0.55f;     // their thick middles and undersides, as a share of the sunny side
    private const float CloudUnderBlue = 0.3f;      // ...leaning this far toward the sky overhead (cool shade)

    // This frame's colors (Refresh)
    public Vector3 Horizon { get; private set; } = new Vector3(0.5f);    // the garden's fog fades into this in the Hologram View
    private Vector3 zenith, glow, treeNear, treeFar, sunDir, moonDir, cloudLit, cloudShade;
    private float stars, sunShows, moonShows, cloudCover;
    private Vector2 cloudDrift, viewCenter;

    private Shader shader;
    private int locResolution, locForward, locRight, locUp, locTanHalfFov, locBoxYaw, locSunDir, locMoonDir;
    private int locZenith, locHorizon, locGlow, locTreeNear, locTreeFar, locStars, locSunShows, locMoonShows, locHoloColor, locTime, locAlpha;
    private int locCloudCover, locCloudDrift, locViewCenter, locCloudLit, locCloudShade;

    public void Load()
    {
        shader = Raylib.LoadShader(null!, Assets.PathTo("shaders/horizon.fs"));    // raylib's own vertex shader
        locResolution = Raylib.GetShaderLocation(shader, "resolution");
        locForward = Raylib.GetShaderLocation(shader, "camForward");
        locRight = Raylib.GetShaderLocation(shader, "camRight");
        locUp = Raylib.GetShaderLocation(shader, "camUp");
        locTanHalfFov = Raylib.GetShaderLocation(shader, "tanHalfFov");
        locBoxYaw = Raylib.GetShaderLocation(shader, "boxYaw");
        locSunDir = Raylib.GetShaderLocation(shader, "sunDir");
        locMoonDir = Raylib.GetShaderLocation(shader, "moonDir");
        locZenith = Raylib.GetShaderLocation(shader, "skyZenith");
        locHorizon = Raylib.GetShaderLocation(shader, "skyHorizon");
        locGlow = Raylib.GetShaderLocation(shader, "sunGlow");
        locTreeNear = Raylib.GetShaderLocation(shader, "treeNear");
        locTreeFar = Raylib.GetShaderLocation(shader, "treeFar");
        locStars = Raylib.GetShaderLocation(shader, "stars");
        locSunShows = Raylib.GetShaderLocation(shader, "sunShows");
        locMoonShows = Raylib.GetShaderLocation(shader, "moonShows");
        locHoloColor = Raylib.GetShaderLocation(shader, "holoColor");
        locTime = Raylib.GetShaderLocation(shader, "time");
        locAlpha = Raylib.GetShaderLocation(shader, "alpha");
        locCloudCover = Raylib.GetShaderLocation(shader, "cloudCover");
        locCloudDrift = Raylib.GetShaderLocation(shader, "cloudDrift");
        locViewCenter = Raylib.GetShaderLocation(shader, "viewCenter");
        locCloudLit = Raylib.GetShaderLocation(shader, "cloudLit");
        locCloudShade = Raylib.GetShaderLocation(shader, "cloudShade");
    }

    public void Unload()
    {
        Raylib.UnloadShader(shader);
    }

    // Works out this moment's colors from the sun's height, the moon, the light on the garden and the clouds.
    // toMoon: where the moon is in the sky; moonLit: how much of its face is lit (0 new, 1 full).
    // light: the garden's light this frame (already dimmed under cloud); cloud: 0 clear to 1 overcast.
    public void Refresh(float sunElevation, Vector3 toSun, Vector3 toMoon, float moonLit, DayLight light, float cloud, Vector3 holoColor)
    {
        Blend(sunElevation, out zenith, out Vector3 horizon, out glow);

        // A touch of the hologram's own color, so the sky belongs to the projection
        zenith = TowardHolo(zenith, holoColor);
        horizon = TowardHolo(horizon, holoColor);

        // Under cloud: grayer and a little darker, no glow, no sun, fewer stars
        zenith = Overcast(zenith, cloud);
        horizon = Overcast(horizon, cloud);
        glow *= 1f - cloud;
        Horizon = horizon;

        // The trees: their own color in this light, faded into the horizon by distance
        float lightLevel = Luminance(light.Ambient + light.Direct * 0.5f);
        Vector3 lit = Foliage * lightLevel * 1.6f;
        treeNear = Vector3.Lerp(lit, horizon, NearMist);
        treeFar = Vector3.Lerp(lit, horizon, FarMist);

        // The drifting clouds: their sunny side takes the color of the light (gold at dawn, white at noon, silver under
        // the moon), their thick middles a cooler, darker shade; under a closing deck both go the overcast sky's gray
        float night = light.Darkness;
        cloudLit = (light.Direct * CloudSunLight + light.Ambient * CloudSkyLight) * (1f - CloudNightDim * night);
        cloudLit = TowardHolo(cloudLit, holoColor);
        cloudShade = Vector3.Lerp(cloudLit * CloudUnderside, zenith, CloudUnderBlue);
        cloudLit = Vector3.Lerp(cloudLit, horizon * 1.05f, cloud);
        cloudShade = Vector3.Lerp(cloudShade, horizon * 0.85f, cloud);

        // The sun, and the moon where it really is (WorldClock's fixed moonlight sits too high to see from the garden);
        // the moon shows faintly by day, and not at all at new moon
        sunDir = toSun;
        moonDir = toMoon;
        stars = Smooth(-4f, -12f, sunElevation) * (1f - cloud * 0.9f);
        sunShows = Smooth(-2f, 1f, sunElevation) * (1f - cloud);
        moonShows = float.Lerp(DayMoon, 1f, Smooth(1f, -5f, sunElevation)) * (1f - cloud) * Smooth(0f, 0.08f, moonLit);
    }

    // The drifting clouds this frame: how much of the sky they cover, how far they've drifted, and where the garden's
    // middle is in the world (the clouds overhead are found from there, so they match the shadows on the ground).
    public void SetClouds(float cover, Vector2 drift, Vector2 center)
    {
        cloudCover = cover;
        cloudDrift = drift;
        viewCenter = center;
    }

    // Draws the sky over the whole screen, seen through the hologram camera, faded in by alpha (the camera's immersion).
    public void Draw(Camera3D camera, float boxYaw, Vector3 holoColor, float alpha)
    {
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, camera.Up));
        Vector3 up = Vector3.Cross(right, forward);
        float tanHalfFov = MathF.Tan(camera.FovY * 0.5f * MathF.PI / 180f);

        Raylib.SetShaderValue(shader, locResolution, new Vector2(Raylib.GetRenderWidth(), Raylib.GetRenderHeight()), ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(shader, locForward, forward, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locRight, right, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locUp, up, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locTanHalfFov, tanHalfFov, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locBoxYaw, boxYaw, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locSunDir, sunDir, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locMoonDir, moonDir, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locZenith, zenith, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locHorizon, Horizon, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locGlow, glow, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locTreeNear, treeNear, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locTreeFar, treeFar, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locStars, stars, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locSunShows, sunShows, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locMoonShows, moonShows, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locHoloColor, holoColor, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locTime, (float)Raylib.GetTime(), ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locAlpha, alpha, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locCloudCover, cloudCover, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, locCloudDrift, cloudDrift, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(shader, locViewCenter, viewCenter, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(shader, locCloudLit, cloudLit, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locCloudShade, cloudShade, ShaderUniformDataType.Vec3);

        Raylib.BeginShaderMode(shader);
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Color.White);
        Raylib.EndShaderMode();
    }

    // ---------------- COLOR HELPERS ----------------

    // The three key colors at this sun elevation, blended between the keys on either side.
    private static void Blend(float elevation, out Vector3 zenith, out Vector3 horizon, out Vector3 glow)
    {
        var first = SkyKeys[0];
        var last = SkyKeys[^1];
        if (elevation <= first.Elevation) { zenith = first.Zenith; horizon = first.Horizon; glow = first.Glow; return; }
        if (elevation >= last.Elevation) { zenith = last.Zenith; horizon = last.Horizon; glow = last.Glow; return; }

        int i = 1;
        while (elevation > SkyKeys[i].Elevation) i++;
        var below = SkyKeys[i - 1];
        var above = SkyKeys[i];
        float t = (elevation - below.Elevation) / (above.Elevation - below.Elevation);
        zenith = Vector3.Lerp(below.Zenith, above.Zenith, t);
        horizon = Vector3.Lerp(below.Horizon, above.Horizon, t);
        glow = Vector3.Lerp(below.Glow, above.Glow, t);
    }

    // Leans a color a little toward the hologram's hue, keeping its brightness.
    private static Vector3 TowardHolo(Vector3 color, Vector3 holoColor)
    {
        float holoLum = MathF.Max(Luminance(holoColor), 0.001f);
        return Vector3.Lerp(color, holoColor * (Luminance(color) / holoLum), HoloShare);
    }

    // A sky color under this much cloud: toward a gray of its own brightness, a little darker.
    private static Vector3 Overcast(Vector3 color, float cloud)
    {
        Vector3 gray = new Vector3(Luminance(color) * CloudDarken);
        return Vector3.Lerp(color, gray, CloudGray * cloud);
    }

    private static float Luminance(Vector3 c) => Vector3.Dot(c, new Vector3(0.299f, 0.587f, 0.114f));

    private static float Smooth(float from, float to, float value)
    {
        float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
