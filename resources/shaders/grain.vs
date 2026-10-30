#version 330

// Seed grains, drawn many at once (instancing, see WorldDrawer.DrawFood). Paired with hologram.fs.
// Each grain brings its own transform in instanceTransform. Its color rides along in the matrix's
// bottom row (unused by a normal transform), and is put back to 0, 0, 0, 1 before the matrix is used.

// Input vertex attributes
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in mat4 instanceTransform;

// Input uniform values
uniform mat4 mvp;

// Output to fragment shader
out vec3 fragPosition;
out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 fragNormal;

void main()
{
    mat4 model = instanceTransform;
    vec3 color = vec3(model[0][3], model[1][3], model[2][3]);
    model[0][3] = 0.0;
    model[1][3] = 0.0;
    model[2][3] = 0.0;
    model[3][3] = 1.0;

    // fragPosition is in scene space, like hologram.vs
    fragPosition = vec3(model * vec4(vertexPosition, 1.0));
    fragTexCoord = vertexTexCoord;
    fragColor = vec4(color, 1.0);
    fragNormal = normalize(transpose(inverse(mat3(model))) * vertexNormal);

    gl_Position = mvp * model * vec4(vertexPosition, 1.0);
}
