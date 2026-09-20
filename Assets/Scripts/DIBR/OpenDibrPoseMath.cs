using Thesis.Calibration;
using UnityEngine;

namespace Thesis.Dibr
{
    // Converts calibration ExtrinsicsData (OpenCV board→camera rvec/tvec,
    // right-handed — see CameraCalibrationData.cs's own comment: "P_cam =
    // R(rvec) * P_board + tvec") into a camera pose expressed IN BOARD FRAME
    // — the one shared reference space both cameras in a pair are calibrated
    // against, used here as DIBR's common "world" for lerping between them.
    //
    // Deliberately avoids Quaternion.AngleAxis to construct the rotation:
    // AngleAxis bakes in Unity's own left-handed rotation convention, which
    // would silently flip the sign of a right-handed OpenCV/Rodrigues
    // rotation. Building the quaternion from raw sin/cos components instead
    // keeps this handedness-neutral pure math — Quaternion is used only as a
    // 4-float container plus Unity's generic (also handedness-neutral)
    // Quaternion.Inverse/Slerp and the '*' vector-rotation operator, never a
    // convention-baking constructor.
    public static class OpenDibrPoseMath
    {
        public static void ExtrinsicsToBoardPose(ExtrinsicsData e, out Vector3 position, out Quaternion rotation)
        {
            var rvec = new Vector3(e.rvec[0], e.rvec[1], e.rvec[2]);
            var tvec = new Vector3(e.tvec[0], e.tvec[1], e.tvec[2]);
            float theta = rvec.magnitude;

            Quaternion boardToCam;
            if (theta < 1e-8f)
            {
                boardToCam = Quaternion.identity;
            }
            else
            {
                Vector3 axis = rvec / theta;
                float half = theta * 0.5f;
                float s = Mathf.Sin(half);
                boardToCam = new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(half));
            }

            // Camera orientation in board frame = inverse of board→camera.
            rotation = Quaternion.Inverse(boardToCam);
            // Camera origin in board frame: the P_board that satisfies
            // R*P_board + t = 0, i.e. P_board = R^-1 * (-t).
            position = rotation * (-tvec);
        }

        // For OpenDIBR's add_camera Rotation field, which (confirmed by
        // opendibr-c3, 2026-09-20) is 3 floats — Rodrigues/axis-angle, same
        // convention as calibration's own rvec — NOT the 4-float quaternion
        // Item 2's live pose channel uses. Inverse of the sin/cos
        // construction in ExtrinsicsToBoardPose above, same handedness-
        // neutral reasoning applies (q built from raw components, not
        // AngleAxis, so this conversion doesn't need to un-bake anything).
        public static Vector3 QuaternionToRodrigues(Quaternion q)
        {
            float w = Mathf.Clamp(q.w, -1f, 1f);
            float angle = 2f * Mathf.Acos(w);
            float s = Mathf.Sqrt(Mathf.Max(1f - w * w, 0f));
            if (s < 1e-6f) return Vector3.zero; // angle ~ 0 — axis choice doesn't matter
            return new Vector3(q.x, q.y, q.z) / s * angle;
        }

        // Interpolated pose along the straight-line path between two
        // extrinsics-derived poses, both already in the same board frame
        // (only valid for a pair calibrated against the same physical board —
        // true for any two cameras returned together by
        // GET /calibration-data/pair, per the server's per-slot calibration
        // storage).
        public static void Lerp(ExtrinsicsData a, ExtrinsicsData b, float t, out Vector3 position, out Quaternion rotation)
        {
            ExtrinsicsToBoardPose(a, out var posA, out var rotA);
            ExtrinsicsToBoardPose(b, out var posB, out var rotB);
            position = Vector3.Lerp(posA, posB, Mathf.Clamp01(t));
            rotation = Quaternion.Slerp(rotA, rotB, Mathf.Clamp01(t));
        }
    }
}
