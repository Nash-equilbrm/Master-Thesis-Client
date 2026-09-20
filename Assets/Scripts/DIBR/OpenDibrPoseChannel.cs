using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Thesis.Dibr
{
    // Sends the virtual camera pose to OpenDIBR's external pose-input channel
    // (Item 2 of Master-Thesis-Reports/handoff_spec_Sep_20th_opendibr_live_control_and_export.md).
    // Fire-and-forget UDP — OpenDIBR falls back to keyboard/mouse if nothing
    // arrives for 250ms, so a dropped packet here is harmless, not retried.
    //
    // NOTE: the position/rotation passed in here are expected to already be
    // in the SAME convention as OpenDIBR's own calibration.json
    // Position/Rotation fields (i.e. computed directly from calibration
    // extrinsics — see OpenDibrPoseMath — not read from a Unity Transform).
    // This is intentional: those JSON fields were themselves always populated
    // straight from OpenCV calibration data, so the live per-frame pose sent
    // here should need no engine-specific axis conversion. The handoff spec's
    // Item 2 still asks the OpenDIBR side to confirm/document this — if that
    // turns out wrong, SendPose's caller (not this class) is where a
    // conversion would need to be inserted, since this class is a dumb wire
    // sender with no opinion on convention.
    public sealed class OpenDibrPoseChannel : IDisposable
    {
        private const int Port = 40123;
        private const int PacketFloatCount = 7; // pos.xyz + rot.xyzw

        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;
        private readonly byte[] _buffer = new byte[PacketFloatCount * sizeof(float)];

        public OpenDibrPoseChannel()
        {
            _client = new UdpClient();
            _endpoint = new IPEndPoint(IPAddress.Loopback, Port);
        }

        public void SendPose(Vector3 position, Quaternion rotation)
        {
            WriteFloat(0, position.x);
            WriteFloat(1, position.y);
            WriteFloat(2, position.z);
            WriteFloat(3, rotation.x);
            WriteFloat(4, rotation.y);
            WriteFloat(5, rotation.z);
            WriteFloat(6, rotation.w);

            try
            {
                _client.Send(_buffer, _buffer.Length, _endpoint);
            }
            catch (Exception e)
            {
                // Best-effort — a send failure just means this frame's pose
                // update is dropped; OpenDIBR's own 250ms fallback covers it.
                Debug.LogWarning($"[OpenDibrPoseChannel] Send failed: {e.Message}");
            }
        }

        private void WriteFloat(int floatIndex, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Array.Copy(bytes, 0, _buffer, floatIndex * sizeof(float), sizeof(float));
        }

        public void Dispose() => _client?.Dispose();
    }
}
