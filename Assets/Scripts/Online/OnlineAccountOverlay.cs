using UnityEngine;
using MagicTouch.Gameplay;
using MagicTouch.UI;

namespace MagicTouch.Online
{
    /// <summary>
    /// Khung tài khoản + gửi điểm online, tự tạo lúc chạy (không cần sửa scene).
    /// - Ở menu chính: chơi bằng tên khách, đăng nhập hoặc đăng ký; xem top 10.
    /// - Khi Game Over (Solo): tự gửi điểm lên server, hiện hạng.
    /// Tắt bằng ONLINE_LEADERBOARD=0 trong .env.
    /// </summary>
    public class OnlineAccountOverlay : MonoBehaviour
    {
        private enum View { Guest, Login, Register }

        private View _view = View.Guest;
        private string _guestName = "";
        private string _username = "", _password = "", _displayName = "";
        private string _status = "";
        private bool _busy;
        private GameManager _gm;

        // Trạng thái gửi điểm của ván vừa xong
        private bool _roundSubmitted;
        private int _lastScore;
        private string _submitStatus = "";
        private LeaderboardApi.BoardRow[] _top;
        private float _nextBoardFetch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (MagicTouch.Tracking.ProjectEnv.Get("ONLINE_LEADERBOARD", "1") == "0") return;
            if (FindAnyObjectByType<OnlineAccountOverlay>() != null) return;
            var go = new GameObject("[OnlineAccountOverlay]");
            DontDestroyOnLoad(go);
            go.AddComponent<OnlineAccountOverlay>();
        }

        private void Start() => _guestName = LeaderboardApi.GuestName;

        private void Update()
        {
            // GameManager có thể được tạo lại khi load lại scene -> đăng ký lại sự kiện.
            var gm = GameManager.Instance;
            if (gm == _gm) return;
            if (_gm != null) { _gm.OnGameOver -= HandleGameOver; _gm.RoundReset -= HandleRoundReset; }
            _gm = gm;
            if (_gm != null) { _gm.OnGameOver += HandleGameOver; _gm.RoundReset += HandleRoundReset; }
        }

        private void OnDestroy()
        {
            if (_gm != null) { _gm.OnGameOver -= HandleGameOver; _gm.RoundReset -= HandleRoundReset; }
        }

        private void HandleRoundReset()
        {
            _roundSubmitted = false;
            _submitStatus = "";
        }

        private void HandleGameOver()
        {
            if (_gm == null || _gm.IsVersus || _roundSubmitted) return;
            _lastScore = _gm.score;
            if (string.IsNullOrWhiteSpace(LeaderboardApi.CurrentPlayerName))
            {
                _submitStatus = "Nhập tên để lưu điểm lên bảng xếp hạng";
                return; // chờ người chơi nhập tên ở khung Game Over
            }
            SubmitLastScore();
        }

        private void SubmitLastScore()
        {
            _roundSubmitted = true;
            _submitStatus = "Đang gửi điểm…";
            StartCoroutine(LeaderboardApi.SubmitScore(_lastScore, "solo",
                r => { _submitStatus = $"Đã lưu {r.score} điểm · Hạng #{r.rank} · Kỷ lục {r.best}"; _nextBoardFetch = 0; },
                e => { _submitStatus = "Không gửi được điểm: " + e; _roundSubmitted = false; }));
        }

        // ------------------------------------------------------------ giao diện (IMGUI, chạy cả khi timeScale = 0)
        private bool InMainMenu
        {
            get
            {
                var menu = MenuManager.Instance;
                return menu != null && menu.mainMenuPanel != null && menu.mainMenuPanel.activeInHierarchy;
            }
        }

        private bool InSoloGameOver => _gm != null && !_gm.IsVersus && _gm.currentState == GameState.GameOver;

        private void OnGUI()
        {
            bool menu = InMainMenu, over = InSoloGameOver;
            if (!menu && !over) return;

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            GUI.skin.label.fontSize = GUI.skin.button.fontSize = GUI.skin.textField.fontSize = 22;
            GUI.skin.label.wordWrap = true;
            GUI.skin.label.richText = true;

            var rect = new Rect(w - 440, 20, 420, menu ? 640 : 300);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Space(6);
            if (over) DrawGameOver(); else DrawAccount();
            GUILayout.EndArea();
        }

        private void DrawGameOver()
        {
            GUILayout.Label($"<b>Bảng xếp hạng online</b>\nĐiểm ván này: {_lastScore}");
            if (!LeaderboardApi.IsLoggedIn && !_roundSubmitted)
            {
                GUILayout.Label("Tên người chơi:");
                _guestName = GUILayout.TextField(_guestName, 20);
                GUI.enabled = _guestName.Trim().Length >= 2;
                if (GUILayout.Button("Lưu điểm"))
                {
                    LeaderboardApi.GuestName = _guestName.Trim();
                    SubmitLastScore();
                }
                GUI.enabled = true;
            }
            else if (!_roundSubmitted && GUILayout.Button("Thử gửi lại"))
            {
                SubmitLastScore();
            }
            GUILayout.Label(_submitStatus);
        }

        private void DrawAccount()
        {
            GUI.enabled = !_busy;
            if (LeaderboardApi.IsLoggedIn)
            {
                GUILayout.Label($"Đã đăng nhập: <b>{LeaderboardApi.DisplayName}</b>");
                if (GUILayout.Button("Đăng xuất")) { LeaderboardApi.Logout(); _status = ""; }
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_view == View.Guest, "Khách", GUI.skin.button)) _view = View.Guest;
                if (GUILayout.Toggle(_view == View.Login, "Đăng nhập", GUI.skin.button)) _view = View.Login;
                if (GUILayout.Toggle(_view == View.Register, "Đăng ký", GUI.skin.button)) _view = View.Register;
                GUILayout.EndHorizontal();

                if (_view == View.Guest)
                {
                    GUILayout.Label("Tên người chơi (hiện trên bảng xếp hạng):");
                    string n = GUILayout.TextField(_guestName, 20);
                    if (n != _guestName) { _guestName = n; LeaderboardApi.GuestName = n.Trim(); }
                }
                else
                {
                    GUILayout.Label("Tên đăng nhập:");
                    _username = GUILayout.TextField(_username, 20);
                    GUILayout.Label("Mật khẩu:");
                    _password = GUILayout.PasswordField(_password, '*', 72);
                    if (_view == View.Register)
                    {
                        GUILayout.Label("Tên hiển thị (tùy chọn):");
                        _displayName = GUILayout.TextField(_displayName, 20);
                    }
                    if (GUILayout.Button(_view == View.Login ? "Đăng nhập" : "Tạo tài khoản")) DoAuth();
                }
            }
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);

            GUILayout.Space(8);
            GUILayout.Label("<b>Top 10 (Solo)</b>");
            if (Time.unscaledTime >= _nextBoardFetch)
            {
                _nextBoardFetch = Time.unscaledTime + 20f;
                StartCoroutine(LeaderboardApi.GetLeaderboard("solo", 10, rows => _top = rows, e => _top = null));
            }
            if (_top == null) GUILayout.Label("(chưa kết nối được server)");
            else if (_top.Length == 0) GUILayout.Label("Chưa có điểm nào.");
            else foreach (var r in _top) GUILayout.Label($"{r.rank}. {r.player_name} — {r.best_score}");
        }

        private void DoAuth()
        {
            _busy = true;
            _status = "Đang xử lý…";
            void Ok(LeaderboardApi.AuthResult a) { _busy = false; _password = ""; _status = $"Xin chào {a.display_name}!"; }
            void Fail(string e) { _busy = false; _status = e; }
            StartCoroutine(_view == View.Login
                ? LeaderboardApi.Login(_username.Trim(), _password, Ok, Fail)
                : LeaderboardApi.Register(_username.Trim(), _password, _displayName.Trim(), Ok, Fail));
        }
    }
}
