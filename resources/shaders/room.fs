#version 330

// Night-cycle lighting for the room. Nothing here changes over time.
//  - a dim cool light from above, so tops, sides and fronts are different shades
//  - the two warm wall lamps, fading out with distance
//  - everything gets darker the farther it is from the table, so the room fades into the night
//  - polished metal (shine above 0, like the pedestal's brass) also mirrors a soft, made-up room:
//    dark below, brighter overhead, so its curves catch light the way real brass does
//  - the Birb Box's own light while it's on: one light just above the cube's floor (lighting the well,
//    the rim and the table nearby) and one at the projector lens, which only reaches inside the well

in vec3 fragPosition;
in vec3 fragNormal;

uniform vec4 colDiffuse;     // the color passed when drawing
uniform vec3 lampA;
uniform vec3 lampB;
uniform vec3 roomCenter;     // middle of the table
uniform vec3 viewPos;        // camera position, for the metal's reflections
uniform float shine;         // 0 = matte (most of the room); up to 1 = mirror-like

uniform vec3 holoLight;      // just above the hologram's floor
uniform vec3 holoLightColor; // the hologram color times how far the cube is up (black when off)
uniform vec3 lensLight;      // the projector lens inside the pedestal
uniform vec3 lensLightColor; // the hologram color times how lit the lens is
uniform float wellRadius;    // the lens's light stays inside this distance from the pedestal's middle

out vec4 finalColor;

const vec3 Ambient = vec3(0.32, 0.34, 0.42);
const vec3 TopLight = vec3(0.45, 0.48, 0.58);
const vec3 TopLightDir = vec3(-0.24, 0.92, 0.32);    // points toward the light: above, a little left and front
const vec3 LampColor = vec3(1.0, 0.72, 0.42);
const float LampReach = 11.0;
const float HoloReach = 11.0;
const float LensReach = 10.0;

const vec3 ReflectBelow = vec3(0.18, 0.17, 0.18);    // what metal mirrors when it faces down: the dark table
const vec3 ReflectAbove = vec3(1.25, 1.18, 1.05);    // and when it faces up: the lit ceiling

// How much a light at lightPos reaches this spot: more when the surface faces it, less with distance.
float pointLight(vec3 lightPos, vec3 normal, float reach)
{
    vec3 toLight = lightPos - fragPosition;
    float distance = length(toLight);
    float facing = max(dot(normal, toLight / distance), 0.0);
    float falloff = clamp(1.0 - distance / reach, 0.0, 1.0);
    return facing * falloff * falloff;
}

void main()
{
    vec3 normal = normalize(fragNormal);

    vec3 light = Ambient;
    light += TopLight * max(dot(normal, normalize(TopLightDir)), 0.0);
    light += LampColor * (pointLight(lampA, normal, LampReach) + pointLight(lampB, normal, LampReach)) * 1.3;
    vec3 color = colDiffuse.rgb * light;

    if (shine > 0.0)
    {
        vec3 toCamera = normalize(viewPos - fragPosition);
        vec3 mirrored = reflect(-toCamera, normal);
        vec3 seen = mix(ReflectBelow, ReflectAbove, smoothstep(-0.3, 0.7, mirrored.y));
        color = mix(color, colDiffuse.rgb * seen, shine);
    }

    // The Birb Box's light, added after the shine so polished brass still picks it up.
    // A little is added on its own too, so even dark surfaces take on the hologram's color.
    float insideWell = 1.0 - smoothstep(wellRadius - 0.05, wellRadius + 0.05, length(fragPosition.xz));
    vec3 boxLight = holoLightColor * pointLight(holoLight, normal, HoloReach)
                  + lensLightColor * pointLight(lensLight, normal, LensReach) * insideWell;
    color += (colDiffuse.rgb + 0.15) * boxLight * 1.6;

    float distance = length(fragPosition.xz - roomCenter.xz);
    float fade = clamp(1.0 - (distance - 18.0) / 22.0, 0.3, 1.0);

    finalColor = vec4(color * fade, colDiffuse.a);
}
