using System;
using Thesis.Calibration;
using UnityEngine;

namespace Thesis.Managers
{
    // Reports this device's own intrinsics+extrinsics to the server, keyed by its
    // /register slot identity. Server pairs two cameras' uploads later for a
    // viewer's synthetic-view request — see the calibration-data spec handoff.
    public class CalibrationUploadClient : PostRequestClient<CalibrationUploadClient>
    {
        public event Action OnUploaded;
        public event Action<string> OnUploadFailed;

        private string _identity;
        private string _cameraName;
        private IntrinsicsData _intrinsics;
        private ExtrinsicsData _extrinsics;

        protected override string Endpoint => "/calibration-data";

        protected override string GetRequestBody() =>
            JsonUtility.ToJson(new CalibrationUploadRequest
            {
                identity = _identity,
                cameraName = _cameraName,
                intrinsics = _intrinsics,
                extrinsics = _extrinsics
            });

        public void Upload(string identity, string cameraName, IntrinsicsData intrinsics, ExtrinsicsData extrinsics)
        {
            _identity = identity;
            _cameraName = cameraName;
            _intrinsics = intrinsics;
            _extrinsics = extrinsics;

            StartCoroutine(PostWithRetry(
                () => OnUploaded?.Invoke(),
                error => OnUploadFailed?.Invoke(error),
                $"Calibration upload failed after {_maxRetries} attempt(s)."));
        }

        // No response body is required to consider the upload successful — a
        // non-error HTTP result from PostWithRetry is enough.
        protected override bool TryApplyResponse(string json) => true;

        [Serializable]
        private class CalibrationUploadRequest
        {
            public string identity;
            public string cameraName;
            public IntrinsicsData intrinsics;
            public ExtrinsicsData extrinsics;
        }
    }
}
