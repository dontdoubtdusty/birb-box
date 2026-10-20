using System.Numerics;
using Raylib_cs;

// Draws a BirbShape part by part, through the hologram shader so birbs get fog and glow.
// The meshes come from PartLibrary and the motion from BirbAnimator; this file only places them.
public static class BirbBuilder
{
    private static Shader shader;
    private static int locFlatShade;

    public static void Load(Shader holoShader)
    {
        shader = holoShader;
        locFlatShade = Raylib.GetShaderLocation(shader, "flatShade");
        PartLibrary.Load(shader);
    }

    public static void Unload()
    {
        PartLibrary.Unload();
    }

    // position: where the shape's origin goes in the scene. yaw: radians, 0 = facing +Z.
    // scale: birb units to scene units (the bird's Size times the view's Zoom).
    // tint is multiplied onto every part's color (used for the selection highlight).
    // motion: how far each part is moved from its rest pose (a default BirbMotion draws the rest pose).
    // highlight: one part to make glow brighter (the Designer's selected part), or null.
    public static void Draw(BirbShape shape, Vector3 position, float yaw, float scale, Color tint, BirbMotion motion,
                            BirbPart? highlight = null)
    {
        float pulse = 0.35f + 0.25f * MathF.Sin((float)Raylib.GetTime() * 6f);
        Matrix4x4 placeBird = Raymath.MatrixMultiply(
            Raymath.MatrixMultiply(Raymath.MatrixScale(scale, scale, scale), Raymath.MatrixRotateY(yaw)),
            Raymath.MatrixTranslate(position.X, position.Y, position.Z));

        Raylib.SetShaderValue(shader, locFlatShade, 1, ShaderUniformDataType.Int);
        foreach (BirbPart part in shape.Parts)
        {
            // mesh base (joint to origin), then the part's rest place on the birb, then its animation,
            // then the birb's place in the scene
            Matrix4x4 rest = Raymath.MatrixMultiply(PartLibrary.GetBase(part.Mesh), part.LocalMatrix());
            Matrix4x4 onBirb = Raymath.MatrixMultiply(rest, TotalMotion(shape, part, motion, 0));

            Model model = PartLibrary.GetModel(part.Mesh);
            model.Transform = Raymath.MatrixMultiply(onBirb, placeBird);
            Color color = Palette.Multiply(part.Color, tint);
            if (part == highlight) color = Palette.Lighten(color, pulse);
            Raylib.DrawModel(model, Vector3.Zero, 1f, color);
        }
        Raylib.SetShaderValue(shader, locFlatShade, 0, ShaderUniformDataType.Int);
    }

    // A part's own motion, then the motion of the part it hangs from, and so on up the chain.
    // depth stops a loop (a part attached to itself through others) from running forever.
    private static Matrix4x4 TotalMotion(BirbShape shape, BirbPart part, BirbMotion motion, int depth)
    {
        Matrix4x4 m = BirbAnimator.PartMotion(part, motion);
        if (part.Parent == null || depth >= shape.Parts.Count) return m;

        BirbPart? parent = shape.Find(part.Parent);
        if (parent == null || parent == part) return m;
        return Raymath.MatrixMultiply(m, TotalMotion(shape, parent, motion, depth + 1));
    }
}
