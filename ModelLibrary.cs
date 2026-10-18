using Raylib_cs;

// Loads each bird model once and shares it between all birds.
// (Before, every Bird loaded its own copy of its .glb file.)
public static class ModelLibrary
{
    private static readonly Dictionary<string, Model> models = new();
    private static readonly Dictionary<string, BoundingBox> bounds = new();

    public static void Load()
    {
        LoadOne("cardinal", "resources/cardinal.glb");
        LoadOne("cardinal_f", "resources/cardinal_f.glb");
        LoadOne("dove", "resources/dove.glb");
        LoadOne("finch", "resources/finch.glb");
    }

    private static void LoadOne(string key, string path)
    {
        Model model = Raylib.LoadModel(path);
        models[key] = model;
        bounds[key] = Raylib.GetModelBoundingBox(model);
    }

    public static Model Get(string key) => models[key];
    public static BoundingBox GetBounds(string key) => bounds[key];

    // Makes every bird model draw with the hologram shader.
    public static void ApplyShader(Shader shader)
    {
        foreach (string key in models.Keys.ToList())
        {
            Model model = models[key];
            for (int i = 0; i < model.MaterialCount; i++)
            {
                Raylib.SetMaterialShader(ref model, i, ref shader);
            }
            models[key] = model;
        }
    }

    public static void Unload()
    {
        foreach (Model model in models.Values)
        {
            Raylib.UnloadModel(model);
        }
        models.Clear();
    }
}
