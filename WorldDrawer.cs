using System.Numerics;
using Raylib_cs;

// Draws the bird world's contents inside the hologram: floor (with the pool's water), feeders, seed heaps, sprouts, splash droplets, coins and birds.
// Used by both the main scene pass and the zoo camera. Nothing in here changes the simulation.
// A partial class: the seed is drawn in SeedDrawing.cs (which also owns the grain batches), the sprouts in SproutDrawing.cs,
// the rain (falling drops, and the floor's wet ground, rain rings and puddles) in RainDrawing.cs.
public partial class WorldDrawer
{
    private Shader holoShader;
    private int locIsFloor;
    private int locWorldHalfSize;
    private int locFlatShade;
    private int locMoss;
    private int locPoolLobeCount, locPoolLobes, locRippleCount, locRipples, locRippleLife;    // the pool, drawn by the floor (see DrawFloor)
    private readonly Vector3[] poolLobes = new Vector3[Pool.MaxLobes];
    private readonly Vector4[] ripples = new Vector4[Pool.MaxRipples];

    // The ground wear map (GroundWear) as a two-channel texture, handed to the floor as its second texture (texture1):
    // the first channel is the wear, the second (read as alpha in the shader) the lush grass around rocks
    private Texture2D wearTexture;
    private byte[] wearPixels = Array.Empty<byte>();
    private int wearVersion = -1;
    private double wearUploadedAt = -1.0;
    private const double WearUploadInterval = 0.2;    // seconds of real time between uploads

    private Model floorModel;
    private Model coinModel;
    private Model rockModel;     // a low, faceted sphere; each rock is a few of these squashed together
    private readonly BirbAnimator animator = new BirbAnimator();
    private readonly UnitShapes shapes = new UnitShapes();    // for feeders

    // grain: the shader for seed grains (grain.vs with hologram.fs), see DrawFood
    public void Load(Shader holo, Shader grain, ViewWindow view)
    {
        holoShader = holo;
        locIsFloor = Raylib.GetShaderLocation(holoShader, "isFloor");
        locWorldHalfSize = Raylib.GetShaderLocation(holoShader, "worldHalfSize");
        locFlatShade = Raylib.GetShaderLocation(holoShader, "flatShade");
        locMoss = Raylib.GetShaderLocation(holoShader, "moss");
        locPoolLobeCount = Raylib.GetShaderLocation(holoShader, "poolLobeCount");
        locPoolLobes = Raylib.GetShaderLocation(holoShader, "poolLobes");
        locRippleCount = Raylib.GetShaderLocation(holoShader, "rippleCount");
        locRipples = Raylib.GetShaderLocation(holoShader, "ripples");
        locRippleLife = Raylib.GetShaderLocation(holoShader, "rippleLife");
        FindWeatherUniforms();

        // A square floor big enough for the widest the cube can be; the shader's fog cuts off the rest.
        float floorSize = view.HalfSize * ViewWindow.MaxCubeScale * 2f;
        floorModel = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(floorSize, floorSize, 1, 1));
        grainRoundMesh = Raylib.GenMeshSphere(0.5f, 6, 8);
        grainBlockMesh = Raylib.GenMeshCube(1f, 1f, 1f);
        grainMaterial = Raylib.LoadMaterialDefault();
        grainMaterial.Shader = grain;
        coinModel = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(Coin.Radius, 0.03f, 20));
        rockModel = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(0.5f, 6, 9));
        shapes.Load(holoShader);

        Raylib.SetMaterialShader(ref floorModel, 0, ref holo);
        Raylib.SetMaterialShader(ref coinModel, 0, ref holo);
        Raylib.SetMaterialShader(ref rockModel, 0, ref holo);
    }

    public void Unload()
    {
        Raylib.UnloadModel(floorModel);
        if (wearTexture.Id != 0) Raylib.UnloadTexture(wearTexture);
        // The grain material isn't unloaded: that would unload its shader too, which HologramRenderer owns.
        Raylib.UnloadMesh(grainRoundMesh);
        Raylib.UnloadMesh(grainBlockMesh);
        Raylib.UnloadModel(coinModel);
        Raylib.UnloadModel(rockModel);
        shapes.Unload();
    }

    public void DrawFloor(ViewWindow view)
    {
        RefreshWearTexture(GameEngine.Instance.Wear);
        SetPoolUniforms();
        SetWeatherUniforms();
        Raylib.SetShaderValue(holoShader, locWorldHalfSize, World.Instance.HalfSize, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(holoShader, locIsFloor, 1, ShaderUniformDataType.Int);
        Raylib.DrawModelEx(floorModel, Vector3.Zero, Vector3.UnitY, view.YawDegrees, Vector3.One, Color.White);
        DrawStats.CountModel();
        Raylib.SetShaderValue(holoShader, locIsFloor, 0, ShaderUniformDataType.Int);
    }

    // Hands the floor shader the pool's shape and its ripple rings (hologram.fs draws the water as part of the floor).
    private void SetPoolUniforms()
    {
        Pool? pool = Pool.Current;
        int lobeCount = pool?.Lobes.Count ?? 0;
        int rippleCount = pool != null ? Math.Min(pool.Ripples.Count, Pool.MaxRipples) : 0;
        for (int i = 0; i < lobeCount; i++)
        {
            Pool.Lobe lobe = pool!.Lobes[i];
            poolLobes[i] = new Vector3(lobe.Center.X, lobe.Center.Y, lobe.Radius);
        }
        for (int i = 0; i < rippleCount; i++)
        {
            Pool.Ripple ripple = pool!.Ripples[i];
            ripples[i] = new Vector4(ripple.Spot.X, ripple.Spot.Y, ripple.Age, ripple.Strength);
        }

        Raylib.SetShaderValue(holoShader, locPoolLobeCount, lobeCount, ShaderUniformDataType.Int);
        if (lobeCount > 0) Raylib.SetShaderValueV(holoShader, locPoolLobes, poolLobes, ShaderUniformDataType.Vec3, lobeCount);
        Raylib.SetShaderValue(holoShader, locRippleCount, rippleCount, ShaderUniformDataType.Int);
        if (rippleCount > 0) Raylib.SetShaderValueV(holoShader, locRipples, ripples, ShaderUniformDataType.Vec4, rippleCount);
        Raylib.SetShaderValue(holoShader, locRippleLife, Pool.RippleLife, ShaderUniformDataType.Float);
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
            Color tint = bird.Plumage;
            if (bird == selected) tint = Palette.Multiply(tint, new Color(150, 200, 255, 255));
            else if (bird == hovered) tint = Palette.Multiply(tint, new Color(200, 230, 255, 255));

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
