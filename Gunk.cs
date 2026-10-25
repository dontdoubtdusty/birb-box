using System.Numerics;
using Raylib_cs;

// What's left of seed that sat on the dirt too long (see Food). Birds steer around it,
// and the more there is in the world, the fewer new birds come (BirdSpawner).
// It stays until the player scrapes it up with the Muck Scraper from the store.
public class Gunk : Entity
{
    public static readonly Color Color = new Color(78, 74, 40, 255);
    public const float AvoidDistance = 0.8f;    // how far past its edge birds keep away

    public float Radius { get; private init; }
    private bool removed = false;

    // Bigger piles of seed leave bigger gunk.
    public Gunk(Vector3 position, float seedAmount)
    {
        Position = new Vector3(position.X, 0f, position.Z);
        Radius = MathF.Min(0.4f, 0.15f + 0.08f * seedAmount);
    }

    public override void Update(float dt) { }

    // Saving (see SaveGame.cs)
    public GunkSave ToSave() => new() { Position = Position, Radius = Radius };
    public static Gunk FromSave(GunkSave save) => new Gunk(save.Position, 0f) { Radius = save.Radius };

    public void ScrapeUp()
    {
        if (removed) return;
        removed = true;
        World.Instance.RemoveEntity(this);
    }
}
