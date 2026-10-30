using System.Numerics;
using Raylib_cs;

// What the mouse is pointing at: the ray through the cursor (in the room, in the hologram, or in the
// un-turned box), the spot on the box's floor under it, and the nearest coin, feeder or bird it passes through.
// Asks only; nothing in here changes the world.
public class MousePicking
{
    private readonly HoloCamera camera;
    private readonly ViewWindow view;
    private readonly World world;

    public MousePicking(HoloCamera camera, ViewWindow view, World world)
    {
        this.camera = camera;
        this.view = view;
        this.world = world;
    }

    // The ray from the camera through the mouse, among the things in the room (the radio, the pedestal).
    public Ray RayInRoom()
    {
        return Raylib.GetScreenToWorldRay(Raylib.GetMousePosition(), camera.Camera);
    }

    // The same ray in the hologram's own space, where its floor is at height 0 (see HoloCamera.HoloLift).
    public Ray RayInHologram()
    {
        return Raylib.GetScreenToWorldRay(Raylib.GetMousePosition(), camera.HologramCamera);
    }

    // The ray in the hologram, turned back into box space,
    // so it can be tested against things where they sit in the un-turned cube.
    public Ray RayInBox()
    {
        Ray sceneRay = RayInHologram();
        return new Ray(view.SceneToBox(sceneRay.Position), view.SceneToBox(sceneRay.Direction));
    }

    // Where the mouse ray hits the floor, in box space.
    // Only the clear dirt counts, not the thin fog at the walls.
    public Vector3? FloorPoint()
    {
        Ray ray = RayInBox();
        if (MathF.Abs(ray.Direction.Y) < 0.0001f) return null;

        float t = -ray.Position.Y / ray.Direction.Y;
        if (t < 0) return null;
        Vector3 localPoint = ray.Position + ray.Direction * t;

        float limit = MathF.Max(0.85f, view.FogStart);
        if (ViewWindow.EdgeAmount(localPoint.X, localPoint.Z, view.HalfWidth) > limit) return null;
        return localPoint;
    }

    // The nearest coin in view the box-space ray passes near (a bigger target than the coin, so it's easy to click).
    public Coin? PickCoin(Ray ray)
    {
        Coin? best = null;
        float bestDist = float.MaxValue;

        foreach (Coin coin in world.GetAllEntities<Coin>())
        {
            if (!view.Contains(coin.Position)) continue;

            Vector3 localCenter = view.ToLocal(coin.Position);
            RayCollision hit = Raylib.GetRayCollisionSphere(ray, localCenter, Coin.Radius * 3f * view.Zoom);
            if (hit.Hit && hit.Distance < bestDist)
            {
                best = coin;
                bestDist = hit.Distance;
            }
        }
        return best;
    }

    // The nearest feeder the box-space ray passes through, tested against a box from its foot up to its top.
    public Feeder? PickFeeder(Ray ray)
    {
        Feeder? best = null;
        float bestDist = float.MaxValue;

        foreach (Feeder feeder in world.GetAllEntities<Feeder>())
        {
            if (!view.Contains(feeder.Position)) continue;

            Vector3 local = view.ToLocal(feeder.Position);
            float r = feeder.Info.FootRadius;
            Vector3 min = local + new Vector3(-r, 0f, -r) * view.Zoom;
            Vector3 max = local + new Vector3(r, feeder.Info.CatchHeight, r) * view.Zoom;
            RayCollision hit = Raylib.GetRayCollisionBox(ray, new BoundingBox(min, max));
            if (hit.Hit && hit.Distance < bestDist)
            {
                best = feeder;
                bestDist = hit.Distance;
            }
        }
        return best;
    }

    // The nearest bird in view the box-space ray passes through.
    public Bird? PickBird(Ray ray)
    {
        Bird? best = null;
        float bestDist = float.MaxValue;

        foreach (Bird bird in world.GetAllEntities<Bird>())
        {
            if (!view.Contains(bird.Position, -0.5f)) continue;

            float? dist = RayHitsBird(ray, bird);
            if (dist != null && dist < bestDist)
            {
                best = bird;
                bestDist = dist.Value;
            }
        }
        return best;
    }

    // Tests the ray against the bird's box. The ray is moved into the bird's own space
    // (unturned, in birb units), where the box from its shape fits it exactly.
    // Returns how far along the box-space ray the hit is, or null for a miss.
    private float? RayHitsBird(Ray ray, Bird bird)
    {
        float scale = bird.Size * view.Zoom;
        Matrix4x4 unturn = Matrix4x4.CreateRotationY(-bird.Heading);

        Vector3 origin = Vector3.Transform(ray.Position - view.ToLocal(bird.Position), unturn) / scale;
        Vector3 direction = Vector3.Transform(ray.Direction, unturn);
        RayCollision hit = Raylib.GetRayCollisionBox(new Ray(origin, direction), bird.Bounds);
        return hit.Hit ? hit.Distance * scale : null;
    }
}
