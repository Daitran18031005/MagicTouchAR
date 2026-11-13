using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using MagicTouch.Gameplay;
using MagicTouch.UI;

namespace MagicTouch.Online
{
    /// <summary>
    /// Điều phối phần online, tự tạo lúc chạy (không cần sửa scene):
    /// - Gắn OnlineMenuScreens (nút ACCOUNT, RANKING và các màn hình online) vào MenuManager.
    /// - Khi Game Over ở Solo: gửi điểm lên server. Chưa có tên/tài khoản thì giữ lại "điểm chờ"
    ///   và tự gửi khi người chơi đăng nhập hoặc đặt tên khách.
    /// Tắt toàn bộ bằng ONLINE_LEADERBOARD=0 trong .env.
    /// </summary>
    public class OnlineAccountOverlay : MonoBehaviour
    {
        private const string PendingKey = "Online.PendingScore";
        private const float RetryDelay = 30f;

        public static OnlineAccountOverlay Instance { get; private set; }

        /// <summary>Chữ ngắn hiện trên màn Game Over (2 dòng, font pixel).</summary>
        public static string GameOverStatus { get; private set; } = "";
        public static event Action<string> GameOverStatusChanged;

        /// <summary>Điểm Solo chưa gửi được (-1 = không có).</summary>
        public static int PendingScore => PlayerPrefs.GetInt(PendingKey, -1);

        private GameManager _gm;
        private bool _submitting;
        private bool _autoRetry;
        private float _nextRetry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (MagicTouch.Tracking.ProjectEnv.Get("ONLINE_LEADERBOARD", "1") == "0") return;
            if (FindAnyObjectByType<OnlineAccountOverlay>() != null) return;
            var go = new GameObject("[Online]");
            DontDestroyOnLoad(go);
            go.AddComponent<OnlineAccountOverlay>();
        }

        private void Awake()
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start() => StartCoroutine(AttachMenu());

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_gm != null) _gm.OnGameOver -= HandleGameOver;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(AttachMenu());

        private IEnumerator AttachMenu()
        {
            for (int frame = 0; frame < 300; frame++)
            {
                var menu = MenuManager.Instance;
                if (menu != null && menu.mainMenuPanel != null)
                {
                    if (menu.GetComponent<OnlineMenuScreens>() == null) menu.gameObject.AddComponent<OnlineMenuScreens>();
                    yield break;
                }
                yield return null;
            }
        }

        private void Update()
        {
            // GameManager có thể được tạo lại khi load lại scene -> đăng ký lại sự kiện.
            var gm = GameManager.Instance;
            if (gm != _gm)
            {
                if (_gm != null) _gm.OnGameOver -= HandleGameOver;
                _gm = gm;
                if (_gm != null) _gm.OnGameOver += HandleGameOver;
            }

            // Lần trước mất mạng: thử gửi lại điểm chờ sau một lúc.
            if (_autoRetry && !_submitting && Time.unscaledTime >= _nextRetry && PendingScore > 0 && LeaderboardApi.HasIdentity)
                Submit(null);
        }

        private void HandleGameOver()
        {
            if (_gm == null || _gm.IsVersus) return; // Versus: 2 người chung 1 máy, không gửi điểm
            int score = _gm.score;
            if (score <= 0) { SetStatus(""); return; }

            PlayerPrefs.SetInt(PendingKey, score);
            PlayerPrefs.Save();
            if (!LeaderboardApi.HasIdentity)
            {
                SetStatus("OPEN ACCOUNT\nTO SAVE ONLINE");
                return;
            }
            Submit(null);
        }

        /// <summary>Gửi điểm chờ (nếu có). report nhận một câu ngắn để hiện lên màn hình.</summary>
        public static void SubmitPending(Action<string> report)
        {
            if (Instance != null) Instance.Submit(report);
        }

        private void Submit(Action<string> report)
        {
            int score = PendingScore;
            if (_submitting || score <= 0 || !LeaderboardApi.HasIdentity) return;
            _submitting = true;
            _autoRetry = false;
            SetStatus("SAVING\nSCORE...");
            StartCoroutine(LeaderboardApi.SubmitScore(score, "solo",
                r =>
                {
                    _submitting = false;
                    if (PendingScore == score) { PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); }
                    SetStatus($"ONLINE\nRANK {r.rank}");
                    report?.Invoke($"SCORE {r.score} SAVED.\nYOU ARE RANK {r.rank} (BEST {r.best})");
                },
                e =>
                {
                    _submitting = false;
                    _autoRetry = e.IsNetwork;
                    _nextRetry = Time.unscaledTime + RetryDelay;
                    SetStatus(e.IsNetwork ? "OFFLINE\nWILL RETRY" : "NOT SAVED\nSEE ACCOUNT");
                    report?.Invoke("SCORE NOT SAVED: " + e.Message);
                }));
        }

        private static void SetStatus(string text)
        {
            GameOverStatus = text;
            GameOverStatusChanged?.Invoke(text);
        }
    }
}
