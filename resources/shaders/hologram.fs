#version 330

// Used for everything INSIDE the hologram: the grass-and-dirt floor, food, birbs and the Gilded Granary.
// It adds simple lighting, the hologram tint, rim glow, scan bands, and the edge fog.

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

out vec4 finalColor;

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
    vec4 base = texture(texture0, fragTexCoord) * colDiffuse;

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

    if (isFloor == 1)
    {
        base = vec4(groundColor(boxPos.xz / viewZoom + viewCenter), 1.0);
        normal = vec3(0.0, 1.0, 0.0);
    }

    // Simple directional light
    vec3 lightDir = normalize(vec3(0.4, 1.0, 0.3));
    float diffuse = max(dot(normal, lightDir), 0.0);
    vec3 color = base.rgb * (0.45 + 0.65 * diffuse);

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
    vec2 q = abs(boxPos.xz) / holoHalfWidth;
    float edge = max(q.x, q.y);
    float clearUntil = mix(fogStart, -0.4, swallow);
    float goneBy = mix(1.0, 0.0, swallow);
    float fade = 1.0 - smoothstep(clearUntil, goneBy, edge);
    fade *= 1.0 - smoothstep(cubeHeight * 0.7, cubeHeight, boxPos.y);

    // Starting up: only what's below the rising edge shows, softly, and the whole picture brightens as it rises
    float revealY = reveal * (cubeHeight + 1.0) - 0.5;
    fade *= 1.0 - smoothstep(revealY - 0.6, revealY, boxPos.y);
    fade *= smoothstep(0.0, 0.4, reveal);

    // As things fade, they also shift toward a dim hologram-colored haze
    color = mix(color, holoColor * 0.3, (1.0 - fade) * 0.6);

    float alpha = base.a * fade;
    if (alpha < 0.01) discard;

    finalColor = vec4(color, alpha);
}
