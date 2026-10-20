#version 330

// Runs once over the finished hologram scene: glow and faint scanlines.
// The scene was drawn onto a clear background, so its alpha says where something was drawn.
// The result is laid over the backdrop with premultiplied blending (color + backdrop * (1 - alpha)).

in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform vec2 resolution;
uniform float time;
uniform float glowStrength;
uniform float scanlineStrength;

out vec4 finalColor;

void main()
{
    vec2 uv = fragTexCoord;
    vec2 pixel = 1.0 / resolution;
    vec4 scene = texture(texture0, uv);
    vec3 color = scene.rgb;

    // Glow: sample a ring of nearby pixels and keep only the bright parts.
    // It spreads past the edges of things, so it also brightens the backdrop around them.
    vec3 glow = vec3(0.0);
    for (int i = 0; i < 12; i++)
    {
        float angle = 6.28318 * float(i) / 12.0;
        vec2 dir = vec2(cos(angle), sin(angle));
        glow += max(texture(texture0, uv + dir * pixel * 3.0).rgb - 0.35, 0.0);
        glow += max(texture(texture0, uv + dir * pixel * 8.0).rgb - 0.35, 0.0) * 0.6;
    }
    color += glow / 12.0 * glowStrength;

    // Scanlines only where something was drawn (not on the backdrop)
    float scan = 0.5 + 0.5 * sin(gl_FragCoord.y * 2.1 - time * 6.0);
    color *= 1.0 - scanlineStrength * scene.a * (1.0 - scan);

    finalColor = vec4(color, scene.a);
}
