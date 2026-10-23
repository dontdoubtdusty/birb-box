// Counts every model drawn this frame, for the tools panel.
// Each DrawModel is one draw call on the graphics card. Shapes like DrawCylinderEx are batched by Raylib and not counted.
public static class DrawStats
{
    public static int ModelDraws { get; private set; }    // last finished frame
    private static int counting;

    public static void CountModel() => counting++;

    // Called once at the end of each frame's drawing.
    public static void EndFrame()
    {
        ModelDraws = counting;
        counting = 0;
    }
}
