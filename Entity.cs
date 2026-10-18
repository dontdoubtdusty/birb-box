using System.Numerics;

public abstract class Entity
{
    private static int nextEntityID = 0;
    public int EntityID { get; private set; }

    // Position is always in WORLD space. The ViewWindow converts it for drawing.
    public Vector3 Position { get; set; }

    protected Entity()
    {
        EntityID = nextEntityID++;
    }

    // Simulation step. No drawing happens here.
    public abstract void Update(float dt);
}
