using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Thesis.Dibr
{
    // Client for OpenDIBR's dynamic camera stream add/remove + active-pair
    // control channel (Item 4 of
    // Master-Thesis-Reports/handoff_spec_Sep_20th_opendibr_live_control_and_export.md,
    // localhost TCP port 40124, newline-delimited JSON). Field names below
    // are the exact confirmed schema from opendibr-c3 (2026-09-20, Item 4
    // done + verified end-to-end) — deliberately identical to OpenDIBR's
    // pre-existing static-dataset input-camera JSON schema (NameColor,
    // NameDepth, Position, Rotation, Resolution, Depth_range, Focal,
    // Principle_point, BitDepthColor, BitDepthDepth, Projection), just
    // delivered live instead of from a file, plus "cmd"/"name" for the
    // control-channel envelope itself.
    public sealed class OpenDibrControlChannel : IDisposable
    {
        private const int Port = 40124;
        private readonly JsonLineTcpClient _tcp = new(Port);

        [Serializable]
        private class AddCameraRequest
        {
            public string cmd = "add_camera";
            public string name;
            public string NameColor;   // RTSP URL
            public string NameDepth;   // RTSP URL
            // OpenGL convention, no OMAF/COLMAP conversion (confirmed by
            // opendibr-c3 — matches what Item 2's pose channel already
            // assumes). Rotation here is 3 floats (Rodrigues/axis-angle,
            // same convention as calibration's rvec) — NOT the 4-float
            // quaternion Item 2's live pose channel uses; those are two
            // separate fields serving two separate purposes (this one is a
            // static per-camera pose, Item 2 drives the live output camera).
            public float[] Position;   // [x, y, z]
            public float[] Rotation;   // [rx, ry, rz] axis-angle (Rodrigues)
            public int[] Resolution;   // [width, height] — must match whatever OpenDIBR's own startup JSON fixed at process launch, or this call is rejected
            // [near, far], inverse-depth decode convention — see
            // OpenDibrSessionManager's doc comment on why this is captured
            // once at a camera's first pairing and held static afterward
            // rather than updated per set_active_pair call.
            public float[] Depth_range;
            public float[] Focal;          // [fx, fy]
            public float[] Principle_point; // [cx, cy]
            public int BitDepthColor;
            public int BitDepthDepth;
            public string Projection = "Perspective";
        }

        [Serializable]
        private class RemoveCameraRequest
        {
            public string cmd = "remove_camera";
            public string name;
        }

        [Serializable]
        private class SetActivePairRequest
        {
            public string cmd = "set_active_pair";
            public string camA;
            public string camB;
        }

        [Serializable]
        private class Reply
        {
            public bool ok;
            public string error;
        }

        // 20s default: OpenDIBR's add_camera connects to the RTSP stream and
        // warms up its decoder before replying (log shows several seconds of
        // "not enough frames to estimate rate" / "decoding for stream 0
        // failed" before "ready (slot N)"). This is worse for a remote camera
        // whose frames travel laptop → LiveKit → bridge → RTSP → OpenDIBR, so
        // the old 5s timeout fired before OpenDIBR replied even though the add
        // eventually succeeded — wedging the pair (see the idempotent handling
        // in OpenDibrSessionManager.EnsureOpenDibrCameraAsync).
        public async Task AddCameraAsync(string name, string nameColor, string nameDepth,
            float[] focal, float[] principlePoint, float[] position, float[] rotationRodrigues,
            int[] resolution, float[] depthRange, int bitDepthColor, int bitDepthDepth, int timeoutMs = 20000)
        {
            var req = new AddCameraRequest
            {
                name = name, NameColor = nameColor, NameDepth = nameDepth,
                Focal = focal, Principle_point = principlePoint,
                Position = position, Rotation = rotationRodrigues,
                Resolution = resolution, Depth_range = depthRange,
                BitDepthColor = bitDepthColor, BitDepthDepth = bitDepthDepth,
            };
            await SendAndCheck(JsonUtility.ToJson(req), timeoutMs);
        }

        public async Task RemoveCameraAsync(string name, int timeoutMs = 8000) =>
            await SendAndCheck(JsonUtility.ToJson(new RemoveCameraRequest { name = name }), timeoutMs);

        public async Task SetActivePairAsync(string camA, string camB, int timeoutMs = 8000) =>
            await SendAndCheck(JsonUtility.ToJson(new SetActivePairRequest { camA = camA, camB = camB }), timeoutMs);

        private async Task SendAndCheck(string jsonLine, int timeoutMs)
        {
            string replyLine = await _tcp.SendAsync(jsonLine, timeoutMs);
            Reply reply;
            try
            {
                reply = JsonUtility.FromJson<Reply>(replyLine);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Malformed reply from OpenDIBR: {replyLine}", e);
            }

            if (!reply.ok)
                throw new InvalidOperationException($"OpenDIBR rejected command: {reply.error}");
        }

        public void Dispose() => _tcp.Dispose();
    }
}
