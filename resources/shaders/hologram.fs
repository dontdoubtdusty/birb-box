#version 330

// Used for everything INSIDE the hologram: the grass-and-dirt floor, food, birbs and the Gilded Granary.
// It adds simple lighting (following the time of day, see WorldClock), the hologram tint, rim glow, scan bands, and the edge fog.

in vec3 fragPosition;
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;

uniform sampler2D texture0;
uniform sampler2D texture1;  // the floor's wear map (GroundWear): r 0 grass, 1 worn bare; a lush grass around rocks
uniform vec4 colDiffuse;

uniform vec3 holoOrigin;     // middle of this hologram's floor, normally (0,0,0)
uniform float boxYaw;        // how far the hologram is turned (radians)
uniform float holoHalfWidth; // half the width of the hologram cube
uniform float fogStart;      // share of the way to the sides that stays clear before the fog begins
uniform float swallow;       // 0 normally; rises to 1 while switching scenes, rolling the fog in until nothing shows
uniform float reveal;        // 1 normally; while the projector starts it rises from 0 and the picture grows up from the floor
uniform float cubeHeight;
uniform vec2 viewCenter;     // world X,Z at the hologram's center (so the ground pattern pans)
uniform float viewZoom;      // box units per world unit (so the ground pattern zooms)
uniform float worldHalfSize; // the world spans -worldHalfSize..worldHalfSize (to find a spot on the wear map)
uniform vec3 viewPos;        // camera position, for rim glow
uniform vec3 holoColor;
uniform float time;
uniform int isFloor;
uniform int flatShade;       // 1 while drawing birbs: each triangle gets one flat shade (the faceted look)
uniform float moss;          // 0 normally; while drawing a rock, how mossy its tops are (0 to 1)
uniform int useVertexColor;  // 1 only in the grain shader (grain.vs), which hands each grain's color over in fragColor
uniform vec3 lightDir;       // toward the sun (or the moon at night), in scene space
uniform vec3 ambientLight;   // light from all around: 0.45 at midday, dim blue at night
uniform vec3 directLight;    // light from the sun or moon: 0.65 at midday, golden when low, faint at night
uniform vec3 hazeColor;      // what fading things shift toward near the fog: hologram-tinted by day, deep blue at night
uniform int poolLobeCount;   // the pool (Pool.cs): how many circles make up its shape, 0 for none
uniform vec3 poolLobes[3];   // each circle's world X, world Z and radius
uniform int rippleCount;     // ripple rings on the water right now
uniform vec4 ripples[8];     // each ring's world X, world Z, age in seconds, strength 0 to 1
uniform float rippleLife;    // seconds a ring lasts (Pool.RippleLife)
uniform float edgeWidth;     // how far in from the cube's walls (box units) things start turning to pure hologram
uniform float edgeStrength;  // how strong that edge look is: 0 off, 1 the normal look, up to 2 (0 while drawing the store)

out vec4 finalColor;

// EDGE LOOK: shares and sizes the edge look reaches at the wall, at edgeStrength 1
const float EdgeTint = 0.7;          // how far the color goes to plain hologram blue
const float EdgeGaps = 0.45;         // share of each stripe that's see-through
const float EdgeSeeThrough = 0.45;   // how much fainter the solid stripes get
const float EdgeRim = 0.6;           // extra rim glow
const float EdgeCutGlow = 0.9;       // the bright line where the fog cuts through
const float StripesPerUnit = 24.0;   // horizontal stripes per box unit of height
const float StripeDrift = 0.5;       // how fast the stripes drift upward (stripes per second)

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x),
               mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}

// Bare dirt, with a few light seed specks scattered on it.
vec3 dirtColor(vec2 world)
{
    float n = noise(world * 1.3) * 0.65 + noise(world * 5.0) * 0.35;
    vec3 dirt = mix(vec3(0.27, 0.19, 0.12), vec3(0.42, 0.30, 0.19), n);

    vec2 cell = floor(world * 3.0);
    vec2 inCell = fract(world * 3.0);
    vec2 seedPos = vec2(hash(cell + 1.3), hash(cell + 7.7)) * 0.6 + 0.2;
    float hasSeed = step(0.8, hash(cell));
    float seed = hasSeed * (1.0 - smoothstep(0.03, 0.06, length(inCell - seedPos)));
    return mix(dirt, vec3(0.86, 0.74, 0.48), seed);
}

// Grass: soft light and dark clumps, plus fine speckle so it reads as many small blades.
vec3 grassColor(vec2 world)
{
    float clumps = noise(world * 0.9) * 0.6 + noise(world * 3.0) * 0.4;
    vec3 grass = mix(vec3(0.20, 0.33, 0.12), vec3(0.33, 0.47, 0.18), clumps);
    float blades = noise(world * 14.0);
    return grass * (0.85 + 0.3 * blades);
}

// The ground at a world position: patches of grass and dirt fixed in the world, with worn spots (texture1) turning bare.
vec3 groundColor(vec2 world)
{
    // Big soft patches; above the threshold is grass. A little fine noise keeps the patch edges ragged.
    float patches = noise(world * 0.16) * 0.6 + noise(world * 0.45) * 0.3 + noise(world * 2.2) * 0.1;
    float grass = smoothstep(0.36, 0.46, patches);

    // Wear: 0 untouched, 1 bare. The ragged noise makes worn spots fray at the edges instead of being round.
    vec2 wearUV = (world + worldHalfSize) / (2.0 * worldHalfSize);
    vec4 wearMap = texture(texture1, wearUV);
    float wear = wearMap.r;
    float lush = wearMap.a;
    float frayed = wear + (noise(world * 4.0) - 0.5) * 0.3;

    // Around rocks the grass fills in even where the patches would leave dirt
    grass = max(grass, lush * smoothstep(0.1, 0.5, lush + (noise(world * 3.0) - 0.5) * 0.4));
    grass *= 1.0 - smoothstep(0.25, 0.65, frayed);

    // ...and grows deeper green, with thicker blades
    vec3 grassLook = grassColor(world);
    grassLook = mix(grassLook, grassLook * vec3(0.85, 1.12, 0.8) * (0.9 + 0.25 * noise(world * 22.0)), lush * 0.8);

    vec3 ground = mix(dirtColor(world), grassLook, grass);

    // Faint world grid, so panning is easy to see
    vec2 g = abs(fract(world * 0.5) - 0.5);
    float line = smoothstep(0.485, 0.5, max(g.x, g.y));
    ground += holoColor * line * 0.06;

    return ground;
}

// ---------------- THE POOL ----------------
// The same shape math as Pool.DistanceAt: how far a world spot is from the shore, below 0 in the water.

float smoothMin(float a, float b, float k)
{
    float h = max(k - abs(a - b), 0.0) / k;
    return min(a, b) - h * h * k * 0.25;
}

float waterDistance(vec2 world)
{
    float d = 100000.0;
    for (int i = 0; i < 3; i++)
    {
        if (i >= poolLobeCount) break;
        float here = length(world - poolLobes[i].xy) - poolLobes[i].z;
        d = (i == 0) ? here : smoothMin(d, here, 0.9);
    }
    return d;
}

// How high the water surface stands at a world spot: two slow drifting swells, plus the ripple rings.
// Only its slope is used (for the glints); the surface itself is drawn flat.
float waterHeight(vec2 world)
{
    float h = noise(world * 1.7 + time * vec2(0.11, 0.07)) * 0.012
            + noise(world * 4.1 - time * vec2(0.09, 0.14)) * 0.005;
    for (int i = 0; i < 8; i++)
    {
        if (i >= rippleCount) break;
        float age = ripples[i].z;
        float r = length(world - ripples[i].xy);
        float front = 0.15 + age * 0.6;                              // the ring spreads outward
        float fadeOut = 1.0 - age / rippleLife;
        float ring = exp(-pow((r - front) / 0.12, 2.0)) * sin((r - front) * 40.0);
        h += ring * ripples[i].w * fadeOut * fadeOut * 0.015;
    }
    return h;
}

// The water's color at a world spot before lighting: lighter and showing a little ground in the shallows, deep teal in the middle.
vec3 waterColor(float d, vec3 ground)
{
    float depth = smoothstep(0.0, 1.4, -d);
    vec3 water = mix(vec3(0.15, 0.25, 0.25), vec3(0.025, 0.07, 0.10), depth);
    return mix(water, ground * 0.5, (1.0 - smoothstep(0.0, 0.3, -d)) * 0.45);
}

void main()
{
    // fragPosition is in scene space, already turned with the hologram.
    // boxPos moves it back to this hologram's origin and undoes the turn, so the fog, ground and bands line up with it.
    vec3 p = fragPosition - holoOrigin;
    float c = cos(boxYaw);
    float s = sin(boxYaw);
    vec3 boxPos = vec3(c * p.x - s * p.z, p.y,
                       s * p.x + c * p.z);

    // Color comes from the texture and the material color (colDiffuse).
    // fragColor is ignored: the meshes have no vertex colors, and some
    // drivers fill the missing value with zero, which made birds invisible.
    // The exception is seed grains: grain.vs fills fragColor with each grain's own color.
    vec4 base = useVertexColor == 1 ? vec4(fragColor.rgb, 1.0) : texture(texture0, fragTexCoord) * colDiffuse;

    // Guard against a zero-length normal, which would turn into NaN
    vec3 normal = length(fragNormal) > 0.0001 ? normalize(fragNormal) : vec3(0.0, 1.0, 0.0);

    // How fragPosition changes across neighboring pixels gives two lines lying flat on this triangle;
    // their cross product points straight out of it. Flipped if needed so it faces the camera.
    if (flatShade == 1)
    {
        normal = normalize(cross(dFdx(fragPosition), dFdy(fragPosition)));
        if (dot(normal, viewPos - fragPosition) < 0.0) normal = -normal;
    }

    // Moss settles on a rock's upward-facing facets, in soft patches fixed in the world
    if (moss > 0.0)
    {
        vec2 world = boxPos.xz / viewZoom + viewCenter;
        float up = smoothstep(0.55, 0.85, normal.y);
        float patches = smoothstep(0.35, 0.6, noise(world * 2.5) * 0.7 + noise(world * 7.0) * 0.3);
        vec3 mossColor = vec3(0.24, 0.38, 0.14) * (0.85 + 0.3 * noise(world * 18.0));
        base.rgb = mix(base.rgb, mossColor, up * patches * moss);
    }

    // The pool is part of the floor: water where waterDistance is below 0, damp dark ground along the shore.
    float wet = 0.0;                     // 1 on the water, 0 on land (soft across the waterline)
    float waterline = 0.0;               // 1 right at the waterline, for its faint glow
    float rippleShine = 0.0;
    if (isFloor == 1)
    {
        vec2 world = boxPos.xz / viewZoom + viewCenter;
        vec3 ground = groundColor(world);
        normal = vec3(0.0, 1.0, 0.0);

        float d = poolLobeCount > 0 ? waterDistance(world) : 100000.0;
        if (d < 0.5)
        {
            // A little ragged noise so the shore isn't a perfect curve; aa softens the waterline over about a pixel
            d += (noise(world * 5.0) - 0.5) * 0.06;
            float aa = max(fwidth(d), 0.002);
            wet = 1.0 - smoothstep(-aa, aa, d);
            waterline = exp(-pow(d / max(0.025, aa * 1.2), 2.0));

            // Damp ground along the shore: darker and a touch cooler toward the water
            float damp = 1.0 - smoothstep(0.0, 0.4, d);
            ground = mix(ground, ground * vec3(0.55, 0.6, 0.62), damp * 0.8);

            // The surface's slope (from the swells and ripples) tips the normal, in world axes, then turned into scene space
            const float e = 0.03;
            float h0 = waterHeight(world);
            vec2 slope = vec2(waterHeight(world + vec2(e, 0.0)) - h0, waterHeight(world + vec2(0.0, e)) - h0) / e;
            vec3 n = normalize(vec3(-slope.x, 1.0, -slope.y));
            vec3 sceneNormal = normalize(vec3(c * n.x + s * n.z, n.y, -s * n.x + c * n.z));
            normal = normalize(mix(normal, sceneNormal, wet));
            rippleShine = clamp(length(slope) * 4.0, 0.0, 1.0) * wet;

            ground = mix(ground, waterColor(d, ground), wet);
        }
        base = vec4(ground, 1.0);
    }

    // Light from all around, plus the sun (or moon) from its side
    float diffuse = max(dot(normal, normalize(lightDir)), 0.0);
    vec3 color = base.rgb * (ambientLight + directLight * diffuse);

    // On the water: the sun's (or moon's) glint off the moving surface, a faint sky sheen at low angles,
    // a few slow sparkles, and the waterline's soft hologram glow
    if (wet > 0.0 || waterline > 0.0)
    {
        vec3 toCamera = normalize(viewPos - fragPosition);
        vec3 halfway = normalize(normalize(lightDir) + toCamera);
        float glint = pow(max(dot(normal, halfway), 0.0), 160.0);
        float sheen = pow(1.0 - max(dot(normal, toCamera), 0.0), 4.0);
        vec2 world = boxPos.xz / viewZoom + viewCenter;
        vec2 cell = floor(world * 5.0);
        float sparkle = step(0.9, hash(cell)) * pow(max(sin(time * (1.0 + hash(cell + 3.1) * 1.5) + hash(cell + 9.7) * 6.28), 0.0), 24.0)
                      * (1.0 - smoothstep(0.08, 0.14, length(fract(world * 5.0) - 0.5)));
        color += directLight * (glint * 0.45 + sparkle * 0.3 + rippleShine * 0.06) * wet;
        color += ambientLight * sheen * 0.12 * wet;
        color += holoColor * waterline * 0.1;
    }

    // Hologram tint: blend part of the color toward the hologram hue
    float lum = dot(color, vec3(0.299, 0.587, 0.114));
    // (the floor gets less tint so the grass still reads as green and the dirt as brown)
    float tintAmount = (isFloor == 1) ? 0.08 : 0.25;
    color = mix(color, holoColor * lum * 1.6, tintAmount);

    // Rim glow on the edges of models
    if (isFloor == 0)
    {
        vec3 toCamera = normalize(viewPos - fragPosition);
        float rim = pow(1.0 - max(dot(normal, toCamera), 0.0), 3.0);
        color += holoColor * rim * 0.45;
    }

    // Edge look: things nearing a wall turn into plain hologram, like a projected figure (not the floor).
    // 'holo' is 0 more than edgeWidth from every wall and edgeStrength right at a wall.
    float holo = 0.0;
    vec2 q = abs(boxPos.xz) / holoHalfWidth;
    float edge = max(q.x, q.y);
    if (isFloor == 0 && edgeStrength > 0.0)
    {
        float toWall = holoHalfWidth * (1.0 - edge);
        holo = (1.0 - smoothstep(0.0, max(edgeWidth, 0.01), toWall)) * edgeStrength;
        vec3 toCamera = normalize(viewPos - fragPosition);
        float rim = pow(1.0 - max(dot(normal, toCamera), 0.0), 2.0);
        color = mix(color, holoColor * (0.35 + lum * 1.4), min(holo * EdgeTint, 1.0));
        color += holoColor * rim * EdgeRim * holo;
    }

    // Fine horizontal scan bands drifting upward
    float bands = 0.5 + 0.5 * sin(boxPos.y * 55.0 - time * 4.0);
    color += holoColor * bands * 0.04;

    // A slow bright sweep that rises through the cube every few seconds
    float sweepY = mod(time * 0.9, cubeHeight + 3.0) - 1.5;
    float sweep = exp(-pow((boxPos.y - sweepY) * 3.5, 2.0));
    color += holoColor * sweep * 0.2;

    // Fog: a thin fade at the cube's sides, and a softer one toward the ceiling.
    // 'edge' is 0 in the middle and 1 at a side or corner (same math as ViewWindow.EdgeAmount), so the ground
    // fills the square. Past 1 fade is 0, so the floor beyond the walls gets discarded.
    // Swallow moves both ends of the fade inward, past the middle, so the fog closes in from the sides.
    float clearUntil = mix(fogStart, -0.4, swallow);
    float goneBy = mix(1.0, 0.0, swallow);
    float fade = 1.0 - smoothstep(clearUntil, goneBy, edge);
    fade *= 1.0 - smoothstep(cubeHeight * 0.7, cubeHeight, boxPos.y);

    // Starting up: only what's below the rising edge shows, softly, and the whole picture brightens as it rises
    float revealY = reveal * (cubeHeight + 1.0) - 0.5;
    fade *= 1.0 - smoothstep(revealY - 0.6, revealY, boxPos.y);
    fade *= smoothstep(0.0, 0.4, reveal);

    // As things fade, they also shift toward a dim haze (hologram-colored by day, deep blue at night)
    color = mix(color, hazeColor, (1.0 - fade) * 0.6);

    // Edge look, part two: horizontal see-through stripes that open up toward the wall,
    // and a bright line where the fog is cutting the thing off (brightest halfway through the fade)
    float solid = 1.0;
    if (holo > 0.0)
    {
        float stripe = boxPos.y * StripesPerUnit - time * StripeDrift;
        float across = abs(fract(stripe) - 0.5) * 2.0;    // 0 in a stripe's middle, 1 at its ends
        // Stripes fade out on faces pointing up or down (a rock's top), where they'd spread into wide blotches
        float side = 1.0 - smoothstep(0.55, 0.9, abs(normal.y));
        float gap = min(holo * EdgeGaps, 0.85) * side;
        float aa = fwidth(stripe) * 2.0;                   // soft over about a pixel, so the stripes don't shimmer
        solid = 1.0 - smoothstep(1.0 - gap - aa, 1.0 - gap + aa, across) * smoothstep(0.0, 0.05, gap);
        solid *= 1.0 - min(holo * EdgeSeeThrough, 0.6);
        color += holoColor * fade * (1.0 - fade) * 4.0 * EdgeCutGlow * min(holo, 1.0);
    }

    float alpha = base.a * fade * solid;
    if (alpha < 0.01) discard;

    finalColor = vec4(color, alpha);
}
