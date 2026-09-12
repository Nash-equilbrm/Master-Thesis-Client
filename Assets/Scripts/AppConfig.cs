using UnityEngine;

namespace Thesis
{
    public static class AppConfig
    {
        public const string DefaultServerUrl = "http://13.228.8.204:3000";

        private const string ServerUrlKey = "dev_server_url";
        private const string UserIdKey    = "user_id";
        private const string UsernameKey  = "username";
        private const string CalibrationAcknowledgedKey = "calibration_acknowledged";
        private const string DevSkipCalibrationUploadKey = "dev_skip_calibration_upload";

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

        // Set once this device has completed the live calibration tutorial and
        // successfully uploaded its result to the server. Cleared by "Recalibrate".
        public static bool CalibrationAcknowledged
        {
            get => PlayerPrefs.GetInt(CalibrationAcknowledgedKey, 0) != 0;
            set => PlayerPrefs.SetInt(CalibrationAcknowledgedKey, value ? 1 : 0);
        }

        // Dev-only escape hatch (toggle in DevCommandPopup): lets the calibration
        // tutorial complete and unblock publishing even if /calibration-data upload
        // fails — for local testing before that server endpoint exists.
        public static bool DevSkipCalibrationUpload
        {
            get => PlayerPrefs.GetInt(DevSkipCalibrationUploadKey, 0) != 0;
            set => PlayerPrefs.SetInt(DevSkipCalibrationUploadKey, value ? 1 : 0);
        }

        public static void SetServerUrl(string url) => PlayerPrefs.SetString(ServerUrlKey, url);
        public static void ClearServerUrl() => PlayerPrefs.DeleteKey(ServerUrlKey);
    }
}
