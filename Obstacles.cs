using System.Numerics;

// Keeps ground things clear of what they can't stand in: rocks, then the pool.
// The one place to add the next obstacle (the stream) so every caller picks it up.
public static class Obstacles
{
    // The same spot moved off any rock (at least rockMargin from its edge), then out of the pool
    // (at least poolMargin from the waterline; negative allows that far into the shallows). Height is kept.
    public static Vector3 PushOut(Vector3 pos, float rockMargin = 0.15f, float poolMargin = 0.15f)
    {
        return Pool.PushOut(Rock.PushOut(pos, rockMargin), poolMargin);
    }
}
