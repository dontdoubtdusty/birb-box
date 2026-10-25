using System.Numerics;
using Raylib_cs;

// The pedestal under the hologram: a dark wood cabinet with brass fittings, and everything built into it:
//  - a wide plinth, a body ringed with buttons, a plain band, and the polished brass bezel ring on top
//  - inside the bezel it's hollow: a deep well, lit by the hologram floating above it
//  - the projector lens inside the well, right behind the logo, throwing the beams up to the cube (see FieldEdges)
//  - the Birb Box logo on a brass plaque at the front. It's the power button, and it glows with the lens.
//  - two glowing windows on either side of the logo: Birbs in the cube (left) and Coins (right)
//  - a row of LEDs on the plinth below the logo, one per bird type, lit while that type is in the cube
//  - in the band under the ring, above the logo, the BIRB / SHOP rocker switch (its own file, Rocker)
//  - on the bezel ring, turning with the hologram: notches around its side, the compass letters and
//    build name printed on its top
// The pedestal itself never turns, so the windows and LEDs always face the player.
// Angles are measured around the pedestal; PI/2 is straight out the front (+Z), where the logo is.
//
// It's a real object in the room: drawn right after the Backdrop, lit by room.fs (a little varnish sheen on the wood,
// metal shine on the brass),
// and the hologram is laid over it afterward. The lights built into it (windows, LEDs, lens, logo) follow the power.
public class Pedestal
{
    // BODY LAYOUT
    private const float BodyHeight = 1.6f;
    private const float PlinthHeight = 0.5f;
    private const int ButtonCount = 18;
    private const float ButtonY = -1.5f;

    // LIP LAYOUT: plain band from LipBottom to LipSplit, bezel ring from LipSplit to LipTop (the rim).
    // The band is tall enough to give the rocker room above and below (see Rocker.CenterY).
    // The hologram floats HoloCamera.HoloLift above the rim.
    private const float LipBottom = -0.8f;
    private const float LipSplit = -0.25f;
    private const float LipTop = 0f;
    private const float WellShare = 0.95f;       // the well's radius as a share of the body's; the bezel's top runs from here out
    private const float WellFloorY = -2.0f;      // the well goes almost down to the plinth

    // LOGO PLAQUE
    private const float PlateWidth = 1.3f, PlateHeight = 1.3f, PlateDepth = 0.08f;
    private const float PlateY = -1.5f;
    private const float PushDepth = 0.05f;        // how far the plate sinks into the wood when pressed (see Projector.ButtonPush)

    // WINDOW LAYOUT
    private const float WindowAngle = 0.27f;      // how far each window sits from the logo, in radians
    private const float WindowY = -1.5f;          // same height as the logo
    private const float WindowWidth = 1.3f;
    private const float WindowHeight = 0.85f;
    private const float FrameBorder = 0.06f;
    private const float FrameDepth = 0.16f;

    // LED LAYOUT
    private const float LedY = -2.45f;            // halfway up the plinth
    private const float LedSpacing = 0.5f;
    private const float LedSize = 0.11f;

    // PROJECTOR LENS: in a brass housing against the well's front wall, at the logo's height
    private const float LensRadius = 0.26f;
    private const float LensInset = 0.45f;       // how far in from the well's wall the lens sits

    // WOOD: the cabinet (plinth, body, plain band). A low shine reads as varnish rather than metal.
    private static readonly Color PlinthWood = new Color(50, 31, 20, 255);
    private static readonly Color BodyWood = new Color(82, 51, 31, 255);
    private const float WoodShine = 0.12f;
    // BRASS: brighter than the flat Palette colors, since the room light takes some away. Shine is how polished it looks.
    // The compass ring is the showpiece, the most polished part; the small fittings (buttons, plaque, frames) a bit less.
    private static readonly Color RingBrass = new Color(252, 214, 134, 255);
    private const float RingShine = 0.85f;
    private static readonly Color LipBrass = new Color(240, 200, 124, 255);
    private const float BrassShine = 0.6f;
    private static readonly Color Seam = Palette.BrassDark;
    // Inside the well: darker, duller brass, so the hologram's light on it stands out
    private static readonly Color InnerBrass = new Color(104, 80, 54, 255);
    private const float InnerShine = 0.25f;

    private static readonly Color Glass = new Color(16, 24, 28, 255);
    private static readonly Color Engraving = Palette.BrassDark;

    private Texture2D logoTexture;       // black, stamped into the plate
    private Texture2D logoGlowTexture;   // white, tinted the hologram color and added on top while the lens is lit

    // The plinth, body, band, bezel ring and well are each one mesh, built once at load (see LatheMesh).
    private Model plinthModel, bodyModel, bandModel, bezelModel, wellModel;

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

    public void Load(Shader roomShader, ViewWindow view)
    {
        logoTexture = Raylib.LoadTexture(Assets.PathTo("birbbox_logo_black.png"));
        Raylib.GenTextureMipmaps(ref logoTexture);
        Raylib.SetTextureFilter(logoTexture, TextureFilter.Trilinear);
        logoGlowTexture = Raylib.LoadTexture(Assets.PathTo("birbbox_logo_white.png"));
        Raylib.GenTextureMipmaps(ref logoGlowTexture);
        Raylib.SetTextureFilter(logoGlowTexture, TextureFilter.Trilinear);

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

        BuildBody(roomShader, view.HalfSize);
    }

    public void Unload()
    {
        Raylib.UnloadTexture(logoTexture);
        Raylib.UnloadTexture(logoGlowTexture);
        Raylib.UnloadRenderTexture(birbTexture);
        Raylib.UnloadRenderTexture(coinTexture);
        Raylib.UnloadRenderTexture(bezelTexture);
        Raylib.UnloadRenderTexture(topTexture);
        Raylib.UnloadModel(plinthModel);
        Raylib.UnloadModel(bodyModel);
        Raylib.UnloadModel(bandModel);
        Raylib.UnloadModel(bezelModel);
        Raylib.UnloadModel(wellModel);
    }

    // The five pieces as side profiles (radius, height), spun into meshes.
    // r is the body's radius at the top; the body and plinth flare out slightly toward the table.
    private void BuildBody(Shader roomShader, float r)
    {
        const int Segments = 128;
        float table = Backdrop.TableY;
        float bodyBottom = table + PlinthHeight;
        float bezelRadius = r * 1.05f;
        float wellRadius = r * WellShare;

        plinthModel = LatheMesh.Build(new[]
        {
            new Vector2(r * 1.1f, table),
            new Vector2(r * 1.03f, bodyBottom),
        }, Segments, roomShader);

        bodyModel = LatheMesh.Build(new[]
        {
            new Vector2(r * 1.03f, bodyBottom),
            new Vector2(r, LipBottom),
        }, Segments, roomShader);

        // The plain wood band just under the ring
        bandModel = LatheMesh.Build(new[]
        {
            new Vector2(r * 1.03f, LipBottom),
            new Vector2(r * 1.03f, LipSplit),
        }, Segments, roomShader);

        // The brass ring: out under the bezel, up its side, and in across its top to the well's edge
        bezelModel = LatheMesh.Build(new[]
        {
            new Vector2(r * 1.03f, LipSplit),
            new Vector2(bezelRadius, LipSplit),
            new Vector2(bezelRadius, LipTop),
            new Vector2(wellRadius, LipTop),
        }, Segments, roomShader);

        // Down the well's wall and in across its floor, facing into the well
        wellModel = LatheMesh.Build(new[]
        {
            new Vector2(wellRadius, LipTop),
            new Vector2(wellRadius, WellFloorY),
            new Vector2(0f, WellFloorY),
        }, Segments, roomShader);
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

    // Where the projector lens sits, in room space: inside the well, right behind the logo.
    public static Vector3 LensSpot(ViewWindow view) => new Vector3(0f, PlateY, view.HalfSize * WellShare - LensInset);

    public static float WellRadius(ViewWindow view) => view.HalfSize * WellShare;

    // The middle of the logo plate's front face, where a finger presses the power button.
    public static Vector3 LogoFace(ViewWindow view) => new Vector3(0f, PlateY, view.HalfSize * 1.015f + PlateDepth);

    // A spot on top of the bezel ring, just in from its outer edge, at this angle (PI/2 is the front).
    // outward: straight out from the middle of the pedestal at that spot.
    public static Vector3 RingTop(ViewWindow view, float angle, out Vector3 outward)
    {
        outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
        float radius = view.HalfSize * 1.05f - 0.2f;
        return outward * radius + new Vector3(0f, LipTop, 0f);
    }

    // Is this room-space ray pointing at the logo plate (the power button)?
    public static bool RayHitsLogo(Ray roomRay, ViewWindow view)
    {
        Vector3 center = new Vector3(0f, PlateY, view.HalfSize * 1.015f + PlateDepth / 2f);
        Vector3 half = new Vector3(PlateWidth, PlateHeight, PlateDepth) / 2f;
        return Raylib.GetRayCollisionBox(roomRay, new BoundingBox(center - half, center + half)).Hit;
    }

    // ---------------- DRAW (in the room pass, inside BeginMode3D) ----------------

    // shapes: the Backdrop's room-lit shapes. ringYaw: how far the bezel ring is turned (the birds' or the
    // store's turn, whichever the box is showing). glowColor: the hologram color, for everything that lights up.
    // projector: how far the box is switched on; the built-in lights follow its Lens level.
    public void Draw(Backdrop backdrop, ViewWindow view, float ringYaw, Color glowColor, Projector projector)
    {
        UnitShapes shapes = backdrop.Shapes;
        float r = view.HalfSize;          // pedestal body radius
        float bezelRadius = r * 1.05f;
        float wellRadius = r * WellShare;
        // The body is a slight cone; this is its radius at the window height, same as the logo plaque uses.
        float windowRadius = r * 1.015f + 0.04f;
        Vector3 lens = LensSpot(view);
        float power = projector.Lens;
        float push = projector.ButtonPush * PushDepth;    // the power button sinking in after a press

        // The varnished wood cabinet
        backdrop.SetShine(WoodShine);
            Raylib.DrawModel(plinthModel, Vector3.Zero, 1f, PlinthWood);
            Raylib.DrawModel(bodyModel, Vector3.Zero, 1f, BodyWood);
            Raylib.DrawModel(bandModel, Vector3.Zero, 1f, BodyWood);
            DrawStats.CountModel();
            DrawStats.CountModel();
            DrawStats.CountModel();
        // The compass ring, the most polished brass
        backdrop.SetShine(RingShine);
            Raylib.DrawModel(bezelModel, Vector3.Zero, 1f, RingBrass);
            DrawStats.CountModel();
        // Brass fittings set into the wood
        backdrop.SetShine(BrassShine);
            DrawButtons(shapes, r);
            DrawLogoPlate(shapes, r, push);
            DrawWindowFrame(shapes, MathF.PI / 2f + WindowAngle, windowRadius);
            DrawWindowFrame(shapes, MathF.PI / 2f - WindowAngle, windowRadius);
            DrawLedRims(shapes, r);
        // The inside of the well, lit mostly by the hologram (see room.fs)
        backdrop.SetShine(InnerShine);
            Raylib.DrawModel(wellModel, Vector3.Zero, 1f, InnerBrass);
            DrawStats.CountModel();
            DrawLensHousing(shapes, lens);
        backdrop.SetShine(0f);

        // Unlit details: seams, printed markings, glass, bulbs, the logo and the lens
        Raylib.DrawCylinderWires(new Vector3(0, LipSplit, 0), bezelRadius, bezelRadius, 0.001f, 64, Seam);
        Raylib.DrawCylinderWires(new Vector3(0, LipTop, 0), bezelRadius, bezelRadius, 0.01f, 64, Seam);
        DrawLogo(logoTexture, r, 1f, 0.005f - push, Color.White);
        DrawBezelMarkings(bezelRadius, wellRadius, ringYaw);
        DrawWindowGlass(MathF.PI / 2f + WindowAngle, windowRadius);
        DrawWindowGlass(MathF.PI / 2f - WindowAngle, windowRadius);
        DrawLedBulbs(r, power);
        Raylib.DrawSphereEx(lens, LensRadius, 14, 14, Palette.Mix(Glass, glowColor, power));

        // Glow: the numbers on the windows, the LEDs' halos, the logo, the lens's halo and the rings on the well floor.
        // Hovering the logo lights it a little even while off, so it reads as a button.
        float logoGlow = MathF.Min(1f, power + (projector.LogoHovered ? 0.35f : 0f));
        HologramRenderer.BeginGlowDrawing();
            if (power > 0f)
            {
                DrawReadout(birbTexture, MathF.PI / 2f + WindowAngle, windowRadius, Raylib.Fade(glowColor, power));
                DrawReadout(coinTexture, MathF.PI / 2f - WindowAngle, windowRadius, Raylib.Fade(glowColor, power));
                DrawLedHalos(r, power);
                Raylib.DrawSphereEx(lens, LensRadius * 2.4f, 12, 12, Raylib.Fade(glowColor, 0.4f * power));
            }
            if (logoGlow > 0f)
            {
                DrawLogo(logoGlowTexture, r, 1f, 0.009f - push, Raylib.Fade(glowColor, logoGlow));
                DrawLogo(logoGlowTexture, r, 1.2f, 0.012f - push, Raylib.Fade(glowColor, logoGlow * 0.25f));
            }
            if (projector.Beams > 0f)
            {
                Vector3 floor = new Vector3(0, WellFloorY + 0.02f, 0);
                Raylib.DrawCylinderWires(floor, wellRadius * 0.8f, wellRadius * 0.8f, 0.01f, 64, Raylib.Fade(glowColor, 0.35f * projector.Beams));
                Raylib.DrawCylinderWires(floor, wellRadius * 0.45f, wellRadius * 0.45f, 0.01f, 64, Raylib.Fade(glowColor, 0.2f * projector.Beams));
            }
        HologramRenderer.EndGlowDrawing();
    }

    // A brass block against the well's front wall holding the lens, with a collar around the lens aimed at the cube.
    private static void DrawLensHousing(UnitShapes shapes, Vector3 lens)
    {
        shapes.Box(lens + new Vector3(0f, 0f, 0.3f), new Vector3(1.1f, 1.0f, 0.5f), InnerBrass);
        Vector3 aim = Vector3.Normalize(new Vector3(0f, HoloCamera.HoloLift, 0f) - lens);
        shapes.CylinderAlong(lens - aim * 0.12f, aim, LensRadius * 1.3f, 0.16f, LipBrass);
    }

    // Round buttons around the body, in the colors of the seed bags, with a gap at the front for the logo and windows.
    private static void DrawButtons(UnitShapes shapes, float r)
    {
        Color[] buttonColors = { Palette.BrassLight, new Color(122, 166, 217, 255), new Color(217, 119, 74, 255) };
        for (int i = 0; i < ButtonCount; i++)
        {
            float angle = i * MathF.PI * 2f / ButtonCount;
            if (MathF.Abs(angle - MathF.PI / 2f) < 0.45f) continue;    // leave room for the logo plaque and windows
            Vector3 p = new Vector3(MathF.Cos(angle) * r * 1.01f, ButtonY, MathF.Sin(angle) * r * 1.01f);
            shapes.Ball(p, new Vector3(0.22f, 0.22f, 0.22f), buttonColors[i % buttonColors.Length]);
        }
    }

    // The brass plate on the front (+Z side) of the pedestal that the logo is stamped into.
    // push: how far it's sunk into the wood right now.
    private static void DrawLogoPlate(UnitShapes shapes, float r, float push)
    {
        float z = r * 1.015f + PlateDepth / 2f - push;    // body radius at this height, plus half the plate
        shapes.Box(new Vector3(0, PlateY, z), new Vector3(PlateWidth, PlateHeight, PlateDepth), LipBrass);
    }

    // The Birb Box logo, a textured square laid on the plate's front face, keeping the image's shape.
    // grow: 1 is its normal size. lift: how far in front of the plate's resting face it floats, so layers don't
    // flicker (less while the plate is pushed in).
    private static void DrawLogo(Texture2D texture, float r, float grow, float lift, Color tint)
    {
        float z = r * 1.015f + PlateDepth / 2f;
        float halfHeight = PlateHeight * 0.4f * grow;
        float halfWidth = halfHeight * texture.Width / texture.Height;
        float front = z + PlateDepth / 2f + lift;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(tint.R, tint.G, tint.B, tint.A);
            Rlgl.Normal3f(0, 0, 1);
            Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(-halfWidth, PlateY + halfHeight, front);
            Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(-halfWidth, PlateY - halfHeight, front);
            Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(halfWidth, PlateY - halfHeight, front);
            Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(halfWidth, PlateY + halfHeight, front);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    // ---------------- WINDOWS ----------------
    // Each window is a brass frame with dark glass in front and the glowing number laid on the glass.
    // 'angle' is where it sits around the pedestal; it's turned to face straight out from there.

    private static Vector3 WindowCenter(float angle, float radius) =>
        new Vector3(MathF.Cos(angle) * radius, WindowY, MathF.Sin(angle) * radius);

    private static float WindowTurnDegrees(float angle) => (MathF.PI / 2f - angle) * Raylib.RAD2DEG;

    private static void DrawWindowFrame(UnitShapes shapes, float angle, float radius)
    {
        Vector3 size = new Vector3(WindowWidth + FrameBorder * 2, WindowHeight + FrameBorder * 2, FrameDepth);
        shapes.Box(WindowCenter(angle, radius), size, LipBrass, Vector3.UnitY, WindowTurnDegrees(angle));
    }

    private static void DrawWindowGlass(float angle, float radius)
    {
        Vector3 c = WindowCenter(angle, radius);
        Rlgl.PushMatrix();
        Rlgl.Translatef(c.X, c.Y, c.Z);
        Rlgl.Rotatef(WindowTurnDegrees(angle), 0, 1, 0);
            Raylib.DrawCube(new Vector3(0, 0, FrameDepth / 2f), WindowWidth, WindowHeight, 0.02f, Glass);
        Rlgl.PopMatrix();
    }

    // The number texture on the glass. Between Push and Pop, +X is the window's right, +Y up, +Z out of the pedestal.
    private static void DrawReadout(RenderTexture2D texture, float angle, float radius, Color glow)
    {
        Vector3 c = WindowCenter(angle, radius);
        Rlgl.PushMatrix();
        Rlgl.Translatef(c.X, c.Y, c.Z);
        Rlgl.Rotatef(WindowTurnDegrees(angle), 0, 1, 0);
            DrawTextureQuad(texture.Texture, FrameDepth / 2f + 0.015f, glow);
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

    // ---------------- BEZEL MARKINGS ----------------

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

    // ---------------- LEDS ----------------
    // One LED per bird type, left to right in enum order. Lit LEDs shine in the bird's color with a
    // soft halo; unlit ones are dark glass with just a hint of their color. All go dark with the power.

    // Where LED i sits, and which way is straight out from the pedestal there.
    private Vector3 LedSpot(float r, int i, out Vector3 outward)
    {
        float radius = r * 1.065f + 0.04f;     // plinth surface at LedY, plus a little
        float step = LedSpacing / radius;      // spacing as an angle around the pedestal
        // Bigger angles are further left when facing the front, so count down from the left end.
        float angle = MathF.PI / 2f + step * ((birdTypes.Length - 1) / 2f - i);
        outward = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
        return new Vector3(outward.X * radius, LedY, outward.Z * radius);
    }

    // The brass ring around each bulb. It sits further in than the bulb, so only a rim shows.
    private void DrawLedRims(UnitShapes shapes, float r)
    {
        float size = LedSize * 1.4f;
        for (int i = 0; i < birdTypes.Length; i++)
        {
            Vector3 spot = LedSpot(r, i, out Vector3 outward);
            shapes.Ball(spot - outward * 0.08f, new Vector3(size, size, size), LipBrass);
        }
    }

    private void DrawLedBulbs(float r, float power)
    {
        for (int i = 0; i < birdTypes.Length; i++)
        {
            Color c = Birds.Get(birdTypes[i]).Color;
            Color dark = new Color(c.R / 3, c.G / 3, c.B / 3, 255);
            Color bulb = typeInCube[i] ? Palette.Mix(dark, c, power) : dark;
            Raylib.DrawSphereEx(LedSpot(r, i, out _), LedSize, 10, 10, bulb);
        }
    }

    private void DrawLedHalos(float r, float power)
    {
        for (int i = 0; i < birdTypes.Length; i++)
        {
            if (!typeInCube[i]) continue;
            Raylib.DrawSphereEx(LedSpot(r, i, out _), LedSize * 2f, 10, 10, Raylib.Fade(Birds.Get(birdTypes[i]).Color, 0.45f * power));
        }
    }
}
