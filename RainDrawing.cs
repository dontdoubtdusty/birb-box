using System.Numerics;
using Raylib_cs;

// The rain, the fourth part of WorldDrawer (4.96, Step 10b). Two things:
//  - the floor shader's weather: how hard it's raining (rain rings on the pool and puddles), how wet the ground is
//    (darker dirt), and the puddles themselves (see Puddles.cs and hologram.fs);
//  - the falling drops: thin streaks that fall through the cube and leave a tiny splash on the ground (none on the water,
//    where the floor's rain rings show the hit). They live in box space, so rain looks the same at every zoom and
//    while panning; the fog swallows them at the walls and the ceiling. Where each drop is comes from the weather's
//    rain clock and the drop's number, so nothing is stored and the drops freeze while the game is paused.
//    They go into the grain batches, so all the rain costs no extra draw calls.
public partial class WorldDrawer
{
    // THE DROPS (box units and seconds)
    private const int RainDrops = 420;                 // drops in the cube in steady rain (drizzle: a share)
    private const float RainFallSpeed = 9f;            // box units a second, give or take 15%
    private const float StreakLength = 0.28f, StreakWidth = 0.014f;
    private const float SplatTime = 0.08f;             // how long a drop's splash lingers on the ground
    private static readonly Vector3 RainColor = new Vector3(0.62f, 0.72f, 0.82f);
    private static readonly Vector3 SplatColor = new Vector3(0.70f, 0.80f, 0.88f);

    private int locRain, locGroundWet, locPuddleCount, locPuddles;
    private readonly Vector4[] puddleData = new Vector4[Puddles.MaxPuddles];

    private void FindWeatherUniforms()
    {
        locRain = Raylib.GetShaderLocation(holoShader, "rain");
        locGroundWet = Raylib.GetShaderLocation(holoShader, "groundWet");
        locPuddleCount = Raylib.GetShaderLocation(holoShader, "puddleCount");
        locPuddles = Raylib.GetShaderLocation(holoShader, "puddles");
    }

    // Hands the floor shader the rain, the ground's wetness and each puddle (spot, full size, how full).
    private void SetWeatherUniforms()
    {
        Weather weather = Weather.Instance;
        IReadOnlyList<Puddle> puddles = weather.Puddles.All;
        int count = Math.Min(puddles.Count, Puddles.MaxPuddles);
        for (int i = 0; i < count; i++)
        {
            Puddle p = puddles[i];
            puddleData[i] = new Vector4(p.Spot.X, p.Spot.Y, p.Size, p.Fill);
        }

        Raylib.SetShaderValue(holoShader, locRain, weather.Rain, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(holoShader, locGroundWet, weather.GroundWet, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(holoShader, locPuddleCount, count, ShaderUniformDataType.Int);
        if (count > 0) Raylib.SetShaderValueV(holoShader, locPuddles, puddleData, ShaderUniformDataType.Vec4, count);
    }

    // Each drop falls from the ceiling to the floor at its own speed, splashes for a moment, then starts again
    // somewhere new. More drops fall the harder it rains (the first ones in the list always fall, so they don't jump about).
    private void AddRain(ViewWindow view)
    {
        Weather weather = Weather.Instance;
        int count = (int)(RainDrops * weather.Rain);
        if (count == 0) return;

        float top = view.Height;
        float reach = view.HalfWidth * 0.98f;
        float clock = weather.RainClock;
        Vector3 streak = new Vector3(StreakWidth, StreakLength, StreakWidth);

        for (int i = 0; i < count; i++)
        {
            float speed = RainFallSpeed * (0.85f + 0.3f * Rng.Fixed(i, 0, 0, 91));
            float fallTime = top / speed;
            float round = clock / (fallTime + SplatTime) + Rng.Fixed(i, 0, 0, 92);    // which fall this is, and how far along
            int k = (int)MathF.Floor(round);
            float since = (round - k) * (fallTime + SplatTime);                       // seconds into this fall
            float x = (Rng.Fixed(i, k, 1, 93) * 2f - 1f) * reach;
            float z = (Rng.Fixed(i, k, 2, 93) * 2f - 1f) * reach;

            if (since < fallTime)
            {
                float y = top - since * speed;    // the streak's bottom end
                AddGrain(GrainMesh.Block, new Vector3(x, y + StreakLength * 0.5f, z), Quaternion.Identity, streak, RainColor, view);
                continue;
            }

            // Landed: a tiny splash spreading flat on the ground (on the water the rain rings show it instead)
            Vector3 world = view.ToWorld(new Vector3(x, 0f, z));
            if (Pool.IsWater(world)) continue;
            float spread = (since - fallTime) / SplatTime;
            Vector3 splat = new Vector3(0.06f, 0.012f, 0.06f) * (0.6f + spread);
            AddGrain(GrainMesh.Round, new Vector3(x, 0.01f, z), Quaternion.Identity, splat, SplatColor, view);
        }
    }
}
