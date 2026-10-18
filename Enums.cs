public enum BirdType
{
    Cardinal,
    Dove,
    Finch
}

public enum BirdState
{
    Arriving,     // walking in from the world edge or flying down from the sky
    Wandering,    // hopping around, flocking, pecking
    SeekingFood,  // hungry and heading to a food pile
    Eating,
    Leaving       // flying away for good
}
