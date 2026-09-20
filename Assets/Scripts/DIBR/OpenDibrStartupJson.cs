using System.Globalization;
using System.Text;

namespace Thesis.Dibr
{
    // Builds OpenDIBR's startup JSON — required at process launch (`-j
    // <path>`), can't be empty (confirmed by opendibr-c3, 2026-09-20: a hard
    // check in main.cpp rejects a zero-camera list, and BInitGL()
    // unconditionally demuxes+decodes every listed camera before the app
    // finishes booting). Two fixed entries, matching their exact verified
    // shape:
    //   1. The viewport camera (NameColor == "viewport", a magic sentinel,
    //      no "name"/NameDepth/BitDepth* keys at all) — its Resolution sizes
    //      both the window (if any) and the frame-export shared memory.
    //      Position/Rotation here are placeholders; Item 2's pose channel
    //      drives the actual live output pose from the moment a transition
    //      starts.
    //   2. A permanently-running placeholder input camera (served by the
    //      bridge, see dibr-bridge/bridge/session.py's PLACEHOLDER_NAME) —
    //      exists purely to satisfy the "at least one decodable camera at
    //      launch" check. Its Resolution is what future add_camera calls get
    //      compared against, NOT the viewport's — must match the real
    //      cameras' resolution.
    //
    // Built via manual string construction rather than JsonUtility: the
    // viewport entry must OMIT "name"/"NameDepth"/"BitDepthColor"/
    // "BitDepthDepth" entirely (not just leave them empty/zero) to match
    // opendibr-c3's exact verified example — JsonUtility has no field-
    // omission support.
    public static class OpenDibrStartupJson
    {
        public static string Build(int width, int height, string placeholderColorUrl, string placeholderDepthUrl)
        {
            // Cosmetic-only: the viewport is never itself "rendered from" its
            // own Focal/Principle_point (it's driven live via Item 2), and
            // the placeholder is never selected into an active pair at all.
            float focal = width;
            float cx = width / 2f;
            float cy = height / 2f;

            var sb = new StringBuilder();
            sb.Append("{\n  \"Axial_system\": \"OPENGL\",\n  \"cameras\": [\n");

            AppendCameraEntry(sb, name: null, nameColor: "viewport", nameDepth: null,
                position: (0f, 0f, 0f), rotation: (0f, 0f, 0f),
                depthRange: (0.1f, 30f), resolution: (width, height),
                focal: (focal, focal), principlePoint: (cx, cy),
                bitDepth: null);
            sb.Append(",\n");

            AppendCameraEntry(sb, name: "__placeholder__", nameColor: placeholderColorUrl, nameDepth: placeholderDepthUrl,
                position: (0f, 0f, -1f), rotation: (0f, 0f, 0f),
                depthRange: (0.1f, 30f), resolution: (width, height),
                focal: (focal, focal), principlePoint: (cx, cy),
                bitDepth: (8, 8));
            sb.Append("\n  ]\n}\n");

            return sb.ToString();
        }

        private static void AppendCameraEntry(StringBuilder sb,
            string name, string nameColor, string nameDepth,
            (float x, float y, float z) position, (float x, float y, float z) rotation,
            (float near, float far) depthRange, (int w, int h) resolution,
            (float fx, float fy) focal, (float cx, float cy) principlePoint,
            (int color, int depth)? bitDepth)
        {
            sb.Append("    {\n");
            if (name != null) sb.Append($"      \"name\": \"{Escape(name)}\",\n");
            sb.Append($"      \"NameColor\": \"{Escape(nameColor)}\",\n");
            if (nameDepth != null) sb.Append($"      \"NameDepth\": \"{Escape(nameDepth)}\",\n");
            sb.Append($"      \"Position\": [{F(position.x)}, {F(position.y)}, {F(position.z)}],\n");
            sb.Append($"      \"Rotation\": [{F(rotation.x)}, {F(rotation.y)}, {F(rotation.z)}],\n");
            sb.Append($"      \"Depth_range\": [{F(depthRange.near)}, {F(depthRange.far)}],\n");
            sb.Append($"      \"Resolution\": [{resolution.w}, {resolution.h}],\n");
            sb.Append("      \"Projection\": \"Perspective\",\n");
            sb.Append($"      \"Focal\": [{F(focal.fx)}, {F(focal.fy)}],\n");
            sb.Append($"      \"Principle_point\": [{F(principlePoint.cx)}, {F(principlePoint.cy)}]");
            if (bitDepth.HasValue)
                sb.Append($",\n      \"BitDepthColor\": {bitDepth.Value.color},\n      \"BitDepthDepth\": {bitDepth.Value.depth}");
            sb.Append("\n    }");
        }

        private static string F(float v) => v.ToString(CultureInfo.InvariantCulture);
        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
