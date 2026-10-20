using System.Numerics;
using Raylib_cs;

// The brass pedestal under the hologram, and everything built into it:
//  - a wide plinth, a body ringed with buttons, a bright lip with the bezel ring, and a dark emitter well
//  - the Birb Box logo on a brass plaque at the front
//  - two glowing windows on either side of the logo: Birbs in the cube (left) and Coins (right)
//  - a row of LEDs on the plinth below the logo, one per bird type, lit while that type is in the cube
//  - on the bezel ring, turning with the hologram: notches around its side, and the
//    compass letters and build name printed on its top
// The pedestal itself never turns, so the windows and LEDs always face the player.
// Angles are measured around the pedestal; PI/2 is straight out the front (+Z), where the logo is.
public class Pedestal
{
    // BODY LAYOUT
    private const float BodyHeight = 1.6f;
    private const float PlinthHeight = 0.5f;
    private const int ButtonCount = 18;
    private const float ButtonY = -1.4f;

    // LIP LAYOUT: plain band from LipBottom to LipSplit, bezel ring from LipSplit to LipTop.
    // LipTop is 0 so the bezel is flush with the hologram floor and the field rises straight out of it.
    private const float LipBottom = -0.6f;
    private const float LipSplit = -0.25f;
    private const float LipTop = 0f;

    // LOGO PLAQUE
    private const float PlateWidth = 1.3f, PlateHeight = 1.4f, PlateDepth = 0.08f;
    private const float PlateY = -1.4f;

    // WINDOW LAYOUT
    private const float WindowAngle = 0.32f;      // how far each window sits from the logo, in radians
    private const float WindowY = -1.4f;          // same height as the logo
    private const float WindowWidth = 1.3f;
    private const float WindowHeight = 0.85f;
    private const float FrameBorder = 0.06f;
    private const float FrameDepth = 0.16f;

    // LED LAYOUT
    private const float LedY = -2.45f;            // halfway up the plinth
    private const float LedSpacing = 0.5f;
    private const float LedSize = 0.11f;

    private static readonly Color Glass = new Color(16, 24, 28, 255);
    private static readonly Color Well = new Color(22, 30, 38, 255);
    private static readonly Color Engraving = Palette.BrassDark;

    private Texture2D logoTexture;

    // Each number is drawn into a small texture, which is then laid on its window like a sticker.
    // The textures are only redrawn when the number changes.
    private const int TextureWidth = 256;
    private const int TextureHeight = 160;
    private RenderTexture2D birbTexture;
    private RenderTexture2D coinTexture;
    private int shownBirbs = -1;
    private int shownCoins = -1;

    // Both strips wrap once around the bezel ring: the notches around its side, the letters and name on its top.
    private const int BezelTextureWidth = 2048, BezelTextureHeight = 64;
    private const int TopTextureWidth = 4096, TopTextureHeight = 112;
    private RenderTexture2D bezelTexture;
    private RenderTexture2D topTexture;

    private readonly BirdType[] birdTypes = Birds.All;
    private readonly bool[] typeInCube;

    public Pedestal()
    {
        typeInCube = new bool[birdTypes.Length];
    }

    public void Load()
    {
        logoTexture = Raylib.LoadTexture(Assets.PathTo("birbbox_logo_black.png"));
        Raylib.GenTextureMipmaps(ref logoTexture);
        Raylib.SetTextureFilter(logoTexture, TextureFilter.Trilinear);

        birbTexture = Raylib.LoadRenderTexture(TextureWidth, TextureHeight);
        coinTexture = Raylib.LoadRenderTexture(TextureWidth, TextureHeight);
        Raylib.SetTextureFilter(birbTexture.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(coinTexture.Texture, TextureFilter.Bilinear);

        bezelTexture = Raylib.LoadRenderTexture(BezelTextureWidth, BezelTextureHeight);
        topTexture = Raylib.LoadRenderTexture(TopTextureWidth, TopTextureHeight);
        Raylib.SetTextureFilter(bezelTexture.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(topTexture.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureWrap(bezelTexture.Texture, TextureWrap.Repeat);    // so the strips can scroll around
        Raylib.SetTextureWrap(topTexture.Texture, TextureWrap.Repeat);
        RenderNotches(bezelTexture);
        RenderTop(topTexture, GameEngine.Version.ToUpper());
    }

    public void Unload()
    {
        Raylib.UnloadTexture(logoTexture);
        Raylib.UnloadRenderTexture(birbTexture);
        Raylib.UnloadRenderTexture(coinTexture);
        Raylib.UnloadRenderTexture(bezelTexture);
        Raylib.UnloadRenderTexture(topTexture);
    }

    // Notches every 10 degrees, taller every 30, tallest at the four compass points.
    // They line up with the letters on top, which sit at the same spots on their strip.
    private static void RenderNotches(RenderTexture2D target)
    {
        const int Notches = 36;
        float spacing = BezelTextureWidth / (float)Notches;

        Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Color.Blank);
            for (int i = 0; i < Notches; i++)
            {
                int x = (int)(i * spacing);
                int height = i % 9 == 0 ? 56 : i % 3 == 0 ? 40 : 22;
                // Drawn at both ends too, so a notch on the seam isn't cut in half
                Raylib.DrawRectangle(x - 3, (BezelTextureHeight - height) / 2, 6, height, Engraving);
                Raylib.DrawRectangle(x - 3 + BezelTextureWidth, (BezelTextureHeight - height) / 2, 6, height, Engraving);
            }
        Raylib.EndTextureMode();
    }

    // The top of the ring: the four letters a quarter turn apart, and the build name between N and W.
    // Left to right the letters go N W S E: as the hologram turns right, the letter on its left side
    // comes to the front, so the front letter shows the direction you're looking into the box.
    private static void RenderTop(RenderTexture2D target, string name)
    {
        string[] letters = { "N", "W", "S", "E" };
        float quarter = TopTextureWidth / 4f;
        const int LetterSize = 90;

        Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Color.Blank);
            for (int i = 0; i < 4; i++)
            {
                int half = UiText.Measure(letters[i], LetterSize) / 2;
                int y = (TopTextureHeight - LetterSize) / 2;
                // Drawn at both ends too, so N on the seam isn't cut in half
                UiText.Draw(letters[i], (int)(i * quarter) - half, y, LetterSize, Engraving);
                UiText.Draw(letters[i], (int)(i * quarter) - half + TopTextureWidth, y, LetterSize, Engraving);
            }

            // Shrink the name until it fits in the gap between two letters
            int size = 60;
            while (size > 10 && UiText.Measure(name, size) > quarter - 260) size -= 2;
            int width = UiText.Measure(name, size);
            UiText.Draw(name, (int)(quarter / 2f) - width / 2, (TopTextureHeight - size) / 2, size, Engraving);
        Raylib.EndTextureMode();
    }

    // ---------------- BEFORE THE SCENE PASS ----------------

    // Counts what's in the cube and redraws the number textures if anything changed.
    // Must be called outside any other BeginTextureMode, since Raylib can't nest them.
    public void RefreshReadouts(ViewWindow view, int coins)
    {
        int birbs = 0;
        Array.Clear(typeInCube);
        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (!view.Contains(bird.Position)) continue;
            birbs++;
            typeInCube[Array.IndexOf(birdTypes, bird.Type)] = true;
        }

        if (birbs != shownBirbs)
        {
            RenderReadout(birbTexture, "BIRBS", birbs);
            shownBirbs = birbs;
        }
        if (coins != shownCoins)
        {
            RenderReadout(coinTexture, "COINS", coins);
            shownCoins = coins;
        }
    }

    // White text on a clear background; it gets tinted the hologram color when drawn on the window.
    private static void RenderReadout(RenderTexture2D target, string label, int value)
    {
        Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Color.Blank);
            DrawCenteredText(label, 10, 36);
            DrawCenteredText(value.ToString(), 64, 80);
        Raylib.EndTextureMode();
    }

    private static void DrawCenteredText(string text, int y, int size)
    {
        int width = UiText.Measure(text, size);
        UiText.Draw(text, (TextureWidth - width) / 2, y, size, Color.White);
    }

    // ---------------- DRAW (inside the 3D scene) ----------------

    // ringYaw: how far the bezel ring is turned (the birds' or the store's turn, whichever the box is showing).
    // glowColor: the hologram color, for the windows and the emitter well.
    public void Draw(ViewWindow view, float ringYaw, Color glowColor)
    {
        float r = view.HalfSize;          // pedestal body radius
        float bezelRadius = r * 1.05f;
        float wellRadius = view.Radius;   // the emitter well matches the hologram
        Color brassLight = Palette.BrassLight;

        DrawBody(r, bezelRadius, wellRadius);
        DrawButtons(r);
        DrawLogoPlaque(r, brassLight);
        DrawBezelMarkings(bezelRadius, wellRadius, ringYaw);

        // The body is a slight cone; this is its radius at the window height, same as the logo plaque uses.
        float windowRadius = r * 1.015f + 0.04f;
        DrawWindow(birbTexture, MathF.PI / 2f + WindowAngle, windowRadius, glowColor, brassLight);
        DrawWindow(coinTexture, MathF.PI / 2f - WindowAngle, windowRadius, glowColor, brassLight);

        DrawLeds(r, brassLight);

        Vector3 wellTop = new Vector3(0, LipTop - 0.02f, 0);
        HologramRenderer.BeginGlowDrawing();
            Raylib.DrawCylinderWires(wellTop, wellRadius, wellRadius, 0.01f, 64, new Color(glowColor.R, glowColor.G, glowColor.B, (byte)160));
            Raylib.DrawCylinderWires(wellTop, r * 0.55f, r * 0.55f, 0.01f, 64, new Color(glowColor.R, glowColor.G, glowColor.B, (byte)70));
        HologramRenderer.EndGlowDrawing();
    }

    // Plinth, body, the lip (a plain band with the bezel ring on top), and the dark well just under the hologram floor.
    private static void DrawBody(float r, float bezelRadius, float wellRadius)
    {
        float table = Backdrop.TableY;
        Raylib.DrawCylinder(new Vector3(0, table, 0), r * 1.03f, r * 1.1f, PlinthHeight, 64, Palette.BrassPlinth);
        Raylib.DrawCylinder(new Vector3(0, table + PlinthHeight, 0), r, r * 1.03f, BodyHeight, 64, Palette.Brass);
        Raylib.DrawCylinder(new Vector3(0, LipBottom, 0), r * 1.03f, r * 1.03f, LipSplit - LipBottom, 64, Palette.BrassLight);
        DrawBezelRing(LipSplit, LipTop, wellRadius, bezelRadius, Palette.BrassLight);
        Raylib.DrawCylinderWires(new Vector3(0, LipSplit, 0), bezelRadius, bezelRadius, 0.001f, 64, Palette.BrassPlinth);
        Raylib.DrawCylinder(new Vector3(0, LipTop - 0.04f, 0), wellRadius, wellRadius, 0.02f, 64, Well);
        Raylib.DrawCylinderWires(new Vector3(0, LipTop, 0), bezelRadius, bezelRadius, 0.01f, 64, Palette.BrassPlinth);
    }

    // The bezel ring is drawn as an outer wall and a flat top, with no lid over the middle.
    // A solid lid would sit at the same height as the dirt floor and the two would flicker
    // through each other (z-fighting). Open in the middle, the floor floats over the dark well.
    private static void DrawBezelRing(float bottom, float top, float innerRadius, float outerRadius, Color color)
    {
        const int Segments = 64;
        float step = MathF.PI * 2f / Segments;

        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);
        for (int i = 0; i < Segments; i++)
        {
            float c0 = MathF.Cos(i * step), s0 = MathF.Sin(i * step);
            float c1 = MathF.Cos((i + 1) * step), s1 = MathF.Sin((i + 1) * step);

            // Outer wall, corners counter-clockwise as seen from outside so it faces out
            Rlgl.Vertex3f(c1 * outerRadius, top, s1 * outerRadius);
            Rlgl.Vertex3f(c1 * outerRadius, bottom, s1 * outerRadius);
            Rlgl.Vertex3f(c0 * outerRadius, bottom, s0 * outerRadius);
            Rlgl.Vertex3f(c0 * outerRadius, top, s0 * outerRadius);

            // Flat top from the well's edge to the outer rim, counter-clockwise from above so it faces up
            Rlgl.Vertex3f(c0 * outerRadius, top, s0 * outerRadius);
            Rlgl.Vertex3f(c0 * innerRadius, top, s0 * innerRadius);
            Rlgl.Vertex3f(c1 * innerRadius, top, s1 * innerRadius);
            Rlgl.Vertex3f(c1 * outerRadius, top, s1 * outerRadius);
        }
        Rlgl.End();
    }

    // Round buttons around the body, in the colors of the seed bags, with a gap at the front for the logo.
    private static void DrawButtons(float r)
    {
        Color[] buttonColors = { Palette.BrassLight, new Color(122, 166, 217, 255), new Color(217, 119, 74, 255) };
        for (int i = 0; i < ButtonCount; i++)
        {
            float angle = i * MathF.PI * 2f / ButtonCount;
            if (MathF.Abs(angle - MathF.PI / 2f) < 0.3f) continue;    // leave room for the logo plaque
            Vector3 p = new Vector3(MathF.Cos(angle) * r * 1.01f, ButtonY, MathF.Sin(angle) * r * 1.01f);
            Raylib.DrawSphereEx(p, 0.22f, 8, 8, buttonColors[i % buttonColors.Length]);
        }
    }

    // The Birb Box logo stamped dark into a brass plate on the front (+Z side) of the pedestal.
    private void DrawLogoPlaque(float r, Color plateColor)
    {
        float z = r * 1.015f + PlateDepth / 2f;    // body radius at this height, plus half the plate
        Raylib.DrawCube(new Vector3(0, PlateY, z), PlateWidth, PlateHeight, PlateDepth, plateColor);

        // The logo is a textured square laid on the plate's front face, keeping the image's shape.
        float halfHeight = PlateHeight * 0.4f;
        float halfWidth = halfHeight * logoTexture.Width / logoTexture.Height;
        float front = z + PlateDepth / 2f + 0.005f;

        Rlgl.SetTexture(logoTexture.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(255, 255, 255, 255);
            Rlgl.Normal3f(0, 0, 1);
            Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(-halfWidth, PlateY + halfHeight, front);
            Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(-halfWidth, PlateY - halfHeight, front);
            Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(halfWidth, PlateY - halfHeight, front);
            Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(halfWidth, PlateY + halfHeight, front);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    // A brass frame with dark glass in front, and the glowing number laid on the glass.
    private static void DrawWindow(RenderTexture2D texture, float angle, float radius, Color glow, Color frame)
    {
        Vector3 position = new Vector3(MathF.Cos(angle) * radius, WindowY, MathF.Sin(angle) * radius);
        float turnDegrees = (MathF.PI / 2f - angle) * Raylib.RAD2DEG;    // turns the window to face outward

        // Everything between Push and Pop is in the window's own space:
        // +X is the window's right, +Y is up, +Z points out of the pedestal.
        Rlgl.PushMatrix();
        Rlgl.Translatef(position.X, position.Y, position.Z);
        Rlgl.Rotatef(turnDegrees, 0, 1, 0);

            Raylib.DrawCube(Vector3.Zero, WindowWidth + FrameBorder * 2, WindowHeight + FrameBorder * 2, FrameDepth, frame);
            float glassFront = FrameDepth / 2f + 0.01f;
            Raylib.DrawCube(new Vector3(0, 0, glassFront - 0.01f), WindowWidth, WindowHeight, 0.02f, Glass);

            HologramRenderer.BeginGlowDrawing();
            DrawTextureQuad(texture.Texture, glassFront + 0.005f, glow);
            HologramRenderer.EndGlowDrawing();

        Rlgl.PopMatrix();
    }

    // A flat textured rectangle the size of the window, facing +Z.
    // Render textures are stored upside down, so the top of the quad uses v = 1.
    private static void DrawTextureQuad(Texture2D texture, float z, Color tint)
    {
        float hw = WindowWidth / 2f;
        float hh = WindowHeight / 2f;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(tint.R, tint.G, tint.B, tint.A);
            Rlgl.Normal3f(0, 0, 1);
            Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(-hw, hh, z);
            Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(-hw, -hh, z);
            Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(hw, -hh, z);
            Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(hw, hh, z);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    // Both strips slide around together as the hologram turns.
    private void DrawBezelMarkings(float bezelRadius, float wellRadius, float yaw)
    {
        float front = MathF.PI / 2f;
        float fullTurn = MathF.PI * 2f;

        // u = which part of the strip shows at each angle. The strip is read left to right, and
        // left is the bigger angle, so u runs opposite to the angle. Adding yaw makes it turn with the box.
        Func<float, float> uAt = angle => (front - angle - yaw) / fullTurn;

        DrawBand(bezelTexture.Texture, LipSplit + 0.01f, LipTop - 0.01f, bezelRadius + 0.005f, uAt, Color.White);
        DrawTopRing(topTexture.Texture, LipTop + 0.004f, wellRadius, bezelRadius, uAt, Color.White);
    }

    private const int RingSegments = 128;

    // A strip of texture wrapped once around the pedestal, like a label on a can.
    // uAt tells which part of the texture goes at each angle.
    private static void DrawBand(Texture2D texture, float bottom, float top, float radius,
                                 Func<float, float> uAt, Color tint)
    {
        float step = MathF.PI * 2f / RingSegments;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(tint.R, tint.G, tint.B, tint.A);
        for (int i = 0; i < RingSegments; i++)
        {
            float a0 = i * step;
            float a1 = a0 + step;
            float x0 = MathF.Cos(a0) * radius, z0 = MathF.Sin(a0) * radius;
            float x1 = MathF.Cos(a1) * radius, z1 = MathF.Sin(a1) * radius;

            // Render textures are stored upside down, so the top edge uses v = 1.
            // Corners go counter-clockwise as seen from outside, so the band faces out.
            Rlgl.TexCoord2f(uAt(a1), 1); Rlgl.Vertex3f(x1, top, z1);
            Rlgl.TexCoord2f(uAt(a1), 0); Rlgl.Vertex3f(x1, bottom, z1);
            Rlgl.TexCoord2f(uAt(a0), 0); Rlgl.Vertex3f(x0, bottom, z0);
            Rlgl.TexCoord2f(uAt(a0), 1); Rlgl.Vertex3f(x0, top, z0);
        }
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    // A flat ring of texture lying face-up, from innerRadius to outerRadius, like print on a record.
    // The top of the text points inward, so it reads upright from outside the pedestal.
    private static void DrawTopRing(Texture2D texture, float y, float innerRadius, float outerRadius,
                                    Func<float, float> uAt, Color tint)
    {
        float step = MathF.PI * 2f / RingSegments;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(tint.R, tint.G, tint.B, tint.A);
        Rlgl.Normal3f(0, 1, 0);
        for (int i = 0; i < RingSegments; i++)
        {
            float a0 = i * step;
            float a1 = a0 + step;
            float c0 = MathF.Cos(a0), s0 = MathF.Sin(a0);
            float c1 = MathF.Cos(a1), s1 = MathF.Sin(a1);

            // Render textures are stored upside down, so the inner edge (top of the text) uses v = 1.
            // Corners go counter-clockwise as seen from above, so the ring faces up.
            Rlgl.TexCoord2f(uAt(a0), 0); Rlgl.Vertex3f(c0 * outerRadius, y, s0 * outerRadius);
            Rlgl.TexCoord2f(uAt(a0), 1); Rlgl.Vertex3f(c0 * innerRadius, y, s0 * innerRadius);
            Rlgl.TexCoord2f(uAt(a1), 1); Rlgl.Vertex3f(c1 * innerRadius, y, s1 * innerRadius);
            Rlgl.TexCoord2f(uAt(a1), 0); Rlgl.Vertex3f(c1 * outerRadius, y, s1 * outerRadius);
        }
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    // One LED per bird type, left to right in enum order. Lit LEDs shine in the bird's color with a
    // soft halo; unlit ones are dark glass with just a hint of their color.
    private void DrawLeds(float r, Color bezelColor)
    {
        float radius = r * 1.065f + 0.04f;     // plinth surface at LedY, plus a little
        float step = LedSpacing / radius;      // spacing as an angle around the pedestal
        Vector3[] positions = new Vector3[birdTypes.Length];

        for (int i = 0; i < birdTypes.Length; i++)
        {
            // Bigger angles are further left when facing the front, so count down from the left end.
            float angle = MathF.PI / 2f + step * ((birdTypes.Length - 1) / 2f - i);
            positions[i] = new Vector3(MathF.Cos(angle) * radius, LedY, MathF.Sin(angle) * radius);

            Color c = Birds.Get(birdTypes[i]).Color;
            Color bulb = typeInCube[i] ? c : new Color(c.R / 3, c.G / 3, c.B / 3, 255);
            // The bezel sits further in than the bulb, so only a brass ring shows around it.
            Vector3 inward = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * -0.08f;

            Raylib.DrawSphereEx(positions[i] + inward, LedSize * 1.4f, 10, 10, bezelColor);
            Raylib.DrawSphereEx(positions[i], LedSize, 10, 10, bulb);
        }

        HologramRenderer.BeginGlowDrawing();
        for (int i = 0; i < birdTypes.Length; i++)
        {
            if (!typeInCube[i]) continue;
            Raylib.DrawSphereEx(positions[i], LedSize * 2f, 10, 10, Raylib.Fade(Birds.Get(birdTypes[i]).Color, 0.45f));
        }
        HologramRenderer.EndGlowDrawing();
    }
}
