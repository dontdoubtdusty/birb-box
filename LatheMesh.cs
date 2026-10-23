using System.Numerics;
using Raylib_cs;

// Builds a round mesh by spinning a side profile around the up axis, like turning a part on a lathe.
// Each profile point is (radius, height), and each pair of neighboring points becomes one band all the way around.
// List the points so the surface you want to see is on your right as you walk along the profile:
// up an outside wall, then inward across a top, then down an inner wall.
// Built once at load, so drawing it costs one draw call instead of many small shapes every frame.
public static class LatheMesh
{
    public static Model Build(Vector2[] profile, int segments, Shader shader)
    {
        int bands = profile.Length - 1;
        Mesh mesh = new Mesh
        {
            VertexCount = bands * segments * 6,
            TriangleCount = bands * segments * 2
        };
        mesh.AllocVertices();
        mesh.AllocNormals();
        mesh.AllocTexCoords();    // unused by the shader, but filled so every vertex has the full set
        Span<Vector3> vertices = mesh.VerticesAs<Vector3>();
        Span<Vector3> normals = mesh.NormalsAs<Vector3>();
        Span<Vector2> texCoords = mesh.TexCoordsAs<Vector2>();
        texCoords.Clear();

        // The six corners of one piece (two triangles), as (which angle, which profile point),
        // in the order that makes them face outward.
        ReadOnlySpan<int> cornerAngle = stackalloc int[] { 1, 1, 0, 1, 0, 0 };
        ReadOnlySpan<int> cornerPoint = stackalloc int[] { 1, 0, 0, 1, 0, 1 };

        int v = 0;
        for (int b = 0; b < bands; b++)
        {
            Vector2 p0 = profile[b], p1 = profile[b + 1];
            // The band's slope turned a quarter to the right: its normal as (outward, up)
            Vector2 n = Vector2.Normalize(new Vector2(p1.Y - p0.Y, -(p1.X - p0.X)));

            for (int s = 0; s < segments; s++)
            {
                for (int k = 0; k < 6; k++)
                {
                    float angle = (s + cornerAngle[k]) * MathF.Tau / segments;
                    Vector2 p = cornerPoint[k] == 0 ? p0 : p1;
                    float c = MathF.Cos(angle), sn = MathF.Sin(angle);
                    vertices[v] = new Vector3(c * p.X, p.Y, sn * p.X);
                    normals[v] = new Vector3(c * n.X, n.Y, sn * n.X);    // smooth around, flat along the profile
                    v++;
                }
            }
        }

        Raylib.UploadMesh(ref mesh, false);
        Model model = Raylib.LoadModelFromMesh(mesh);
        Raylib.SetMaterialShader(ref model, 0, ref shader);
        return model;
    }
}
