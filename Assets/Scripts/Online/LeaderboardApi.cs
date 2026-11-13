using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using MagicTouch.Tracking;

namespace MagicTouch.Online
{
    /// <summary>
    /// Gọi server bảng xếp hạng (thư mục Server/ của repo, chạy bằng Docker trên cloud).
    /// Địa chỉ server: LEADERBOARD_URL trong .env, nếu không có thì dùng DefaultServerUrl.
    /// </summary>
    public static class LeaderboardApi
    {
        // Sau khi deploy lên cloud, sửa dòng này (hoặc đặt LEADERBOARD_URL trong .env) thành URL của server.
        public const string DefaultServerUrl = "https://magictouch-leaderboard.onrender.com";

        private const string TokenKey = "Online.Token";
        private const string DisplayNameKey = "Online.DisplayName";
        private const string GuestNameKey = "Online.GuestName";

        public static string ServerUrl => ProjectEnv.Get("LEADERBOARD_URL", DefaultServerUrl).TrimEnd('/');
        private static string GameKey => ProjectEnv.Get("GAME_API_KEY", "");

        public static string Token => PlayerPrefs.GetString(TokenKey, "");
        public static string DisplayName => PlayerPrefs.GetString(DisplayNameKey, "");
        public static bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        public static string GuestName
        {
            get => PlayerPrefs.GetString(GuestNameKey, "");
            set { PlayerPrefs.SetString(GuestNameKey, value ?? ""); PlayerPrefs.Save(); }
        }

        /// <summary>Tên sẽ được ghi lên bảng xếp hạng (tài khoản hoặc tên khách).</summary>
        public static string CurrentPlayerName => IsLoggedIn ? DisplayName : GuestName;

        // ------------------------------------------------------------ DTO (JsonUtility)
        [Serializable] private class RegisterBody { public string username; public string password; public string display_name; }
        [Serializable] private class LoginBody { public string username; public string password; }
        [Serializable] private class ScoreBody { public int score; public string mode; public string player_name; }
        [Serializable] public class AuthResult { public string token; public string username; public string display_name; }
        [Serializable] public class ScoreResult { public int id; public string player_name; public int score; public string mode; public int rank; public int best; }
        [Serializable] public class BoardRow { public int rank; public string player_name; public int best_score; public int games; public bool registered; }
        [Serializable] private class BoardWrapper { public BoardRow[] rows; }
        [Serializable] private class ErrorBody { public string detail; }

        // ------------------------------------------------------------ API
        public static IEnumerator Register(string username, string password, string displayName, Action<AuthResult> ok, Action<string> fail)
        {
            var body = new RegisterBody { username = username, password = password, display_name = string.IsNullOrWhiteSpace(displayName) ? null : displayName };
            yield return Send("POST", "/api/auth/register", JsonUtility.ToJson(body), false, text => SaveAuth(text, ok), fail);
        }

        public static IEnumerator Login(string username, string password, Action<AuthResult> ok, Action<string> fail)
        {
            var body = new LoginBody { username = username, password = password };
            yield return Send("POST", "/api/auth/login", JsonUtility.ToJson(body), false, text => SaveAuth(text, ok), fail);
        }

        public static void Logout()
        {
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(DisplayNameKey);
            PlayerPrefs.Save();
        }

        public static IEnumerator SubmitScore(int score, string mode, Action<ScoreResult> ok, Action<string> fail)
        {
            var body = new ScoreBody { score = score, mode = mode, player_name = IsLoggedIn ? null : GuestName };
            yield return Send("POST", "/api/scores", JsonUtility.ToJson(body), true,
                text => ok?.Invoke(JsonUtility.FromJson<ScoreResult>(text)), fail);
        }

        public static IEnumerator GetLeaderboard(string mode, int limit, Action<BoardRow[]> ok, Action<string> fail)
        {
            yield return Send("GET", $"/api/leaderboard?mode={UnityWebRequest.EscapeURL(mode)}&limit={limit}", null, false,
                text => ok?.Invoke(JsonUtility.FromJson<BoardWrapper>("{\"rows\":" + text + "}").rows ?? new BoardRow[0]), fail);
        }

        // ------------------------------------------------------------ nội bộ
        private static void SaveAuth(string text, Action<AuthResult> ok)
        {
            var auth = JsonUtility.FromJson<AuthResult>(text);
            PlayerPrefs.SetString(TokenKey, auth.token);
            PlayerPrefs.SetString(DisplayNameKey, auth.display_name);
            PlayerPrefs.Save();
            ok?.Invoke(auth);
        }

        private static IEnumerator Send(string method, string path, string json, bool withAuth, Action<string> ok, Action<string> fail)
        {
            using var req = new UnityWebRequest(ServerUrl + path, method);
            req.downloadHandler = new DownloadHandlerBuffer();
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            if (withAuth && IsLoggedIn) req.SetRequestHeader("Authorization", "Bearer " + Token);
            if (!string.IsNullOrEmpty(GameKey)) req.SetRequestHeader("X-Game-Key", GameKey);
            req.timeout = 15;

            yield return req.SendWebRequest();

            string text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result == UnityWebRequest.Result.Success)
            {
                ok?.Invoke(text);
                yield break;
            }

            if (req.responseCode == 401 && withAuth) Logout(); // token hết hạn
            fail?.Invoke(ReadError(req, text));
        }

        private static string ReadError(UnityWebRequest req, string text)
        {
            if (req.result == UnityWebRequest.Result.ConnectionError)
                return "Không kết nối được server (" + ServerUrl + ")";
            try
            {
                var err = JsonUtility.FromJson<ErrorBody>(text);
                if (err != null && !string.IsNullOrEmpty(err.detail)) return err.detail;
            }
            catch (Exception) { } // detail không phải chuỗi
            // 422 của FastAPI trả về detail dạng mảng -> thông báo chung.
            return req.responseCode == 422 ? "Dữ liệu không hợp lệ (tên 3-20 ký tự, mật khẩu ≥ 6 ký tự)" : $"Lỗi server {req.responseCode}";
        }
    }
}
