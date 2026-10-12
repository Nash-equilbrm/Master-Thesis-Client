using UnityEditor;
using UnityEngine;

namespace Thesis.EditorTools
{
    // Editor-only shortcuts for AppConfig's PlayerPrefs "dev_server_url"
    // override (same key ServerConfigPopup writes at runtime).
    public static class DevServerUrlMenu
    {
        private const string LocalUrl = "http://192.168.1.3:3000";

        [MenuItem("Tools/Dev Server/Use Local (192.168.1.3:3000)")]
        private static void UseLocal()
        {
            AppConfig.SetServerUrl(LocalUrl);
            PlayerPrefs.Save();
            Debug.Log($"[DevServerUrl] dev_server_url = {LocalUrl}");
        }

        [MenuItem("Tools/Dev Server/Clear Override (use default)")]
        private static void Clear()
        {
            AppConfig.ClearServerUrl();
            PlayerPrefs.Save();
            Debug.Log($"[DevServerUrl] override cleared — default is {AppConfig.DefaultServerUrl}");
        }
    }
}
