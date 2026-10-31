#version 330

// The Hologram View's sky (HorizonSky.cs): drawn over the whole screen behind the hologram while you stand in the garden.
// A sky that follows the time of day, the sun or moon, stars at night, and three faint rows of hills and trees on the
// horizon that rise out of the mist. Everything is worked out from the direction each pixel looks in, turned back into
// box space so the sky and the treeline turn with the garden. The garden's own fog fades into the horizon color.

in vec2 fragTexCoord;
in vec4 fragColor;

uniform vec2 resolution;      // the screen in pixels
uniform vec3 camForward;      // the hologram camera, in scene space
uniform vec3 camRight;
uniform vec3 camUp;
uniform float tanHalfFov;     // tan of half the camera's vertical view angle
uniform float boxYaw;         // how far the garden is turned (radians)
uniform vec3 sunDir;          // toward the sun, in box space (below the horizon at night)
uniform vec3 moonDir;         // toward the moon, in box space
uniform vec3 skyZenith;       // straight up
uniform vec3 skyHorizon;      // at the horizon (the fog's haze matches it)
uniform vec3 sunGlow;         // the warm glow around the sun and along the horizon on its side; strongest at dawn and dusk
uniform vec3 treeNear;        // the nearest row of trees, and the farthest row of hills
uniform vec3 treeFar;
uniform float stars;          // how much the stars and moon show: 0 by day, 1 at night (less under cloud)
uniform float sunShows;       // how much the sun's disc shows (0 under cloud or below the horizon)
uniform float moonShows;      // how much the moon shows: from sunset on, less under cloud
uniform vec3 holoColor;
uniform float time;
uniform float alpha;          // how far the camera has stepped into the garden
uniform float cloudCover;     // how much of the sky the drifting clouds cover (Weather.Cover): 0 clear, 1 solid
uniform vec2 cloudDrift;      // how far they've drifted, in world units (Weather.CloudDrift)
uniform vec2 viewCenter;      // the garden's middle in the world: the clouds overhead are found from here
uniform vec3 cloudLit;        // the clouds' sunny (or moonlit) side
uniform vec3 cloudShade;      // their thick middles and shaded sides

out vec4 finalColor;

// SHAPES (radians above the horizon)
const float SunSize = 0.022;
const float MoonSize = 0.016;
const float StarCells = 140.0;    // star cells per unit of direction; more is finer
const float StarShare = 0.02;     // share of cells with a star
const float RidgeGlow = 0.05;     // the faint hologram line along the nearest ridge
const float CloudHeight = 8.0;    // how high the cloud layer is, in world units (sets how big clouds look overhead)
const float CloudOpacity = 0.92;  // how much a cloud's thick middle hides what's behind it
const float Earthshine = 0.12;    // the moon's dark side, faintly lit, at night

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float hash3(vec3 p)
{
    return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x),
               mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}

// The drifting clouds, by world position: 0 open sky, 1 inside a cloud.
// The SAME function as in hologram.fs (the clouds' shadows on the ground). Keep the two in step.
float cloudField(vec2 world, float cover)
{
    if (cover <= 0.001) return 0.0;
    vec2 p = (world - cloudDrift) * 0.14;
    float f = noise(p) * 0.5 + noise(p * 2.03 + 7.1) * 0.27 + noise(p * 4.1 + 3.7) * 0.15 + noise(p * 8.3 + 1.3) * 0.08;
    f = (f - 0.5) * 1.8 + 0.5;
    float threshold = mix(0.85, 0.0, cover);
    return smoothstep(threshold, threshold + 0.12, f);
}

// Noise around the horizon: sampled on a circle so it joins up seamlessly all the way round.
float around(vec2 compass, float scale, float seed)
{
    return noise(compass * scale + vec2(seed, seed * 1.7));
}

// Tree tops along the horizon: rounded domes of varied width and height, two staggered rows of them overlapping, like
// the canopy of a wood seen from afar. around: 0 to 1 once round the horizon; count: how many crowns fit round it.
// Returns 0 to 1 (1 the tallest crown's top).
float canopy(float around01, float count, float seed)
{
    float top = 0.0;
    for (int k = 0; k < 2; k++)
    {
        float x = around01 * count + float(k) * 0.5;
        float cell = floor(x);
        float f = fract(x);
        for (int j = -1; j <= 1; j++)
        {
            float id = mod(cell + float(j), count);    // wraps, so the crowns join up all the way round
            vec2 h = vec2(hash(vec2(id, seed + float(k))), hash(vec2(id + 0.37, seed - float(k))));
            float center = float(j) + 0.5 + (h.x - 0.5) * 0.5;
            float radius = 0.5 + 0.4 * h.y;
            float u = (f - center) / radius;
            if (abs(u) < 1.0) top = max(top, (0.4 + 0.6 * h.y * h.y) * sqrt(1.0 - u * u));    // mostly low, a few tall
        }
    }
    return top;
}

// How high a row of hills and trees stands at this compass direction (radians above the horizon).
// base: where its lowest land sits; hills: the slow rise and fall of the land; crowns: how many tree tops fit around
// the horizon (more makes them smaller); height: how tall the trees on top are.
float ridge(vec2 compass, float around01, float seed, float base, float hills, float crowns, float height)
{
    float land = (around(compass, 2.0, seed) * 0.65 + around(compass, 5.0, seed + 3.1) * 0.35) * hills;
    float clearing = smoothstep(0.3, 0.5, around(compass, 3.0, seed + 11.0));   // gaps where the trees thin out
    float leaves = around(compass, crowns * 3.0, seed + 5.0) * 0.07;    // a little raggedness on each crown's edge
    return base + land + (canopy(around01, crowns, seed) + leaves) * height * mix(0.25, 1.0, clearing);
}

// Lays one row over the color: solid below its ridge, its foot melting into the mist between 'foot' and 'clear'
// (both heights above the horizon, the same all the way round, so the mist lies level).
vec3 row(vec3 color, float el, float top, float foot, float clear, vec3 rowColor, float aa)
{
    float mist = 1.0 - smoothstep(foot, clear, el);
    vec3 here = mix(rowColor, skyHorizon, mist);
    return mix(color, here, 1.0 - smoothstep(top - aa, top + aa, el));
}

void main()
{
    // The direction this pixel looks in, in scene space, then turned back into box space
    vec2 uv = gl_FragCoord.xy / resolution * 2.0 - 1.0;
    float aspect = resolution.x / resolution.y;
    vec3 ray = normalize(camForward + camRight * uv.x * tanHalfFov * aspect + camUp * uv.y * tanHalfFov);
    float c = cos(boxYaw);
    float s = sin(boxYaw);
    vec3 d = vec3(c * ray.x - s * ray.z, ray.y, s * ray.x + c * ray.z);

    float el = asin(clamp(d.y, -1.0, 1.0));
    vec2 compass = length(d.xz) > 0.0001 ? normalize(d.xz) : vec2(1.0, 0.0);    // which way along the horizon

    // The sky: the horizon color low down, the zenith color overhead
    float up = smoothstep(0.0, 0.9, max(el, 0.0));
    vec3 color = mix(skyHorizon, skyZenith, pow(up, 0.6));

    // The warm glow: a wide band along the horizon on the sun's side, and a softer halo around the sun itself
    vec2 sunCompass = length(sunDir.xz) > 0.0001 ? normalize(sunDir.xz) : vec2(1.0, 0.0);
    float sunSide = max(dot(compass, sunCompass), 0.0);
    float band = exp(-pow(max(el, 0.0) / 0.18, 2.0)) * pow(sunSide, 3.0);
    float halo = pow(max(dot(d, sunDir), 0.0), 24.0);
    color += sunGlow * (band * 0.7 + halo * 0.5);

    // Stars: one in a few cells, each its own brightness, twinkling gently; thinning out toward the horizon
    if (stars > 0.0 && el > 0.0)
    {
        vec3 cellPos = d * StarCells;
        vec3 cell = floor(cellPos);
        float h = hash3(cell);
        if (h < StarShare)
        {
            vec3 spot = vec3(hash3(cell + 3.1), hash3(cell + 5.7), hash3(cell + 9.2)) * 0.6 + 0.2;
            float r = length(fract(cellPos) - spot);
            float twinkle = 0.75 + 0.25 * sin(time * (1.0 + hash3(cell + 2.2) * 2.0) + h * 600.0);
            float bright = (0.35 + 0.65 * hash3(cell + 7.7)) * twinkle;
            float star = (1.0 - smoothstep(0.06, 0.2, r)) * bright;
            color += vec3(0.85, 0.9, 1.0) * star * 1.3 * stars * smoothstep(0.02, 0.25, el);
        }
    }

    // The moon: a pale disc lit on the side facing the sun (so its phase shows), the dark side faintly there at night,
    // and a faint halo that grows with the lit share
    float toMoon = acos(clamp(dot(d, moonDir), -1.0, 1.0));
    float moon = 1.0 - smoothstep(MoonSize * 0.85, MoonSize, toMoon);
    float litSide = 0.0;
    if (moon > 0.0)
    {
        // This spot on the disc as a point on a ball facing us, then whether the sun reaches it
        vec3 across = (d - moonDir * dot(d, moonDir)) / sin(MoonSize);
        float r2 = min(dot(across, across), 1.0);
        vec3 surface = across - moonDir * sqrt(1.0 - r2);
        litSide = smoothstep(-0.06, 0.06, dot(surface, sunDir));
    }
    float moonGlow = dot(-moonDir, sunDir) * 0.5 + 0.5;    // how full it is, from where it stands against the sun
    color += vec3(0.08, 0.1, 0.16) * exp(-toMoon * 14.0) * moonShows * stars * moonGlow;
    color = mix(color, vec3(0.78, 0.82, 0.9), moon * moonShows * (litSide + Earthshine * stars * (1.0 - litSide)));

    // The sun: a soft bright disc
    float toSun = acos(clamp(dot(d, sunDir), -1.0, 1.0));
    float sun = 1.0 - smoothstep(SunSize * 0.7, SunSize, toSun);
    color = mix(color, vec3(1.0, 0.95, 0.85), sun * sunShows);

    // The drifting clouds: where this pixel's line of sight meets the cloud layer. Far off they shrink too small to
    // draw, so toward the horizon they blend into an even band of their average cover. Each cloud is lit on its sunny
    // side (the moon's at night) and darker where more cloud lies between it and the light; thin edges near the sun glow.
    if (cloudCover > 0.001 && el > 0.0)
    {
        vec2 hit = viewCenter + d.xz / max(d.y, 0.02) * CloudHeight;
        float here = cloudField(hit, cloudCover);
        float average = smoothstep(0.0, 1.0, cloudCover) * 0.85;
        float density = mix(average, here, smoothstep(0.03, 0.3, el)) * smoothstep(0.0, 0.025, el);

        vec3 toLight = sunDir.y > -0.05 ? sunDir : moonDir;
        vec2 lightWay = length(toLight.xz) > 0.0001 ? normalize(toLight.xz) : vec2(1.0, 0.0);
        float between = cloudField(hit + lightWay * 4.0, cloudCover);
        float sunny = (1.0 - 0.75 * between) * (1.0 - 0.3 * here);
        vec3 cloud = mix(cloudShade, cloudLit, sunny);
        cloud += sunGlow * pow(max(dot(d, sunDir), 0.0), 6.0) * (1.0 - here) * 1.5;
        cloud += sunGlow * pow(sunSide, 2.0) * (1.0 - 0.5 * here) * 0.9;    // at dawn and dusk the sun's side of the sky lights them rose and gold
        color = mix(color, cloud, density * CloudOpacity);
    }

    // Three rows on the horizon, farthest first: low hills, then woods, then the nearest trees just past the fog.
    // Each rises out of the mist: its foot fades into the horizon color, so the garden's fog runs straight into it.
    // The nearest row reaches down below the horizon, filling the gap down to where the garden fades out.
    float aa = 1.5 * tanHalfFov * 2.0 / resolution.y;    // about a pixel and a half, as an angle
    float around01 = atan(compass.y, compass.x) / 6.2831853 + 0.5;
    float far = ridge(compass, around01, 0.0, 0.002, 0.07, 380.0, 0.008);
    float mid = ridge(compass, around01, 21.0, -0.008, 0.05, 200.0, 0.026);
    float near = ridge(compass, around01, 47.0, -0.024, 0.05, 110.0, 0.05);
    color = row(color, el, far, -0.03, 0.02, treeFar, aa);
    color = row(color, el, mid, -0.08, 0.0, mix(treeFar, treeNear, 0.5), aa);
    color = row(color, el, near, -0.16, -0.01, treeNear, aa);
    color += holoColor * RidgeGlow * exp(-pow((el - near) / (aa * 1.5), 2.0));

    finalColor = vec4(color, alpha);    // normal alpha blending over the fading room
}
