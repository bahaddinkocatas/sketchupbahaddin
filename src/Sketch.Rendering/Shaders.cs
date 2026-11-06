namespace Sketch.Rendering;

internal static class Shaders
{
    // ---------------- Wide lines: a geometry shader turns each segment into a screen-space quad ----------------

    public const string LineVertex = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec4 aColor;
        layout(location = 2) in float aWidth;
        layout(location = 3) in float aDashed;
        uniform mat4 uViewProj;
        uniform vec4 uClip;
        out vec4 gColor;
        out float gWidth;
        out float gDashed;
        void main() {
            gl_Position = uViewProj * vec4(aPos, 1.0);
            gl_ClipDistance[0] = dot(uClip.xyz, aPos) + uClip.w;
            gColor = aColor;
            gWidth = aWidth;
            gDashed = aDashed;
        }
        """;

    public const string LineGeometry = """
        #version 330 core
        layout(lines) in;
        layout(triangle_strip, max_vertices = 4) out;
        in vec4 gColor[];
        in float gWidth[];
        in float gDashed[];
        uniform vec2 uViewport;
        out vec4 fColor;
        noperspective out float fDistPx;
        noperspective out float fAcross;
        flat out float fDashed;
        flat out float fHalfWidth;
        void main() {
            vec4 p0 = gl_in[0].gl_Position;
            vec4 p1 = gl_in[1].gl_Position;
            // Clip segments that pass behind the eye (w <= 0, perspective only) so they don't flip. Near/far
            // depth is not clipped here: depth clamping keeps lines visible however far the view is zoomed out.
            float d0 = p0.w - 1e-5;
            float d1 = p1.w - 1e-5;
            if (d0 < 0.0 && d1 < 0.0) return;
            if (d0 < 0.0) p0 = mix(p0, p1, d0 / (d0 - d1));
            else if (d1 < 0.0) p1 = mix(p1, p0, d1 / (d1 - d0));

            vec2 halfVp = 0.5 * uViewport;
            vec2 s0 = p0.xy / p0.w * halfVp;
            vec2 s1 = p1.xy / p1.w * halfVp;
            vec2 dir = s1 - s0;
            float len = length(dir);
            dir = len > 1e-6 ? dir / len : vec2(1.0, 0.0);
            vec2 nrm = vec2(-dir.y, dir.x);
            float hw = max(gWidth[0], 1.0) * 0.5 + 0.5;
            vec2 off = nrm * hw / halfVp;

            fColor = gColor[0];
            fDashed = gDashed[0];
            fHalfWidth = hw;

            float c0 = gl_in[0].gl_ClipDistance[0];
            float c1 = gl_in[1].gl_ClipDistance[0];
            fDistPx = 0.0; fAcross = -hw; gl_ClipDistance[0] = c0;
            gl_Position = vec4((p0.xy / p0.w - off) * p0.w, p0.z, p0.w); EmitVertex();
            fDistPx = 0.0; fAcross = hw; gl_ClipDistance[0] = c0;
            gl_Position = vec4((p0.xy / p0.w + off) * p0.w, p0.z, p0.w); EmitVertex();
            fDistPx = len; fAcross = -hw; gl_ClipDistance[0] = c1;
            gl_Position = vec4((p1.xy / p1.w - off) * p1.w, p1.z, p1.w); EmitVertex();
            fDistPx = len; fAcross = hw; gl_ClipDistance[0] = c1;
            gl_Position = vec4((p1.xy / p1.w + off) * p1.w, p1.z, p1.w); EmitVertex();
            EndPrimitive();
        }
        """;

    public const string LineFragment = """
        #version 330 core
        in vec4 fColor;
        noperspective in float fDistPx;
        noperspective in float fAcross;
        flat in float fDashed;
        flat in float fHalfWidth;
        out vec4 FragColor;
        void main() {
            // 1 = dashes (4 on, 4 off), 2 = fine dots (like SketchUp's negative axes).
            if (fDashed > 1.5) { if (mod(fDistPx, 4.0) > 1.6) discard; }
            else if (fDashed > 0.5 && mod(fDistPx, 8.0) > 4.0) discard;
            // Soft outer half pixel for smoother edges.
            float edge = fHalfWidth - abs(fAcross);
            float alpha = clamp(edge + 0.5, 0.0, 1.0);
            FragColor = vec4(fColor.rgb, fColor.a * alpha);
        }
        """;

    // ---------------- Point markers ----------------

    public const string PointVertex = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec4 aColor;
        layout(location = 2) in float aSize;
        layout(location = 3) in float aShape;
        uniform mat4 uViewProj;
        out vec4 vColor;
        flat out float vShape;
        flat out float vSize;
        void main() {
            gl_Position = uViewProj * vec4(aPos, 1.0);
            gl_PointSize = aSize;
            vColor = aColor;
            vShape = aShape;
            vSize = aSize;
        }
        """;

    public const string PointFragment = """
        #version 330 core
        in vec4 vColor;
        flat in float vShape;
        flat in float vSize;
        out vec4 FragColor;
        void main() {
            vec2 c = gl_PointCoord * 2.0 - 1.0;   // -1..1
            float shape = mod(vShape, 4.0);
            bool filled = vShape >= 4.0;
            float px = 2.0 / vSize;                 // one pixel in point coordinates
            float d;                                // signed distance-ish: <= 0 inside
            if (shape < 0.5) d = max(abs(c.x), abs(c.y)) - 1.0;          // square
            else if (shape < 1.5) d = length(c) - 1.0;                     // circle
            else if (shape < 2.5) d = (abs(c.x) + abs(c.y)) - 1.0;         // diamond
            else d = min(abs(c.x), abs(c.y)) - 1.5 * px;                   // cross
            if (d > 0.0) discard;
            vec3 rgb = vColor.rgb;
            if (shape < 2.5) {
                // Outline in the marker colour; filled markers get a lighter centre for contrast.
                bool border = d > -2.0 * px;
                if (!border) {
                    if (!filled) discard;
                    rgb = mix(rgb, vec3(1.0), 0.15);
                }
            }
            FragColor = vec4(rgb, vColor.a);
        }
        """;

    // ---------------- Faces ----------------

    public const string FaceVertex = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec4 aFront;
        layout(location = 3) in vec4 aBack;
        layout(location = 4) in float aSelected;
        uniform mat4 uViewProj;
        uniform vec4 uClip;
        out vec3 vPos;
        out vec3 vNormal;
        out vec4 vFront;
        out vec4 vBack;
        flat out float vSelected;
        void main() {
            gl_Position = uViewProj * vec4(aPos, 1.0);
            gl_ClipDistance[0] = dot(uClip.xyz, aPos) + uClip.w;
            vPos = aPos;
            vNormal = aNormal;
            vFront = aFront;
            vBack = aBack;
            vSelected = aSelected;
        }
        """;

    public const string FaceFragment = """
        #version 330 core
        in vec3 vPos;
        in vec3 vNormal;
        in vec4 vFront;
        in vec4 vBack;
        flat in float vSelected;
        uniform vec3 uEye;
        uniform vec3 uViewDir;
        uniform int uPerspective;
        uniform vec3 uLightDir;
        uniform int uLit;
        out vec4 FragColor;
        void main() {
            vec3 toEye = uPerspective == 1 ? normalize(uEye - vPos) : -uViewDir;
            vec3 n = normalize(vNormal);
            bool back = dot(n, toEye) < 0.0;
            vec4 base = back ? vBack : vFront;
            if (back) n = -n;
            float diffuse = max(dot(n, uLightDir), 0.0);
            float light = uLit == 1 ? 0.62 + 0.38 * diffuse : 1.0;
            vec3 rgb = base.rgb * light;
            if (vSelected > 0.5) {
                // Selected faces: a fine blue dot screen pattern.
                vec2 cell = mod(gl_FragCoord.xy, 4.0);
                if (cell.x < 1.5 && cell.y < 1.5) rgb = vec3(0.0, 0.36, 0.9);
            }
            FragColor = vec4(rgb, base.a);
        }
        """;

    // ---------------- Flat translucent polygons (tool previews) ----------------

    public const string FlatVertex = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec4 aColor;
        uniform mat4 uViewProj;
        out vec4 vColor;
        void main() {
            gl_Position = uViewProj * vec4(aPos, 1.0);
            vColor = aColor;
        }
        """;

    public const string FlatFragment = """
        #version 330 core
        in vec4 vColor;
        out vec4 FragColor;
        void main() { FragColor = vColor; }
        """;
}
