#version 330

// The sun or the moon under the cube's ceiling in the room view (SkyOrbs.cs): one camera-facing square, drawn
// as a soft disc with a halo. The moon is lit on one side so its phase shows, the same way as in horizon.fs.
// Blended by adding (SkyOrbs sets the blend): the color is added to the hologram, and alpha is how much it
// hides the room behind it.

in vec2 fragTexCoord;
in vec4 fragColor;

uniform int isMoon;           // 0 the sun, 1 the moon
uniform float discShare;      // the disc's radius as a share of the square's half-width
uniform vec3 discColor;       // the disc itself
uniform vec3 haloColor;       // the glow around it
uniform float haloReach;      // how far the halo spreads, as a share of the square (1 reaches its edge)
uniform float strength;       // 0 hidden, 1 fully shown
uniform vec3 moonLight;       // toward the sun, seen from the moon: x right, y up, z toward the camera
uniform float earthshine;     // the moon's dark side, faintly there at night

out vec4 finalColor;

// TUNING
const float DiscCover = 0.75;     // how much the disc hides the room behind it
const float HaloCover = 0.15;     // ...and the halo's brightest part

void main()
{
    vec2 uv = fragTexCoord * 2.0 - 1.0;    // -1..1 across the square
    float r = length(uv);

    // The disc, with a soft rim
    float disc = 1.0 - smoothstep(discShare * 0.8, discShare, r);

    // The moon's phase: this spot on the disc as a point on a ball facing the camera, then whether the sun reaches it
    float lit = 1.0;
    if (isMoon == 1 && disc > 0.0)
    {
        vec2 across = uv / discShare;
        float r2 = min(dot(across, across), 1.0);
        vec3 surface = vec3(across, sqrt(1.0 - r2));
        lit = smoothstep(-0.06, 0.06, dot(surface, moonLight));
        lit = lit + earthshine * (1.0 - lit);
    }

    // The halo: brightest at the disc's rim, fading to nothing at the square's edge
    float h = max(1.0 - r / haloReach, 0.0);
    float halo = h * h * (1.0 - disc);

    vec3 color = discColor * disc * lit + haloColor * halo;
    float cover = DiscCover * disc * lit + HaloCover * halo;
    finalColor = vec4(color, cover) * strength;
}
