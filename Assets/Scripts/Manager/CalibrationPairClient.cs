using System;
using System.Collections;
using Thesis.Calibration;
using Thesis.Patterns;
using UnityEngine;
using UnityEngine.Networking;

namespace Thesis.Managers
{
    // GET-fetches a pair of cameras' stored calibration from the registration
    // service, for the Viewer's synthetic-view (DIBR) feature — see
    // Master-Thesis-Reports/handoff_spec_Sep_20th_opendibr_live_control_and_export.md
    // for the consumer side. Unlike CalibrationConfigClient there is no
    // fallback-to-default here: a missing/failed pair is a real "not
    // available for this pair" result the caller uses to decide whether to
    // fall back to a plain crossfade, not something to paper over with a
    // guessed value.
    public class CalibrationPairClient : Singleton<CalibrationPairClient>
    {
        private const string Endpoint = "/calibration-data/pair";

        [SerializeField] private int _maxRetries = 2;
        [SerializeField] private float _retryDelay = 1f;

        // identity → cached pair result, so repeat switches between an
        // already-checked pair don't re-hit the network every time. Keyed by
        // the exact requested order ("camA|camB", NOT order-normalized) —
        // the response's cam1/cam2 fields correspond to that exact query
        // order (cam1=data for the identity passed as camA), and
        // CameraCalibrationData.cameraName isn't guaranteed to equal the
        // slot identity (it's the device's display label), so there's no
        // safe way to re-map a cached result fetched in the opposite order.
        // A query for the reverse order is simply a cache miss — one extra
        // request in the rare case that happens, in exchange for never
        // silently swapping which camera's data the caller thinks is which.
        private readonly System.Collections.Generic.Dictionary<string, SceneCalibrationData> _cache = new();

        public void FetchPair(string serverUrl, string camA, string camB,
            Action<SceneCalibrationData> onFound, Action onNotFound, Action<string> onError)
        {
            string key = $"{camA}|{camB}";
            if (_cache.TryGetValue(key, out var cached))
            {
                onFound?.Invoke(cached);
                return;
            }

            StartCoroutine(FetchWithRetry((serverUrl ?? "").TrimEnd('/'), camA, camB, key, onFound, onNotFound, onError));
        }

        private IEnumerator FetchWithRetry(string serverUrl, string camA, string camB, string key,
            Action<SceneCalibrationData> onFound, Action onNotFound, Action<string> onError)
        {
            string url = $"{serverUrl}{Endpoint}?cam1={UnityWebRequest.EscapeURL(camA)}&cam2={UnityWebRequest.EscapeURL(camB)}";

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var data = TryParse(req.downloadHandler.text);
                        if (data != null)
                        {
                            _cache[key] = data;
                            onFound?.Invoke(data);
                            yield break;
                        }

                        Debug.LogWarning("[CalibrationPairClient] Invalid response body from server.");
                        onError?.Invoke("Invalid response body.");
                        yield break;
                    }

                    // 404 = server-confirmed "no fresh calibration for one or both
                    // identities" — not a transient failure, don't retry.
                    if (req.responseCode == 404)
                    {
                        onNotFound?.Invoke();
                        yield break;
                    }

                    Debug.LogWarning($"[CalibrationPairClient] Attempt {attempt} failed: {req.error}");
                }

                if (attempt < _maxRetries)
                    yield return new WaitForSeconds(_retryDelay);
            }

            onError?.Invoke($"Calibration pair fetch failed after {_maxRetries} attempt(s).");
        }

        private static SceneCalibrationData TryParse(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<SceneCalibrationData>(json);
                if (data?.cam1?.intrinsics == null || data.cam2?.intrinsics == null) return null;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CalibrationPairClient] Failed to parse response: {e.Message}");
                return null;
            }
        }
    }
}
