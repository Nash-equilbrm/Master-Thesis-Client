using System;
using System.Collections;
using Thesis.Calibration;
using Thesis.Patterns;
using UnityEngine;
using UnityEngine.Networking;

namespace Thesis.Managers
{
    // GET-only fetch, unlike PostRequestClient<T> which is POST-with-body — kept
    // as its own small client rather than forcing that base class to support GET.
    public class CalibrationConfigClient : Singleton<CalibrationConfigClient>
    {
        private const string Endpoint = "/calibration-config";
        private const string PrefCachedConfigJson = "calibration_config_cache";

        [SerializeField] private int _maxRetries = 3;
        [SerializeField] private float _retryDelay = 2f;

        public BoardConfig Config { get; private set; }
        public bool UsedFallback { get; private set; }

        public event Action<BoardConfig> OnConfigReady;

        public void FetchConfig(string serverUrl)
        {
            StartCoroutine(FetchWithRetry((serverUrl ?? "").TrimEnd('/')));
        }

        private IEnumerator FetchWithRetry(string serverUrl)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                bool success = false;

                using (UnityWebRequest req = UnityWebRequest.Get(serverUrl + Endpoint))
                {
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        success = TryApplyResponse(req.downloadHandler.text);
                        if (!success)
                            Debug.LogWarning("[CalibrationConfigClient] Invalid response from server.");
                    }
                    else
                    {
                        Debug.LogWarning($"[CalibrationConfigClient] Attempt {attempt} failed: {req.error}");
                    }
                }

                if (success)
                {
                    OnConfigReady?.Invoke(Config);
                    yield break;
                }

                if (attempt < _maxRetries)
                    yield return new WaitForSeconds(_retryDelay);
            }

            UseFallback();
        }

        private bool TryApplyResponse(string json)
        {
            BoardConfig cfg;
            try
            {
                cfg = JsonUtility.FromJson<BoardConfig>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CalibrationConfigClient] Failed to parse response: {e.Message}");
                return false;
            }

            if (cfg == null || cfg.squaresX <= 0 || cfg.squaresY <= 0 || cfg.squareLengthMm <= 0f)
                return false;

            Config = cfg;
            UsedFallback = false;
            PlayerPrefs.SetString(PrefCachedConfigJson, json);
            PlayerPrefs.Save();
            return true;
        }

        private void UseFallback()
        {
            string cached = PlayerPrefs.GetString(PrefCachedConfigJson, "");
            if (!string.IsNullOrEmpty(cached))
            {
                Config = JsonUtility.FromJson<BoardConfig>(cached);
                Debug.LogWarning("[CalibrationConfigClient] Server unreachable — using last cached board config.");
            }
            else
            {
                Config = BoardConfig.Default;
                Debug.LogWarning("[CalibrationConfigClient] Server unreachable — using built-in default board config.");
            }

            UsedFallback = true;
            OnConfigReady?.Invoke(Config);
        }
    }
}
