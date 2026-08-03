using System;
using System.Collections;
using Thesis.Patterns;
using UnityEngine;
using UnityEngine.Networking;

namespace Thesis.Managers
{
    public class ViewerTokenClient : Singleton<ViewerTokenClient>
    {
        [SerializeField] private string _serverUrl = "http://localhost:3000";
        [SerializeField] private int _maxRetries = 3;
        [SerializeField] private float _retryDelay = 2f;

        public string ServerUrl => _serverUrl;
        public string Token { get; private set; }
        public string LiveKitUrl { get; private set; }

        public event Action OnTokenReceived;
        public event Action<string> OnTokenFetchFailed;

        public void FetchToken(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            Token = null;
            LiveKitUrl = null;
            StartCoroutine(FetchRoutine());
        }

        private IEnumerator FetchRoutine()
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                yield return StartCoroutine(TryFetch());

                if (Token != null)
                {
                    OnTokenReceived?.Invoke();
                    yield break;
                }

                if (attempt < _maxRetries)
                    yield return new WaitForSeconds(_retryDelay);
            }

            OnTokenFetchFailed?.Invoke($"Failed to fetch viewer token after {_maxRetries} attempt(s).");
        }

        private IEnumerator TryFetch()
        {
            using UnityWebRequest req = new UnityWebRequest(_serverUrl + "/viewer-token", "POST");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ViewerTokenClient] Attempt failed: {req.error}");
                yield break;
            }

            var response = JsonUtility.FromJson<ViewerTokenResponse>(req.downloadHandler.text);
            if (string.IsNullOrEmpty(response.token) || string.IsNullOrEmpty(response.livekit_url))
            {
                Debug.LogWarning("[ViewerTokenClient] Invalid response from server.");
                yield break;
            }

            Token = response.token;
            LiveKitUrl = response.livekit_url;
            Debug.Log("[ViewerTokenClient] Viewer token received.");
        }

        [Serializable]
        private class ViewerTokenResponse
        {
            public string token;
            public string livekit_url;
        }
    }
}
