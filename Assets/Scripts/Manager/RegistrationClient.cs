using System;
using UnityEngine;

namespace Thesis.Managers
{
    public class RegistrationClient : PostRequestClient<RegistrationClient>
    {
        public string Identity { get; private set; }
        public string Token { get; private set; }
        public string LiveKitUrl { get; private set; }

        public event Action OnRegistered;
        public event Action<string> OnRegistrationFailed;

        protected override string Endpoint => "/register";

        public void Register()
        {
            StartCoroutine(PostWithRetry(
                () => OnRegistered?.Invoke(),
                error => OnRegistrationFailed?.Invoke(error),
                $"Registration failed after {_maxRetries} attempt(s)."));
        }

        protected override bool TryApplyResponse(string json)
        {
            var response = JsonUtility.FromJson<RegistrationResponse>(json);
            if (string.IsNullOrEmpty(response.identity) || string.IsNullOrEmpty(response.token))
                return false;

            Identity = response.identity;
            Token = response.token;
            LiveKitUrl = response.livekit_url;
            Debug.Log($"[RegistrationClient] Registered as {Identity}");
            return true;
        }

        [Serializable]
        private class RegistrationResponse
        {
            public string identity;
            public string token;
            public string livekit_url;
        }
    }
}
