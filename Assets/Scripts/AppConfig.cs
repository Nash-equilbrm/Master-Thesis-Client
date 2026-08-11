using UnityEngine;

namespace Thesis
{
    public static class AppConfig
    {
        public const string DefaultServerUrl = "http://192.168.1.5:3000";

        private const string PrefsKey = "dev_server_url";

        public static string ServerUrl => PlayerPrefs.GetString(PrefsKey, DefaultServerUrl);

        public static bool HasOverride => PlayerPrefs.HasKey(PrefsKey);

        public static void SetServerUrl(string url) => PlayerPrefs.SetString(PrefsKey, url);

        public static void ClearServerUrl() => PlayerPrefs.DeleteKey(PrefsKey);
    }
}
