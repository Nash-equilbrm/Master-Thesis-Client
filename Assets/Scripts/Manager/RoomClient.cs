using System;
using Thesis.Patterns;

namespace Thesis.Managers
{
    public class RoomClient : Singleton<RoomClient>
    {
        public event Action<string> OnRoomReady;
        public event Action<string> OnFailed;

        // Characters that are visually unambiguous (no 0/O, 1/I)
        private static readonly char[] CodeChars =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        public void CreateRoom(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                OnFailed?.Invoke("Please enter a username.");
                return;
            }

            AppConfig.Username = username.Trim();
            AppConfig.RoomCode = GenerateCode();
            OnRoomReady?.Invoke(AppConfig.RoomCode);
        }

        public void JoinRoom(string code, string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                OnFailed?.Invoke("Please enter a username.");
                return;
            }

            var trimmedCode = code?.Trim().ToUpper() ?? "";
            if (trimmedCode.Length != 6 || !IsValidCode(trimmedCode))
            {
                OnFailed?.Invoke("Please enter a valid 6-character room code.");
                return;
            }

            AppConfig.Username = username.Trim();
            AppConfig.RoomCode = trimmedCode;
            OnRoomReady?.Invoke(AppConfig.RoomCode);
        }

        private static string GenerateCode()
        {
            var rng   = new System.Random();
            var chars = new char[6];
            for (int i = 0; i < 6; i++)
                chars[i] = CodeChars[rng.Next(CodeChars.Length)];
            return new string(chars);
        }

        private static bool IsValidCode(string code)
        {
            foreach (var c in code)
                if (Array.IndexOf(CodeChars, c) < 0) return false;
            return true;
        }
    }
}
