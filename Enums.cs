// BirdType lives in Birds.cs, next to the bird table.
public enum BirdState
{
    Arriving,     // walking in from the world edge or flying down from the sky
    Wandering,    // hopping around, flocking, pecking
    SeekingFood,  // hungry and heading to a food pile
    Eating,
    Leaving       // flying away for good
}

// How a bird gets around on the ground. Hoppers move in bursts; walkers move steadily.
public enum Gait
{
    Hop,
    Walk
}

// What a bird's body is doing right now. The mover sets it; BirbAnimator decides how it looks.
public enum BirdPose
{
    Idle,         // standing still
    Hop,          // getting around in little hops
    Walk,         // getting around one step at a time
    Peck,         // head down at the ground or at food
    LookAround,   // standing still, turning its head
    Fly
}
