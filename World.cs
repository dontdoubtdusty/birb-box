using System.Numerics;

public class World
{
    public static World Instance { get; private set; } = null!;

    // The world is a square from -HalfSize to +HalfSize on X and Z. Ground is Y = 0.
    public float HalfSize { get; } = 40f;

    private readonly List<Entity> entities = new();
    private readonly List<Entity> toAdd = new();
    private readonly List<Entity> toRemove = new();
    private readonly Random ran = new();

    public World()
    {
        Instance = this;
    }

    // Adds and removes are queued and applied in FlushChanges(),
    // so the list never changes while we are looping over it.
    public void AddEntity(Entity entity) => toAdd.Add(entity);
    public void RemoveEntity(Entity entity) => toRemove.Add(entity);

    public void FlushChanges()
    {
        foreach (Entity e in toRemove) entities.Remove(e);
        foreach (Entity e in toAdd) entities.Add(e);
        toRemove.Clear();
        toAdd.Clear();
    }

    public IReadOnlyList<Entity> Entities => entities;
    public bool Contains(Entity entity) => entities.Contains(entity);

    public List<T> GetAllEntities<T>()
    {
        List<T> result = new List<T>();
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] is T match)
            {
                result.Add(match);
            }
        }
        return result;
    }

    public bool IsInside(Vector3 pos, float margin = 0f)
    {
        return MathF.Abs(pos.X) <= HalfSize + margin && MathF.Abs(pos.Z) <= HalfSize + margin;
    }

    public Vector3 RandomGroundPoint(float edgeMargin = 2f)
    {
        float range = HalfSize - edgeMargin;
        float x = (float)(ran.NextDouble() * 2 - 1) * range;
        float z = (float)(ran.NextDouble() * 2 - 1) * range;
        return new Vector3(x, 0, z);
    }
}
