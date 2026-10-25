using System.Numerics;
using Raylib_cs;

// The cathedral radio on the table just right of the pedestal, level with its middle (so it stays in view
// when leaning in): a walnut cabinet with a pointed-arch speaker grille,
// a glowing tuning window, and a row of brass controls along the bottom:
//   [volume knob]  [power button]  [next button]  [tuning knob]
// Knobs turn with a drag (sideways or up/down) or the mouse wheel; buttons press on click.
// Hovering shows a small tag with what's playing. The sound itself is in RadioPlayer.
//
// It's part of the room (drawn in Backdrop's pass, lit by room.fs), so it never picks up the hologram's glow.
// Positions inside the radio are "local": X across the front, Y up from the tabletop, Z out toward the viewer.
// Scale: 1 unit is about 7.5 cm, so the radio is about 25 cm wide and 26 cm tall.
public class Radio
{
    public RadioPlayer Player { get; } = new RadioPlayer();

    // WHERE IT SITS: right of the pedestal, turned to face the camera
    public static readonly Vector3 Spot = new Vector3(10.4f, Backdrop.TableY, -0.3f);
    private const float YawDegrees = -22f;

    // SHAPE (local units)
    private const float Width = 3.4f, Depth = 1.5f;
    private const float PlinthHeight = 0.12f;
    private const float BoxTop = 1.62f;                    // top of the square part; the arch's middle
    private const float ArchRadius = Width / 2f;
    private const float Front = Depth / 2f;                // the cabinet's front face
    private const float PlateTop = 1.32f;                  // the lower front panel that holds the dial and controls
    private const float PlateFront = Front + 0.08f;
    private const float ClothRadius = 1.45f;
    private const float DialY = 0.95f, DialHalfWidth = 1.1f, DialHalfHeight = 0.18f;
    private const float DialFront = PlateFront + 0.04f;
    private const float EyeY = 1.22f;
    private const float ControlY = 0.45f;
    private const float KnobX = 1.18f, KnobRadius = 0.23f, KnobLength = 0.16f;
    private const float ButtonX = 0.4f, ButtonHalfWidth = 0.25f, ButtonHalfHeight = 0.16f, ButtonDepth = 0.16f;
    private const float ButtonTravel = 0.08f;               // how far a button sinks when pressed
    private const float TopY = BoxTop + ArchRadius + 0.2f;  // top of the finial

    // FEEL
    private const float VolumeDragRate = 0.004f;    // volume per pixel dragged
    private const float VolumeWheelStep = 0.05f;
    private const float VolumeNotch = 0.05f;        // a click every 5%
    private const float TuneDragRate = 0.0012f;     // band per pixel dragged; fine, so you can hunt for a station
    private const float TuneWheelStep = 0.01f;
    private const float TuneSettleRate = 4f;        // let go near a station and the needle eases onto it
    private const float LabelLinger = 1.5f;         // seconds the tag stays after the mouse leaves or a setting changes

    // COLORS
    private static readonly Color Walnut = new Color(96, 58, 36, 255);
    private static readonly Color WalnutDark = new Color(58, 36, 24, 255);
    private static readonly Color Cloth = new Color(70, 56, 46, 255);
    private static readonly Color Brass = new Color(190, 150, 80, 255);
    private static readonly Color BrassDark = new Color(120, 90, 50, 255);
    private static readonly Color DialOff = new Color(38, 32, 26, 255);
    private static readonly Color DialLit = new Color(255, 196, 120, 255);
    private static readonly Color Ink = new Color(70, 40, 20, 255);
    private static readonly Color Needle = new Color(160, 30, 24, 255);
    private static readonly Color EyeLit = new Color(120, 255, 160, 255);

    // TURNING local positions into the room
    private static readonly Matrix4x4 TurnMatrix = Matrix4x4.CreateRotationY(YawDegrees * Raylib.DEG2RAD);
    private static readonly Matrix4x4 UnturnMatrix = Matrix4x4.CreateRotationY(-YawDegrees * Raylib.DEG2RAD);
    private static readonly Vector3 Right = Vector3.Transform(Vector3.UnitX, TurnMatrix);
    private static readonly Vector3 Forward = Vector3.Transform(Vector3.UnitZ, TurnMatrix);

    private enum Part { None, Body, Volume, Power, Next, Tuning }
    private Part hovered = Part.None;
    private Part held = Part.None;          // a knob being dragged, or a button held down
    private float powerPush, nextPush;      // 0 = up, 1 = pushed all the way in
    private bool tuningSettles;             // the needle is easing onto the nearest station
    private float labelTimer;
    private float grabAngle;                // a held knob's angle when it was grabbed, so the hand turns with it

    // True while the mouse is over the radio or holding one of its controls, so it doesn't reach the hologram.
    public bool UsingMouse => hovered != Part.None || held != Part.None;

    public void Load() => Player.Load();
    public void Unload() => Player.Unload();

    // ---------------- UPDATE (no drawing) ----------------

    // Every frame, even while the Esc menu is open. ducked: the menu is open, so play quieter and let go of the mouse.
    public void Update(float dt, bool ducked)
    {
        if (ducked)
        {
            hovered = Part.None;
            held = Part.None;
        }

        // Buttons stay down while held, and spring back up once let go
        powerPush = held == Part.Power ? 1f : MathF.Max(0f, powerPush - dt * 8f);
        nextPush = held == Part.Next ? 1f : MathF.Max(0f, nextPush - dt * 8f);

        if (tuningSettles && held != Part.Tuning) SettleTuning(dt);

        if (UsingMouse) labelTimer = LabelLinger;
        else labelTimer = MathF.Max(0f, labelTimer - dt);

        Player.Update(dt, ducked);
    }

    // Mouse input. ray: from the camera through the mouse, in room space. blocked: a panel or ImGui has the mouse.
    public void HandleInput(Ray ray, bool blocked)
    {
        if (held != Part.None && !Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            if (held == Part.Tuning) tuningSettles = true;
            held = Part.None;
        }

        hovered = blocked && held == Part.None ? Part.None : PartUnderMouse(ray);
        if (hovered == Part.None && held == Part.None) return;

        if (held == Part.None && Raylib.IsMouseButtonPressed(MouseButton.Left)) Press(hovered);

        // Dragging a knob: right or up turns it up
        if (held == Part.Volume || held == Part.Tuning)
        {
            Vector2 delta = Raylib.GetMouseDelta();
            float turn = delta.X - delta.Y;
            if (held == Part.Volume) SetVolume(Player.Volume + turn * VolumeDragRate);
            else Player.Tuning = Math.Clamp(Player.Tuning + turn * TuneDragRate, 0f, 1f);
        }

        // The wheel turns whichever knob it's over
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f && held == Part.None)
        {
            if (hovered == Part.Volume) SetVolume(Player.Volume + wheel * VolumeWheelStep);
            if (hovered == Part.Tuning)
            {
                Player.Tuning = Math.Clamp(Player.Tuning + wheel * TuneWheelStep, 0f, 1f);
                tuningSettles = true;
            }
        }
    }

    private void Press(Part part)
    {
        held = part;
        tuningSettles = false;
        if (part == Part.Volume || part == Part.Tuning) grabAngle = KnobAngle(part);
        switch (part)
        {
            case Part.Power:
                Sounds.PlayRadioButton();
                Player.SetOn(!Player.On);
                break;
            case Part.Next:
                Sounds.PlayRadioButton();
                Player.NextTrack();
                break;
            case Part.Body:
                held = Part.None;    // the cabinet itself does nothing; it just keeps the click off the hologram
                break;
        }
    }

    // Clicks once for every notch the knob passes.
    private void SetVolume(float value)
    {
        float before = Player.Volume;
        Player.Volume = Math.Clamp(value, 0f, 1f);
        int notchBefore = (int)MathF.Floor(before / VolumeNotch + 0.001f);
        int notchAfter = (int)MathF.Floor(Player.Volume / VolumeNotch + 0.001f);
        if (notchBefore != notchAfter) Sounds.PlayDialClick();
    }

    // Eases the needle onto the station it's within reach of. Stops once it's there or there's none.
    private void SettleTuning(float dt)
    {
        RadioStation? station = Player.NearestStation(out _);
        if (station == null)
        {
            tuningSettles = false;
            return;
        }
        float target = station.Position;
        Player.Tuning += (target - Player.Tuning) * MathF.Min(1f, TuneSettleRate * dt);
        if (MathF.Abs(target - Player.Tuning) < 0.0005f)
        {
            Player.Tuning = target;
            tuningSettles = false;
        }
    }

    // Which part the mouse ray hits, tested in the radio's own unturned space.
    private Part PartUnderMouse(Ray ray)
    {
        Ray local = new Ray(Vector3.Transform(ray.Position - Spot, UnturnMatrix), Vector3.Transform(ray.Direction, UnturnMatrix));

        Vector3 bodyMin = new Vector3(-Width / 2f - 0.1f, 0f, -Depth / 2f - 0.1f);
        Vector3 bodyMax = new Vector3(Width / 2f + 0.1f, TopY, PlateFront + 0.4f);
        if (!Hits(local, bodyMin, bodyMax)) return Part.None;

        if (HitsControl(local, -KnobX, KnobRadius + 0.05f, KnobRadius + 0.05f)) return Part.Volume;
        if (HitsControl(local, KnobX, KnobRadius + 0.05f, KnobRadius + 0.05f)) return Part.Tuning;
        if (HitsControl(local, -ButtonX, ButtonHalfWidth + 0.03f, ButtonHalfHeight + 0.03f)) return Part.Power;
        if (HitsControl(local, ButtonX, ButtonHalfWidth + 0.03f, ButtonHalfHeight + 0.03f)) return Part.Next;
        return Part.Body;
    }

    // A control's box: centered on x along the control row, from the front panel out a little past the control.
    private static bool HitsControl(Ray local, float x, float halfWidth, float halfHeight)
    {
        Vector3 min = new Vector3(x - halfWidth, ControlY - halfHeight, Front);
        Vector3 max = new Vector3(x + halfWidth, ControlY + halfHeight, PlateFront + 0.3f);
        return Hits(local, min, max);
    }

    private static bool Hits(Ray ray, Vector3 min, Vector3 max) => Raylib.GetRayCollisionBox(ray, new BoundingBox(min, max)).Hit;

    // Where a hand should go for the control the mouse is on or holding, in room space (see Hands).
    // point: a button's face, or a knob's middle. facing: out of the radio's front. holding: pressed or gripped.
    // twist: how far a held knob has turned since it was grabbed. False when it's no control (or just the cabinet).
    public bool HandTarget(out Vector3 point, out Vector3 facing, out bool isKnob, out bool holding, out float twist)
    {
        Part part = held != Part.None ? held : hovered;
        facing = Forward;
        holding = held == part && held != Part.None;
        isKnob = part == Part.Volume || part == Part.Tuning;
        twist = isKnob && holding ? KnobAngle(part) - grabAngle : 0f;

        switch (part)
        {
            case Part.Volume: point = ToScene(-KnobX, ControlY, PlateFront + KnobLength * 0.5f); return true;
            case Part.Tuning: point = ToScene(KnobX, ControlY, PlateFront + KnobLength * 0.5f); return true;
            case Part.Power: point = ToScene(-ButtonX, ControlY, PlateFront + ButtonDepth - powerPush * ButtonTravel); return true;
            case Part.Next: point = ToScene(ButtonX, ControlY, PlateFront + ButtonDepth - nextPush * ButtonTravel); return true;
            default: point = Vector3.Zero; return false;
        }
    }

    // How far a knob is turned, in radians, the same way its pointer dot is drawn.
    private float KnobAngle(Part part) => part == Part.Volume ? VolumeAngle() : Player.Tuning * MathF.PI * 6f;

    // ---------------- DRAW (no changes) ----------------

    // The solid parts, lit by the room. Called from Backdrop.Draw with the room's shapes.
    public void Draw(UnitShapes shapes)
    {
        DrawCabinet(shapes);
        DrawGrille(shapes);
        DrawDial(shapes);
        DrawKnob(shapes, -KnobX, VolumeAngle(), hovered == Part.Volume || held == Part.Volume);
        DrawKnob(shapes, KnobX, KnobAngle(Part.Tuning), hovered == Part.Tuning || held == Part.Tuning);
        DrawButton(shapes, -ButtonX, powerPush, hovered == Part.Power, isPower: true);
        DrawButton(shapes, ButtonX, nextPush, hovered == Part.Next, isPower: false);
    }

    // The soft light around the tuning window while it's on. Called from Backdrop's see-through, additive pass.
    public void DrawGlow()
    {
        if (Player.Warmth <= 0f) return;
        Color glow = Raylib.Fade(DialLit, 0.18f * Player.Warmth);
        FaceQuad(0f, DialY, DialFront + 0.01f, DialHalfWidth + 0.25f, DialHalfHeight + 0.2f, glow);
        FaceQuad(0f, DialY, DialFront + 0.01f, DialHalfWidth + 0.1f, DialHalfHeight + 0.08f, glow);
    }

    // Walnut base, square body with an arched top, the lower front panel, and a brass finial.
    private void DrawCabinet(UnitShapes shapes)
    {
        Box(shapes, new Vector3(0f, PlinthHeight / 2f, 0f), new Vector3(Width + 0.2f, PlinthHeight, Depth + 0.2f), WalnutDark);
        Box(shapes, new Vector3(0f, (PlinthHeight + BoxTop) / 2f, 0f), new Vector3(Width, BoxTop - PlinthHeight, Depth), Walnut);
        shapes.CylinderAlong(ToScene(0f, BoxTop, -Depth / 2f), Forward, ArchRadius, Depth, Walnut);

        Box(shapes, new Vector3(0f, (PlinthHeight + PlateTop) / 2f, Front + 0.04f), new Vector3(Width, PlateTop - PlinthHeight, 0.08f), WalnutDark);
        Box(shapes, new Vector3(0f, PlateTop + 0.01f, Front + 0.05f), new Vector3(Width, 0.05f, 0.1f), Brass);
        shapes.Ball(ToScene(0f, TopY - 0.14f, 0f), new Vector3(0.14f, 0.14f, 0.14f), Brass);
    }

    // The speaker: cloth filling the arch, behind upright brass bars that follow its curve, like a chapel window.
    private void DrawGrille(UnitShapes shapes)
    {
        shapes.CylinderAlong(ToScene(0f, BoxTop, Front - 0.01f), Forward, ClothRadius, 0.02f, Cloth);
        for (float x = -1.2f; x <= 1.21f; x += 0.3f)
        {
            float top = BoxTop + MathF.Sqrt(ClothRadius * ClothRadius - x * x) - 0.05f;
            Box(shapes, new Vector3(x, (top + PlateTop) / 2f, Front + 0.03f), new Vector3(0.07f, top - PlateTop, 0.04f), Brass);
        }
    }

    // The tuning window: brass frame, glass that glows amber when on, a mark for every station, and the red needle.
    // Above it, the little green "magic eye" that brightens the clearer the station comes in.
    private void DrawDial(UnitShapes shapes)
    {
        Box(shapes, new Vector3(0f, DialY, PlateFront + 0.02f), new Vector3(DialHalfWidth * 2f + 0.16f, DialHalfHeight * 2f + 0.16f, 0.04f), Brass);

        float warmth = Player.Warmth;
        FaceQuad(0f, DialY, DialFront + 0.001f, DialHalfWidth, DialHalfHeight, Palette.Mix(DialOff, DialLit, warmth));

        // Scale along the top, one mark per MHz from 88 to 108, longer at 90, 95, 100 and 105.
        // Under it, a tall mark for each of your folder stations and a short one for each web station.
        Color ink = Palette.Mix(DialOff, Ink, warmth);
        for (int i = 0; i <= 20; i++)
        {
            bool major = (88 + i) % 5 == 0;
            FaceQuad(BandToX(i / 20f), DialY + DialHalfHeight - 0.05f, DialFront + 0.002f, 0.006f, major ? 0.04f : 0.02f, ink);
        }
        foreach (RadioStation station in Player.Stations)
        {
            float halfHeight = station.IsWeb ? 0.04f : 0.07f;
            FaceQuad(BandToX(station.Position), DialY - 0.11f + halfHeight, DialFront + 0.002f, 0.03f, halfHeight, ink);
        }

        FaceQuad(BandToX(Player.Tuning), DialY, DialFront + 0.003f, 0.012f, DialHalfHeight - 0.02f, Needle);

        Color eye = Palette.Mix(new Color(20, 40, 28, 255), EyeLit, warmth * (0.2f + 0.8f * Player.Signal));
        Raylib.DrawSphereEx(ToScene(0f, EyeY, PlateFront - 0.02f), 0.07f, 8, 8, eye);
    }

    // A brass knob with a darker skirt, and a dark dot showing which way it's turned (0 = pointing up).
    private void DrawKnob(UnitShapes shapes, float x, float angle, bool lit)
    {
        Color brass = lit ? Palette.Lighten(Brass, 0.25f) : Brass;
        shapes.CylinderAlong(ToScene(x, ControlY, PlateFront), Forward, KnobRadius + 0.05f, 0.05f, BrassDark);
        shapes.CylinderAlong(ToScene(x, ControlY, PlateFront), Forward, KnobRadius, KnobLength, brass);

        float faceZ = PlateFront + KnobLength;
        Vector3 dot = ToScene(x + MathF.Sin(angle) * KnobRadius * 0.65f, ControlY + MathF.Cos(angle) * KnobRadius * 0.65f, faceZ);
        shapes.Ball(dot, new Vector3(0.035f, 0.035f, 0.035f), Ink);
    }

    // A rectangular push button. push: 0 up, 1 in. The power symbol is a bar that glows when on;
    // the next symbol is a small arrow with a stop bar.
    private void DrawButton(UnitShapes shapes, float x, float push, bool lit, bool isPower)
    {
        float sink = push * ButtonTravel;
        float faceZ = PlateFront + ButtonDepth - sink;
        Color brass = lit ? Palette.Lighten(Brass, 0.25f) : Brass;
        Box(shapes, new Vector3(x, ControlY, PlateFront + (ButtonDepth - sink) / 2f), new Vector3(ButtonHalfWidth * 2f, ButtonHalfHeight * 2f, ButtonDepth - sink), brass);

        float z = faceZ + 0.002f;
        if (isPower)
        {
            FaceQuad(x, ControlY, z, 0.02f, 0.07f, Palette.Mix(Ink, DialLit, Player.Warmth));
        }
        else
        {
            FaceQuad(x - 0.01f, ControlY + 0.025f, z, 0.045f, 0.012f, Ink, -35f);
            FaceQuad(x - 0.01f, ControlY - 0.025f, z, 0.045f, 0.012f, Ink, 35f);
            FaceQuad(x + 0.05f, ControlY, z, 0.012f, 0.055f, Ink);
        }
    }

    // The volume knob's pointer runs from down-left (silent) to down-right (full), like most volume knobs.
    private float VolumeAngle() => (-135f + 270f * Player.Volume) * Raylib.DEG2RAD;

    // Where a point on the band (0..1) sits across the dial window.
    private static float BandToX(float band) => (band - 0.5f) * 2f * (DialHalfWidth - 0.08f);

    // ---------------- HOVER TAG (2D, drawn over everything) ----------------

    // A small panel above the radio: which control the mouse is on, and what's playing.
    public void DrawLabel(Camera3D camera, Color accent)
    {
        if (labelTimer <= 0f) return;

        const int Size = 12, Pad = 8, LineHeight = 16;
        (string station, string detail) = Player.Describe();
        string[] lines = new[] { ControlLine(), station, Shorten(detail) }.Where(line => line.Length > 0).ToArray();
        int width = lines.Max(line => UiText.Measure(line, Size)) + Pad * 2;
        int height = lines.Length * LineHeight + Pad * 2 - 4;

        Vector2 top = Raylib.GetWorldToScreen(ToScene(0f, TopY + 0.3f, 0f), camera);
        float x = Math.Clamp(top.X - width / 2f, 4f, Raylib.GetScreenWidth() - width - 4f);
        float y = Math.Clamp(top.Y - height, 4f, Raylib.GetScreenHeight() - height - 4f);
        UiDraw.Panel(new Rectangle(x, y, width, height), accent);

        for (int i = 0; i < lines.Length; i++)
        {
            UiText.Draw(lines[i], (int)x + Pad, (int)y + Pad + i * LineHeight, Size, i == 0 ? accent : Palette.Text);
        }
    }

    private string ControlLine()
    {
        Part part = held != Part.None ? held : hovered;
        return part switch
        {
            Part.Volume => $"VOLUME  {(int)MathF.Round(Player.Volume * 100)}%",
            Part.Tuning => $"TUNING  {Player.TunedFrequency:0.0} FM",
            Part.Power => Player.On ? "POWER  on" : "POWER  off",
            Part.Next => Player.CanSkip || !Player.On ? "NEXT SONG" : "NEXT SONG  (live, can't skip)",
            _ => "RADIO",
        };
    }

    // Long song names are cut short so the tag stays small.
    private static string Shorten(string text)
    {
        const int MaxChars = 44;
        return text.Length > MaxChars ? text[..(MaxChars - 3)] + "..." : text;
    }

    // ---------------- SHAPE HELPERS ----------------

    private static Vector3 ToScene(float x, float y, float z) => Spot + Vector3.Transform(new Vector3(x, y, z), TurnMatrix);

    // A box given in local space, turned with the radio.
    private static void Box(UnitShapes shapes, Vector3 localCenter, Vector3 size, Color color)
    {
        shapes.Box(ToScene(localCenter.X, localCenter.Y, localCenter.Z), size, color, Vector3.UnitY, YawDegrees);
    }

    // An unlit flat rectangle facing out of the front at depth z, turned in its own plane by angleDegrees.
    // Unlit so the dial and its marks read clearly whatever the room light.
    private static void FaceQuad(float x, float y, float z, float halfWidth, float halfHeight, Color color, float angleDegrees = 0f)
    {
        Vector3 center = ToScene(x, y, z);
        float a = angleDegrees * Raylib.DEG2RAD;
        Vector3 across = (Right * MathF.Cos(a) + Vector3.UnitY * MathF.Sin(a)) * halfWidth;
        Vector3 up = (-Right * MathF.Sin(a) + Vector3.UnitY * MathF.Cos(a)) * halfHeight;

        Vector3 topLeft = center - across + up, bottomLeft = center - across - up;
        Vector3 bottomRight = center + across - up, topRight = center + across + up;
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(color.R, color.G, color.B, color.A);
            Rlgl.Vertex3f(topLeft.X, topLeft.Y, topLeft.Z);
            Rlgl.Vertex3f(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
            Rlgl.Vertex3f(bottomRight.X, bottomRight.Y, bottomRight.Z);
            Rlgl.Vertex3f(topRight.X, topRight.Y, topRight.Z);
        Rlgl.End();
    }
}
