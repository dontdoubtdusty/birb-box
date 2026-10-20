using System.Numerics;
using Raylib_cs;

// Draws a feeder from plain boxes, cylinders and balls. Used in the bird world, in the store, and for the
// see-through feeder that follows the mouse while placing one. Nothing in here changes the simulation.
// Sizes are world units; scale turns them into scene units (the view's zoom in the world, 1 in the store).
public static class FeederModel
{
    private static readonly Color WoodLight = new Color(150, 108, 66, 255);
    private static readonly Color WoodDark = new Color(104, 70, 42, 255);
    private static readonly Color Glass = new Color(200, 230, 240, 70);

    // basePos: the middle of the feeder's foot, in the scene. fill: 0 empty to 1 full (the Tube shows its seed level;
    // the Tray's seed is drawn as grains by HologramRenderer). tint is multiplied onto every part;
    // lighten moves every part toward white (for a hovered item in the store).
    public static void Draw(UnitShapes shapes, Vector3 basePos, float scale, float yawDegrees, FeederStyle style,
                            float fill, Color seedColor, Color tint, float lighten = 0f)
    {
        // Each part's offset is turned with the feeder, then scaled, then placed.
        void Box(Vector3 offset, Vector3 size, Color color) =>
            shapes.Box(Place(offset), size * scale, Look(color), angleDegrees: yawDegrees);
        void Cylinder(Vector3 offset, float radius, float height, Color color) =>
            shapes.Cylinder(Place(offset), radius * scale, height * scale, Look(color), yawDegrees);
        void Ball(Vector3 offset, Vector3 radii, Color color) =>
            shapes.Ball(Place(offset), radii * scale, Look(color), yawDegrees);

        Vector3 Place(Vector3 offset) => basePos + Vector3.Transform(offset * scale, Matrix4x4.CreateRotationY(yawDegrees * Raylib.DEG2RAD));
        Color Look(Color color) => Palette.Lighten(Palette.Multiply(color, tint), lighten);

        if (style == FeederStyle.Tray)
        {
            // Four short legs, a floor board, and a rim to keep the seed in
            foreach (float x in new[] { -0.28f, 0.28f })
            {
                foreach (float z in new[] { -0.28f, 0.28f })
                {
                    Box(new Vector3(x, 0.09f, z), new Vector3(0.04f, 0.18f, 0.04f), WoodDark);
                }
            }
            Box(new Vector3(0f, 0.195f, 0f), new Vector3(0.66f, 0.03f, 0.66f), WoodLight);
            Box(new Vector3(0f, 0.225f, 0.315f), new Vector3(0.66f, 0.04f, 0.03f), WoodDark);
            Box(new Vector3(0f, 0.225f, -0.315f), new Vector3(0.66f, 0.04f, 0.03f), WoodDark);
            Box(new Vector3(0.315f, 0.225f, 0f), new Vector3(0.03f, 0.04f, 0.66f), WoodDark);
            Box(new Vector3(-0.315f, 0.225f, 0f), new Vector3(0.03f, 0.04f, 0.66f), WoodDark);
        }
        else
        {
            // A post, a brass dish, two crossed perches, the seed inside a glass tube, and a domed brass roof.
            // The seed is drawn before the glass so it shows through.
            Cylinder(new Vector3(0f, 0f, 0f), 0.025f, 0.53f, WoodDark);
            Cylinder(new Vector3(0f, 0.53f, 0f), 0.11f, 0.03f, Palette.Brass);
            Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.46f, 0.015f, 0.015f), WoodLight);
            Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.015f, 0.015f, 0.46f), WoodLight);
            if (fill > 0f) Cylinder(new Vector3(0f, 0.56f, 0f), 0.075f, 0.38f * fill, seedColor);
            Cylinder(new Vector3(0f, 0.56f, 0f), 0.085f, 0.38f, Glass);
            Ball(new Vector3(0f, 0.96f, 0f), new Vector3(0.12f, 0.06f, 0.12f), Palette.BrassLight);
            Ball(new Vector3(0f, 1.03f, 0f), new Vector3(0.025f, 0.025f, 0.025f), Palette.BrassLight);
        }
    }
}
