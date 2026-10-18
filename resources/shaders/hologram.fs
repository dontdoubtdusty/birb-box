#version 330

// Used for everything INSIDE the cube: the dirt floor, food and bird models.
// It adds simple lighting, the hologram tint, rim glow, scan bands, and the edge fog.

in vec3 fragPosition;
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

uniform float cubeHalf;      // half width of the cube
uniform float cubeHeight;
uniform vec2 viewCenter;     // world X,Z at the cube's center (so the dirt pattern pans)
uniform vec3 viewPos;        // camera position, for rim glow
uniform vec3 holoColor;
uniform float time;
uniform int isFloor;

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
    vec4 base = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
    vec3 normal = normalize(fragNormal);

    if (isFloor == 1)
    {
        base = vec4(dirtColor(fragPosition.xz + viewCenter), 1.0);
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
    float bands = 0.5 + 0.5 * sin(fragPosition.y * 55.0 - time * 4.0);
    color += holoColor * bands * 0.04;

    // A slow bright sweep that rises through the cube every few seconds
    float sweepY = mod(time * 0.9, cubeHeight + 3.0) - 1.5;
    float sweep = exp(-pow((fragPosition.y - sweepY) * 3.5, 2.0));
    color += holoColor * sweep * 0.2;

    // Fog: fade out toward the cube walls and the ceiling
    float edge = max(abs(fragPosition.x), abs(fragPosition.z)) / cubeHalf;
    float fade = 1.0 - smoothstep(0.55, 1.0, edge);
    fade *= 1.0 - smoothstep(cubeHeight * 0.7, cubeHeight, fragPosition.y);

    // As things fade, they also shift toward a dim hologram-colored haze
    color = mix(color, holoColor * 0.3, (1.0 - fade) * 0.6);

    float alpha = base.a * fade;
    if (alpha < 0.01) discard;

    finalColor = vec4(color, alpha);
}
