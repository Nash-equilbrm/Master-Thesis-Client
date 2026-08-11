using UnityEngine;

namespace Thesis
{
    public static class AppConfig
    {
        public const string DefaultServerUrl = "http://localhost:3000";

        private const string ServerUrlKey = "dev_server_url";
        private const string UserIdKey    = "user_id";
        private const string UsernameKey  = "username";

        public static string ServerUrl => PlayerPrefs.GetString(ServerUrlKey, DefaultServerUrl);
        public static bool   HasOverride => PlayerPrefs.HasKey(ServerUrlKey);

        // Persistent UUID generated once on first launch
        public static string UserId
        {
            get
            {
                if (!PlayerPrefs.HasKey(UserIdKey))
                    PlayerPrefs.SetString(UserIdKey, System.Guid.NewGuid().ToString());
                return PlayerPrefs.GetString(UserIdKey);
            }
        }

        // Persisted display name — pre-fills UI on return
        public static string Username
        {
            get => PlayerPrefs.GetString(UsernameKey, "");
            set => PlayerPrefs.SetString(UsernameKey, value);
        }

        // Session-only room code — set when creating or joining a room
        public static string RoomCode { get; set; }

        public static void SetServerUrl(string url) => PlayerPrefs.SetString(ServerUrlKey, url);
        public static void ClearServerUrl() => PlayerPrefs.DeleteKey(ServerUrlKey);
    }
}
