#version 330

// Night-cycle lighting for the room. Nothing here changes over time.
//  - a dim cool light from above, so tops, sides and fronts are different shades
//  - the two warm wall lamps, fading out with distance
//  - everything gets darker the farther it is from the table, so the room fades into the night

in vec3 fragPosition;
in vec3 fragNormal;

uniform vec4 colDiffuse;     // the color passed when drawing
uniform vec3 lampA;
uniform vec3 lampB;
uniform vec3 roomCenter;     // middle of the table

out vec4 finalColor;

const vec3 Ambient = vec3(0.32, 0.34, 0.42);
const vec3 TopLight = vec3(0.45, 0.48, 0.58);
const vec3 TopLightDir = vec3(-0.24, 0.92, 0.32);    // points toward the light: above, a little left and front
const vec3 LampColor = vec3(1.0, 0.72, 0.42);
const float LampReach = 11.0;

float lamp(vec3 lampPos, vec3 normal)
{
    vec3 toLamp = lampPos - fragPosition;
    float distance = length(toLamp);
    float facing = max(dot(normal, toLamp / distance), 0.0);
    float falloff = clamp(1.0 - distance / LampReach, 0.0, 1.0);
    return facing * falloff * falloff;
}

void main()
{
    vec3 normal = normalize(fragNormal);

    vec3 light = Ambient;
    light += TopLight * max(dot(normal, normalize(TopLightDir)), 0.0);
    light += LampColor * (lamp(lampA, normal) + lamp(lampB, normal)) * 1.3;

    float distance = length(fragPosition.xz - roomCenter.xz);
    float fade = clamp(1.0 - (distance - 18.0) / 22.0, 0.3, 1.0);

    finalColor = vec4(colDiffuse.rgb * light * fade, colDiffuse.a);
}
