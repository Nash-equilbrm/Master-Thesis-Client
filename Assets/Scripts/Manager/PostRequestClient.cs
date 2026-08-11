using System;
using System.Collections;
using Thesis.Patterns;
using UnityEngine;
using UnityEngine.Networking;

namespace Thesis.Managers
{
    public abstract class PostRequestClient<T> : Singleton<T> where T : PostRequestClient<T>
    {
        [SerializeField] protected string _serverUrl = "http://localhost:3000";
        [SerializeField] protected int _maxRetries = 3;
        [SerializeField] protected float _retryDelay = 2f;

        public string ServerUrl { get => _serverUrl; set => _serverUrl = value; }

        protected abstract string Endpoint { get; }

        private bool _lastAttemptSucceeded;

        protected abstract bool TryApplyResponse(string json);

        protected IEnumerator PostWithRetry(Action onSuccess, Action<string> onFailure, string failureMessage)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                yield return StartCoroutine(TryPostOnce());

                if (_lastAttemptSucceeded)
                {
                    onSuccess?.Invoke();
                    yield break;
                }

                if (attempt < _maxRetries)
                    yield return new WaitForSeconds(_retryDelay);
            }

            onFailure?.Invoke(failureMessage);
        }

        protected virtual string GetRequestBody() => null;

        private IEnumerator TryPostOnce()
        {
            _lastAttemptSucceeded = false;

            using UnityWebRequest req = new UnityWebRequest(_serverUrl + Endpoint, "POST");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var body = GetRequestBody();
            if (body != null)
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Attempt failed: {req.error}");
                yield break;
            }

            if (!TryApplyResponse(req.downloadHandler.text))
            {
                Debug.LogWarning($"[{typeof(T).Name}] Invalid response from server.");
                yield break;
            }

            _lastAttemptSucceeded = true;
        }
    }
}
