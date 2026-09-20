using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Thesis.Dibr
{
    // Client for the DIBR bridge process's control channel (Phase A of the
    // plan — a new Python process, not yet built; this client is written
    // against the contract it needs to expose, mirroring OpenDIBR's Item 4
    // transport shape: localhost TCP, newline-delimited JSON).
    //
    // IMPORTANT asymmetry vs. OpenDIBR's control channel: stereo depth is
    // inherently pairwise (camera A's depth output depends on which camera
    // it's currently matched against), unlike OpenDIBR's raw color/depth RTSP
    // decoding which is per-camera and pairing-independent. So here:
    //   - AddCameraAsync(name) starts that camera's color passthrough
    //     immediately and returns its STABLE color/depth RTSP URLs (the depth
    //     URL exists but carries no valid content yet).
    //   - SetActivePairAsync(camA, camB) is what makes the bridge actually
    //     (re)compute stereo depth for that specific pair and start encoding
    //     real content into both cameras' depth URLs — call this every time
    //     the active pair changes, even if both cameras were already added.
    // The URLs returned by AddCameraAsync never change for that identity, so
    // OpenDIBR only needs AddCameraAsync called once per identity even across
    // repeated re-pairing.
    public sealed class DibrBridgeControlChannel : IDisposable
    {
        private const int Port = 40125;
        private readonly JsonLineTcpClient _tcp = new(Port);

        [Serializable]
        private class AddCameraRequest
        {
            public string cmd = "add_camera";
            public string name; // LiveKit participant identity, e.g. "cam3"
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
        private class AddCameraReply
        {
            public bool ok;
            public string error;
            public string colorUrl;
            public string depthUrl;
        }

        [Serializable]
        private class Reply
        {
            public bool ok;
            public string error;
        }

        // Depth is published as a normalized (not metric) grayscale video —
        // OpenDIBR's own dataset JSON schema already carries a per-camera
        // "Depth_range" field for exactly this (see
        // Master-Thesis-Reports/action_log_Sep_11th_opendibr.md). Unlike a
        // camera's URLs, this range is inherently PAIRING-dependent (stereo
        // depth's scale depends on which partner it's matched against), so
        // it can only be known once set_active_pair actually runs — not at
        // add_camera time.
        [Serializable]
        private class SetActivePairReply
        {
            public bool ok;
            public string error;
            public float depthMinA;
            public float depthMaxA;
            public float depthMinB;
            public float depthMaxB;
        }

        public readonly struct CameraUrls
        {
            public readonly string ColorUrl;
            public readonly string DepthUrl;
            public CameraUrls(string colorUrl, string depthUrl) { ColorUrl = colorUrl; DepthUrl = depthUrl; }
        }

        public readonly struct PairDepthRange
        {
            public readonly float MinA, MaxA, MinB, MaxB;
            public PairDepthRange(float minA, float maxA, float minB, float maxB)
            { MinA = minA; MaxA = maxA; MinB = minB; MaxB = maxB; }
        }

        // Camera subscription + color passthrough startup — bounded by
        // however long LiveKit subscription takes, not stereo depth compute.
        public async Task<CameraUrls> AddCameraAsync(string name, int timeoutMs = 5000)
        {
            string replyLine = await _tcp.SendAsync(JsonUtility.ToJson(new AddCameraRequest { name = name }), timeoutMs);
            AddCameraReply reply;
            try { reply = JsonUtility.FromJson<AddCameraReply>(replyLine); }
            catch (Exception e) { throw new InvalidOperationException($"Malformed reply from bridge: {replyLine}", e); }

            if (!reply.ok) throw new InvalidOperationException($"Bridge rejected add_camera: {reply.error}");
            return new CameraUrls(reply.colorUrl, reply.depthUrl);
        }

        public async Task RemoveCameraAsync(string name, int timeoutMs = 3000) =>
            await SendAndCheck(JsonUtility.ToJson(new RemoveCameraRequest { name = name }), timeoutMs);

        // Triggers a fresh stereo-depth pipeline for this exact pair — not
        // ack'd until real depth frames are flowing, so this can legitimately
        // take longer than a plain add/remove. Returns the depth
        // normalization range for each camera's depth stream under THIS
        // pairing — pass it straight through to OpenDIBR's own
        // SetActivePairAsync (see OpenDibrControlChannel), don't cache it
        // per-camera since it's only valid for this specific pairing.
        public async Task<PairDepthRange> SetActivePairAsync(string camA, string camB, int timeoutMs = 15000)
        {
            string replyLine = await _tcp.SendAsync(JsonUtility.ToJson(new SetActivePairRequest { camA = camA, camB = camB }), timeoutMs);
            SetActivePairReply reply;
            try { reply = JsonUtility.FromJson<SetActivePairReply>(replyLine); }
            catch (Exception e) { throw new InvalidOperationException($"Malformed reply from bridge: {replyLine}", e); }

            if (!reply.ok) throw new InvalidOperationException($"Bridge rejected set_active_pair: {reply.error}");
            return new PairDepthRange(reply.depthMinA, reply.depthMaxA, reply.depthMinB, reply.depthMaxB);
        }

        private async Task SendAndCheck(string jsonLine, int timeoutMs)
        {
            string replyLine = await _tcp.SendAsync(jsonLine, timeoutMs);
            Reply reply;
            try { reply = JsonUtility.FromJson<Reply>(replyLine); }
            catch (Exception e) { throw new InvalidOperationException($"Malformed reply from bridge: {replyLine}", e); }

            if (!reply.ok) throw new InvalidOperationException($"Bridge rejected command: {reply.error}");
        }

        public void Dispose() => _tcp.Dispose();
    }
}
