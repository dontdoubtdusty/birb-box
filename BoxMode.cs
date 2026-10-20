using System.Numerics;
using Raylib_cs;

public enum BoxScene { Birds, Store }

// What the Birb Box is projecting: the bird world or the Gilded Granary.
// A holographic sign hangs above the box naming the other scene; clicking it switches (HoloSign draws it).
//
// Switching is a "fog swallow": the fog rolls in from the edges until the box is all haze,
// the scene swaps at the thickest point, then the fog clears on the new scene.
// Swallow is handed to the hologram shader, which pulls its fog inward by that much.
public class BoxMode
{
    public BoxScene Showing { get; private set; } = BoxScene.Birds;
    public bool InStore => Showing == BoxScene.Store;
    public bool IsSwitching => switchingTo != null || Swallow > 0f;

    public float Swallow { get; private set; }    // 0 = clear, 1 = all fog
    private const float FogTime = 0.3f;            // seconds for the fog to roll in, and again to clear
    private BoxScene? switchingTo;

    // THE SIGN (scene units): the board's size, and the middle of the board, hanging clear of the top ring
    public const float SignWidth = 3.0f;
    public const float SignHeight = 0.95f;
    public bool SignHovered;

    public static Vector3 SignPosition(ViewWindow view) => new Vector3(0, view.Height + 0.7f + SignHeight / 2f, 0);

    public void Toggle()
    {
        if (IsSwitching) return;
        switchingTo = InStore ? BoxScene.Birds : BoxScene.Store;
    }

    // Starts a switch to this scene, unless it's already showing or already on its way.
    public void SwitchTo(BoxScene scene)
    {
        if (switchingTo == scene) return;
        if (switchingTo == null && Showing == scene) return;
        switchingTo = scene;
    }

    // Returns true on the one frame the scene actually swaps.
    public bool Update(float dt)
    {
        if (switchingTo != null)
        {
            Swallow = MathF.Min(1f, Swallow + dt / FogTime);
            if (Swallow >= 1f)
            {
                Showing = switchingTo.Value;
                switchingTo = null;
                return true;
            }
        }
        else
        {
            Swallow = MathF.Max(0f, Swallow - dt / FogTime);
        }
        return false;
    }

    // The sign faces the camera, which always looks from the front (+Z), so a flat box around it is enough.
    // It covers the board plus the chains and rod above it.
    public static bool RayHitsSign(Ray sceneRay, ViewWindow view)
    {
        Vector3 center = SignPosition(view);
        Vector3 min = center + new Vector3(-SignWidth / 2f - 0.2f, -SignHeight / 2f - 0.1f, -0.4f);
        Vector3 max = center + new Vector3(SignWidth / 2f + 0.2f, SignHeight / 2f + 0.6f, 0.4f);
        return Raylib.GetRayCollisionBox(sceneRay, new BoundingBox(min, max)).Hit;
    }
}
