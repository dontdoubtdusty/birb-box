#version 330

// The starfield in the cabin window, with a few of its bright stars twinkling, very gently.
// The picture is cut into square cells; a small share of cells is picked to twinkle, each on its own
// speed and timing, and only their bright pixels dim a little, so faint stars and the haze stay still.

in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform float time;
uniform vec2 textureSize;    // the picture's size in pixels

out vec4 finalColor;

const float CellPixels = 28.0;       // one cell is about one bright star with its glow
const float TwinkleShare = 0.15;     // share of cells that twinkle
const float TwinkleDepth = 0.15;     // how much a twinkling star dims at its faintest
const float BrightFrom = 0.55;       // pixels dimmer than this never twinkle,
const float BrightTo = 0.85;         // and from here up they twinkle fully

// A steady random number from 0 to 1 for a cell
float hash(vec2 cell)
{
    return fract(sin(dot(cell, vec2(127.1, 311.7))) * 43758.5453);
}

void main()
{
    vec4 sky = texture(texture0, fragTexCoord);

    vec2 cell = floor(fragTexCoord * textureSize / CellPixels);
    if (hash(cell) < TwinkleShare)
    {
        float speed = 0.4 + 0.8 * hash(cell + 17.0);
        float phase = 6.2832 * hash(cell + 41.0);
        float wave = 0.5 + 0.5 * sin(time * speed + phase);
        float bright = smoothstep(BrightFrom, BrightTo, max(sky.r, max(sky.g, sky.b)));
        sky.rgb *= 1.0 - TwinkleDepth * wave * bright;
    }

    finalColor = vec4(sky.rgb, 1.0) * fragColor;
}
