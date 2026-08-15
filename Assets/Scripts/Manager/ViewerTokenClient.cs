using System;
using UnityEngine;

namespace Thesis.Managers
{
    public class ViewerTokenClient : PostRequestClient<ViewerTokenClient>
    {
        public string Token { get; private set; }
        public string LiveKitUrl { get; private set; }

        public event Action OnTokenReceived;
        public event Action<string> OnTokenFetchFailed;

        protected override string Endpoint => "/viewer-token";

        protected override string GetRequestBody() =>
            JsonUtility.ToJson(new ViewerTokenRequest
            {
                roomCode = Thesis.AppConfig.RoomCode,
                userId   = Thesis.AppConfig.UserId,
                username = Thesis.AppConfig.Username
            });

        public void FetchToken(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            Token = null;
            LiveKitUrl = null;
            StartCoroutine(PostWithRetry(
                () => OnTokenReceived?.Invoke(),
                error => OnTokenFetchFailed?.Invoke(error),
                $"Failed to fetch viewer token after {_maxRetries} attempt(s)."));
        }

        protected override bool TryApplyResponse(string json)
        {
            var response = JsonUtility.FromJson<ViewerTokenResponse>(json);
            if (string.IsNullOrEmpty(response.token) || string.IsNullOrEmpty(response.livekit_url))
                return false;

            Token = response.token;
            LiveKitUrl = response.livekit_url;
            Debug.Log("[ViewerTokenClient] Viewer token received.");
            return true;
        }

        [Serializable]
        private class ViewerTokenResponse
        {
            public string token;
            public string livekit_url;
        }

        [Serializable]
        private class ViewerTokenRequest
        {
            public string roomCode;
            public string userId;
            public string username;
        }
    }
}
