using System.Numerics;

// A coin a bird drops after a good meal. It pops up, lands on the dirt,
// and waits to be clicked. Uncollected coins fade away after a while,
// and twice as fast while they're outside the view window.
public class Coin : Entity
{
    public const float Radius = 0.24f;
    private const float OffscreenDrainRate = 2f;  // life drains this many times faster when hidden
    private const float FadeTime = 5f;            // last seconds spent fading out

    public float LifeLeft { get; private set; }    // starts at SimTuning.CoinLifetime
    public bool IsLanded => Position.Y <= Radius;

    // 1 while fresh, drops to 0 over the last few seconds
    public float Alpha => Math.Clamp(LifeLeft / FadeTime, 0f, 1f);

    private Vector3 velocity;
    private bool removed = false;

    public Coin(Vector3 position)
    {
        Position = position;
        LifeLeft = SimTuning.Now.CoinLifetime;
        // A little hop out of the bird in a random direction
        float angle = Rng.Range(0f, MathF.PI * 2f);
        velocity = new Vector3(MathF.Sin(angle) * 0.6f, 2.5f, MathF.Cos(angle) * 0.6f);
    }

    public override void Update(float dt)
    {
        bool onScreen = GameEngine.Instance.View.Contains(Position);
        LifeLeft -= dt * (onScreen ? 1f : OffscreenDrainRate);

        if (!IsLanded || velocity.Y > 0f)
        {
            velocity.Y -= World.Gravity * dt;
            Position += velocity * dt;
            if (Position.Y < Radius)
            {
                Position = Rock.PushOut(new Vector3(Position.X, Radius, Position.Z), Radius);
                velocity = Vector3.Zero;
                if (onScreen) Sounds.PlayCoinLand();
            }
        }

        if (LifeLeft <= 0f) Remove();
    }

    // Saving (see SaveGame.cs)
    public CoinSave ToSave() => new() { Position = Position, Velocity = velocity, LifeLeft = LifeLeft };

    public static Coin FromSave(CoinSave save)
    {
        Coin coin = new Coin(save.Position);
        coin.velocity = save.Velocity;
        coin.LifeLeft = save.LifeLeft;
        return coin;
    }

    public void Collect()
    {
        if (removed) return;
        GameEngine.Instance.Coins++;
        Sounds.PlayCoinPickup();
        Remove();
    }

    private void Remove()
    {
        if (removed) return;
        removed = true;
        World.Instance.RemoveEntity(this);
    }
}
