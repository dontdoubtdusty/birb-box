using System.Numerics;
using Raylib_cs;

// The room the Birb Box sits in: a cabin on a space station during the night cycle.
// A dark dining table with a woven runner, a chair at each end, and a panelled back wall
// with a wide window, two warm lamps and the Royal Guild of Fowlers' crest.
// The things sitting on the table (mug, books, candle holder...) are in TableProps; the radio is in Radio.
//
// It's drawn straight to the screen first; the hologram scene is laid over it afterward,
// so nothing in here can appear in front of the pedestals. It never turns with the hologram.
//
// The Birb Box's pedestal is drawn right after this, in the same pass, by HologramRenderer.
//
// Scale: 1 unit is about 7.5 cm. The table is 30 x 19 units (about 2.25 m x 1.4 m).
public class Backdrop
{
    public const float TableY = -2.7f;              // the tabletop, where the pedestal stands

    // TABLE (scene units). Centered a little right of the Birb Box, and deep enough for the pedestal's plinth.
    public static readonly Vector3 TableCenter = new Vector3(2.5f, TableY, -0.5f);
    public const float TableLength = 30f;           // along X
    public const float TableDepth = 19f;            // along Z
    private const float TableThickness = 0.6f;
    private const float TableHeight = 10f;          // tabletop to floor
    public const float FloorY = TableY - TableHeight;

    // ROOM
    private const float BackWallZ = -12f;
    private const float WindowLeft = -12f, WindowRight = 12f, WindowBottom = 0f, WindowTop = 10f;
    public static readonly Vector3 LampLeft = new Vector3(-16f, 6f, BackWallZ + 0.6f);
    public static readonly Vector3 LampRight = new Vector3(16f, 6f, BackWallZ + 0.6f);

    // COLORS
    private static readonly Color Floor = new Color(21, 26, 35, 255);
    private static readonly Color Wall = new Color(30, 36, 50, 255);
    private static readonly Color WallSeam = new Color(22, 27, 38, 255);
    private static readonly Color WindowFrame = new Color(52, 60, 76, 255);
    private static readonly Color TableTop = new Color(46, 50, 60, 255);
    private static readonly Color TableLeg = new Color(30, 33, 40, 255);
    private static readonly Color Runner = new Color(110, 38, 34, 255);
    private static readonly Color RunnerGold = new Color(201, 160, 74, 255);
    private static readonly Color ChairColor = new Color(48, 54, 68, 255);
    private static readonly Color CrestBlue = new Color(38, 62, 104, 255);
    private static readonly Color LampGlow = new Color(255, 200, 130, 255);

    // Everything in the room is lit by room.fs: a dim fixed light from above plus the two warm wall lamps.
    private Shader roomShader;
    public Shader RoomShader => roomShader;
    private int locViewPos, locShine;
    private int locHoloLight, locHoloLightColor, locLensLight, locLensLightColor, locWellRadius;
    public UnitShapes Shapes { get; } = new UnitShapes();
    public TableProps Props { get; } = new TableProps();
    private Texture2D windowView;

    public void Load()
    {
        roomShader = Raylib.LoadShader(Assets.PathTo("shaders/room.vs"), Assets.PathTo("shaders/room.fs"));
        SetRoomUniform("lampA", LampLeft);
        SetRoomUniform("lampB", LampRight);
        SetRoomUniform("roomCenter", TableCenter);
        locViewPos = Raylib.GetShaderLocation(roomShader, "viewPos");
        locShine = Raylib.GetShaderLocation(roomShader, "shine");
        locHoloLight = Raylib.GetShaderLocation(roomShader, "holoLight");
        locHoloLightColor = Raylib.GetShaderLocation(roomShader, "holoLightColor");
        locLensLight = Raylib.GetShaderLocation(roomShader, "lensLight");
        locLensLightColor = Raylib.GetShaderLocation(roomShader, "lensLightColor");
        locWellRadius = Raylib.GetShaderLocation(roomShader, "wellRadius");
        SetShine(0f);

        Shapes.Load(roomShader);
        Props.Load();
        windowView = PaintWindowView();
    }

    public void Unload()
    {
        Shapes.Unload();
        Props.Unload();
        Raylib.UnloadTexture(windowView);
        Raylib.UnloadShader(roomShader);
    }

    private void SetRoomUniform(string name, Vector3 value)
    {
        Raylib.SetShaderValue(roomShader, Raylib.GetShaderLocation(roomShader, name), value, ShaderUniformDataType.Vec3);
    }

    // How polished the next things drawn with the room shader are: 0 is matte, the default for the room.
    // Whoever turns it up turns it back to 0 when done.
    public void SetShine(float shine)
    {
        Raylib.SetShaderValue(roomShader, locShine, shine, ShaderUniformDataType.Float);
    }

    // The Birb Box's own light on the room: where it comes from and how bright (color times power; black when off).
    // Set once a frame, before the room and the pedestal are drawn (see room.fs).
    public void SetProjectorLights(Vector3 holoLight, Vector3 holoColor, Vector3 lensLight, Vector3 lensColor, float wellRadius)
    {
        Raylib.SetShaderValue(roomShader, locHoloLight, holoLight, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(roomShader, locHoloLightColor, holoColor, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(roomShader, locLensLight, lensLight, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(roomShader, locLensLightColor, lensColor, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(roomShader, locWellRadius, wellRadius, ShaderUniformDataType.Float);
    }

    public void Draw(Camera3D camera, float pedestalRadius, Radio radio)
    {
        Raylib.ClearBackground(new Color(8, 10, 16, 255));    // also clears depth, so the room sorts itself correctly
        Raylib.SetShaderValue(roomShader, locViewPos, camera.Position, ShaderUniformDataType.Vec3);

        Raylib.BeginMode3D(camera);
            DrawFloorAndWall();
            DrawWindow();
            DrawCrest();
            DrawTable();
            DrawRunner();
            DrawChairs();
            Props.Draw(Shapes);
            radio.Draw(Shapes);

            // Soft shadows on the table under the pedestal and the radio
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableDepthMask();
            Rlgl.DisableBackfaceCulling();
            Color shadow = new Color(0, 0, 0, 255);
            DrawSoftDisc(new Vector3(0, TableY + 0.02f, 0), pedestalRadius * 1.1f, pedestalRadius * 1.6f, shadow, 0.55f);
            DrawSoftDisc(Radio.Spot + new Vector3(0, 0.02f, 0), 1.5f, 2.6f, shadow, 0.4f);

            // Warm glow on the wall around each lamp, and around the radio's dial
            Raylib.BeginBlendMode(BlendMode.Additive);
                DrawWallGlow(LampLeft, 5f, LampGlow);
                DrawWallGlow(LampRight, 5f, LampGlow);
                radio.DrawGlow();
            Raylib.EndBlendMode();
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableBackfaceCulling();
            Rlgl.EnableDepthMask();
        Raylib.EndMode3D();
    }

    // ---------------- ROOM ----------------

    private void DrawFloorAndWall()
    {
        Shapes.Box(new Vector3(0, FloorY - 0.5f, 0), new Vector3(120f, 1f, 80f), Floor);
        float wallHeight = 40f;
        Shapes.Box(new Vector3(0, FloorY + wallHeight / 2f, BackWallZ - 0.5f), new Vector3(120f, wallHeight, 1f), Wall);

        // Panel seams: vertical every 6 units, and a band along the bottom
        for (float x = -42f; x <= 42f; x += 6f)
        {
            if (x > WindowLeft - 1f && x < WindowRight + 1f) continue;    // none behind the window
            Shapes.Box(new Vector3(x, FloorY + wallHeight / 2f, BackWallZ + 0.02f), new Vector3(0.15f, wallHeight, 0.05f), WallSeam);
        }
        Shapes.Box(new Vector3(0, -4f, BackWallZ + 0.02f), new Vector3(120f, 0.2f, 0.05f), WallSeam);
    }

    // The view out of the window is a painted picture (see PaintWindowView), laid behind a thick frame.
    private void DrawWindow()
    {
        float z = BackWallZ + 0.05f;
        Rlgl.SetTexture(windowView.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(255, 255, 255, 255);
            Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(WindowLeft, WindowTop, z);
            Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(WindowLeft, WindowBottom, z);
            Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(WindowRight, WindowBottom, z);
            Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(WindowRight, WindowTop, z);
        Rlgl.End();
        Rlgl.SetTexture(0);

        // Frame: top, bottom, sides, and two upright bars dividing the glass into three panes
        float frame = 0.8f, depth = 0.8f, frameZ = BackWallZ + depth / 2f;
        float midX = (WindowLeft + WindowRight) / 2f, midY = (WindowBottom + WindowTop) / 2f;
        float width = WindowRight - WindowLeft, height = WindowTop - WindowBottom;
        Shapes.Box(new Vector3(midX, WindowTop + frame / 2f, frameZ), new Vector3(width + frame * 2f, frame, depth), WindowFrame);
        Shapes.Box(new Vector3(midX, WindowBottom - frame / 2f, frameZ), new Vector3(width + frame * 2f, frame, depth), WindowFrame);
        Shapes.Box(new Vector3(WindowLeft - frame / 2f, midY, frameZ), new Vector3(frame, height, depth), WindowFrame);
        Shapes.Box(new Vector3(WindowRight + frame / 2f, midY, frameZ), new Vector3(frame, height, depth), WindowFrame);
        Shapes.Box(new Vector3(midX - width / 6f, midY, frameZ), new Vector3(0.35f, height, depth * 0.6f), WindowFrame);
        Shapes.Box(new Vector3(midX + width / 6f, midY, frameZ), new Vector3(0.35f, height, depth * 0.6f), WindowFrame);

        // The two lamps: small bright shades, unlit so they always read as glowing
        foreach (Vector3 lamp in new[] { LampLeft, LampRight })
        {
            Raylib.DrawCube(lamp, 0.9f, 1.3f, 0.5f, LampGlow);
            Shapes.Box(lamp + new Vector3(0, -0.8f, -0.2f), new Vector3(0.4f, 0.4f, 0.4f), WindowFrame);
        }
    }

    // The Royal Guild of Fowlers' crest: a blue shield with a gold rim and a gold bird.
    // The shield's point is a square turned 45 degrees and half hidden behind the shield.
    private void DrawCrest()
    {
        Vector3 c = new Vector3(21f, 3f, BackWallZ + 0.1f);
        Shapes.Box(c, new Vector3(3.4f, 3.2f, 0.1f), RunnerGold);
        Shapes.Box(c + new Vector3(0, -1.6f, 0), new Vector3(2.4f, 2.4f, 0.1f), RunnerGold, Vector3.UnitZ, 45f);
        Shapes.Box(c + new Vector3(0, 0, 0.06f), new Vector3(3f, 2.8f, 0.1f), CrestBlue);
        Shapes.Box(c + new Vector3(0, -1.5f, 0.06f), new Vector3(2.1f, 2.1f, 0.1f), CrestBlue, Vector3.UnitZ, 45f);

        Vector3 bird = c + new Vector3(0, -0.3f, 0.15f);
        Shapes.Ball(bird, new Vector3(0.75f, 0.5f, 0.08f), RunnerGold);                                // body
        Shapes.Ball(bird + new Vector3(0.65f, 0.45f, 0), new Vector3(0.3f, 0.3f, 0.08f), RunnerGold);   // head
        Shapes.Box(bird + new Vector3(1.0f, 0.45f, 0), new Vector3(0.25f, 0.12f, 0.08f), RunnerGold);   // beak
        Shapes.Box(bird + new Vector3(-0.85f, 0.2f, 0), new Vector3(0.6f, 0.2f, 0.08f), RunnerGold, Vector3.UnitZ, 30f);  // tail
    }

    // ---------------- TABLE AND CHAIRS ----------------

    private void DrawTable()
    {
        Vector3 c = TableCenter;
        Shapes.Box(c + new Vector3(0, -TableThickness / 2f, 0), new Vector3(TableLength, TableThickness, TableDepth), TableTop);

        float legHeight = TableHeight - TableThickness;
        float legX = TableLength / 2f - 1.5f, legZ = TableDepth / 2f - 1.5f;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 leg = new Vector3(c.X + sx * legX, FloorY + legHeight / 2f, c.Z + sz * legZ);
                Shapes.Box(leg, new Vector3(0.9f, legHeight, 0.9f), TableLeg);
            }
        }
    }

    // A red woven runner from the back of the table, under the Birb Box, and down over the front edge,
    // with gold bands, gold diamonds on the hanging end, and a fringe.
    private void DrawRunner()
    {
        const float Width = 7f, Hang = 3f;
        float back = TableCenter.Z - TableDepth / 2f, front = TableCenter.Z + TableDepth / 2f;
        float y = TableY + 0.02f;

        Shapes.Box(new Vector3(0, y, (back + front) / 2f), new Vector3(Width, 0.04f, front - back), Runner);
        Shapes.Box(new Vector3(0, TableY - Hang / 2f, front + 0.04f), new Vector3(Width, Hang, 0.06f), Runner);

        // Gold bands across the top, near the front edge
        Shapes.Box(new Vector3(0, y + 0.01f, front - 0.8f), new Vector3(Width - 0.4f, 0.04f, 0.15f), RunnerGold);
        Shapes.Box(new Vector3(0, y + 0.01f, back + 0.8f), new Vector3(Width - 0.4f, 0.04f, 0.15f), RunnerGold);

        // On the hanging end: two bands with a row of diamonds between them
        float faceZ = front + 0.09f;
        Shapes.Box(new Vector3(0, TableY - 0.5f, faceZ), new Vector3(Width - 0.4f, 0.12f, 0.03f), RunnerGold);
        Shapes.Box(new Vector3(0, TableY - Hang + 0.5f, faceZ), new Vector3(Width - 0.4f, 0.12f, 0.03f), RunnerGold);
        for (int i = -2; i <= 2; i++)
        {
            Shapes.Box(new Vector3(i * 1.3f, TableY - Hang / 2f, faceZ), new Vector3(0.6f, 0.6f, 0.03f), RunnerGold, Vector3.UnitZ, 45f);
        }
        for (float x = -Width / 2f + 0.15f; x < Width / 2f; x += 0.3f)
        {
            Shapes.Box(new Vector3(x, TableY - Hang - 0.2f, front + 0.04f), new Vector3(0.06f, 0.4f, 0.06f), RunnerGold);
        }
    }

    // One chair at each end of the table, facing in.
    private void DrawChairs()
    {
        float seatY = FloorY + 6f;
        float leftX = TableCenter.X - TableLength / 2f - 2.5f;
        float rightX = TableCenter.X + TableLength / 2f + 2.5f;
        DrawChair(new Vector3(leftX, seatY, TableCenter.Z), -1);
        DrawChair(new Vector3(rightX, seatY, TableCenter.Z), 1);
    }

    // seat: the middle of the seat's top. backSide: -1 puts the back on the left (-X), 1 on the right.
    private void DrawChair(Vector3 seat, int backSide)
    {
        Shapes.Box(seat + new Vector3(0, -0.25f, 0), new Vector3(4f, 0.5f, 4f), ChairColor);
        Shapes.Box(seat + new Vector3(backSide * 1.8f, 4f, 0), new Vector3(0.4f, 8f, 4f), ChairColor);

        float legHeight = seat.Y - 0.5f - FloorY;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 leg = new Vector3(seat.X + sx * 1.7f, FloorY + legHeight / 2f, seat.Z + sz * 1.7f);
                Shapes.Box(leg, new Vector3(0.35f, legHeight, 0.35f), TableLeg);
            }
        }
    }

    // ---------------- PAINTED WINDOW VIEW ----------------

    // Stars, and the night side of a planet with a thin lit edge and a few city lights.
    // Painted once at startup into an image, then turned into a texture.
    // A fixed random seed keeps the stars in the same places every time.
    private static Texture2D PaintWindowView()
    {
        const int Width = 960, Height = 400;
        Image image = Raylib.GenImageColor(Width, Height, new Color(3, 6, 15, 255));
        Random random = new Random(7);

        for (int i = 0; i < 450; i++)
        {
            int x = random.Next(Width), y = random.Next(Height);
            int b = 120 + random.Next(136);
            Raylib.ImageDrawPixel(ref image, x, y, new Color(b, b, Math.Min(255, b + 20), 255));
            if (random.Next(12) == 0) Raylib.ImageDrawRectangle(ref image, x, y, 2, 2, new Color(b, b, 255, 255));
        }

        // Planet: an atmosphere ring, the lit body, then a big dark circle offset to the lower right for the night side
        Vector2 planet = new Vector2(760, 560);
        Raylib.ImageDrawCircle(ref image, (int)planet.X, (int)planet.Y, 432, new Color(80, 170, 230, 255));
        Raylib.ImageDrawCircle(ref image, (int)planet.X, (int)planet.Y, 424, new Color(38, 96, 146, 255));
        Raylib.ImageDrawCircle(ref image, (int)planet.X + 50, (int)planet.Y + 50, 400, new Color(12, 34, 58, 255));

        for (int i = 0; i < 60; i++)
        {
            int x = 600 + random.Next(340), y = 260 + random.Next(140);
            if (Vector2.Distance(new Vector2(x, y), planet + new Vector2(50, 50)) > 390) continue;    // only on the night side
            Raylib.ImageDrawPixel(ref image, x, y, new Color(255, 205, 130, 255));
        }

        Texture2D texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
        return texture;
    }

    // ---------------- FLAT GLOWS AND SHADOWS ----------------

    private const int RingSegments = 48;

    // A flat ring lying on the table: full strength out to innerRadius, fading to nothing at outerRadius.
    private static void DrawSoftDisc(Vector3 center, float innerRadius, float outerRadius, Color color, float strength)
    {
        Color solid = Raylib.Fade(color, strength), clear = Raylib.Fade(color, 0f);
        Rlgl.Begin(DrawMode.Triangles);
        for (int i = 0; i < RingSegments; i++)
        {
            float a0 = i * MathF.PI * 2f / RingSegments, a1 = (i + 1) * MathF.PI * 2f / RingSegments;
            Vector3 d0 = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)), d1 = new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1));

            // Solid middle, then the fading ring
            Vertex(center, solid); Vertex(center + d1 * innerRadius, solid); Vertex(center + d0 * innerRadius, solid);
            Vertex(center + d0 * innerRadius, solid); Vertex(center + d1 * outerRadius, clear); Vertex(center + d0 * outerRadius, clear);
            Vertex(center + d0 * innerRadius, solid); Vertex(center + d1 * innerRadius, solid); Vertex(center + d1 * outerRadius, clear);
        }
        Rlgl.End();
    }

    // A round glow standing up against the back wall, bright in the middle and fading out.
    private static void DrawWallGlow(Vector3 center, float radius, Color color)
    {
        Vector3 c = new Vector3(center.X, center.Y, BackWallZ + 0.08f);
        Color middle = Raylib.Fade(color, 0.35f), edge = Raylib.Fade(color, 0f);
        Rlgl.Begin(DrawMode.Triangles);
        for (int i = 0; i < RingSegments; i++)
        {
            float a0 = i * MathF.PI * 2f / RingSegments, a1 = (i + 1) * MathF.PI * 2f / RingSegments;
            Vertex(c, middle);
            Vertex(c + new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0) * radius, edge);
            Vertex(c + new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0) * radius, edge);
        }
        Rlgl.End();
    }

    private static void Vertex(Vector3 p, Color c)
    {
        Rlgl.Color4ub(c.R, c.G, c.B, c.A);
        Rlgl.Vertex3f(p.X, p.Y, p.Z);
    }
}
