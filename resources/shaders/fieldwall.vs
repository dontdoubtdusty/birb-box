#version 330

// For the field's scanline walls. The walls are drawn straight in scene space, so the position needs no model matrix.

in vec3 vertexPosition;
in vec4 vertexColor;

uniform mat4 mvp;

out vec3 fragPosition;
out vec4 fragColor;

void main()
{
    fragPosition = vertexPosition;
    fragColor = vertexColor;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
