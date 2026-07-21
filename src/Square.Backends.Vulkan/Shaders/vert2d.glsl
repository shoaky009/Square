#version 450

layout(push_constant) uniform PC {
    mat4 proj;
} pc;

layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aUV;
layout(location = 2) in vec4 aColor;

layout(location = 0) out vec2 fragUV;
layout(location = 1) out vec4 fragColor;

void main() {
    gl_Position = pc.proj * vec4(aPos, 0.0, 1.0);
    fragUV = aUV;
    fragColor = aColor;
}
