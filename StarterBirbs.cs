using System.Numerics;
using Raylib_cs;

// The birbs built into the box, written by hand. Designer saves go to JSON files instead (see BirbFile.cs).
// Units, joints and directions are explained at the top of BirbShape.cs.
//
// Every part grows from its joint along +Y, so the rotations below aim each part:
//   X = 90 points it forward (beaks), X = 180 hangs it down (legs),
//   X near -100 sends it back and a little down (wings and tails).
// To try a Blender part, change a mesh name (e.g. PartLibrary.Cone to "beak_hooked").
// Roles say how a part animates; a Parent makes it follow another part (see BirbShape.cs).
public static class StarterBirbs
{
    private const string Sphere = PartLibrary.Sphere;
    private const string Cone = PartLibrary.Cone;
    private const string Cube = PartLibrary.Cube;

    private static readonly Color Black = Rgb(25, 22, 24);
    private static readonly Color LegBrown = Rgb(95, 75, 65);

    // How each family moves (see MotionStyle.cs). Declared before the birbs, which use them.
    private static readonly MotionStyle CardinalStyle = new MotionStyle();
    private static readonly MotionStyle DoveStyle = new MotionStyle
    {
        HeadBob = 0.18f, StepSwing = 22f, FlapSpeed = 14f, FlapAngle = 50f, LookAngle = 35f, LookStep = 1.1f,
    };
    private static readonly MotionStyle FinchStyle = new MotionStyle
    {
        HopHeight = 0.1f, FlapSpeed = 28f, FlapAngle = 40f, LookStep = 0.45f, TailFlick = 45f,
    };

    public static readonly BirbShape EmberwingCock = Cardinal("Emberwing Cock",
        body: Rgb(210, 40, 40), wing: Rgb(160, 30, 35), crest: Rgb(225, 50, 45), mask: Black, beak: Rgb(240, 140, 60));

    public static readonly BirbShape EmberwingHen = Cardinal("Emberwing Hen",
        body: Rgb(175, 140, 105), wing: Rgb(165, 85, 65), crest: Rgb(190, 95, 70), mask: Rgb(90, 80, 75), beak: Rgb(240, 140, 60));

    public static readonly BirbShape Frostcrest = Cardinal("Frostcrest",
        body: Rgb(80, 130, 240), wing: Rgb(50, 85, 180), crest: Rgb(185, 225, 255), mask: Rgb(20, 30, 60), beak: Rgb(215, 230, 240));

    public static readonly BirbShape DuskDove = MakeDuskDove();
    public static readonly BirbShape Gildfinch = MakeGildfinch();

    // Emberwings and Frostcrests share one build: a pointed crest, a dark face mask and a thick beak.
    private static BirbShape Cardinal(string name, Color body, Color wing, Color crest, Color mask, Color beak)
    {
        BirbShape s = new BirbShape(name)
            .Add(new BirbPart("Body", Sphere, V(0, -0.72f, 0), V(0.75f, 0.72f, 1f), V(0, 0, 0), body) { Role = PartRole.Body })
            .Add(new BirbPart("Head", Sphere, V(0, 0.22f, 0.7f), V(0.5f, 0.5f, 0.5f), V(0, 0, 0), body) { Role = PartRole.Head, Parent = "Body" })
            .Add(new BirbPart("Crest", Cone, V(0, 0.99f, 0.62f), V(0.2f, 0.32f, 0.2f), V(-25, 0, 0), crest) { Parent = "Head" })
            .Add(new BirbPart("Mask", Sphere, V(0, 0.38f, 1.02f), V(0.3f, 0.24f, 0.2f), V(0, 0, 0), mask) { Parent = "Head" })
            .Add(new BirbPart("Beak", Cone, V(0, 0.66f, 1.08f), V(0.16f, 0.22f, 0.16f), V(90, 0, 0), beak) { Role = PartRole.Beak, Parent = "Head" })
            .AddPair(new BirbPart("Left Eye", Sphere, V(0.3f, 0.75f, 1.05f), V(0.07f, 0.07f, 0.07f), V(0, 0, 0), Black) { Role = PartRole.Eye, Parent = "Head" }, "Right Eye")
            .AddPair(new BirbPart("Left Wing", Sphere, V(0.62f, 0.25f, 0.63f), V(0.16f, 0.8f, 0.48f), V(-102, 0, 8), wing) { Role = PartRole.Wing, Parent = "Body" }, "Right Wing")
            .Add(new BirbPart("Tail", Cube, V(0, 0.29f, -0.73f), V(0.26f, 0.55f, 0.06f), V(-108, 0, 0), wing) { Role = PartRole.Tail, Parent = "Body" });
        AddLegs(s, x: 0.25f, hipY: -0.64f, length: 0.5f, LegBrown);
        s.Gait = Gait.Hop;
        s.Style = CardinalStyle.Copy();
        return s;
    }

    private static BirbShape MakeDuskDove()
    {
        Color body = Rgb(200, 195, 185);
        Color wing = Rgb(165, 160, 152);

        BirbShape s = new BirbShape("Dusk Dove")
            .Add(new BirbPart("Body", Sphere, V(0, -0.75f, 0), V(0.85f, 0.75f, 1.1f), V(0, 0, 0), body) { Role = PartRole.Body })
            .Add(new BirbPart("Neck Sheen", Sphere, V(0, 0.2f, 0.85f), V(0.42f, 0.25f, 0.3f), V(0, 0, 0), Rgb(150, 140, 170)) { Parent = "Body" })
            .Add(new BirbPart("Head", Sphere, V(0, 0.28f, 0.85f), V(0.4f, 0.4f, 0.4f), V(0, 0, 0), Rgb(180, 175, 170)) { Role = PartRole.Head, Parent = "Body" })
            .Add(new BirbPart("Beak", Cone, V(0, 0.62f, 1.17f), V(0.07f, 0.13f, 0.07f), V(90, 0, 0), Rgb(70, 60, 60)) { Role = PartRole.Beak, Parent = "Head" })
            .AddPair(new BirbPart("Left Eye", Sphere, V(0.25f, 0.7f, 1.12f), V(0.055f, 0.055f, 0.055f), V(0, 0, 0), Black) { Role = PartRole.Eye, Parent = "Head" }, "Right Eye")
            .AddPair(new BirbPart("Left Wing", Sphere, V(0.68f, 0.25f, 0.64f), V(0.16f, 0.85f, 0.5f), V(-100, 0, 8), wing) { Role = PartRole.Wing, Parent = "Body" }, "Right Wing")
            .Add(new BirbPart("Tail", Cube, V(0, 0.27f, -0.76f), V(0.22f, 0.7f, 0.05f), V(-100, 0, 0), Rgb(150, 145, 140)) { Role = PartRole.Tail, Parent = "Body" });
        AddLegs(s, x: 0.25f, hipY: -0.66f, length: 0.4f, Rgb(200, 110, 120));
        s.Gait = Gait.Walk;
        s.Style = DoveStyle.Copy();
        return s;
    }

    private static BirbShape MakeGildfinch()
    {
        Color gold = Rgb(240, 185, 40);
        Color wing = Rgb(35, 35, 40);

        BirbShape s = new BirbShape("Gildfinch")
            .Add(new BirbPart("Body", Sphere, V(0, -0.72f, 0), V(0.75f, 0.72f, 0.95f), V(0, 0, 0), gold) { Role = PartRole.Body })
            .Add(new BirbPart("Head", Sphere, V(0, 0.22f, 0.75f), V(0.48f, 0.48f, 0.48f), V(0, 0, 0), gold) { Role = PartRole.Head, Parent = "Body" })
            .Add(new BirbPart("Cap", Sphere, V(0, 0.8f, 0.82f), V(0.4f, 0.18f, 0.38f), V(0, 0, 0), Black) { Parent = "Head" })
            .Add(new BirbPart("Beak", Cone, V(0, 0.64f, 1.1f), V(0.15f, 0.18f, 0.15f), V(90, 0, 0), Rgb(235, 170, 120)) { Role = PartRole.Beak, Parent = "Head" })
            .AddPair(new BirbPart("Left Eye", Sphere, V(0.28f, 0.74f, 1.08f), V(0.065f, 0.065f, 0.065f), V(0, 0, 0), Black) { Role = PartRole.Eye, Parent = "Head" }, "Right Eye")
            .AddPair(new BirbPart("Left Wing", Sphere, V(0.6f, 0.26f, 0.58f), V(0.15f, 0.75f, 0.45f), V(-102, 0, 8), wing) { Role = PartRole.Wing, Parent = "Body" }, "Right Wing")
            .AddPair(new BirbPart("Left Wing Bar", Cube, V(0.72f, 0.08f, 0.29f), V(0.05f, 0.4f, 0.08f), V(-102, 0, 8), Rgb(245, 245, 240)) { Parent = "Left Wing" }, "Right Wing Bar", rightParent: "Right Wing")
            .Add(new BirbPart("Tail", Cube, V(0, 0.25f, -0.72f), V(0.24f, 0.5f, 0.05f), V(-105, 0, 0), wing) { Role = PartRole.Tail, Parent = "Body" });
        AddLegs(s, x: 0.24f, hipY: -0.64f, length: 0.48f, Rgb(120, 90, 80));
        s.Gait = Gait.Hop;
        s.Style = FinchStyle.Copy();
        return s;
    }

    // Two thin legs hanging from the hip, each with a flat foot pointing forward from the ankle.
    private static void AddLegs(BirbShape s, float x, float hipY, float length, Color color)
    {
        float ankleY = hipY - length;
        s.AddPair(new BirbPart("Left Leg", Cube, V(x, hipY, 0.1f), V(0.04f, length / 2f, 0.04f), V(180, 0, 0), color) { Role = PartRole.Leg }, "Right Leg");
        s.AddPair(new BirbPart("Left Foot", Cube, V(x, ankleY, 0.08f), V(0.05f, 0.08f, 0.02f), V(90, 0, 0), color) { Parent = "Left Leg" }, "Right Foot", rightParent: "Right Leg");
    }

    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    private static Color Rgb(int r, int g, int b) => new Color(r, g, b, 255);
}
