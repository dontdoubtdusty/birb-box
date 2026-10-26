using System.Numerics;
using Raylib_cs;

// Draws the Gilded Granary inside the Birb Cube, when the box is showing the store (see BoxMode).
// Everything uses the hologram shader, so it gets the same tint, scan bands and edge fog as the birds.
// Nothing in here changes the store.
//
// Positions and sizes below are in store units; the helpers at the bottom scale them up by SeedStore.Scale.
// The room is round (see SeedStore for the layout): a horseshoe wall open at the front, curved shelves
// at the back, and the counter in the middle with the shopkeeper behind it.
//
// The shop is built from plain boxes, cylinders and spheres for now; models can replace them later.
// The builders printed wood onto everything, but the ship's steel and bolts still show through.
public class StoreRenderer
{
    // COLORS
    private static readonly Color WoodLight = new Color(138, 94, 58, 255);
    private static readonly Color WoodDark = new Color(104, 68, 40, 255);
    private static readonly Color WoodTrim = new Color(78, 50, 30, 255);
    private static readonly Color Steel = new Color(84, 90, 98, 255);
    private static readonly Color SteelLight = new Color(150, 156, 166, 255);
    private static readonly Color Burlap = new Color(176, 150, 108, 255);
    private static readonly Color OpenSpace = new Color(8, 12, 28, 255);
    private static readonly Color Star = new Color(240, 240, 255, 255);

    // ROOM
    private const float WallHeight = 2.3f;
    private const float SeamY = 1.75f;
    private const float BoardWidth = 0.28f;                        // wall boards, along the curve
    private const float WindowAngle = 280f * MathF.PI / 180f;      // on the left side, past the end of the shelves

    // Where the stars sit in the window, as -1..1 across and up the glass
    private static readonly Vector2[] StarSpots =
    {
        new(-0.7f, 0.6f), new(-0.2f, 0.8f), new(0.5f, 0.5f), new(0.8f, -0.3f), new(0.1f, -0.6f),
        new(-0.6f, -0.2f), new(0.3f, 0.1f), new(-0.4f, 0.3f), new(0.7f, 0.85f), new(-0.85f, -0.75f),
    };

    private readonly UnitShapes shapes = new UnitShapes();
    private Shader shader;
    private int locBoxYaw;

    // Set at the start of each Draw so the shape helpers below can use them
    private SeedStore store = null!;
    private float yawDegrees;
    private float roomRadius;    // middle to the cube's corners, in store units; the floor reaches this far, into the fog

    public void Load(Shader holoShader)
    {
        shader = holoShader;
        locBoxYaw = Raylib.GetShaderLocation(shader, "boxYaw");
        shapes.Load(shader);
    }

    public void Unload()
    {
        shapes.Unload();
    }

    // The store has its own turn, so this changes the shader's boxYaw. The caller puts the birds' back afterward.
    public void Draw(SeedStore store, ViewWindow view)
    {
        this.store = store;
        yawDegrees = store.Yaw * Raylib.RAD2DEG;
        roomRadius = view.CornerReach / SeedStore.Scale;    // out to the cube's corners
        Raylib.SetShaderValue(shader, locBoxYaw, store.Yaw, ShaderUniformDataType.Float);

        DrawFloor();
        DrawWall();
        DrawWindow();
        DrawShelves();
        DrawStock();
        DrawCounter();
        DrawShopkeeper();
        DrawOddsAndEnds();
        DrawAirRecycler();
    }

    // ---------------- THE ROOM ----------------

    // Steel deck plating with wooden planks printed over it. Steel shows in the seams,
    // and every other seam has a row of bolts.
    private void DrawFloor()
    {
        float span = roomRadius * 2f;
        Box(new Vector3(0, 0.02f, 0), new Vector3(span, 0.04f, span), Steel);

        const float PlankWidth = 0.3f;
        int plankCount = (int)(span / PlankWidth);
        for (int i = 0; i < plankCount; i++)
        {
            float z = -roomRadius + PlankWidth * (i + 0.5f);
            Box(new Vector3(0, 0.045f, z), new Vector3(span, 0.012f, PlankWidth - 0.025f), i % 2 == 0 ? WoodLight : WoodDark);

            if (i % 2 == 1)
            {
                for (float x = -roomRadius + 0.25f; x < roomRadius; x += 0.5f)
                {
                    Bolt(new Vector3(x, 0.05f, z + PlankWidth / 2f));
                }
            }
        }
    }

    // The horseshoe wall, built from short straight pieces around the curve: steel outside, a wood board
    // on the inside of each, and a bolted steel seam running all the way around above the shelves.
    // A wooden post finishes each end of the wall at the opening.
    private void DrawWall()
    {
        float start = SeedStore.WallOpening, end = MathF.PI * 2f - SeedStore.WallOpening;
        int pieces = (int)((end - start) * SeedStore.WallRadius / BoardWidth);
        float step = (end - start) / pieces;
        float pieceLength = step * SeedStore.WallRadius * 1.04f;    // a touch longer so there are no gaps

        for (int i = 0; i < pieces; i++)
        {
            float angle = start + step * (i + 0.5f);
            AlongWall(angle, SeedStore.WallRadius, WallHeight / 2f, new Vector3(pieceLength, WallHeight, 0.08f), Steel);
            AlongWall(angle, SeedStore.WallRadius - 0.045f, WallHeight / 2f, new Vector3(pieceLength * 0.92f, WallHeight, 0.01f),
                      i % 2 == 0 ? WoodDark : WoodLight);
            AlongWall(angle, SeedStore.WallRadius - 0.055f, SeamY, new Vector3(pieceLength, 0.06f, 0.01f), Steel);
            if (i % 2 == 0) Bolt(SeedStore.OnCircle(angle, SeedStore.WallRadius - 0.06f, SeamY));
        }

        foreach (float angle in new[] { start, end })
        {
            Cylinder(SeedStore.OnCircle(angle, SeedStore.WallRadius, 0f), 0.07f, WallHeight, WoodTrim);
        }
    }

    // A proper four-pane window with a sill, set into the curved wall. There's no street outside. Only stars.
    private void DrawWindow()
    {
        const float Width = 0.9f, Height = 0.8f, Frame = 0.06f;
        float r = SeedStore.WallRadius - 0.06f;
        Vector3 c = SeedStore.OnCircle(WindowAngle, r, 1.2f);    // middle of the glass

        // Directions for building on the wall: across the glass, and straight in toward the room
        Vector3 across = new Vector3(MathF.Cos(WindowAngle), 0f, -MathF.Sin(WindowAngle));
        Vector3 inward = -SeedStore.OnCircle(WindowAngle, 1f, 0f);
        Vector3 up = Vector3.UnitY;

        AlongWall(WindowAngle, r, 1.2f, new Vector3(Width, Height, 0.01f), OpenSpace);
        foreach (Vector2 spot in StarSpots)
        {
            Ball(c + inward * 0.008f + across * (spot.X * Width / 2f) + up * (spot.Y * Height / 2f), new Vector3(0.012f), Star);
        }

        Vector3 lift = inward * 0.02f;    // frame pieces stand a little out from the glass
        AtWall(c + lift + up * (Height / 2f), new Vector3(Width + Frame, Frame, 0.04f), WoodTrim);
        AtWall(c + lift - up * (Height / 2f), new Vector3(Width + Frame, Frame, 0.04f), WoodTrim);
        AtWall(c + lift + across * (Width / 2f), new Vector3(Frame, Height, 0.04f), WoodTrim);
        AtWall(c + lift - across * (Width / 2f), new Vector3(Frame, Height, 0.04f), WoodTrim);
        AtWall(c + lift, new Vector3(0.025f, Height, 0.03f), WoodTrim);     // cross bars
        AtWall(c + lift, new Vector3(Width, 0.025f, 0.03f), WoodTrim);
        AtWall(c + inward * 0.06f - up * (Height / 2f + 0.02f), new Vector3(Width + 0.12f, 0.03f, 0.12f), WoodLight);   // sill

        // Everything above sits flat against the wall at WindowAngle
        void AtWall(Vector3 center, Vector3 size, Color color) => TurnedBox(center, size, color, WindowAngle);
    }

    // Three rows of shelf boards curving along the back of the wall, with a bracket every few pieces.
    private void DrawShelves()
    {
        const float Depth = 0.3f;
        float start = SeedStore.ShelfStart, end = SeedStore.ShelfEnd;
        int pieces = (int)((end - start) * SeedStore.ShelfRadius / 0.2f);
        float step = (end - start) / pieces;
        float pieceLength = step * SeedStore.ShelfRadius * 1.05f;

        foreach (float y in SeedStore.ShelfHeights)
        {
            for (int i = 0; i < pieces; i++)
            {
                float angle = start + step * (i + 0.5f);
                AlongWall(angle, SeedStore.ShelfRadius, y - 0.02f, new Vector3(pieceLength, 0.04f, Depth), WoodLight);
                if (i % 4 == 0)
                {
                    AlongWall(angle, SeedStore.ShelfRadius + 0.06f, y - 0.1f, new Vector3(0.04f, 0.12f, 0.2f), WoodTrim);
                }
            }
        }
    }

    // Jars are filled with the seed's color and have brass lids.
    // Sacks are burlap with the seed showing at the open top.
    // Bought items leave a gap until they're restocked. The hovered or selected item is drawn lighter.
    private void DrawStock()
    {
        foreach (ShelfItem item in store.Stock)
        {
            if (!item.OnShelf) continue;

            bool lit = item == store.HoveredItem || item == store.SelectedItem;
            if (item.IsFeeder)
            {
                // Feeders are shown at their real size, the same as in the bird world
                FeederModel.Draw(shapes, store.ToScene(item.Position), 1f, yawDegrees, item.Feeder,
                                 0f, Color.Blank, Color.White, lighten: lit ? 0.4f : 0f);
                continue;
            }

            Color seedColor = Lighten(Seeds.Get(item.Seed).Color, lit);
            if (item.IsSack)
            {
                Ball(item.Position + new Vector3(0, 0.17f, 0), new Vector3(0.15f, 0.17f, 0.12f), Lighten(Burlap, lit));
                Ball(item.Position + new Vector3(0, 0.32f, 0), new Vector3(0.09f, 0.035f, 0.07f), seedColor);
            }
            else
            {
                Cylinder(item.Position, 0.08f, 0.2f, seedColor);
                Cylinder(item.Position + new Vector3(0, 0.2f, 0), 0.085f, 0.04f, Lighten(Palette.BrassLight, lit));
            }
        }
    }

    // The hovered or selected item is drawn lighter.
    private static Color Lighten(Color color, bool lit) => lit ? Palette.Lighten(color, 0.4f) : color;

    // The counter faces the customer at the opening (+Z). On top: a brass register on one side,
    // the honor box where you'd leave your coin on the other, and room for the keeper in between.
    // The kick plate along the bottom is bare ship steel.
    private void DrawCounter()
    {
        Vector3 c = SeedStore.CounterSpot;
        const float Width = 1.3f, Depth = 0.42f;
        float height = SeedStore.CounterTop - 0.04f;

        Box(c + new Vector3(0, height / 2f, 0), new Vector3(Width, height, Depth), WoodDark);
        Box(c + new Vector3(0, height + 0.02f, 0), new Vector3(Width + 0.08f, 0.04f, Depth + 0.08f), WoodTrim);
        for (int i = 0; i < 3; i++)
        {
            Vector3 panel = new Vector3(-Width / 3f + i * Width / 3f, height / 2f + 0.04f, Depth / 2f + 0.005f);
            Box(c + panel, new Vector3(Width / 3f - 0.08f, height - 0.24f, 0.01f), WoodLight);
        }
        Box(c + new Vector3(0, 0.07f, Depth / 2f + 0.01f), new Vector3(Width, 0.12f, 0.02f), Steel);
        for (int i = 0; i < 5; i++)
        {
            Bolt(c + new Vector3(-Width / 2f + 0.1f + i * (Width - 0.2f) / 4f, 0.07f, Depth / 2f + 0.025f));
        }

        Vector3 top = c + new Vector3(0, SeedStore.CounterTop, 0);

        // Register: body, a raised price sign, a bank of keys, and a crank handle on the side
        Box(top + new Vector3(-0.4f, 0.1f, 0), new Vector3(0.3f, 0.2f, 0.24f), Palette.Brass);
        Box(top + new Vector3(-0.4f, 0.25f, -0.06f), new Vector3(0.26f, 0.1f, 0.06f), Palette.BrassLight);
        Box(top + new Vector3(-0.4f, 0.21f, 0.06f), new Vector3(0.26f, 0.03f, 0.1f), Palette.BrassDark);
        Box(top + new Vector3(-0.23f, 0.12f, 0), new Vector3(0.04f, 0.03f, 0.12f), SteelLight);

        // Honor box: wooden, with a coin slot on top and a brass plate on the front
        Vector3 honor = top + new Vector3(0.4f, 0, 0.02f);
        Box(honor + new Vector3(0, 0.08f, 0), new Vector3(0.2f, 0.16f, 0.16f), WoodLight);
        Box(honor + new Vector3(0, 0.162f, 0), new Vector3(0.1f, 0.005f, 0.02f), OpenSpace);
        Box(honor + new Vector3(0, 0.08f, 0.085f), new Vector3(0.12f, 0.06f, 0.01f), Palette.BrassLight);
    }

    // The shopkeeper birb on a three-legged wooden stool behind the counter.
    // The birb is drawn at its real size (not scaled with the shop), so the stool makes up the difference.
    private void DrawShopkeeper()
    {
        Shopkeeper keeper = store.Keeper;
        Vector3 spot = SeedStore.KeeperSpot;
        float stool = keeper.StoolHeight;

        Cylinder(spot + new Vector3(0, stool - 0.04f, 0), 0.14f, 0.04f, WoodLight);
        for (int i = 0; i < 3; i++)
        {
            float angle = i * MathF.PI * 2f / 3f;
            Cylinder(spot + SeedStore.OnCircle(angle, 0.09f, 0f), 0.015f, stool - 0.04f, WoodTrim);
        }

        // BirbBuilder places the shape's origin, so lift it by how far the shape reaches below its origin
        float lift = -keeper.Bounds.Min.Y * keeper.Size + keeper.Motion.Bounce;
        Vector3 feet = store.ToScene(spot + new Vector3(0, stool, 0)) + new Vector3(0, lift, 0);
        BirbBuilder.Draw(keeper.Shape, feet, store.Yaw + keeper.Turn, keeper.Size, Color.White, keeper.Motion);
    }

    // A hooped barrel of corn by the opening on the right, and a crate with a sack of millet on the left.
    private void DrawOddsAndEnds()
    {
        Vector3 barrel = SeedStore.OnCircle(70f * MathF.PI / 180f, 1.3f, 0f);
        Cylinder(barrel, 0.2f, 0.5f, WoodLight);
        Cylinder(barrel + new Vector3(0, 0.08f, 0), 0.208f, 0.04f, Steel);
        Cylinder(barrel + new Vector3(0, 0.38f, 0), 0.208f, 0.04f, Steel);
        Cylinder(barrel + new Vector3(0, 0.5f, 0), 0.17f, 0.015f, Seeds.Get(SeedType.Corn).Color);

        float crateAngle = 305f * MathF.PI / 180f;
        Vector3 crate = SeedStore.OnCircle(crateAngle, 1.25f, 0.18f);
        TurnedBox(crate, new Vector3(0.36f, 0.36f, 0.36f), WoodDark, crateAngle);

        Vector3 sack = SeedStore.OnCircle(crateAngle + 0.3f, 1.05f, 0f);
        Ball(sack + new Vector3(0, 0.15f, 0), new Vector3(0.14f, 0.15f, 0.12f), Burlap);
        Ball(sack + new Vector3(0, 0.29f, 0), new Vector3(0.08f, 0.03f, 0.06f), Seeds.Get(SeedType.Millet).Color);
    }

    // What looks like a ceiling fan is the ship's air recycler: a steel motor housing and a guard ring,
    // with wooden-looking blades turning underneath. The rod rises up into the ceiling fog.
    private void DrawAirRecycler()
    {
        Vector3 hub = new Vector3(0f, 2.0f, -0.15f);
        Cylinder(hub + new Vector3(0, 0.08f, 0), 0.02f, 0.9f, Steel);
        Cylinder(hub, 0.12f, 0.1f, Steel);
        Cylinder(hub - new Vector3(0, 0.03f, 0), 0.06f, 0.03f, SteelLight);

        for (int i = 0; i < 4; i++)
        {
            float angle = store.FanAngle + i * MathF.PI / 2f;
            TurnedBox(hub + Outward(angle, 0.3f), new Vector3(0.4f, 0.015f, 0.09f), WoodLight, angle);
        }

        const int Segments = 20;
        const float GuardRadius = 0.55f;
        float segmentLength = GuardRadius * 2f * MathF.PI / Segments * 1.05f;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * MathF.PI * 2f / Segments;
            TurnedBox(hub + Outward(angle, GuardRadius) + new Vector3(0, 0.03f, 0), new Vector3(0.025f, 0.05f, segmentLength), Steel, angle);
        }
    }

    // ---------------- SHAPE HELPERS ----------------

    // These take store-space positions and sizes, scale them up to fill the box, and turn everything with the store.

    // A box centered at 'center', turned with the store plus an extra 'turn' in radians.
    private void TurnedBox(Vector3 center, Vector3 size, Color color, float turn)
    {
        shapes.Box(store.ToScene(center), size * SeedStore.Scale, color, angleDegrees: yawDegrees + turn * Raylib.RAD2DEG);
    }

    private void Box(Vector3 center, Vector3 size, Color color) => TurnedBox(center, size, color, 0f);

    // A box lying along the curve of the room at this angle and distance from the middle:
    // size X runs along the curve, Y is up, Z points in toward the middle.
    private void AlongWall(float angle, float radius, float y, Vector3 size, Color color)
    {
        TurnedBox(SeedStore.OnCircle(angle, radius, y), size, color, angle);
    }

    // An upright cylinder standing on 'bottom'.
    private void Cylinder(Vector3 bottom, float radius, float height, Color color)
    {
        shapes.Cylinder(store.ToScene(bottom), radius * SeedStore.Scale, height * SeedStore.Scale, color, yawDegrees);
    }

    // A sphere squashed or stretched to these radii.
    private void Ball(Vector3 center, Vector3 radii, Color color)
    {
        shapes.Ball(store.ToScene(center), radii * SeedStore.Scale, color, yawDegrees);
    }

    private void Bolt(Vector3 position) => Ball(position, new Vector3(0.022f), SteelLight);

    // The point this far out from the middle, in the direction a box's long side points after turning by 'angle'.
    private static Vector3 Outward(float angle, float distance)
    {
        return new Vector3(MathF.Cos(angle), 0, -MathF.Sin(angle)) * distance;
    }
}
