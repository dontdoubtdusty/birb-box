// The bezel ring's feel. It turns whatever the box is showing, keeps spinning after a fast drag,
// slows down with friction, then settles into the next notch. Each notch it passes plays a click.
// Turns are in radians.
public class Dial
{
    public const float NotchSpacing = MathF.PI * 2f / 36f;    // every 10 degrees, matching Pedestal.RenderNotches

    private const float Friction = 2.5f;      // how quickly a coast slows down; higher stops sooner
    private const float MaxSpeed = 12f;       // fastest coast, in radians per second
    private const float SettleSpeed = 0.4f;   // below this speed the ring stops coasting and settles
    private const float SettleRate = 12f;     // how quickly it eases into the notch

    private float speed = 0f;                 // radians per second
    private float? settleTarget;              // the notch it's easing into, or null while held or coasting

    // drag: how far the mouse turned the ring this frame. held: the mouse is pressed on the box.
    public void Turn(ref float yaw, float dt, float drag, bool held)
    {
        if (dt <= 0f) return;
        float before = yaw;

        if (held)
        {
            // A hand on the ring: it moves only with the drag, which also sets its speed for when it's let go
            float dragSpeed = Math.Clamp(drag / dt, -MaxSpeed, MaxSpeed);
            speed += (dragSpeed - speed) * MathF.Min(1f, dt * 20f);
            settleTarget = null;
            yaw += drag;
        }
        else if (MathF.Abs(speed) > SettleSpeed)
        {
            speed *= MathF.Exp(-Friction * dt);
            yaw += speed * dt;
        }
        else
        {
            // Settle into the next notch in the direction it was going (or the nearest, if it wasn't moving).
            // A target more than a notch away is stale, from the other scene's turn, so it's picked again.
            if (settleTarget == null || MathF.Abs(settleTarget.Value - yaw) > NotchSpacing)
            {
                float notches = yaw / NotchSpacing;
                float index = speed > 0f ? MathF.Ceiling(notches) : speed < 0f ? MathF.Floor(notches) : MathF.Round(notches);
                settleTarget = index * NotchSpacing;
            }
            speed = 0f;
            yaw += (settleTarget.Value - yaw) * MathF.Min(1f, SettleRate * dt);
        }

        if (NotchOf(before) != NotchOf(yaw)) Sounds.PlayDialClick();
    }

    // The tiny nudge counts a ring that settles a hair short of a notch as having reached it.
    private static int NotchOf(float yaw) => (int)MathF.Floor(yaw / NotchSpacing + 0.001f);
}
