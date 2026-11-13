using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using MagicTouch.Tracking;

namespace MagicTouch.Online
{
    /// <summary>Lỗi trả về từ server, thông báo đã viết sẵn bằng chữ font pixel hiển thị được.</summary>
    public sealed class ApiError
    {
        public long Code;
        public string Message;
        /// <summary>True khi không tới được server (mất mạng, server đang ngủ...): có thể thử lại sau.</summary>
        public bool IsNetwork;
    }

    /// <summary>
    /// Gọi server bảng xếp hạng (thư mục Server/ của repo, chạy bằng Docker trên cloud).
    /// Địa chỉ server: LEADERBOARD_URL trong .env, nếu không có thì dùng DefaultServerUrl.
    /// </summary>
    public static class LeaderboardApi
    {
        // Sau khi deploy lên cloud, sửa dòng này (hoặc đặt LEADERBOARD_URL trong .env) thành URL của server.
        public const string DefaultServerUrl = "https://magictouch-leaderboard.onrender.com";

        // Server miễn phí trên Render "ngủ" khi không ai dùng, thức dậy mất tới khoảng 50 giây.
        private const int TimeoutSeconds = 60;

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
        public static bool HasIdentity => !string.IsNullOrWhiteSpace(CurrentPlayerName);

        // ------------------------------------------------------------ DTO (JsonUtility)
        [Serializable] private class RegisterBody { public string username; public string password; public string display_name; }
        [Serializable] private class LoginBody { public string username; public string password; }
        [Serializable] private class ScoreBody { public int score; public string mode; public string player_name; }
        [Serializable] public class AuthResult { public string token; public string username; public string display_name; }
        [Serializable] public class MeResult { public string username; public string display_name; public int best_score; public int games; }
        [Serializable] public class ScoreResult { public int id; public string player_name; public int score; public string mode; public int rank; public int best; }
        [Serializable] public class BoardRow { public int rank; public string player_name; public int best_score; public int games; public bool registered; }
        [Serializable] private class BoardWrapper { public BoardRow[] rows; }
        [Serializable] private class ErrorBody { public string detail; }

        // ------------------------------------------------------------ API
        public static IEnumerator Register(string username, string password, string displayName, Action<AuthResult> ok, Action<ApiError> fail)
        {
            var body = new RegisterBody { username = username, password = password, display_name = string.IsNullOrWhiteSpace(displayName) ? null : displayName };
            yield return Send("POST", "/api/auth/register", JsonUtility.ToJson(body), false, text => SaveAuth(text, ok), fail);
        }

        public static IEnumerator Login(string username, string password, Action<AuthResult> ok, Action<ApiError> fail)
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

        public static IEnumerator GetMe(Action<MeResult> ok, Action<ApiError> fail)
        {
            yield return Send("GET", "/api/me", null, true, text => ok?.Invoke(JsonUtility.FromJson<MeResult>(text)), fail);
        }

        public static IEnumerator SubmitScore(int score, string mode, Action<ScoreResult> ok, Action<ApiError> fail)
        {
            var body = new ScoreBody { score = score, mode = mode, player_name = IsLoggedIn ? null : GuestName };
            yield return Send("POST", "/api/scores", JsonUtility.ToJson(body), true,
                text => ok?.Invoke(JsonUtility.FromJson<ScoreResult>(text)), fail);
        }

        public static IEnumerator GetLeaderboard(string mode, int limit, Action<BoardRow[]> ok, Action<ApiError> fail)
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

        private static IEnumerator Send(string method, string path, string json, bool withAuth, Action<string> ok, Action<ApiError> fail)
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
            req.timeout = TimeoutSeconds;

            yield return req.SendWebRequest();

            string text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result == UnityWebRequest.Result.Success)
            {
                ok?.Invoke(text);
                yield break;
            }

            if (req.responseCode == 401 && withAuth) Logout(); // token hết hạn
            var error = ReadError(req, path, text);
            Debug.LogWarning($"[Online] {method} {path} failed: {req.responseCode} {req.error} {text}");
            fail?.Invoke(error);
        }

        /// <summary>Đổi lỗi server (tiếng Việt) sang câu ngắn tiếng Anh in hoa: font pixel của menu chỉ có chữ Latin không dấu.</summary>
        private static ApiError ReadError(UnityWebRequest req, string path, string text)
        {
            long code = req.responseCode;
            if (req.result == UnityWebRequest.Result.ConnectionError || code == 0)
                return new ApiError { Code = 0, IsNetwork = true, Message = "CANNOT REACH THE SERVER. CHECK INTERNET AND TRY AGAIN" };

            string detail = "";
            try { detail = JsonUtility.FromJson<ErrorBody>(text)?.detail ?? ""; }
            catch (Exception) { } // 422 của FastAPI trả về detail dạng mảng

            string message;
            bool register = path.StartsWith("/api/auth/register");
            bool login = path.StartsWith("/api/auth/login");
            bool score = path.StartsWith("/api/scores");
            if (code == 401) message = login ? "WRONG USERNAME OR PASSWORD" : "SESSION EXPIRED. PLEASE LOG IN AGAIN";
            else if (code == 403) message = "GAME KEY REJECTED BY THE SERVER";
            else if (code == 409)
            {
                if (detail.Contains("đăng nhập đã tồn tại")) message = "USERNAME ALREADY TAKEN";
                else if (detail.Contains("hiển thị")) message = "DISPLAY NAME ALREADY TAKEN";
                else if (score) message = "THIS NAME BELONGS TO AN ACCOUNT. LOG IN OR PICK ANOTHER NAME";
                else message = "ALREADY TAKEN";
            }
            else if (code == 400 || code == 422)
            {
                if (register) message = detail.Contains("hiển thị") ? "DISPLAY NAME: 2-20 CHARACTERS"
                    : "USERNAME: 3-20 LETTERS OR DIGITS. PASSWORD: 6+ CHARACTERS";
                else if (score) message = "NAME MUST BE 2-20 CHARACTERS";
                else message = "PLEASE CHECK WHAT YOU TYPED";
            }
            else if (code >= 500) { message = "SERVER ERROR. TRY AGAIN LATER"; return new ApiError { Code = code, IsNetwork = true, Message = message }; }
            else message = "REQUEST FAILED (" + code + ")";
            return new ApiError { Code = code, Message = message };
        }
    }
}
