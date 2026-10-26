using System.Numerics;
using System.Text.Json.Serialization;
using Raylib_cs;

// A birb's body as a list of parts. This is only data: BirbBuilder draws it.
//
// Joints: a part's Offset is its joint, the point it turns around. Each mesh grows from its joint along +Y
// (see PartLibrary), so a beak is turned to point forward and a leg is turned to hang down.
// Units: a primitive is 2 wide and 2 tall before scaling, so Scale is its half-size along each axis.
// Directions: +Z is the way the birb faces, +Y is up, +X is the birb's left side.
// Attaching: a part with a Parent follows that part's animation (a beak turns with the head).
// Offsets are always measured from the birb's middle, never from the parent.

// Tells BirbAnimator how a part moves. None means it only moves with its parent.
public enum PartRole
{
    None,
    Body,
    Head,
    Beak,
    Wing,
    Tail,
    Leg,
    Eye
}

public class BirbPart
{
    public string Name;
    public string Mesh;         // "sphere", "cone", "cube", or a .glb in resources/parts/ (see PartLibrary)
    public Vector3 Offset;      // where the part's joint sits
    public Vector3 Scale;       // half-size along X, Y, Z
    public Vector3 Rotation;    // degrees around X, Y, Z
    public Color Color;
    public PartRole Role = PartRole.None;
    public string? Parent;      // name of the part this one is attached to, or null

    // An empty part for BirbFiles to fill in when loading.
    public BirbPart() : this("Part", "sphere", Vector3.Zero, Vector3.One, Vector3.Zero, Color.White) { }

    public BirbPart(string name, string mesh, Vector3 offset, Vector3 scale, Vector3 rotation, Color color)
    {
        Name = name;
        Mesh = mesh;
        Offset = offset;
        Scale = scale;
        Rotation = rotation;
        Color = color;
    }

    // Scale, then turn around the joint, then move the joint into place.
    public Matrix4x4 LocalMatrix()
    {
        Matrix4x4 scale = Raymath.MatrixScale(Scale.X, Scale.Y, Scale.Z);
        Matrix4x4 turn = Raymath.MatrixRotateXYZ(Rotation * Raylib.DEG2RAD);
        Matrix4x4 move = Raymath.MatrixTranslate(Offset.X, Offset.Y, Offset.Z);
        return Raymath.MatrixMultiply(Raymath.MatrixMultiply(scale, turn), move);
    }

    // The same part on the other side of the birb (for wings, legs and eyes).
    // Flipping X also flips which way it turns around Y and Z.
    public BirbPart Mirrored(string name, string? parent)
    {
        return new BirbPart(
            name, Mesh,
            new Vector3(-Offset.X, Offset.Y, Offset.Z),
            Scale,
            new Vector3(Rotation.X, -Rotation.Y, -Rotation.Z),
            Color)
        { Role = Role, Parent = parent };
    }

    public BirbPart Copy()
    {
        return new BirbPart(Name, Mesh, Offset, Scale, Rotation, Color) { Role = Role, Parent = Parent };
    }

    // Which side of the birb the part is on: 1 for left (+X), -1 for right. Wings flap the opposite way.
    [JsonIgnore]
    public float Side => Offset.X >= 0f ? 1f : -1f;
}

public class BirbShape
{
    public string Name;
    public List<BirbPart> Parts = new();
    public Gait Gait = Gait.Hop;                  // how it moves in the simulation (BirdMover)
    public MotionStyle Style = new MotionStyle(); // how its movements look (BirbAnimator)

    // An empty shape for BirbFiles to fill in when loading.
    public BirbShape() : this("New Birb") { }

    public BirbShape(string name)
    {
        Name = name;
    }

    public BirbShape Add(BirbPart part)
    {
        Parts.Add(part);
        return this;
    }

    // Adds a part on the left and its mirror on the right.
    // The right one gets the same parent unless it needs its own (a right foot hangs from the right leg).
    public BirbShape AddPair(BirbPart left, string rightName, string? rightParent = null)
    {
        Parts.Add(left);
        Parts.Add(left.Mirrored(rightName, rightParent ?? left.Parent));
        return this;
    }

    public BirbPart? Find(string name) => Parts.Find(p => p.Name == name);

    // A separate copy with its own parts, so editing it (in the Designer) leaves the original alone.
    public BirbShape Copy()
    {
        BirbShape copy = new BirbShape(Name) { Gait = Gait, Style = Style.Copy() };
        foreach (BirbPart part in Parts) copy.Parts.Add(part.Copy());
        return copy;
    }

    // The box around every part, in birb units (multiply by the bird's Size for world units).
    // The corners of each mesh's own box are moved into place and the box around all those corners is kept.
    // Needs PartLibrary to be loaded.
    public BoundingBox GetBounds()
    {
        Vector3 min = new Vector3(float.MaxValue);
        Vector3 max = new Vector3(float.MinValue);

        foreach (BirbPart part in Parts)
        {
            Matrix4x4 m = part.LocalMatrix();
            BoundingBox box = PartLibrary.GetBounds(part.Mesh);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 c = new Vector3(
                    (corner & 1) == 0 ? box.Min.X : box.Max.X,
                    (corner & 2) == 0 ? box.Min.Y : box.Max.Y,
                    (corner & 4) == 0 ? box.Min.Z : box.Max.Z);
                Vector3 p = Raymath.Vector3Transform(c, m);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        if (Parts.Count == 0) return new BoundingBox(-Vector3.One, Vector3.One);
        return new BoundingBox(min, max);
    }
}
