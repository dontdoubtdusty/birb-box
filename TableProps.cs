using System.Numerics;
using Raylib_cs;

// The things sitting on the dining table around the Birb Box and the Gilded Granary.
// They're there to show how big the toys are, so they're kept to real sizes (1 unit is about 7.5 cm).
//
// Nothing can sit in front of the pedestals (the hologram scene is drawn over the room),
// so the mug takes the front-left corner and everything else sits along the sides and back.
// Only the steam and the candle's LED flame move; the room's light never changes.
public class TableProps
{
    private const float T = Backdrop.TableY;

    // WHERE THINGS SIT (on the tabletop)
    private static readonly Vector3 MugSpot = new Vector3(-9.3f, T, 6.6f);
    private static readonly Vector3 DrawingSpot = new Vector3(-10.9f, T, 2.6f);
    private static readonly Vector3 TabletSpot = new Vector3(-11.3f, T, -1.2f);
    private static readonly Vector3 BooksSpot = new Vector3(-12f, T, -5f);
    private static readonly Vector3 PlantSpot = new Vector3(-8.4f, T, -5.6f);
    private static readonly Vector3 GobletSpot = new Vector3(14f, T, 1.5f);
    private static readonly Vector3 CandleSpot = new Vector3(15f, T, -4f);

    // COLORS
    private static readonly Color MugRed = new Color(123, 23, 27, 255);    // #7B171B
    private static readonly Color Coffee = new Color(58, 34, 22, 255);
    private static readonly Color Steam = new Color(210, 215, 225, 255);
    private static readonly Color TabletBody = new Color(28, 30, 36, 255);
    private static readonly Color TabletScreen = new Color(28, 80, 96, 255);
    private static readonly Color TabletText = new Color(110, 205, 215, 255);
    private static readonly Color Pages = new Color(214, 204, 180, 255);
    private static readonly Color Paper = new Color(168, 163, 152, 255);    // unlit, so already dimmed for night
    private static readonly Color Pot = new Color(150, 90, 62, 255);
    private static readonly Color Soil = new Color(50, 35, 25, 255);
    private static readonly Color Leaf = new Color(80, 140, 80, 255);
    private static readonly Color LeafLight = new Color(104, 164, 92, 255);
    private static readonly Color Brass = new Color(190, 150, 80, 255);
    private static readonly Color Wax = new Color(225, 215, 190, 255);
    private static readonly Color Flame = new Color(255, 210, 120, 255);
    private static readonly Color Pewter = new Color(140, 144, 150, 255);
    private static readonly Color Wine = new Color(70, 15, 25, 255);

    private Texture2D logo;

    public void Load()
    {
        logo = Raylib.LoadTexture(Assets.PathTo("birbbox_logo_white.png"));
        Raylib.GenTextureMipmaps(ref logo);
        Raylib.SetTextureFilter(logo, TextureFilter.Trilinear);
    }

    public void Unload()
    {
        Raylib.UnloadTexture(logo);
    }

    public void Draw(UnitShapes shapes)
    {
        float time = (float)Raylib.GetTime();

        DrawMug(shapes);
        DrawDrawing();
        DrawTablet(shapes);
        DrawBooks(shapes);
        DrawPlant(shapes);
        DrawGoblet(shapes);
        DrawCandleHolder(shapes, time);

        // See-through things last, without writing depth, so they don't hide what's behind them
        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableDepthMask();
            DrawSteam(time);
            DrawFlameHalo(time);
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableDepthMask();
    }

    // ---------------- MUG ----------------

    // The #7B171B mug with the Birb Box logo on the front, half full of coffee.
    private const float MugRadius = 0.55f, MugHeight = 1.3f;

    private void DrawMug(UnitShapes shapes)
    {
        Vector3 m = MugSpot;
        shapes.Cylinder(m, MugRadius, MugHeight, MugRed);
        shapes.Cylinder(m + new Vector3(0, MugHeight, 0), MugRadius * 0.85f, 0.01f, Coffee);

        // Handle on the right side: a C-shaped loop made of a chain of small balls,
        // from the bottom of the mug, out to the right, and back in at the top
        const int HandleBalls = 18;
        Vector3 loopCenter = new Vector3(m.X + MugRadius, m.Y + 0.675f, m.Z);
        for (int i = 0; i <= HandleBalls; i++)
        {
            float angle = -MathF.PI / 2f + MathF.PI * i / HandleBalls;
            Vector3 point = loopCenter + new Vector3(MathF.Cos(angle) * 0.34f, MathF.Sin(angle) * 0.33f, 0);
            shapes.Ball(point, new Vector3(0.075f, 0.075f, 0.09f), MugRed);
        }

        DrawLogo(m);
    }

    // The logo printed on the front of the mug, bent around its curve.
    // It's drawn as a strip of narrow upright pieces, each one a slice of the picture
    // set a little further around the mug, sitting just outside its surface.
    private void DrawLogo(Vector3 mug)
    {
        const int Slices = 12;
        float halfHeight = 0.3f;
        float width = halfHeight * 2f * logo.Width / logo.Height;
        float radius = MugRadius + 0.005f;
        float wrapAngle = width / radius;              // how far around the mug the logo reaches, in radians
        float top = mug.Y + 0.68f + halfHeight, bottom = mug.Y + 0.68f - halfHeight;

        Rlgl.SetTexture(logo.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(235, 225, 215, 255);
            for (int i = 0; i < Slices; i++)
            {
                float u0 = (float)i / Slices, u1 = (float)(i + 1) / Slices;    // left and right edge of this slice, 0..1 across the picture
                float a0 = (u0 - 0.5f) * wrapAngle, a1 = (u1 - 0.5f) * wrapAngle;
                float x0 = mug.X + MathF.Sin(a0) * radius, z0 = mug.Z + MathF.Cos(a0) * radius;
                float x1 = mug.X + MathF.Sin(a1) * radius, z1 = mug.Z + MathF.Cos(a1) * radius;

                Rlgl.TexCoord2f(u0, 0); Rlgl.Vertex3f(x0, top, z0);
                Rlgl.TexCoord2f(u0, 1); Rlgl.Vertex3f(x0, bottom, z0);
                Rlgl.TexCoord2f(u1, 1); Rlgl.Vertex3f(x1, bottom, z1);
                Rlgl.TexCoord2f(u1, 0); Rlgl.Vertex3f(x1, top, z1);
            }
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    // Three wisps of puffs rising from the coffee. Each puff drifts up, swells, sways and fades,
    // then starts again at the bottom. Many small, faint puffs overlap so they blend into a wisp.
    private void DrawSteam(float time)
    {
        Vector3 top = MugSpot + new Vector3(0, MugHeight + 0.05f, 0);
        const int Wisps = 3, PuffsPerWisp = 12;
        const float RiseTime = 4f, RiseHeight = 2.4f;

        for (int w = 0; w < Wisps; w++)
        {
            for (int p = 0; p < PuffsPerWisp; p++)
            {
                float t = (time / RiseTime + (float)p / PuffsPerWisp + w * 0.37f) % 1f;    // 0 at the coffee, 1 at the top
                float sway = MathF.Sin(t * 6f + w * 2f) * 0.25f;
                Vector3 pos = top + new Vector3((w - 1) * 0.18f + sway, t * RiseHeight, sway * 0.4f);
                float fadeIn = MathF.Min(t * 5f, 1f);
                float alpha = fadeIn * (1f - t) * 0.07f;
                Raylib.DrawSphere(pos, 0.06f + t * 0.2f, Raylib.Fade(Steam, alpha));
            }
        }
    }

    // ---------------- PERSONAL CLUTTER ----------------

    // A child's crayon drawing of a bird under a sun, lying on the table.
    // The marks are listed in paper space: X across, Z down the page (toward the camera), page is 2.8 x 2.
    private void DrawDrawing()
    {
        const float Yaw = -20f;
        Vector3 c = DrawingSpot;
        FlatRect(c + new Vector3(0, 0.01f, 0), new Vector2(2.8f, 2f), Yaw, Paper);

        Color orange = new Color(200, 100, 40, 255), blue = new Color(60, 95, 190, 255);
        Color yellow = new Color(215, 185, 50, 255), green = new Color(70, 150, 65, 255);

        CrayonLoop(c, Yaw, new Vector2(0.1f, 0.15f), new Vector2(0.5f, 0.33f), orange);     // body
        CrayonLoop(c, Yaw, new Vector2(0.6f, -0.25f), new Vector2(0.2f, 0.2f), orange);     // head
        Crayon(c, Yaw, new Vector2(0.78f, -0.3f), new Vector2(1.0f, -0.22f), blue);         // beak
        Crayon(c, Yaw, new Vector2(1.0f, -0.22f), new Vector2(0.78f, -0.16f), blue);
        Crayon(c, Yaw, new Vector2(0.0f, 0.47f), new Vector2(-0.05f, 0.72f), blue);         // legs
        Crayon(c, Yaw, new Vector2(0.25f, 0.47f), new Vector2(0.3f, 0.72f), blue);
        Crayon(c, Yaw, new Vector2(-0.4f, 0.05f), new Vector2(-0.75f, -0.1f), orange);      // tail
        CrayonLoop(c, Yaw, new Vector2(-1.0f, -0.62f), new Vector2(0.2f, 0.2f), yellow);    // sun
        Crayon(c, Yaw, new Vector2(-1.3f, 0.78f), new Vector2(1.3f, 0.74f), green);         // grass
    }

    // A tablet lying at an angle, its screen showing a few lines of a bird list.
    private void DrawTablet(UnitShapes shapes)
    {
        const float Yaw = 15f;
        Vector3 c = TabletSpot;
        shapes.Box(c + new Vector3(0, 0.06f, 0), new Vector3(3.2f, 0.12f, 2.2f), TabletBody, Vector3.UnitY, Yaw);
        FlatRect(c + new Vector3(0, 0.125f, 0), new Vector2(2.9f, 1.9f), Yaw, TabletScreen);
        for (int i = 0; i < 4; i++)
        {
            float lineWidth = i % 2 == 0 ? 1.8f : 1.2f;
            Vector3 offset = Turn(new Vector3(-1.2f + lineWidth / 2f, 0, -0.6f + i * 0.4f), Yaw);
            FlatRect(c + offset + new Vector3(0, 0.13f, 0), new Vector2(lineWidth, 0.12f), Yaw, TabletText);
        }
    }

    // Two books, the top one turned a little.
    private void DrawBooks(UnitShapes shapes)
    {
        DrawBook(shapes, BooksSpot, 0f, new Color(110, 40, 35, 255));
        DrawBook(shapes, BooksSpot + new Vector3(0, 0.46f, 0), 12f, new Color(40, 70, 100, 255));
    }

    // A block of pages between two covers, with the spine on the left. bottom: middle of the book's underside.
    private void DrawBook(UnitShapes shapes, Vector3 bottom, float yaw, Color cover)
    {
        shapes.Box(bottom + new Vector3(0, 0.03f, 0), new Vector3(3f, 0.06f, 2.2f), cover, Vector3.UnitY, yaw);
        shapes.Box(bottom + new Vector3(0, 0.23f, 0) + Turn(new Vector3(0.05f, 0, 0), yaw), new Vector3(2.85f, 0.34f, 2.05f), Pages, Vector3.UnitY, yaw);
        shapes.Box(bottom + new Vector3(0, 0.43f, 0), new Vector3(3f, 0.06f, 2.2f), cover, Vector3.UnitY, yaw);
        shapes.Box(bottom + new Vector3(0, 0.23f, 0) + Turn(new Vector3(-1.47f, 0, 0), yaw), new Vector3(0.06f, 0.46f, 2.2f), cover, Vector3.UnitY, yaw);
    }

    // A clay pot with a lip, dark soil, and a bushy clump of leaves.
    private void DrawPlant(UnitShapes shapes)
    {
        Vector3 p = PlantSpot;
        shapes.Cylinder(p, 0.7f, 1.1f, Pot);
        shapes.Cylinder(p + new Vector3(0, 1.0f, 0), 0.8f, 0.18f, Pot);
        shapes.Cylinder(p + new Vector3(0, 1.12f, 0), 0.68f, 0.07f, Soil);

        Vector3[] leaves =
        {
            new(0.35f, 1.6f, 0.1f), new(-0.4f, 1.8f, -0.1f), new(0f, 2.1f, 0.2f),
            new(0.15f, 1.9f, -0.35f), new(-0.2f, 2.4f, 0f), new(0.3f, 2.3f, 0.1f),
        };
        for (int i = 0; i < leaves.Length; i++)
        {
            shapes.Ball(p + leaves[i], new Vector3(0.5f, 0.25f, 0.32f), i % 2 == 0 ? Leaf : LeafLight);
        }
    }

    // ---------------- MEDIEVAL-REVIVAL DECOR ----------------

    // A pewter-look goblet with a little wine in it.
    private void DrawGoblet(UnitShapes shapes)
    {
        Vector3 g = GobletSpot;
        shapes.Cylinder(g, 0.6f, 0.1f, Pewter);
        shapes.Cylinder(g + new Vector3(0, 0.1f, 0), 0.12f, 1.0f, Pewter);
        shapes.Ball(g + new Vector3(0, 0.55f, 0), new Vector3(0.2f, 0.2f, 0.2f), Pewter);
        shapes.Cylinder(g + new Vector3(0, 1.1f, 0), 0.35f, 0.25f, Pewter);
        shapes.Cylinder(g + new Vector3(0, 1.35f, 0), 0.55f, 0.85f, Pewter);
        shapes.Cylinder(g + new Vector3(0, 2.2f, 0), 0.49f, 0.01f, Wine);
    }

    // A printed brass candle holder: base, stem with a knob, drip pan, a wax candle, and an LED flame.
    private const float CandleTop = 3.32f;    // height of the candle's top above the table

    private void DrawCandleHolder(UnitShapes shapes, float time)
    {
        Vector3 c = CandleSpot;
        shapes.Cylinder(c, 0.85f, 0.2f, Brass);
        shapes.Cylinder(c + new Vector3(0, 0.2f, 0), 0.2f, 1.5f, Brass);
        shapes.Ball(c + new Vector3(0, 0.9f, 0), new Vector3(0.32f, 0.32f, 0.32f), Brass);
        shapes.Cylinder(c + new Vector3(0, 1.7f, 0), 0.7f, 0.12f, Brass);
        shapes.Cylinder(c + new Vector3(0, 1.82f, 0), 0.32f, CandleTop - 1.82f, Wax);
        shapes.Box(c + new Vector3(0, CandleTop + 0.06f, 0), new Vector3(0.04f, 0.12f, 0.04f), new Color(40, 30, 25, 255));

        // The flame is unlit so it always looks bright. Its size wobbles a little, like a cheap LED candle.
        float flicker = 1f + 0.08f * MathF.Sin(time * 13f) + 0.05f * MathF.Sin(time * 7.3f);
        Vector3 flame = c + new Vector3(0, CandleTop + 0.3f, 0);
        Raylib.DrawSphereEx(flame, 0.12f * flicker, 8, 8, Flame);
        Raylib.DrawSphereEx(flame + new Vector3(0, 0.12f, 0), 0.08f * flicker, 8, 8, Flame);
    }

    private void DrawFlameHalo(float time)
    {
        float flicker = 1f + 0.08f * MathF.Sin(time * 13f);
        Vector3 flame = CandleSpot + new Vector3(0, CandleTop + 0.3f, 0);
        Raylib.BeginBlendMode(BlendMode.Additive);
            Raylib.DrawSphere(flame, 0.45f * flicker, Raylib.Fade(Flame, 0.25f));
        Raylib.EndBlendMode();
    }

    // ---------------- FLAT SHAPES ----------------

    // Turns an offset around the up axis by yaw degrees, the same way UnitShapes turns a box.
    private static Vector3 Turn(Vector3 offset, float yawDegrees)
    {
        return Vector3.Transform(offset, Matrix4x4.CreateRotationY(yawDegrees * Raylib.DEG2RAD));
    }

    // An unlit flat rectangle lying face up, turned by yaw degrees. Used for screens and paper.
    private static void FlatRect(Vector3 center, Vector2 size, float yawDegrees, Color color)
    {
        Vector3 a = center + Turn(new Vector3(-size.X / 2f, 0, -size.Y / 2f), yawDegrees);
        Vector3 b = center + Turn(new Vector3(-size.X / 2f, 0, size.Y / 2f), yawDegrees);
        Vector3 c = center + Turn(new Vector3(size.X / 2f, 0, size.Y / 2f), yawDegrees);
        Vector3 d = center + Turn(new Vector3(size.X / 2f, 0, -size.Y / 2f), yawDegrees);

        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(color.R, color.G, color.B, color.A);
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
            Rlgl.Vertex3f(c.X, c.Y, c.Z);
            Rlgl.Vertex3f(d.X, d.Y, d.Z);
        Rlgl.End();
    }

    // One crayon stroke on the drawing, from a to b in paper space.
    private static void Crayon(Vector3 paperCenter, float yaw, Vector2 a, Vector2 b, Color color)
    {
        Vector2 middle = (a + b) / 2f;
        Vector2 along = b - a;
        float angle = -MathF.Atan2(along.Y, along.X) * Raylib.RAD2DEG;    // turn the stroke to point from a to b
        Vector3 center = paperCenter + Turn(new Vector3(middle.X, 0, middle.Y), yaw) + new Vector3(0, 0.015f, 0);
        FlatRect(center, new Vector2(along.Length() + 0.05f, 0.05f), yaw + angle, color);
    }

    // A wobbly crayon oval made of short strokes.
    private static void CrayonLoop(Vector3 paperCenter, float yaw, Vector2 middle, Vector2 radii, Color color)
    {
        const int Steps = 10;
        for (int i = 0; i < Steps; i++)
        {
            float a0 = i * MathF.PI * 2f / Steps, a1 = (i + 1) * MathF.PI * 2f / Steps;
            Vector2 p0 = middle + new Vector2(MathF.Cos(a0) * radii.X, MathF.Sin(a0) * radii.Y);
            Vector2 p1 = middle + new Vector2(MathF.Cos(a1) * radii.X, MathF.Sin(a1) * radii.Y);
            Crayon(paperCenter, yaw, p0, p1, color);
        }
    }
}
