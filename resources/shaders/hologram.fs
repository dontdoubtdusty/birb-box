#version 330

// Used for everything INSIDE the hologram: the dirt floor, food, birbs and the Gilded Granary.
// It adds simple lighting, the hologram tint, rim glow, scan bands, and the edge fog.

in vec3 fragPosition;
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

uniform vec3 holoOrigin;     // middle of this hologram's floor, normally (0,0,0)
uniform float boxYaw;        // how far the hologram is turned (radians)
uniform float holoHalfWidth; // half the width of the hologram cube
uniform float fogStart;      // share of the way to the sides that stays clear before the fog begins
uniform float swallow;       // 0 normally; rises to 1 while switching scenes, rolling the fog in until nothing shows
uniform float reveal;        // 1 normally; while the projector starts it rises from 0 and the picture grows up from the floor
uniform float cubeHeight;
uniform vec2 viewCenter;     // world X,Z at the hologram's center (so the dirt pattern pans)
uniform float viewZoom;      // box units per world unit (so the dirt pattern zooms)
uniform vec3 viewPos;        // camera position, for rim glow
uniform vec3 holoColor;
uniform float time;
uniform int isFloor;
uniform int flatShade;       // 1 while drawing birbs: each triangle gets one flat shade (the faceted look)

out vec4 finalColor;

const float CornerPower = 6.0;    // how round the fog makes the corners; must match ViewWindow.CornerPower

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

vec3 dirtColor(vec2 world)
{
    float n = noise(world * 1.3) * 0.65 + noise(world * 5.0) * 0.35;
    vec3 dirt = mix(vec3(0.27, 0.19, 0.12), vec3(0.42, 0.30, 0.19), n);

    // Scattered seeds: some grid cells get one small light speck
    vec2 cell = floor(world * 3.0);
    vec2 inCell = fract(world * 3.0);
    vec2 seedPos = vec2(hash(cell + 1.3), hash(cell + 7.7)) * 0.6 + 0.2;
    float hasSeed = step(0.8, hash(cell));
    float seed = hasSeed * (1.0 - smoothstep(0.03, 0.06, length(inCell - seedPos)));
    dirt = mix(dirt, vec3(0.86, 0.74, 0.48), seed);

    // Faint world grid, so panning is easy to see
    vec2 g = abs(fract(world * 0.5) - 0.5);
    float line = smoothstep(0.485, 0.5, max(g.x, g.y));
    dirt += holoColor * line * 0.12;

    return dirt;
}

void main()
{
    // fragPosition is in scene space, already turned with the hologram.
    // boxPos moves it back to this hologram's origin and undoes the turn, so the fog, dirt and bands line up with it.
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

    if (isFloor == 1)
    {
        base = vec4(dirtColor(boxPos.xz / viewZoom + viewCenter), 1.0);
        normal = vec3(0.0, 1.0, 0.0);
    }

    // Simple directional light
    vec3 lightDir = normalize(vec3(0.4, 1.0, 0.3));
    float diffuse = max(dot(normal, lightDir), 0.0);
    vec3 color = base.rgb * (0.45 + 0.65 * diffuse);

    // Hologram tint: blend part of the color toward the hologram hue
    float lum = dot(color, vec3(0.299, 0.587, 0.114));
    // (the floor gets less tint so the dirt still reads as brown)
    float tintAmount = (isFloor == 1) ? 0.12 : 0.25;
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

    // Fog: fade out toward the cube's sides and the ceiling.
    // 'edge' is 0 in the middle and 1 at a side; the corners count as further out, which rounds them off
    // (same math as ViewWindow.EdgeAmount). Past 1 fade is 0, so the floor's leftover corners get discarded.
    // Swallow moves both ends of the fade inward, past the middle, so the fog closes in from the sides.
    vec2 q = abs(boxPos.xz) / holoHalfWidth;
    float edge = pow(pow(q.x, CornerPower) + pow(q.y, CornerPower), 1.0 / CornerPower);
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
