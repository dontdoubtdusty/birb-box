using System.Numerics;
using Raylib_cs;

public enum BoxScene { Birds, Store }

// What the Birb Cube is projecting: the bird world or the Gilded Granary.
// The BIRBS / SHOP rocker on the pedestal's front switches between them (see Rocker and GameEngine).
//
// Switching is a "fog swallow": the fog rolls in from the edges until the box is all haze,
// the scene swaps at the thickest point, then the fog clears on the new scene.
// Swallow is handed to the hologram shader, which pulls its fog inward by that much.
public class BoxMode
{
    public BoxScene Showing { get; private set; } = BoxScene.Birds;
    public bool InStore => Showing == BoxScene.Store;
    public bool IsSwitching => switchingTo != null || Swallow > 0f;
    public BoxScene Heading => switchingTo ?? Showing;    // the scene on its way in, or the one showing

    public float Swallow { get; private set; }    // 0 = clear, 1 = all fog
    private const float FogTime = 0.3f;            // seconds for the fog to roll in, and again to clear
    private BoxScene? switchingTo;

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
}
