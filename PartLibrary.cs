using System.Numerics;
using Raylib_cs;

// Every mesh a birb part can use: three built-in primitives, plus any .glb file in resources/parts/.
// A part names its mesh with text: "sphere", "cone", "cube", or a file name without ".glb" (e.g. "beak_hooked").
//
// Joint rule: a mesh's origin is the joint it turns around, and the mesh grows from there along +Y.
// The primitives are moved so their bottom sits on the origin. Blender parts: put the origin at the joint.
public static class PartLibrary
{
    public const string Sphere = "sphere";
    public const string Cone = "cone";
    public const string Cube = "cube";

    private const string PartsFolder = "parts";

    // Few rings and slices keep the facets visible. The shader's flatShade switch makes each face one shade.
    private const int SphereRings = 5;
    private const int SphereSlices = 8;
    private const int ConeSlices = 6;

    private class PartMesh
    {
        public Model Model;
        public Matrix4x4 Base;       // moves the mesh so its joint is at the origin (identity for .glb files)
        public BoundingBox Bounds;   // around the mesh after Base is applied
    }

    // Names ignore capital letters, so "Beak_Hooked" and "beak_hooked" are the same part.
    private static readonly Dictionary<string, PartMesh> meshes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> warnedMissing = new(StringComparer.OrdinalIgnoreCase);

    // Every name a part can use, for the Designer's Mesh dropdown.
    public static IEnumerable<string> Names => meshes.Keys;

    public static void Load(Shader shader)
    {
        // Raylib's sphere and cube sit around their middle, so they are lifted by 1 to put their bottom on the joint.
        // Raylib's cone already runs from y = 0 to y = 2.
        Matrix4x4 lift = Raymath.MatrixTranslate(0f, 1f, 0f);
        BoundingBox primitiveBox = new BoundingBox(new Vector3(-1, 0, -1), new Vector3(1, 2, 1));

        AddMesh(Sphere, Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(1f, SphereRings, SphereSlices)), lift, primitiveBox, shader);
        AddMesh(Cone, Raylib.LoadModelFromMesh(Raylib.GenMeshCone(1f, 2f, ConeSlices)), Matrix4x4.Identity, primitiveBox, shader);
        AddMesh(Cube, Raylib.LoadModelFromMesh(Raylib.GenMeshCube(2f, 2f, 2f)), lift, primitiveBox, shader);

        string folder = Assets.PathTo(PartsFolder);
        if (!Directory.Exists(folder)) return;

        foreach (string file in Directory.GetFiles(folder, "*.glb"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            Model model = Raylib.LoadModel(file);
            AddMesh(name, model, Matrix4x4.Identity, Raylib.GetModelBoundingBox(model), shader);
        }
    }

    // A file with a primitive's name (like sphere.glb) replaces that primitive everywhere.
    private static void AddMesh(string name, Model model, Matrix4x4 baseMatrix, BoundingBox bounds, Shader shader)
    {
        if (meshes.TryGetValue(name, out PartMesh? old)) Raylib.UnloadModel(old.Model);

        for (int i = 0; i < model.MaterialCount; i++)
        {
            Raylib.SetMaterialShader(ref model, i, ref shader);
        }
        meshes[name] = new PartMesh { Model = model, Base = baseMatrix, Bounds = bounds };
    }

    public static void Unload()
    {
        foreach (PartMesh mesh in meshes.Values) Raylib.UnloadModel(mesh.Model);
        meshes.Clear();
    }

    public static Model GetModel(string name) => Find(name).Model;
    public static Matrix4x4 GetBase(string name) => Find(name).Base;
    public static BoundingBox GetBounds(string name) => Find(name).Bounds;

    // A name with no mesh (a typo, or a file not made yet) is drawn as a sphere, with one warning in the log.
    private static PartMesh Find(string name)
    {
        if (meshes.TryGetValue(name, out PartMesh? mesh)) return mesh;

        if (warnedMissing.Add(name))
        {
            EventLog.Add($"No birb part called \"{name}\" in resources/{PartsFolder}/, so a sphere stands in.");
        }
        return meshes[Sphere];
    }
}
