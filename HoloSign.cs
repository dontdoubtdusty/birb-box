using System.Numerics;
using Raylib_cs;

// The holographic shop sign floating above the box: an old-timey hanging sign (a rod with a scrolled end,
// two chains, and a framed board) drawn in hologram light. It names the scene the box will switch to:
// STORE over the birbs, BIRBS over the store. BoxMode holds where it hangs and handles clicks on it.
//
// The board has a dark see-through backing, like a real sign, so the box's top ring doesn't show through it.
// Positions below are in sign space: X right, Y up, (0, 0) at the middle of the board, facing the camera (+Z).
// The board and chains sway gently from the rod; the rod stays still.
public class HoloSign
{
    private const string FontFile = "fonts/Quicksand-SemiBold.ttf";    // the same font UiText uses
    private const float TextHeight = 0.5f;
    private const float Chamfer = 0.16f;        // how much is cut off each corner of the board
    private const float FrameInset = 0.09f;     // the inner frame line sits this far inside the outer one
    private const float RodGap = 0.45f;         // from the board's top edge up to the rod
    private const float ChainX = 1.05f;         // the chains hang this far either side of the middle
    private const int ChainLinks = 4;
    private const float SwayDegrees = 1.5f;

    private static readonly Color Backing = new Color(8, 14, 20, 150);

    private Texture2D storeWord, birbsWord;

    // Set at the start of Draw so the helpers below can use them
    private Vector3 center;
    private Matrix4x4 sway;
    private Color core, halo;

    public void Load()
    {
        storeWord = LoadWord("STORE");
        birbsWord = LoadWord("BIRBS");
    }

    public void Unload()
    {
        Raylib.UnloadTexture(storeWord);
        Raylib.UnloadTexture(birbsWord);
    }

    public void Draw(ViewWindow view, BoxMode mode, Vector3 holoColor)
    {
        float brightness = mode.SignHovered ? 1.5f : 1f;
        core = Palette.ToColor(holoColor, Palette.GlowAlpha(200, brightness));
        halo = Palette.ToColor(holoColor, Palette.GlowAlpha(28, brightness));

        center = BoxMode.SignPosition(view);
        float halfWidth = BoxMode.SignWidth / 2f, halfHeight = BoxMode.SignHeight / 2f;
        float rodY = halfHeight + RodGap;

        // Side to side around the middle of the rod
        float angle = MathF.Sin((float)Raylib.GetTime() * 0.9f) * SwayDegrees * Raylib.DEG2RAD;
        sway = Matrix4x4.CreateTranslation(0, -rodY, 0) * Matrix4x4.CreateRotationZ(angle) * Matrix4x4.CreateTranslation(0, rodY, 0);

        Vector3[] outline = BoardOutline(halfWidth, halfHeight);
        // Normal blending, so it covers what's behind. No depth writes, so the glow drawn on it never flickers.
        Rlgl.DisableDepthMask();
        FillShape(outline, Backing);
        Rlgl.EnableDepthMask();

        HologramRenderer.BeginGlowDrawing();
        FillShape(outline, Palette.ToColor(holoColor, Palette.GlowAlpha(18, brightness)));
        DrawLoop(outline);
        DrawLoop(BoardOutline(halfWidth - FrameInset, halfHeight - FrameInset));
        DrawWord(mode.InStore ? birbsWord : storeWord, holoColor, brightness);
        DrawRod(halfWidth, rodY);
        DrawChain(-ChainX, rodY, halfHeight);
        DrawChain(ChainX, rodY, halfHeight);
        HologramRenderer.EndGlowDrawing();
    }

    // ---------------- PIECES ----------------

    // A rectangle with its corners cut off, going around counter-clockwise, swaying with the board.
    private Vector3[] BoardOutline(float halfWidth, float halfHeight)
    {
        float c = Chamfer;
        return new[]
        {
            Board(-halfWidth + c, halfHeight), Board(-halfWidth, halfHeight - c),
            Board(-halfWidth, -halfHeight + c), Board(-halfWidth + c, -halfHeight),
            Board(halfWidth - c, -halfHeight), Board(halfWidth, -halfHeight + c),
            Board(halfWidth, halfHeight - c), Board(halfWidth - c, halfHeight),
        };
    }

    // The word, with a slightly bigger faint copy behind it for a halo.
    private void DrawWord(Texture2D word, Vector3 holoColor, float brightness)
    {
        float height = TextHeight;
        float width = height * word.Width / word.Height;
        WordQuad(word, width * 1.06f, height * 1.06f, Palette.ToColor(holoColor, Palette.GlowAlpha(60, brightness)));
        WordQuad(word, width, height, Palette.ToColor(holoColor, Palette.GlowAlpha(230, brightness)));
    }

    private void WordQuad(Texture2D word, float width, float height, Color color)
    {
        float x = width / 2f, y = height / 2f;
        Vector3 topLeft = Board(-x, y), bottomLeft = Board(-x, -y), bottomRight = Board(x, -y), topRight = Board(x, y);

        Rlgl.SetTexture(word.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(color.R, color.G, color.B, color.A);
            Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(topLeft.X, topLeft.Y, topLeft.Z);
            Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
            Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(bottomRight.X, bottomRight.Y, bottomRight.Z);
            Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(topRight.X, topRight.Y, topRight.Z);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    // A straight rod above the board, curling into a small scroll at the left end, with a ball on the right.
    private void DrawRod(float halfWidth, float rodY)
    {
        float left = -halfWidth - 0.3f, right = halfWidth + 0.25f;
        Line(Fixed(left, rodY), Fixed(right, rodY));
        Raylib.DrawSphereEx(Fixed(right, rodY), 0.06f, 8, 8, core);

        // The scroll: a spiral that starts at the rod's end and winds inward below it
        const int Steps = 16;
        const float Radius = 0.16f;
        Vector3 previous = Fixed(left, rodY);
        for (int i = 1; i <= Steps; i++)
        {
            float t = i / (float)Steps;
            float a = MathF.PI / 2f + t * MathF.PI * 1.7f;
            float r = Radius * (1f - 0.55f * t);
            Vector3 next = Fixed(left + MathF.Cos(a) * r, rodY - Radius + MathF.Sin(a) * r);
            Line(previous, next);
            previous = next;
        }
    }

    // Links from the still rod down to the swaying board. Every other link is seen face-on (a small diamond),
    // the rest edge-on (a straight line), like a real chain.
    private void DrawChain(float x, float rodY, float boardTop)
    {
        Vector3 top = Fixed(x, rodY);
        Vector3 bottom = Board(x, boardTop);
        Vector3 along = (bottom - top) / ChainLinks;
        Vector3 side = Vector3.Normalize(new Vector3(-along.Y, along.X, 0f)) * 0.04f;

        for (int i = 0; i < ChainLinks; i++)
        {
            Vector3 start = top + along * i;
            Vector3 end = start + along;
            if (i % 2 == 0)
            {
                Vector3 middle = (start + end) / 2f;
                Line(start, middle + side);
                Line(middle + side, end);
                Line(end, middle - side);
                Line(middle - side, start);
            }
            else
            {
                Line(start, end);
            }
        }
    }

    // ---------------- HELPERS ----------------

    // Sign space to scene space: Board sways, Fixed doesn't.
    private Vector3 Board(float x, float y) => center + Vector3.Transform(new Vector3(x, y, 0f), sway);
    private Vector3 Fixed(float x, float y) => center + new Vector3(x, y, 0f);

    // A glowing line in the same style as the box's rings: a wide faint halo around a thin bright core.
    private void Line(Vector3 a, Vector3 b)
    {
        Raylib.DrawCylinderEx(a, b, 0.045f, 0.045f, 6, halo);
        Raylib.DrawCylinderEx(a, b, 0.013f, 0.013f, 5, core);
    }

    private void DrawLoop(Vector3[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Line(points[i], points[(i + 1) % points.Length]);
        }
    }

    // Fills a flat shape whose points go around its edge, as a fan of triangles from its middle.
    private static void FillShape(Vector3[] points, Color color)
    {
        Vector3 middle = Vector3.Zero;
        foreach (Vector3 p in points) middle += p;
        middle /= points.Length;

        Rlgl.DisableBackfaceCulling();
        Rlgl.Begin(DrawMode.Triangles);
            Rlgl.Color4ub(color.R, color.G, color.B, color.A);
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 a = points[i], b = points[(i + 1) % points.Length];
                Rlgl.Vertex3f(middle.X, middle.Y, middle.Z);
                Rlgl.Vertex3f(a.X, a.Y, a.Z);
                Rlgl.Vertex3f(b.X, b.Y, b.Z);
            }
        Rlgl.End();
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableBackfaceCulling();
    }

    // The word is drawn into a texture once, white, so it can be tinted with the hologram color when drawn.
    private static Texture2D LoadWord(string word)
    {
        Font font = Raylib.LoadFontEx(Assets.PathTo(FontFile), 128, null!, 0);
        Image text = Raylib.ImageTextEx(font, word, 128, 16, Color.White);
        Texture2D texture = Raylib.LoadTextureFromImage(text);
        Raylib.GenTextureMipmaps(ref texture);
        Raylib.SetTextureFilter(texture, TextureFilter.Trilinear);
        Raylib.UnloadImage(text);
        Raylib.UnloadFont(font);
        return texture;
    }
}
