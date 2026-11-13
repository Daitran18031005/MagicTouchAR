using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MagicTouch.UI;

namespace MagicTouch.Online
{
    /// <summary>
    /// Màn hình online trong menu chính, dựng lúc chạy (không sửa SampleScene):
    /// - Nút ACCOUNT (dưới SETTINGS) mở màn Account, từ đó vào riêng từng màn LOG IN / REGISTER / GUEST.
    /// - Nút CREDITS đổi thành RANKING: bảng xếp hạng Solo lấy từ server cloud.
    /// - Dòng trạng thái điểm online trên màn Game Over.
    /// Được OnlineAccountOverlay gắn vào GameObject của MenuManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineMenuScreens : MonoBehaviour
    {
        private const int RankRows = 10;

        private MenuManager _menu;
        private OnlineUiKit _kit;
        private GameObject _current;
        private GameObject _account, _login, _register, _guest, _ranking;

        // Account
        private TextMeshProUGUI _accState, _accName, _accInfo, _accHint;
        private Button _accLogin, _accRegister, _accGuest, _accLogout;

        // Biểu mẫu
        private TMP_InputField _loginUser, _loginPass, _regUser, _regPass, _regName, _guestName;
        private TextMeshProUGUI _loginStatus, _regStatus, _guestStatus;
        private Button _loginSubmit, _regSubmit, _guestSubmit;
        private TMP_InputField[] _loginFields, _regFields, _guestFields;

        // Ranking
        private TextMeshProUGUI _rankStatus, _rankYou;
        private Button _rankRefresh;
        private RankRow[] _rows;

        // Game Over
        private TextMeshProUGUI _gameOverStatus;

        // Yêu cầu đang chạy
        private bool _busy;
        private float _busySince;
        private TextMeshProUGUI _busyLabel;
        private Button _busyButton;

        private struct RankRow
        {
            public Image Back;
            public TextMeshProUGUI Rank, Name, Score;
        }

        private IEnumerator Start()
        {
            yield return null; // chờ MenuManager.Start ẩn/hiện các panel xong
            _menu = GetComponent<MenuManager>();
            if (_menu == null || _menu.mainMenuPanel == null) yield break;

            _kit = OnlineUiKit.TryCreate(_menu, out string missing);
            if (_kit == null)
            {
                Debug.LogWarning("[Online] Menu online không được tạo vì thiếu " + missing + ". Game vẫn chơi bình thường.");
                yield break;
            }

            InjectMenuButtons();
            BuildAccount();
            BuildLogin();
            BuildRegister();
            BuildGuest();
            BuildRanking();
            BuildGameOverStatus();
            OnlineAccountOverlay.GameOverStatusChanged += ShowGameOverStatus;
        }

        private void OnDestroy() => OnlineAccountOverlay.GameOverStatusChanged -= ShowGameOverStatus;

        // ================================================================ menu chính
        private void InjectMenuButtons()
        {
            var buttons = FindDeep(_menu.mainMenuPanel.transform, "MenuButtons");
            if (buttons == null) { Debug.LogWarning("[Online] Không thấy MenuButtons trong MainMenu."); return; }

            var settings = buttons.Find("SettingsButton");
            if (settings != null)
            {
                var account = Instantiate(settings.gameObject, buttons);
                account.name = "AccountButton";
                account.transform.SetSiblingIndex(settings.GetSiblingIndex() + 1);
                var button = account.GetComponent<Button>();
                OnlineUiKit.Retarget(button, "ACCOUNT", OpenAccount);
                button.image.color = new Color32(176, 246, 166, 255);
            }

            var credits = buttons.Find("CreditsButton");
            if (credits != null)
            {
                credits.name = "RankingButton";
                OnlineUiKit.Retarget(credits.GetComponent<Button>(), "RANKING", OpenRanking);
            }

            // 5 nút (PLAY SOLO, VERSUS, SETTINGS, ACCOUNT, RANKING): thu gọn để vừa giữa cổng thành và dòng chữ cuối.
            var layout = buttons.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.spacing = 16;
            int count = 0;
            foreach (Transform child in buttons)
            {
                if (!child.gameObject.activeSelf) continue;
                count++;
                var element = child.GetComponent<LayoutElement>();
                if (element != null) element.preferredHeight = element.minHeight = 100;
            }
            var rect = (RectTransform)buttons;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, count * 100 + (count - 1) * 16);
        }

        private void Show(GameObject screen)
        {
            if (_menu.mainMenuPanel != null) _menu.mainMenuPanel.SetActive(false);
            foreach (var s in new[] { _account, _login, _register, _guest, _ranking })
                if (s != null) s.SetActive(s == screen);
            _current = screen;
        }

        private void BackToMainMenu()
        {
            _menu.PlayButtonBack();
            if (_current != null) _current.SetActive(false);
            _current = null;
            if (_menu.mainMenuPanel != null) _menu.mainMenuPanel.SetActive(true);
        }

        private void Back()
        {
            if (_current == _login || _current == _register || _current == _guest)
            {
                _menu.PlayButtonBack();
                ShowAccountScreen(null);
            }
            else BackToMainMenu();
        }

        private void Update()
        {
            if (_current == null || !_current.activeInHierarchy) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) { Back(); return; }
            if (keyboard.tabKey.wasPressedThisFrame) FocusNext(CurrentFields(), keyboard.shiftKey.isPressed ? -1 : 1);

            if (_busy && _busyLabel != null && Time.unscaledTime - _busySince > 4f && _busyLabel.text == "PLEASE WAIT...")
                _busyLabel.text = "WAKING UP THE SERVER...\nTHIS CAN TAKE UP TO 1 MINUTE";
        }

        // ================================================================ ACCOUNT
        private void BuildAccount()
        {
            var d = _kit.MakeScreen("AccountPanel", "ACCOUNT", new Vector2(820, 1150), 1040, 470, out _account);
            _accState = _kit.Label(d, "State", "", new Vector2(0, 335), new Vector2(660, 50), 24, OnlineUiKit.Muted);
            _accName = _kit.PlainText(d, "PlayerName", "", new Vector2(0, 268), new Vector2(660, 72), 48, OnlineUiKit.Cream);
            _accInfo = _kit.Label(d, "Info", "", new Vector2(0, 196), new Vector2(660, 46), 20, OnlineUiKit.Good);
            _accLogin = _kit.BannerButton(d, "LoginButton", "LOG IN", Vector2.zero, new Vector2(540, 110), () => { _menu.PlayButtonClick(); OpenForm(_login); });
            _accRegister = _kit.BannerButton(d, "RegisterButton", "REGISTER", Vector2.zero, new Vector2(540, 110), () => { _menu.PlayButtonClick(); OpenForm(_register); });
            _accGuest = _kit.BannerButton(d, "GuestButton", "PLAY AS GUEST", Vector2.zero, new Vector2(540, 110), () => { _menu.PlayButtonClick(); OpenForm(_guest); });
            _accLogout = _kit.BannerButton(d, "LogoutButton", "LOG OUT", Vector2.zero, new Vector2(540, 110), Logout);
            _accLogin.image.color = new Color32(163, 250, 255, 255);
            _accRegister.image.color = new Color32(255, 229, 152, 255);
            _accGuest.image.color = new Color32(229, 193, 255, 255);
            _accLogout.image.color = new Color32(255, 170, 160, 255);
            _accHint = _kit.Label(d, "Hint", "", new Vector2(0, -292), new Vector2(680, 90), 19, OnlineUiKit.Muted);
            _kit.BannerButton(d, "BackButton", "BACK", new Vector2(0, -420), new Vector2(450, 110), BackToMainMenu, 35);
        }

        private void OpenAccount()
        {
            if (_account == null) return;
            _menu.PlayButtonClick();
            ShowAccountScreen(null);
        }

        private void ShowAccountScreen(string message)
        {
            Show(_account);
            bool account = LeaderboardApi.IsLoggedIn;
            bool guest = !account && !string.IsNullOrWhiteSpace(LeaderboardApi.GuestName);

            _accState.text = account ? "SIGNED IN AS" : guest ? "PLAYING AS GUEST" : "NOT SIGNED IN";
            _accName.text = account ? LeaderboardApi.DisplayName : guest ? LeaderboardApi.GuestName : "";
            _accInfo.text = "";

            _accLogin.gameObject.SetActive(!account);
            _accRegister.gameObject.SetActive(!account);
            _accGuest.gameObject.SetActive(!account);
            _accLogout.gameObject.SetActive(account);
            _accGuest.GetComponentInChildren<TMP_Text>(true).text = guest ? "CHANGE GUEST NAME" : "PLAY AS GUEST";

            float y = 85;
            foreach (var b in new[] { _accLogin, _accRegister, _accGuest, _accLogout })
            {
                if (!b.gameObject.activeSelf) continue;
                ((RectTransform)b.transform).anchoredPosition = new Vector2(0, y);
                y -= 125;
            }

            int pending = OnlineAccountOverlay.PendingScore;
            if (!string.IsNullOrEmpty(message)) _accHint.text = message;
            else if (account) _accHint.text = "YOUR SOLO SCORES ARE SAVED\nTO THE ONLINE RANKING";
            else if (pending > 0) _accHint.text = $"YOUR LAST SCORE ({pending}) WILL BE SAVED\nWHEN YOU LOG IN OR PICK A NAME";
            else if (guest) _accHint.text = "GUEST NAMES HAVE NO PASSWORD.\nREGISTER TO KEEP YOUR NAME";
            else _accHint.text = "LOG IN OR PICK A NAME\nTO SAVE YOUR SCORE ONLINE";

            if (account)
                StartCoroutine(LeaderboardApi.GetMe(
                    me => { if (_current == _account) _accInfo.text = $"BEST {me.best_score}   GAMES {me.games}"; },
                    e => { if (_current == _account && !LeaderboardApi.IsLoggedIn) ShowAccountScreen(e.Message); }));
        }

        private void Logout()
        {
            _menu.PlayButtonBack();
            LeaderboardApi.Logout();
            ShowAccountScreen("YOU ARE LOGGED OUT");
        }

        private void OpenForm(GameObject form)
        {
            Show(form);
            var fields = CurrentFields();
            if (form == _guest) _guestName.text = LeaderboardApi.GuestName;
            if (form == _login) _loginPass.text = "";
            if (form == _register) _regPass.text = "";
            SetStatus(form == _login ? _loginStatus : form == _register ? _regStatus : _guestStatus, "", OnlineUiKit.Muted);
            if (fields != null && fields.Length > 0) Focus(fields[0]);
        }

        /// <summary>Sau khi đăng nhập / đăng ký / đặt tên: về màn Account và gửi điểm đang chờ (nếu có).</summary>
        private void SignedIn(string message)
        {
            _menu.PlayButtonConfirm();
            ShowAccountScreen(message);
            if (OnlineAccountOverlay.PendingScore > 0)
                OnlineAccountOverlay.SubmitPending(report => { if (_current == _account) _accHint.text = report; });
        }

        // ================================================================ LOG IN
        private void BuildLogin()
        {
            var d = _kit.MakeScreen("LoginPanel", "LOG IN", new Vector2(820, 1100), 990, 445, out _login);
            _kit.Label(d, "UserLabel", "USERNAME", new Vector2(0, 305), new Vector2(580, 44), 24, OnlineUiKit.Cream);
            _loginUser = _kit.Field(d, "Username", new Vector2(0, 233), new Vector2(580, 88), "your username", false, 20, UsernameChar);
            _kit.Label(d, "PassLabel", "PASSWORD", new Vector2(0, 140), new Vector2(580, 44), 24, OnlineUiKit.Cream);
            _loginPass = _kit.Field(d, "Password", new Vector2(0, 68), new Vector2(580, 88), "your password", true, 72);
            _loginStatus = _kit.Label(d, "Status", "", new Vector2(0, -32), new Vector2(680, 74), 19, OnlineUiKit.Muted);
            _loginSubmit = _kit.GreenButton(d, "LoginButton", "LOG IN", new Vector2(0, -148), new Vector2(540, 118), SubmitLogin);
            _kit.BannerButton(d, "ToRegister", "NEW HERE? REGISTER", new Vector2(0, -275), new Vector2(500, 92), () => { _menu.PlayButtonClick(); OpenForm(_register); }, 22);
            _kit.BannerButton(d, "BackButton", "BACK", new Vector2(0, -395), new Vector2(420, 100), Back, 33);
            _loginFields = new[] { _loginUser, _loginPass };
            HookSubmit(_loginFields, SubmitLogin);
        }

        private void SubmitLogin()
        {
            if (_busy) return;
            string user = _loginUser.text.Trim(), pass = _loginPass.text;
            if (user.Length == 0 || pass.Length == 0) { SetStatus(_loginStatus, "ENTER YOUR USERNAME AND PASSWORD", OnlineUiKit.Bad); return; }
            _menu.PlayButtonClick();
            BeginBusy(_loginStatus, _loginSubmit);
            StartCoroutine(LeaderboardApi.Login(user, pass,
                auth => { EndBusy(); _loginPass.text = ""; SignedIn("WELCOME BACK!"); },
                e => { EndBusy(); SetStatus(_loginStatus, e.Message, OnlineUiKit.Bad); }));
        }

        // ================================================================ REGISTER
        private void BuildRegister()
        {
            var d = _kit.MakeScreen("RegisterPanel", "REGISTER", new Vector2(820, 1240), 1130, 518, out _register);
            _kit.Label(d, "UserLabel", "USERNAME", new Vector2(0, 385), new Vector2(580, 42), 23, OnlineUiKit.Cream);
            _regUser = _kit.Field(d, "Username", new Vector2(0, 315), new Vector2(580, 86), "3-20 letters or digits", false, 20, UsernameChar);
            _kit.Label(d, "PassLabel", "PASSWORD", new Vector2(0, 225), new Vector2(580, 42), 23, OnlineUiKit.Cream);
            _regPass = _kit.Field(d, "Password", new Vector2(0, 155), new Vector2(580, 86), "at least 6 characters", true, 72);
            _kit.Label(d, "NameLabel", "DISPLAY NAME (OPTIONAL)", new Vector2(0, 65), new Vector2(580, 42), 21, OnlineUiKit.Cream);
            _regName = _kit.Field(d, "DisplayName", new Vector2(0, -5), new Vector2(580, 86), "shown on the ranking", false, 20, NameChar);
            _regStatus = _kit.Label(d, "Status", "", new Vector2(0, -102), new Vector2(680, 74), 19, OnlineUiKit.Muted);
            _regSubmit = _kit.GreenButton(d, "CreateButton", "CREATE ACCOUNT", new Vector2(0, -220), new Vector2(580, 118), SubmitRegister, 32);
            _kit.BannerButton(d, "ToLogin", "HAVE AN ACCOUNT? LOG IN", new Vector2(0, -345), new Vector2(560, 92), () => { _menu.PlayButtonClick(); OpenForm(_login); }, 20);
            _kit.BannerButton(d, "BackButton", "BACK", new Vector2(0, -465), new Vector2(420, 100), Back, 33);
            _regFields = new[] { _regUser, _regPass, _regName };
            HookSubmit(_regFields, SubmitRegister);
        }

        private void SubmitRegister()
        {
            if (_busy) return;
            string user = _regUser.text.Trim(), pass = _regPass.text, name = _regName.text.Trim();
            if (user.Length < 3) { SetStatus(_regStatus, "USERNAME: 3-20 LETTERS OR DIGITS", OnlineUiKit.Bad); return; }
            if (pass.Length < 6) { SetStatus(_regStatus, "PASSWORD: AT LEAST 6 CHARACTERS", OnlineUiKit.Bad); return; }
            if (name.Length == 1) { SetStatus(_regStatus, "DISPLAY NAME: 2-20 CHARACTERS", OnlineUiKit.Bad); return; }
            _menu.PlayButtonClick();
            BeginBusy(_regStatus, _regSubmit);
            StartCoroutine(LeaderboardApi.Register(user, pass, name,
                auth => { EndBusy(); _regPass.text = ""; SignedIn("ACCOUNT CREATED!"); },
                e => { EndBusy(); SetStatus(_regStatus, e.Message, OnlineUiKit.Bad); }));
        }

        // ================================================================ GUEST
        private void BuildGuest()
        {
            var d = _kit.MakeScreen("GuestPanel", "GUEST", new Vector2(820, 1000), 900, 395, out _guest);
            _kit.Label(d, "NameLabel", "YOUR NAME", new Vector2(0, 250), new Vector2(580, 46), 26, OnlineUiKit.Cream);
            _guestName = _kit.Field(d, "GuestName", new Vector2(0, 175), new Vector2(580, 90), "2-20 characters", false, 20, NameChar);
            _kit.Label(d, "Hint", "SHOWN ON THE ONLINE RANKING.\nNO PASSWORD NEEDED.", new Vector2(0, 80), new Vector2(660, 70), 18, OnlineUiKit.Muted);
            _guestStatus = _kit.Label(d, "Status", "", new Vector2(0, 5), new Vector2(680, 64), 19, OnlineUiKit.Muted);
            _guestSubmit = _kit.GreenButton(d, "SaveButton", "SAVE NAME", new Vector2(0, -110), new Vector2(540, 118), SubmitGuest);
            _kit.BannerButton(d, "BackButton", "BACK", new Vector2(0, -265), new Vector2(420, 100), Back, 33);
            _guestFields = new[] { _guestName };
            HookSubmit(_guestFields, SubmitGuest);
        }

        private void SubmitGuest()
        {
            string name = _guestName.text.Trim();
            if (name.Length < 2) { SetStatus(_guestStatus, "NAME MUST BE 2-20 CHARACTERS", OnlineUiKit.Bad); return; }
            LeaderboardApi.GuestName = name;
            SignedIn("NAME SAVED. GOOD LUCK!");
        }

        // ================================================================ RANKING
        private void BuildRanking()
        {
            var d = _kit.MakeScreen("RankingPanel", "RANKING", new Vector2(820, 1300), 1190, 553, out _ranking);
            _kit.Label(d, "ColRank", "RANK", new Vector2(-283, 432), new Vector2(110, 40), 17, OnlineUiKit.Muted);
            _kit.Label(d, "ColName", "PLAYER", new Vector2(-55, 432), new Vector2(330, 40), 17, OnlineUiKit.Muted, TextAlignmentOptions.MidlineLeft);
            _kit.Label(d, "ColScore", "BEST", new Vector2(240, 432), new Vector2(160, 40), 17, OnlineUiKit.Muted, TextAlignmentOptions.MidlineRight);

            _rows = new RankRow[RankRows];
            for (int i = 0; i < RankRows; i++)
            {
                float y = 372 - i * 64;
                var row = new RankRow
                {
                    Back = OnlineUiKit.Box(d, "Row" + (i + 1), new Vector2(0, y), new Vector2(690, 56), RowColor(i, false)),
                };
                row.Rank = _kit.Label(row.Back.transform, "Rank", (i + 1).ToString(), new Vector2(-283, 0), new Vector2(100, 50), 26, OnlineUiKit.Cream);
                row.Name = _kit.PlainText(row.Back.transform, "Name", "", new Vector2(-55, 0), new Vector2(330, 50), 30, OnlineUiKit.Cream, TextAlignmentOptions.MidlineLeft);
                row.Score = _kit.Label(row.Back.transform, "Score", "", new Vector2(240, 0), new Vector2(160, 50), 24, OnlineUiKit.Cream, TextAlignmentOptions.MidlineRight);
                _rows[i] = row;
            }

            _rankStatus = _kit.Label(d, "Status", "", new Vector2(0, 90), new Vector2(620, 220), 24, OnlineUiKit.Ink);
            _rankYou = _kit.Label(d, "You", "", new Vector2(0, -292), new Vector2(690, 56), 20, OnlineUiKit.Good);
            _rankRefresh = _kit.BannerButton(d, "RefreshButton", "REFRESH", new Vector2(-175, -440), new Vector2(310, 100), () => { _menu.PlayButtonClick(); LoadRanking(); }, 28);
            _kit.BannerButton(d, "BackButton", "BACK", new Vector2(175, -440), new Vector2(310, 100), BackToMainMenu, 30);
            _rankRefresh.image.color = new Color32(163, 250, 255, 255);
        }

        private static Color RowColor(int index, bool me) =>
            me ? new Color(.45f, .95f, 1f, .55f) : index % 2 == 0 ? new Color(0f, 0f, 0f, .30f) : new Color(1f, 1f, 1f, .07f);

        private void OpenRanking()
        {
            if (_ranking == null) return;
            _menu.PlayButtonClick();
            Show(_ranking);
            LoadRanking();
        }

        private void LoadRanking()
        {
            if (_busy) return;
            foreach (var row in _rows) row.Back.gameObject.SetActive(false);
            _rankYou.text = "";
            BeginBusy(_rankStatus, _rankRefresh);
            OnlineAccountOverlay.SubmitPending(null); // gửi luôn điểm còn treo (nếu có) để bảng mới nhất
            StartCoroutine(LeaderboardApi.GetLeaderboard("solo", RankRows, FillRanking,
                e => { EndBusy(); _rankStatus.text = e.Message; OnlineUiKit.SetColor(_rankStatus, OnlineUiKit.Bad); }));
        }

        private void FillRanking(LeaderboardApi.BoardRow[] rows)
        {
            EndBusy();
            string me = LeaderboardApi.CurrentPlayerName;
            int myRank = 0;
            for (int i = 0; i < _rows.Length; i++)
            {
                var view = _rows[i];
                bool has = i < rows.Length;
                view.Back.gameObject.SetActive(has);
                if (!has) continue;
                var r = rows[i];
                bool isMe = !string.IsNullOrEmpty(me) && string.Equals(r.player_name, me, System.StringComparison.OrdinalIgnoreCase);
                if (isMe) myRank = r.rank;
                view.Back.color = RowColor(i, isMe);
                view.Rank.text = r.rank.ToString();
                view.Rank.color = r.rank == 1 ? new Color32(255, 215, 90, 255) : r.rank == 2 ? new Color32(222, 228, 245, 255)
                    : r.rank == 3 ? new Color32(232, 162, 100, 255) : OnlineUiKit.Cream;
                view.Name.text = r.registered ? r.player_name : r.player_name + " <size=60%><color=#BFB3E0>(guest)</color></size>";
                view.Score.text = r.best_score.ToString();
            }

            OnlineUiKit.SetColor(_rankStatus, OnlineUiKit.Ink);
            _rankStatus.text = rows.Length == 0 ? "NO SCORES YET.\nPLAY SOLO TO BE THE FIRST!" : "";
            if (string.IsNullOrEmpty(me)) _rankYou.text = "OPEN ACCOUNT TO JOIN THE RANKING";
            else if (myRank > 0) _rankYou.text = $"YOU ARE RANK {myRank}!";
            else _rankYou.text = "YOU ARE NOT IN THE TOP 10 YET";
        }

        // ================================================================ GAME OVER
        private void BuildGameOverStatus()
        {
            var design = _menu.gameOverPanel != null ? _menu.gameOverPanel.transform.Find("SafeArea/Design") : null;
            if (design == null) return;
            // Khoảng trống giữa hai nút tròn Restart (-160) và Home (+160).
            _gameOverStatus = _kit.Label(design, "OnlineStatus", OnlineAccountOverlay.GameOverStatus, new Vector2(0, -287), new Vector2(214, 110), 21, new Color32(255, 215, 90, 255));
            _gameOverStatus.lineSpacing = 28;
        }

        private void ShowGameOverStatus(string text)
        {
            if (_gameOverStatus != null) _gameOverStatus.text = text;
        }

        // ================================================================ tiện ích
        private void BeginBusy(TextMeshProUGUI label, Button button)
        {
            _busy = true;
            _busySince = Time.unscaledTime;
            _busyLabel = label;
            _busyButton = button;
            if (button != null) button.interactable = false;
            SetStatus(label, "PLEASE WAIT...", OnlineUiKit.Ink);
        }

        private void EndBusy()
        {
            _busy = false;
            if (_busyButton != null) _busyButton.interactable = true;
            if (_busyLabel != null && (_busyLabel.text.StartsWith("PLEASE WAIT") || _busyLabel.text.StartsWith("WAKING UP")))
                _busyLabel.text = "";
            _busyLabel = null;
            _busyButton = null;
        }

        private static void SetStatus(TextMeshProUGUI label, string text, Color color)
        {
            if (label == null) return;
            label.text = text;
            OnlineUiKit.SetColor(label, color);
        }

        private TMP_InputField[] CurrentFields() =>
            _current == _login ? _loginFields : _current == _register ? _regFields : _current == _guest ? _guestFields : null;

        private static void HookSubmit(TMP_InputField[] fields, System.Action submit)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                int index = i;
                fields[i].onSubmit.AddListener(_ =>
                {
                    if (index == fields.Length - 1) submit();
                    else Focus(fields[index + 1]);
                });
            }
        }

        private static void FocusNext(TMP_InputField[] fields, int step)
        {
            if (fields == null || fields.Length == 0) return;
            int current = -1;
            for (int i = 0; i < fields.Length; i++) if (fields[i].isFocused) current = i;
            int next = current < 0 ? 0 : (current + step + fields.Length) % fields.Length;
            Focus(fields[next]);
        }

        private static void Focus(TMP_InputField field)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
        }

        private static char UsernameChar(string text, int index, char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '.' ? c : '\0';

        private static char NameChar(string text, int index, char c) =>
            char.IsLetterOrDigit(c) || c == ' ' || c == '.' || c == '-' || c == '_' ? c : '\0';

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
