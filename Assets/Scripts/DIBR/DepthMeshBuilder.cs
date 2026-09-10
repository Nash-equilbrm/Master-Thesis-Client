using UnityEngine;
using UnityEngine.Rendering;
using Thesis.Calibration;

namespace Thesis.DIBR {

    /// <summary>
    /// Each frame, reads a depth RenderTexture, computes camera-space 3D positions for every
    /// pixel, and writes them into a mesh.  The mesh lives in the camera's local space
    /// (this GameObject should be a child of the camera GameObject, or the camera transform
    /// should be applied externally).
    ///
    /// Coordinate convention: OpenCV camera space (x right, y DOWN, z forward) is converted
    /// to Unity local space by negating Y.  The parent transform handles world placement.
    ///
    /// Assign a material using the DIBR/DepthMesh shader and set ColorTexture each frame.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DepthMeshBuilder : MonoBehaviour {

        [Header("Camera Intrinsics (filled from calibration JSON)")]
        public float fx = 600f, fy = 600f;
        public float cx = 320f, cy = 240f;

        [Header("Depth Range (meters)")]
        public float depthMin = 0.3f;
        public float depthMax = 5.0f;

        [Header("Source Resolution")]
        public int sourceWidth = 1280, sourceHeight = 720;

        [Header("Mesh Quality")]
        [Tooltip("Build mesh at 1/N resolution. 4 = 320x180 for 1280x720 input.")]
        [Range(1, 8)] public int downsampleFactor = 4;
        [Tooltip("Skip triangles whose endpoints differ in depth by more than this (meters). Hides depth edges.")]
        public float depthDiscontinuityThreshold = 0.3f;

        public Texture ColorTexture {
            set {
                if (_renderer && _renderer.material)
                    _renderer.material.mainTexture = value;
            }
        }

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;

        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private int[] _triangles;
        private float[] _depthValues;

        private Texture2D _depthReadback;
        private int _meshW, _meshH;

        private void Awake() {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = $"DepthMesh_{name}" };
            _mesh.indexFormat = IndexFormat.UInt32;
            _filter.sharedMesh = _mesh;
        }

        /// <summary>Copies intrinsics and depth range from loaded calibration data.</summary>
        public void ApplyCalibration(IntrinsicsData intrinsics, float dMin, float dMax) {
            fx = intrinsics.fx;
            fy = intrinsics.fy;
            cx = intrinsics.cx;
            cy = intrinsics.cy;
            sourceWidth = intrinsics.imageWidth;
            sourceHeight = intrinsics.imageHeight;
            depthMin = dMin;
            depthMax = dMax;
        }

        /// <summary>Reads the depth RT, recomputes vertex positions, and uploads the mesh.</summary>
        public void UpdateMesh(RenderTexture depthRT) {
            if (!depthRT) return;

            int rtW = depthRT.width, rtH = depthRT.height;
            EnsureReadbackTex(rtW, rtH);
            ReadPixels(depthRT);

            _meshW = rtW / downsampleFactor;
            _meshH = rtH / downsampleFactor;
            int vertCount = _meshW * _meshH;

            bool layoutChanged = _vertices == null || _vertices.Length != vertCount;
            if (layoutChanged) {
                _vertices = new Vector3[vertCount];
                _uvs = new Vector2[vertCount];
                _depthValues = new float[vertCount];
            }

            // Scale intrinsics from source to actual RT resolution
            float sx = (float)rtW / sourceWidth;
            float sy = (float)rtH / sourceHeight;
            float fxS = fx * sx, fyS = fy * sy;
            float cxS = cx * sx, cyS = cy * sy;

            Color[] pixels = _depthReadback.GetPixels();

            for (int y = 0; y < _meshH; y++) {
                for (int x = 0; x < _meshW; x++) {
                    int srcX = x * downsampleFactor;
                    int srcY = y * downsampleFactor;

                    // Depth Anything V2 (--grayscale): bright = near, dark = far
                    float depthNorm = pixels[srcY * rtW + srcX].r;
                    float d = Mathf.Lerp(depthMax, depthMin, depthNorm);

                    // Camera-space, then flip Y for Unity (OpenCV y-down → Unity y-up)
                    float X = (srcX - cxS) / fxS * d;
                    float Y = -((srcY - cyS) / fyS * d);
                    float Z = d;

                    int idx = y * _meshW + x;
                    _vertices[idx] = new Vector3(X, Y, Z);
                    _uvs[idx] = new Vector2((float)srcX / rtW, 1f - (float)srcY / rtH);
                    _depthValues[idx] = d;
                }
            }

            if (layoutChanged) RebuildTriangles();

            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();
        }

        private void RebuildTriangles() {
            // Upper bound: every quad → 2 triangles × 3 indices = 6
            var tris = new System.Collections.Generic.List<int>((_meshW - 1) * (_meshH - 1) * 6);

            for (int y = 0; y < _meshH - 1; y++) {
                for (int x = 0; x < _meshW - 1; x++) {
                    int i00 = y * _meshW + x;
                    int i10 = i00 + 1;
                    int i01 = i00 + _meshW;
                    int i11 = i01 + 1;

                    if (depthDiscontinuityThreshold > 0) {
                        float dMax = Mathf.Max(
                            _depthValues[i00], _depthValues[i10],
                            _depthValues[i01], _depthValues[i11]);
                        float dMin = Mathf.Min(
                            _depthValues[i00], _depthValues[i10],
                            _depthValues[i01], _depthValues[i11]);
                        if (dMax - dMin > depthDiscontinuityThreshold) continue;
                    }

                    tris.Add(i00); tris.Add(i01); tris.Add(i10);
                    tris.Add(i10); tris.Add(i01); tris.Add(i11);
                }
            }

            _triangles = tris.ToArray();
        }

        private void EnsureReadbackTex(int w, int h) {
            if (_depthReadback != null && _depthReadback.width == w && _depthReadback.height == h)
                return;
            if (_depthReadback) Destroy(_depthReadback);
            _depthReadback = new Texture2D(w, h, TextureFormat.RGB24, false);
        }

        private void ReadPixels(RenderTexture rt) {
            RenderTexture.active = rt;
            _depthReadback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            _depthReadback.Apply(false);
            RenderTexture.active = null;
        }

        private void OnDestroy() {
            if (_mesh) Destroy(_mesh);
            if (_depthReadback) Destroy(_depthReadback);
        }
    }
}
