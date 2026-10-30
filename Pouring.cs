using System.Numerics;
using Raylib_cs;

// Holding right mouse pours the seed bucket onto the floor under the cursor.
// A pile drops each time the cursor moves a little, or on a timer while it holds still.
public class Pouring
{
    private const float PileSpacing = 0.35f;     // cursor travel between piles
    private const float PileInterval = 0.5f;     // seconds between piles while holding still

    private Vector3? lastPilePoint;              // box space, null when not pouring
    private float pileTimer = 0f;
    private bool warnedEmptyBucket = false;

    // blocked: the mouse is over something else, or the birds aren't showing.
    public void Update(float dt, bool blocked, MousePicking mouse, SeedBucket bucket, FoodSpawner spawner, ViewWindow view)
    {
        if (!Raylib.IsMouseButtonDown(MouseButton.Right) || blocked)
        {
            lastPilePoint = null;
            warnedEmptyBucket = false;
            return;
        }

        Vector3? point = mouse.FloorPoint();
        if (point == null) return;

        pileTimer -= dt;
        bool movedFar = lastPilePoint == null || Vector3.Distance(point.Value, lastPilePoint.Value) >= PileSpacing;
        if (!movedFar && pileTimer > 0f) return;

        lastPilePoint = point;
        pileTimer = PileInterval;

        if (bucket.IsEmpty)
        {
            if (!warnedEmptyBucket) EventLog.Add("The seed bucket is empty. Click a seed bag on the bucket panel to fill it.");
            warnedEmptyBucket = true;
            return;
        }
        if (!spawner.HasRoom) return;

        Dictionary<SeedType, float> scoop = bucket.Take(FoodSpawner.PileAmount);
        spawner.ThrowFood(view.ToWorld(point.Value), view.WorldHeight, scoop);
    }
}
