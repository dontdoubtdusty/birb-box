using System.Numerics;

public class World
{
    public static World Instance { get; private set; } = null!;

    // The world is a square from -HalfSize to +HalfSize on X and Z. Ground is Y = 0.
    public float HalfSize { get; } = 40f;
    public const float Gravity = 9.8f;

    private readonly List<Entity> entities = new();
    private readonly List<Entity> toAdd = new();
    private readonly List<Entity> toRemove = new();

    // Ready-made lists per type (all birds, all food...), so asking for one doesn't build a new list
    // every call. They're thrown away in FlushChanges() whenever something is added or removed.
    private readonly Dictionary<Type, object> typeLists = new();

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
        if (toAdd.Count > 0 || toRemove.Count > 0) typeLists.Clear();

        foreach (Entity e in toRemove) entities.Remove(e);
        foreach (Entity e in toAdd) entities.Add(e);
        toRemove.Clear();
        toAdd.Clear();
    }

    public IReadOnlyList<Entity> Entities => entities;
    public bool Contains(Entity entity) => entities.Contains(entity);

    // Read-only: the same list is shared by every caller until the next change.
    public IReadOnlyList<T> GetAllEntities<T>()
    {
        if (typeLists.TryGetValue(typeof(T), out object? cached)) return (List<T>)cached;

        List<T> result = new List<T>();
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] is T match)
            {
                result.Add(match);
            }
        }
        typeLists[typeof(T)] = result;
        return result;
    }

    public bool IsInside(Vector3 pos, float margin = 0f)
    {
        return MathF.Abs(pos.X) <= HalfSize + margin && MathF.Abs(pos.Z) <= HalfSize + margin;
    }

    public Vector3 RandomGroundPoint(float edgeMargin = 2f)
    {
        float range = HalfSize - edgeMargin;
        return new Vector3(Rng.Range(-range, range), 0, Rng.Range(-range, range));
    }
}
