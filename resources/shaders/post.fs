#version 330

// Runs once over the whole finished frame: glow, faint scanlines, and a soft vignette.

in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform vec2 resolution;
uniform vec3 backgroundColor;
uniform float time;
uniform float glowStrength;
uniform float scanlineStrength;

out vec4 finalColor;

void main()
{
    vec2 uv = fragTexCoord;
    vec2 pixel = 1.0 / resolution;
    vec3 color = texture(texture0, uv).rgb;

    // Glow: sample a ring of nearby pixels and keep only the bright parts
    vec3 glow = vec3(0.0);
    for (int i = 0; i < 12; i++)
    {
        float angle = 6.28318 * float(i) / 12.0;
        vec2 dir = vec2(cos(angle), sin(angle));
        glow += max(texture(texture0, uv + dir * pixel * 3.0).rgb - 0.35, 0.0);
        glow += max(texture(texture0, uv + dir * pixel * 8.0).rgb - 0.35, 0.0) * 0.6;
    }
    color += glow / 12.0 * glowStrength;

    // Scanlines only where the hologram is (not on the empty background)
    float holoMask = clamp(length(color - backgroundColor) * 4.0, 0.0, 1.0);
    float scan = 0.5 + 0.5 * sin(gl_FragCoord.y * 2.1 - time * 6.0);
    color *= 1.0 - scanlineStrength * holoMask * (1.0 - scan);

    // Soft vignette
    vec2 d = uv - 0.5;
    color *= 1.0 - dot(d, d) * 0.45;

    finalColor = vec4(color, 1.0);
}
