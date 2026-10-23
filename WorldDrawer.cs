using System.Numerics;
using Raylib_cs;

// Draws the bird world's contents inside the hologram: floor, feeders, food, gunk, coins and birds.
// Used by both the main scene pass and the zoo camera. Nothing in here changes the simulation.
public class WorldDrawer
{
    private Shader holoShader;
    private int locIsFloor;

    private Model floorModel;
    private Model grainRound;    // unit-sized; each seed type stretches these into its own grain shape
    private Model grainBlock;
    private Model coinModel;
    private readonly BirbAnimator animator = new BirbAnimator();
    private readonly UnitShapes shapes = new UnitShapes();    // for feeders

    public void Load(Shader holo, ViewWindow view)
    {
        holoShader = holo;
        locIsFloor = Raylib.GetShaderLocation(holoShader, "isFloor");

        // A square floor big enough for the widest the cube can be; the shader's fog cuts off the rest.
        float floorSize = view.HalfSize * ViewWindow.MaxCubeScale * 2f;
        floorModel = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(floorSize, floorSize, 1, 1));
        grainRound = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(0.5f, 6, 8));
        grainBlock = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
        coinModel = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(Coin.Radius, 0.03f, 20));
        shapes.Load(holoShader);

        Raylib.SetMaterialShader(ref floorModel, 0, ref holo);
        Raylib.SetMaterialShader(ref grainRound, 0, ref holo);
        Raylib.SetMaterialShader(ref grainBlock, 0, ref holo);
        Raylib.SetMaterialShader(ref coinModel, 0, ref holo);
    }

    public void Unload()
    {
        Raylib.UnloadModel(floorModel);
        Raylib.UnloadModel(grainRound);
        Raylib.UnloadModel(grainBlock);
        Raylib.UnloadModel(coinModel);
        shapes.Unload();
    }

    public void DrawFloor(ViewWindow view)
    {
        Raylib.SetShaderValue(holoShader, locIsFloor, 1, ShaderUniformDataType.Int);
        Raylib.DrawModelEx(floorModel, Vector3.Zero, Vector3.UnitY, view.YawDegrees, Vector3.One, Color.White);
        DrawStats.CountModel();
        Raylib.SetShaderValue(holoShader, locIsFloor, 0, ShaderUniformDataType.Int);
    }

    // Everything standing on the floor. ringColor is the selected bird's ring.
    public void DrawContents(ViewWindow view, Bird? selected, Bird? hovered, Color ringColor)
    {
        DrawFeeders(view);
        DrawFood(view);
        DrawGunk(view);
        DrawCoins(view);
        DrawBirds(view, selected, hovered, ringColor);
    }

    // Each pile is drawn as a scatter of small grains, shaped and spread by its seed type (GrainLook in Seeds.cs).
    // The grains are only for show: each bite eaten removes GrainsPerBite of them.
    // A grain's spot comes from its pile, seed and number, so the rest stay put while others disappear.
    // As seed goes bad its grains darken toward gunk and slump flatter and closer together.
    // Seed in a tray stays inside its rim; seed in a tube is shown by the tube itself (FeederModel).
    private const int GrainsPerBite = 6;
    private const float TraySpread = 0.24f;

    private void DrawFood(ViewWindow view)
    {
        foreach (Food food in World.Instance.GetAllEntities<Food>())
        {
            if (!view.Contains(food.Position, 0.5f)) continue;
            if (food.Feeder?.Style == FeederStyle.Tube) continue;

            Vector3 local = view.ToLocal(food.Position);
            foreach ((SeedType seed, float amount) in food.Contents)
            {
                SeedInfo info = Seeds.Get(seed);
                GrainLook look = info.Grain;
                float spread = food.Feeder != null ? MathF.Min(look.Spread, TraySpread) : look.Spread;
                Model model = look.Mesh == GrainMesh.Block ? grainBlock : grainRound;
                float gone = Math.Clamp((food.Rot(seed) - 0.5f) / (Food.GunkAfter - 0.5f), 0f, 1f);    // 0 fresh, 1 gunk
                Vector3 size = look.Size * new Vector3(1f, 1f - 0.5f * gone, 1f);
                Color color = Palette.Mix(info.Color, Gunk.Color, gone);
                int grains = (int)MathF.Ceiling(amount / Food.BiteSize * GrainsPerBite - 0.01f);

                for (int g = 0; g < grains; g++)
                {
                    // The square root spreads grains evenly over the patch instead of bunching at the middle.
                    float angle = Rng.Fixed(food.EntityID, (int)seed, g, 1) * MathF.PI * 2f;
                    float distance = spread * (1f - 0.3f * gone) * MathF.Sqrt(Rng.Fixed(food.EntityID, (int)seed, g, 2));
                    float turn = Rng.Fixed(food.EntityID, (int)seed, g, 3) * 360f;
                    float shade = Rng.Fixed(food.EntityID, (int)seed, g, 4) * 0.3f - 0.15f;

                    Vector3 offset = new Vector3(MathF.Cos(angle) * distance, size.Y * 0.5f, MathF.Sin(angle) * distance);
                    Raylib.DrawModelEx(model, view.BoxToScene(local + offset * view.Zoom), Vector3.UnitY,
                                       turn + view.YawDegrees, size * view.Zoom, Raylib.ColorBrightness(color, shade));
                    DrawStats.CountModel();
                }
            }
        }
    }

    // Gunk is a flat dark blob with a few lumps around it.
    private void DrawGunk(ViewWindow view)
    {
        const int Lumps = 5;
        foreach (Gunk gunk in World.Instance.GetAllEntities<Gunk>())
        {
            if (!view.Contains(gunk.Position, 0.5f)) continue;

            Vector3 local = view.ToLocal(gunk.Position);
            float r = gunk.Radius;
            Vector3 blob = new Vector3(r * 2f, 0.05f, r * 1.7f);
            float blobTurn = Rng.Fixed(gunk.EntityID, 0, 0, 1) * 360f;
            Raylib.DrawModelEx(grainRound, view.BoxToScene(local + new Vector3(0, 0.02f, 0) * view.Zoom), Vector3.UnitY,
                               blobTurn + view.YawDegrees, blob * view.Zoom, Gunk.Color);
            DrawStats.CountModel();

            for (int i = 0; i < Lumps; i++)
            {
                float angle = Rng.Fixed(gunk.EntityID, 1, i, 1) * MathF.PI * 2f;
                float distance = r * 0.7f * Rng.Fixed(gunk.EntityID, 1, i, 2);
                float lumpSize = r * (0.3f + 0.3f * Rng.Fixed(gunk.EntityID, 1, i, 3));
                float shade = Rng.Fixed(gunk.EntityID, 1, i, 4) * 0.3f - 0.2f;

                Vector3 offset = new Vector3(MathF.Cos(angle) * distance, 0.04f, MathF.Sin(angle) * distance);
                Vector3 lump = new Vector3(lumpSize, lumpSize * 0.5f, lumpSize);
                Raylib.DrawModelEx(grainRound, view.BoxToScene(local + offset * view.Zoom), Vector3.UnitY,
                                   view.YawDegrees, lump * view.Zoom, Raylib.ColorBrightness(Gunk.Color, shade));
                DrawStats.CountModel();
            }
        }
    }

    // Feeders standing in view, plus the see-through one following the mouse while placing.
    private void DrawFeeders(ViewWindow view)
    {
        foreach (Feeder feeder in World.Instance.GetAllEntities<Feeder>())
        {
            if (!view.Contains(feeder.Position, 0.5f)) continue;

            float fill = feeder.Held / feeder.Info.Capacity;
            Color seedColor = feeder.Seed != null ? Seeds.Get(feeder.Seed.MainSeed).Color : Color.Blank;
            FeederModel.Draw(shapes, view.BoxToScene(view.ToLocal(feeder.Position)), view.Zoom, view.YawDegrees,
                             feeder.Style, fill, seedColor, Color.White);
        }

        // The engine works out where the mouse points on the floor; null when it isn't over the clear floor.
        GameEngine engine = GameEngine.Instance;
        Feeder? carried = engine.Placer.Carried;
        if (carried != null && engine.PlacingSpot != null)
        {
            Color ghost = new Color(160, 255, 240, 130);
            FeederModel.Draw(shapes, view.BoxToScene(engine.PlacingSpot.Value), view.Zoom, view.YawDegrees,
                             carried.Style, carried.Held / carried.Info.Capacity, Color.White, ghost);
        }
    }

    // Coins stand on their edge and spin, so they catch the eye from any angle.
    private void DrawCoins(ViewWindow view)
    {
        foreach (Coin coin in World.Instance.GetAllEntities<Coin>())
        {
            if (!view.Contains(coin.Position, 0.5f)) continue;

            float spin = (float)Raylib.GetTime() * 3f + coin.EntityID + view.Yaw;
            coinModel.Transform = Raymath.MatrixMultiply(Raymath.MatrixRotateX(MathF.PI / 2f), Raymath.MatrixRotateY(spin));

            Color gold = new Color(255, 200, 60, (int)(255 * coin.Alpha));
            Raylib.DrawModel(coinModel, view.BoxToScene(view.ToLocal(coin.Position)), view.Zoom, gold);
            DrawStats.CountModel();
        }
    }

    private void DrawBirds(ViewWindow view, Bird? selected, Bird? hovered, Color ringColor)
    {
        // Animation follows simulation time, so it freezes when paused and speeds up with the sim.
        GameEngine engine = GameEngine.Instance;
        float animDt = engine.Paused ? 0f : Raylib.GetFrameTime() * engine.SimSpeed;

        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (!view.Contains(bird.Position, 0.5f)) continue;

            BirbMotion motion = animator.Animate(bird, animDt);
            Vector3 scenePos = view.BoxToScene(view.ToLocal(bird.Position)) + new Vector3(0f, motion.Bounce * view.Zoom, 0f);
            Color tint = Color.White;
            if (bird == selected) tint = new Color(150, 200, 255, 255);
            else if (bird == hovered) tint = new Color(200, 230, 255, 255);

            BirbBuilder.Draw(bird.Shape, scenePos, bird.Heading + view.Yaw, bird.Size * view.Zoom, tint, motion);

            if (bird == selected)
            {
                Vector3 ringPos = new Vector3(scenePos.X, 0.02f, scenePos.Z);
                Raylib.DrawCircle3D(ringPos, bird.FootprintRadius * 1.1f * view.Zoom, Vector3.UnitX, 90f, ringColor);
            }
        }
        animator.ForgetUnseen();
    }
}
