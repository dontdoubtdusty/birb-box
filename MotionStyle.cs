// How a birb's movements look: the numbers BirbAnimator uses for one birb.
// Saved with the birb's shape, so each design can move in its own way.
// Angles are degrees. HopHeight is in world units; HeadBob is in birb units.
public class MotionStyle
{
    public float HopHeight = 0.12f;    // how high each hop goes
    public float HeadBob = 0.12f;      // how far the head thrusts forward and back
    public float StepSwing = 20f;      // walkers: how far each leg swings per step
    public float WingSpread = 80f;     // how far the wings open when flying
    public float FlapAngle = 45f;      // how far the wings beat up and down
    public float FlapSpeed = 20f;      // wing beats (radians per second)
    public float LookAngle = 55f;      // how far the head turns when looking around
    public float LookStep = 0.7f;      // seconds between head turns (smaller = more jittery)
    public float TailFlick = 35f;      // tail flick after landing

    public MotionStyle Copy() => (MotionStyle)MemberwiseClone();
}
