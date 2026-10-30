using System.Numerics;
using Raylib_cs;

// The room the Birb Cube sits in: a cabin on a space station during the night cycle.
// A dark dining table with a woven runner, a chair at each end, and a panelled back wall
// with a wide window, two warm lamps and the Royal Guild of Fowlers' crest.
// The things sitting on the table (mug, books, candle holder...) are in TableProps; the radio is in Radio.
//
// It's drawn straight to the screen first; the hologram scene is laid over it afterward,
// so nothing in here can appear in front of the pedestals. It never turns with the hologram.
//
// The Birb Cube's pedestal is drawn right after this, in the same pass, by HologramRenderer.
//
// Scale: 1 unit is about 7.5 cm. The table is 30 x 19 units (about 2.25 m x 1.4 m).
public class Backdrop
{
    public const float TableY = -2.7f;              // the tabletop, where the pedestal stands

    // TABLE (scene units). Centered a little right of the Birb Cube, and deep enough for the pedestal's plinth.
    public static readonly Vector3 TableCenter = new Vector3(2.5f, TableY, -0.5f);
    public const float TableLength = 30f;           // along X
    public const float TableDepth = 19f;            // along Z
    private const float TableThickness = 0.6f;
    private const float TableHeight = 10f;          // tabletop to floor
    public const float FloorY = TableY - TableHeight;

    // ROOM
    private const float BackWallZ = -12f;
    private const float WindowLeft = -14f, WindowRight = 14f, WindowBottom = -1.5f, WindowTop = 13f;

    // WINDOW VIEW: the starfield picture (resources/starfield.png) behind the glass, with the painted planet in front
    private const float SkyPixelsPerUnit = 50f;     // how many of the picture's pixels one unit of glass shows
    private const double SkyDrift = 1.0 / 300.0;    // picture widths per second the stars slide by (one width in 5 minutes)
    private const float WindowDepth = 0.5f;         // how far away the stars look when the camera moves: 0 painted on the glass, 1 truly far
    private const float PlanetPixelsPerUnit = 40f;  // the painted planet's picture size
    private static readonly Color SkyTint = new Color(225, 225, 235, 255);    // a touch dimmer and cooler than the picture
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
    private int locViewPos, locShine, locDissolve;
    private int locHoloLight, locHoloLightColor, locLensLight, locLensLightColor, locWellRadius;
    public UnitShapes Shapes { get; } = new UnitShapes();
    public TableProps Props { get; } = new TableProps();
    private Texture2D starfield, planet;
    private Shader skyShader;    // sky.fs: the twinkle
    private int locSkyTime;

    public void Load()
    {
        roomShader = Raylib.LoadShader(Assets.PathTo("shaders/room.vs"), Assets.PathTo("shaders/room.fs"));
        SetRoomUniform("lampA", LampLeft);
        SetRoomUniform("lampB", LampRight);
        SetRoomUniform("roomCenter", TableCenter);
        locViewPos = Raylib.GetShaderLocation(roomShader, "viewPos");
        locShine = Raylib.GetShaderLocation(roomShader, "shine");
        locDissolve = Raylib.GetShaderLocation(roomShader, "dissolve");
        locHoloLight = Raylib.GetShaderLocation(roomShader, "holoLight");
        locHoloLightColor = Raylib.GetShaderLocation(roomShader, "holoLightColor");
        locLensLight = Raylib.GetShaderLocation(roomShader, "lensLight");
        locLensLightColor = Raylib.GetShaderLocation(roomShader, "lensLightColor");
        locWellRadius = Raylib.GetShaderLocation(roomShader, "wellRadius");
        SetShine(0f);
        SetDissolve(0f);

        Shapes.Load(roomShader);
        Props.Load();
        LoadWindowView();
    }

    public void Unload()
    {
        Shapes.Unload();
        Props.Unload();
        Raylib.UnloadTexture(starfield);
        Raylib.UnloadTexture(planet);
        Raylib.UnloadShader(skyShader);
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

    // How much of the next things drawn are dithered away: 0 solid (the default), 1 gone. Used to fade the hands.
    // Whoever turns it up turns it back to 0 when done.
    public void SetDissolve(float dissolve)
    {
        Raylib.SetShaderValue(roomShader, locDissolve, dissolve, ShaderUniformDataType.Float);
    }

    // The Birb Cube's own light on the room: where it comes from and how bright (color times power; black when off).
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
            DrawWindow(camera);
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

    // The view out of the window: the drifting, twinkling stars (through sky.fs), the planet in front of them,
    // then a thick frame.
    private void DrawWindow(Camera3D camera)
    {
        float z = BackWallZ + 0.05f;
        Vector2 sky = SkyOffset(camera);
        Vector2 skySpan = new Vector2((WindowRight - WindowLeft) * SkyPixelsPerUnit / starfield.Width,
                                      (WindowTop - WindowBottom) * SkyPixelsPerUnit / starfield.Height);
        Raylib.SetShaderValue(skyShader, locSkyTime, (float)Raylib.GetTime(), ShaderUniformDataType.Float);
        Raylib.BeginShaderMode(skyShader);
            DrawGlass(starfield, z, sky, sky + skySpan, SkyTint);
        Raylib.EndShaderMode();
        DrawGlass(planet, z + 0.01f, Vector2.Zero, Vector2.One, Color.White);

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

    // A red woven runner from the back of the table, under the Birb Cube, and down over the front edge,
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

    // ---------------- WINDOW VIEW ----------------

    // The starfield repeats mirrored, so it can slide forever without a seam, and has mipmaps,
    // so its small stars don't shimmer as they slide. The planet is painted, and the sky shader loaded.
    private void LoadWindowView()
    {
        starfield = Raylib.LoadTexture(Assets.PathTo("starfield.png"));
        Raylib.GenTextureMipmaps(ref starfield);
        Raylib.SetTextureFilter(starfield, TextureFilter.Trilinear);
        Raylib.SetTextureWrap(starfield, TextureWrap.MirrorRepeat);

        planet = PaintPlanet();

        skyShader = Raylib.LoadShader(Assets.PathTo("shaders/sky.vs"), Assets.PathTo("shaders/sky.fs"));
        locSkyTime = Raylib.GetShaderLocation(skyShader, "time");
        Raylib.SetShaderValue(skyShader, Raylib.GetShaderLocation(skyShader, "textureSize"),
                              new Vector2(starfield.Width, starfield.Height), ShaderUniformDataType.Vec2);
    }

    // Where in the starfield the window looks: the slow drift, plus the direction from the camera to the window,
    // so moving the camera slides the stars behind the glass the way something far away would.
    private Vector2 SkyOffset(Camera3D camera)
    {
        Vector3 middle = new Vector3((WindowLeft + WindowRight) / 2f, (WindowBottom + WindowTop) / 2f, BackWallZ);
        Vector3 toWindow = middle - camera.Position;
        float across = MathF.Atan2(toWindow.X, -toWindow.Z);
        float up = MathF.Atan2(toWindow.Y, MathF.Sqrt(toWindow.X * toWindow.X + toWindow.Z * toWindow.Z));

        // Truly far stars move one unit of glass (SkyPixelsPerUnit) for each 1/distance radians the view turns
        float pixelsPerRadian = SkyPixelsPerUnit * toWindow.Length() * WindowDepth;
        // The mirrored picture repeats every 2 widths, so the drift is kept below 2: small numbers stay precise in long sessions
        float drift = (float)(Raylib.GetTime() * SkyDrift % 2.0);
        float u = drift + across * pixelsPerRadian / starfield.Width;
        float v = -up * pixelsPerRadian / starfield.Height;
        return new Vector2(u, v);
    }

    // One textured quad filling the window's glass at depth z, showing the picture from uvMin (top left) to uvMax.
    private static void DrawGlass(Texture2D texture, float z, Vector2 uvMin, Vector2 uvMax, Color tint)
    {
        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(tint.R, tint.G, tint.B, tint.A);
            Rlgl.TexCoord2f(uvMin.X, uvMin.Y); Rlgl.Vertex3f(WindowLeft, WindowTop, z);
            Rlgl.TexCoord2f(uvMin.X, uvMax.Y); Rlgl.Vertex3f(WindowLeft, WindowBottom, z);
            Rlgl.TexCoord2f(uvMax.X, uvMax.Y); Rlgl.Vertex3f(WindowRight, WindowBottom, z);
            Rlgl.TexCoord2f(uvMax.X, uvMin.Y); Rlgl.Vertex3f(WindowRight, WindowTop, z);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    // The night side of a planet with a thin lit edge and a few city lights, on a clear picture the size of the glass.
    // Painted once at startup. A fixed random seed keeps the city lights in the same places every time.
    private static Texture2D PaintPlanet()
    {
        int width = (int)((WindowRight - WindowLeft) * PlanetPixelsPerUnit);
        int height = (int)((WindowTop - WindowBottom) * PlanetPixelsPerUnit);
        Image image = Raylib.GenImageColor(width, height, Color.Blank);
        Random random = new Random(7);

        // A spot on the back wall (room units) as a pixel in the picture
        static Vector2 Pixel(float x, float y) => new Vector2((x - WindowLeft) * PlanetPixelsPerUnit, (WindowTop - y) * PlanetPixelsPerUnit);

        // An atmosphere ring, the lit body, then a big dark circle offset to the lower right for the night side
        Vector2 middle = Pixel(7f, -4f);
        Vector2 night = middle + new Vector2(50, 50);
        Raylib.ImageDrawCircle(ref image, (int)middle.X, (int)middle.Y, 432, new Color(80, 170, 230, 255));
        Raylib.ImageDrawCircle(ref image, (int)middle.X, (int)middle.Y, 424, new Color(38, 96, 146, 255));
        Raylib.ImageDrawCircle(ref image, (int)night.X, (int)night.Y, 400, new Color(12, 34, 58, 255));

        // City lights, only on the night side
        Vector2 lightsFrom = Pixel(3f, 3.5f), lightsTo = Pixel(11.5f, WindowBottom);
        for (int i = 0; i < 60; i++)
        {
            int x = random.Next((int)lightsFrom.X, (int)lightsTo.X);
            int y = random.Next((int)lightsFrom.Y, (int)lightsTo.Y);
            if (Vector2.Distance(new Vector2(x, y), night) > 390) continue;
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
