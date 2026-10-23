using System.Numerics;
using Raylib_cs;

// A unit box, cylinder and sphere, stretched to size when drawn, all drawn with one shader.
// The room and the table props use a set lit by room.fs; the Gilded Granary uses a set with hologram.fs.
public class UnitShapes
{
    private Model cube, cylinder, sphere;

    public void Load(Shader shader)
    {
        cube = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
        cylinder = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(1f, 1f, 24));    // stands on its base
        sphere = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(1f, 12, 12));
        Raylib.SetMaterialShader(ref cube, 0, ref shader);
        Raylib.SetMaterialShader(ref cylinder, 0, ref shader);
        Raylib.SetMaterialShader(ref sphere, 0, ref shader);
    }

    public void Unload()
    {
        Raylib.UnloadModel(cube);
        Raylib.UnloadModel(cylinder);
        Raylib.UnloadModel(sphere);
    }

    // A box centered on center, turned by angleDegrees around the axis (straight up if none is given).
    public void Box(Vector3 center, Vector3 size, Color color, Vector3? axis = null, float angleDegrees = 0f)
    {
        Raylib.DrawModelEx(cube, center, axis ?? Vector3.UnitY, angleDegrees, size, color);
        DrawStats.CountModel();
    }

    // An upright cylinder standing on basePoint, turned around the up axis by yawDegrees.
    public void Cylinder(Vector3 basePoint, float radius, float height, Color color, float yawDegrees = 0f)
    {
        Raylib.DrawModelEx(cylinder, basePoint, Vector3.UnitY, yawDegrees, new Vector3(radius, height, radius), color);
        DrawStats.CountModel();
    }

    // A cylinder lying along any direction: its base circle sits on basePoint and it reaches length along direction.
    // Used for knobs and round fronts that face outward instead of standing up.
    public void CylinderAlong(Vector3 basePoint, Vector3 direction, float radius, float length, Color color)
    {
        Vector3 dir = Vector3.Normalize(direction);
        Vector3 axis = Vector3.Cross(Vector3.UnitY, dir);
        float angle;
        if (axis.LengthSquared() < 1e-6f)
        {
            axis = Vector3.UnitX;
            angle = dir.Y > 0f ? 0f : 180f;
        }
        else
        {
            angle = MathF.Acos(Math.Clamp(dir.Y, -1f, 1f)) * Raylib.RAD2DEG;
            axis = Vector3.Normalize(axis);
        }
        Raylib.DrawModelEx(cylinder, basePoint, axis, angle, new Vector3(radius, length, radius), color);
        DrawStats.CountModel();
    }

    // A sphere squashed or stretched to these radii along X, Y and Z, turned around the up axis by yawDegrees.
    public void Ball(Vector3 center, Vector3 radii, Color color, float yawDegrees = 0f)
    {
        Raylib.DrawModelEx(sphere, center, Vector3.UnitY, yawDegrees, radii, color);
        DrawStats.CountModel();
    }
}
