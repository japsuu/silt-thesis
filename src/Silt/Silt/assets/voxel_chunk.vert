layout (location = 0) in uint v_packed;

uniform ivec3 u_chunk_pos;
uniform mat4 u_mat_view;
uniform mat4 u_mat_proj;

// Color lookup table
const vec3 COLORS[7] = vec3[7](
    vec3(1.0, 0.0, 0.0),    // red
    vec3(0.0, 1.0, 0.0),    // green
    vec3(0.0, 0.0, 1.0),    // blue
    vec3(1.0, 1.0, 0.0),    // yellow
    vec3(0.0, 1.0, 1.0),    // cyan
    vec3(1.0, 0.0, 1.0),    // magenta
    vec3(1.0, 1.0, 1.0)     // white
);

// Normal lookup table
const vec3 NORMALS[6] = vec3[6](
    vec3( 1.0,  0.0,  0.0),  // +X
    vec3(-1.0,  0.0,  0.0),  // -X
    vec3( 0.0,  1.0,  0.0),  // +Y
    vec3( 0.0, -1.0,  0.0),  // -Y
    vec3( 0.0,  0.0,  1.0),  // +Z
    vec3( 0.0,  0.0, -1.0)   // -Z
);

out vec3 f_color;
out vec3 f_normal;

void main()
{
    // Decode packed vertex data
    uint px = v_packed & 0x3Fu;                // bits 0-5:   local X (0-32)
    uint py = (v_packed >> 6u)  & 0x3Fu;       // bits 6-11:  local Y (0-32)
    uint pz = (v_packed >> 12u) & 0x3Fu;       // bits 12-17: local Z (0-32)
    uint colorIdx  = (v_packed >> 18u) & 0x7u; // bits 18-20: color index (0-6)
    uint normalIdx = (v_packed >> 21u) & 0x7u; // bits 21-23: normal index (0-5)

    vec3 localPos = vec3(float(px), float(py), float(pz));
    vec3 worldPos = u_chunk_pos + localPos;

    gl_Position = u_mat_proj * u_mat_view * vec4(worldPos, 1.0);
    f_color = COLORS[colorIdx];
    f_normal = NORMALS[normalIdx];
}

