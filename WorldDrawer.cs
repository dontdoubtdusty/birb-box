using System.Numerics;
using Raylib_cs;

// Draws the bird world's contents inside the hologram: floor, feeders, food, coins and birds.
// Used by both the main scene pass and the zoo camera. Nothing in here changes the simulation.
public class WorldDrawer
{
    private Shader holoShader;
    private int locIsFloor;
    private int locWorldHalfSize;
    private int locFlatShade;
    private int locMoss;

    // The ground wear map (GroundWear) as a two-channel texture, handed to the floor as its second texture (texture1):
    // the first channel is the wear, the second (read as alpha in the shader) the lush grass around rocks
    private Texture2D wearTexture;
    private byte[] wearPixels = Array.Empty<byte>();
    private int wearVersion = -1;
    private double wearUploadedAt = -1.0;
    private const double WearUploadInterval = 0.2;    // seconds of real time between uploads

    private Model floorModel;
    private Model grainRound;    // unit-sized; each seed type stretches these into its own grain shape
    private Model grainBlock;
    private Model coinModel;
    private Model rockModel;     // a low, faceted sphere; each rock is a few of these squashed together
    private readonly BirbAnimator animator = new BirbAnimator();
    private readonly UnitShapes shapes = new UnitShapes();    // for feeders

    public void Load(Shader holo, ViewWindow view)
    {
        holoShader = holo;
        locIsFloor = Raylib.GetShaderLocation(holoShader, "isFloor");
        locWorldHalfSize = Raylib.GetShaderLocation(holoShader, "worldHalfSize");
        locFlatShade = Raylib.GetShaderLocation(holoShader, "flatShade");
        locMoss = Raylib.GetShaderLocation(holoShader, "moss");

        // A square floor big enough for the widest the cube can be; the shader's fog cuts off the rest.
        float floorSize = view.HalfSize * ViewWindow.MaxCubeScale * 2f;
        floorModel = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(floorSize, floorSize, 1, 1));
        grainRound = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(0.5f, 6, 8));
        grainBlock = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
        coinModel = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(Coin.Radius, 0.03f, 20));
        rockModel = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(0.5f, 6, 9));
        shapes.Load(holoShader);

        Raylib.SetMaterialShader(ref floorModel, 0, ref holo);
        Raylib.SetMaterialShader(ref grainRound, 0, ref holo);
        Raylib.SetMaterialShader(ref grainBlock, 0, ref holo);
        Raylib.SetMaterialShader(ref coinModel, 0, ref holo);
        Raylib.SetMaterialShader(ref rockModel, 0, ref holo);
    }

    public void Unload()
    {
        Raylib.UnloadModel(floorModel);
        if (wearTexture.Id != 0) Raylib.UnloadTexture(wearTexture);
        Raylib.UnloadModel(grainRound);
        Raylib.UnloadModel(grainBlock);
        Raylib.UnloadModel(coinModel);
        Raylib.UnloadModel(rockModel);
        shapes.Unload();
    }

    public void DrawFloor(ViewWindow view)
    {
        RefreshWearTexture(GameEngine.Instance.Wear);
        Raylib.SetShaderValue(holoShader, locWorldHalfSize, World.Instance.HalfSize, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(holoShader, locIsFloor, 1, ShaderUniformDataType.Int);
        Raylib.DrawModelEx(floorModel, Vector3.Zero, Vector3.UnitY, view.YawDegrees, Vector3.One, Color.White);
        DrawStats.CountModel();
        Raylib.SetShaderValue(holoShader, locIsFloor, 0, ShaderUniformDataType.Int);
    }

    // Copies the wear map into its texture when it has changed (a few times a second at most).
    // Made, or made again, when the grid's size doesn't match the texture.
    private void RefreshWearTexture(GroundWear wear)
    {
        if (wear.Size == 0) return;

        if (wearTexture.Id == 0 || wearTexture.Width != wear.Size)
        {
            if (wearTexture.Id != 0) Raylib.UnloadTexture(wearTexture);
            Image image = Raylib.GenImageColor(wear.Size, wear.Size, Color.Black);
            Raylib.ImageFormat(ref image, PixelFormat.UncompressedGrayAlpha);
            wearTexture = Raylib.LoadTextureFromImage(image);
            Raylib.UnloadImage(image);
            Raylib.SetTextureFilter(wearTexture, TextureFilter.Bilinear);
            Raylib.SetTextureWrap(wearTexture, TextureWrap.Clamp);
            Raylib.SetMaterialTexture(ref floorModel, 0, MaterialMapIndex.Metalness, ref wearTexture);
            wearPixels = new byte[wear.Size * wear.Size * 2];
            wearVersion = -1;
        }

        double now = Raylib.GetTime();
        if (wear.Version == wearVersion || now - wearUploadedAt < WearUploadInterval) return;

        float[] cells = wear.Cells;
        float[] lush = wear.Lush;
        for (int i = 0; i < cells.Length; i++)
        {
            wearPixels[i * 2] = (byte)(cells[i] * 255f);
            wearPixels[i * 2 + 1] = (byte)(lush[i] * 255f);
        }
        Raylib.UpdateTexture(wearTexture, wearPixels);
        wearVersion = wear.Version;
        wearUploadedAt = now;
    }

    // Everything standing on the floor. ringColor is the ring under the selected bird or feeder.
    public void DrawContents(ViewWindow view, Bird? selected, Bird? hovered, Color ringColor)
    {
        DrawRocks(view);
        DrawFeeders(view, ringColor);
        DrawFood(view);
        DrawCoins(view);
        DrawBirds(view, selected, hovered, ringColor);
    }

    // Each pile is drawn as a scatter of small grains, shaped and spread by its seed type (GrainLook in Seeds.cs).
    // The grains are only for show: each bite eaten removes GrainsPerBite of them.
    // A grain's spot comes from its pile, seed and number, so the rest stay put while others disappear.
    // As seed goes bad its grains darken and slump flatter and closer together, until the pile fades into the dirt.
    // Seed in a tray stays inside its rim; seed in a tube is shown by the tube itself (FeederModel).
    private const int GrainsPerBite = 6;
    private const float TraySpread = 0.24f * Feeders.WorldScale;
    private static readonly Color RotColor = new Color(78, 74, 40, 255);    // the dark brown spoiling seed turns

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
                float gone = Math.Clamp((food.Rot(seed) - 0.5f) / (Food.FadeAfter - 0.5f), 0f, 1f);    // 0 fresh, 1 rotted through
                Vector3 size = look.Size * new Vector3(1f, 1f - 0.5f * gone, 1f);
                Color color = Palette.Mix(info.Color, RotColor, gone);
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

    // A rock is one squashed dome (its true shape, see Rock.HeightAt) with a few lower lumps around its sides,
    // all faceted like the birbs. Lumps, turn, shade and how mossy its top is come from the rock's Look,
    // so each rock always looks the same. The moss itself is drawn by hologram.fs.
    private static readonly Color StoneColor = new Color(120, 114, 104, 255);
    private const float MossMin = 0.4f, MossMax = 1f;

    private void DrawRocks(ViewWindow view)
    {
        Raylib.SetShaderValue(holoShader, locFlatShade, 1, ShaderUniformDataType.Int);
        foreach (Rock rock in World.Instance.GetAllEntities<Rock>())
        {
            if (!view.Contains(rock.Position, rock.Radius + 0.5f)) continue;

            Vector3 local = view.ToLocal(rock.Position);
            int look = rock.Look;
            float shade = Rng.Fixed(look, 0, 0, 1) * 0.3f - 0.15f;
            Color stone = Raylib.ColorBrightness(Palette.Mix(StoneColor, new Color(150, 128, 104, 255), Rng.Fixed(look, 0, 0, 2) * 0.6f), shade);
            float turn = Rng.Fixed(look, 0, 0, 3) * 360f;
            float moss = float.Lerp(MossMin, MossMax, Rng.Fixed(look, 2, 0, 0));
            Raylib.SetShaderValue(holoShader, locMoss, moss, ShaderUniformDataType.Float);

            Vector3 dome = new Vector3(rock.Radius * 2f, rock.Height * 2f, rock.Radius * 2f);
            Raylib.DrawModelEx(rockModel, view.BoxToScene(local), Vector3.UnitY, turn + view.YawDegrees, dome * view.Zoom, stone);
            DrawStats.CountModel();

            int lumps = 2 + (int)(Rng.Fixed(look, 1, 0, 0) * 2f);
            for (int i = 0; i < lumps; i++)
            {
                float angle = (Rng.Fixed(look, 1, i, 1) + i / (float)lumps) * MathF.PI * 2f;
                float distance = rock.Radius * (0.45f + 0.15f * Rng.Fixed(look, 1, i, 2));
                float lumpRadius = rock.Radius * (0.4f + 0.15f * Rng.Fixed(look, 1, i, 3));
                float lumpHeight = rock.Height * (0.55f + 0.2f * Rng.Fixed(look, 1, i, 4));
                float lumpShade = Rng.Fixed(look, 1, i, 5) * 0.16f - 0.08f;

                Vector3 offset = new Vector3(MathF.Cos(angle) * distance, 0f, MathF.Sin(angle) * distance);
                Vector3 size = new Vector3(lumpRadius * 2f, lumpHeight * 2f, lumpRadius * 2f);
                Raylib.DrawModelEx(rockModel, view.BoxToScene(local + offset * view.Zoom), Vector3.UnitY,
                                   turn + i * 47f + view.YawDegrees, size * view.Zoom, Raylib.ColorBrightness(stone, lumpShade));
                DrawStats.CountModel();
            }
        }
        Raylib.SetShaderValue(holoShader, locFlatShade, 0, ShaderUniformDataType.Int);
        Raylib.SetShaderValue(holoShader, locMoss, 0f, ShaderUniformDataType.Float);
    }

    // Feeders standing in view (a ring under the selected one), plus the see-through one following the mouse while placing.
    private void DrawFeeders(ViewWindow view, Color ringColor)
    {
        GameEngine engine = GameEngine.Instance;
        foreach (Feeder feeder in World.Instance.GetAllEntities<Feeder>())
        {
            if (!view.Contains(feeder.Position, feeder.Info.FootRadius + 0.5f)) continue;

            float fill = feeder.Held / feeder.Info.Capacity;
            Color seedColor = feeder.Seed != null ? Seeds.Get(feeder.Seed.MainSeed).Color : Color.Blank;
            Vector3 scenePos = view.BoxToScene(view.ToLocal(feeder.Position));
            FeederModel.Draw(shapes, scenePos, view.Zoom * Feeders.WorldScale, view.YawDegrees,
                             feeder.Style, fill, seedColor, Color.White);

            if (feeder == engine.SelectedFeeder)
            {
                float ringRadius = (feeder.Info.FootRadius + 0.25f) * view.Zoom;
                Raylib.DrawCircle3D(scenePos + new Vector3(0f, 0.02f, 0f), ringRadius, Vector3.UnitX, 90f, ringColor);
            }
        }

        // The engine works out where the mouse points on the floor; null when it isn't over the clear floor.
        Feeder? carried = engine.Placer.Carried;
        if (carried != null && engine.PlacingSpot != null)
        {
            Color ghost = new Color(160, 255, 240, 130);
            FeederModel.Draw(shapes, view.BoxToScene(engine.PlacingSpot.Value), view.Zoom * Feeders.WorldScale, view.YawDegrees,
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
                float feet = bird.Mover.IsPerched ? (bird.Position.Y - bird.GroundY) * view.Zoom : 0f;    // on top of the rock
                Vector3 ringPos = new Vector3(scenePos.X, 0.02f + feet, scenePos.Z);
                Raylib.DrawCircle3D(ringPos, bird.FootprintRadius * 1.1f * view.Zoom, Vector3.UnitX, 90f, ringColor);
            }
        }
        animator.ForgetUnseen();
    }
}
