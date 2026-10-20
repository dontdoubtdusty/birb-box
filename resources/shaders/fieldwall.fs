#version 330

// The see-through wall around the hologram: thin horizontal lines drifting slowly upward.
// Drawn with additive blending, so alpha works as brightness.

in vec3 fragPosition;
in vec4 fragColor;

uniform vec3 viewPos;       // camera position
uniform float wallHeight;
uniform float time;

out vec4 finalColor;

const float LineSpacing = 0.14;    // distance between lines, in box units
const float DriftSpeed = 0.25;     // lines per second
const float BaseGlow = 0.12;       // faint fill between the lines so the wall reads as a surface
const float NearSideDim = 0.12;    // how much of the wall is left on the side facing the camera

void main()
{
    // Scanlines: band runs 0 to 1 between two lines; keep only a thin slice near 1.
    float band = fract(fragPosition.y / LineSpacing - time * DriftSpeed);
    float line = smoothstep(0.80, 0.90, band) * (1.0 - smoothstep(0.92, 1.0, band));
    float strength = BaseGlow + line;

    // Fade out toward the top so the field has no hard ceiling
    float up = clamp(fragPosition.y / wallHeight, 0.0, 1.0);
    strength *= (1.0 - up) * (1.0 - up);

    // Dim the near side so the wall never sits between you and the birds.
    // 'facing' is 1 where the wall faces the camera head-on, -1 on the far side.
    vec2 outward = normalize(fragPosition.xz);
    vec2 toCamera = normalize(viewPos.xz - fragPosition.xz);
    float facing = dot(outward, toCamera);
    strength *= mix(1.0, NearSideDim, smoothstep(-0.2, 0.4, facing));

    finalColor = vec4(fragColor.rgb, fragColor.a * strength);
}
