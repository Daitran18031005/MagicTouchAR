using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MagicTouch.Gameplay;

public static partial class UIAutoBuilder
{
    private static readonly Color LeftColor = new Color32(130, 239, 255, 255);
    private static readonly Color RightColor = new Color32(255, 171, 216, 255);

    private static void BuildVersus(Transform root, GameManager game)
    {
        var buttons = root.Find("MainMenu/SafeArea/Design/MenuButtons");
        Place((RectTransform)buttons, new Vector2(0, -275), new Vector2(430, 530));
        MenuButton(buttons, "VersusButton", "VERSUS", _menu.OpenVersusSetup, RightColor);
        buttons.Find("VersusButton").SetSiblingIndex(1);
        BuildVersusSetup(root);
        BuildVersusResult(root);
        BuildVersusHUD(root, game);
        var pause = root.Find("Pause/SafeArea/Design");
        _menu.soloPauseElements = new[] { "Trophy", "BestLabel", "BestScore", "Medal", "ScoreLabel", "Score" }
            .Select(n => pause.Find(n).gameObject).ToArray();
        var summary = Node(pause, "VersusSummary");
        Place(summary, new Vector2(0, 152), new Vector2(650, 450));
        _menu.versusPauseSummary = summary.gameObject;
        _menu.versusPauseScores = Label(summary, "Scores", "P1  0\n\nP2  0\n\n03:00 LEFT", Vector2.zero, new Vector2(620, 400), 37, Cream);
        summary.gameObject.SetActive(false);
    }

    private static void BuildVersusSetup(Transform root)
    {
        var panel = Panel(root, "VersusSetup", true, true);
        _menu.versusSetupPanel = panel.gameObject;
        var d = Design(Safe(panel), new Vector2(900, 1320));
        Picture(d, "Tablet", Sprite("Tablet"), Vector2.zero, new Vector2(850, 1190));
        Header(d, "VERSUS", 560);
        Label(d, "Subtitle", "TWO PLAYERS / ONE ARENA", new Vector2(0, 430), new Vector2(760, 55), 23, Cream);
        var left = Picture(d, "LeftWizard", Wizard(), new Vector2(-190, 296), new Vector2(160, 172));
        left.preserveAspect = true; left.color = LeftColor;
        var right = Picture(d, "RightWizard", Wizard(), new Vector2(190, 296), new Vector2(160, 172));
        right.preserveAspect = true; right.color = RightColor;
        Label(d, "LeftLabel", "P1 / LEFT", new Vector2(-190, 180), new Vector2(360, 60), 28, LeftColor);
        Label(d, "RightLabel", "P2 / RIGHT", new Vector2(190, 180), new Vector2(360, 60), 28, RightColor);
        Label(d, "Rules", "DRAW IN YOUR HALF\nRACE FOR SHARED TARGETS\nNO HP. MOST POINTS WINS.", new Vector2(0, 27), new Vector2(770, 180), 23, Cream);
        int minutes = VersusMatch.ValidMinutes(PlayerPrefs.GetInt("VersusMinutes", 3));
        _menu.versusDurationText = Label(d, "Duration", "MATCH TIME: " + minutes + " MIN", new Vector2(0, -155), new Vector2(760, 70), 30, Cream);
        _menu.durationButtons = new[] {
            Button(d, "ThreeMinutes", "3 MIN", "Banner", new Vector2(-240, -268), new Vector2(220, 100), _menu.SelectThreeMinutes, 28),
            Button(d, "FiveMinutes", "5 MIN", "Banner", new Vector2(0, -268), new Vector2(220, 100), _menu.SelectFiveMinutes, 28),
            Button(d, "SevenMinutes", "7 MIN", "Banner", new Vector2(240, -268), new Vector2(220, 100), _menu.SelectSevenMinutes, 28)
        };
        for (int i = 0; i < 3; i++) _menu.durationButtons[i].image.color = minutes == 3 + 2 * i ? new Color32(148, 255, 130, 255) : Color.white;
        Button(d, "StartMatch", "START MATCH", "GreenButton", new Vector2(0, -423), new Vector2(640, 125), _menu.StartVersusGame, 36);
        Button(d, "Back", "BACK", "Banner", new Vector2(0, -542), new Vector2(340, 75), _menu.CloseVersusSetup, 27);
    }

    private static void BuildVersusResult(Transform root)
    {
        var panel = Panel(root, "VersusResult", true, true);
        _menu.versusResultPanel = panel.gameObject;
        var d = Design(Safe(panel), new Vector2(820, 1120));
        Picture(d, "Frame", Sprite("Modal"), Vector2.zero, new Vector2(770, 1030));
        Header(d, "TIME'S UP", 460);
        _menu.versusWinnerText = Label(d, "Winner", "PLAYER 1 WINS!", new Vector2(0, 330), new Vector2(690, 88), 34, Cream);
        Picture(d, "Trophy", Sprite("Trophy"), new Vector2(0, 214), new Vector2(110, 110));
        _menu.versusResultScores = Label(d, "Scores", "<color=#82EFFF>PLAYER 1\n0</color>\n\n<color=#FFABD8>PLAYER 2\n0</color>", new Vector2(0, -25), new Vector2(660, 355), 40, Cream);
        Button(d, "Rematch", "REMATCH", "GreenButton", new Vector2(0, -304), new Vector2(560, 125), _menu.RestartGame, 38);
        IconButton(d, "Home", "Home", new Vector2(0, -445), 100, _menu.ReturnToMainMenu);
    }

    private static void BuildVersusHUD(Transform root, GameManager game)
    {
        var panel = Panel(root, "VersusHUD", false);
        _menu.versusHUDPanel = panel.gameObject;
        Undo.RecordObject(game, "Connect versus HUD");
        game.versusHUD = panel.gameObject;
        // One arena and one spawn stream. This line divides input ownership, not the enemy pool.
        var line = Picture(panel, "Divider", null, Vector2.zero, new Vector2(3, 1));
        line.color = new Color(1, .94f, .72f, .38f);
        line.rectTransform.anchorMin = new Vector2(.5f, 0);
        line.rectTransform.anchorMax = new Vector2(.5f, 1);
        line.rectTransform.offsetMin = new Vector2(-1.5f, 230);
        line.rectTransform.offsetMax = new Vector2(1.5f, -165);
        var safe = Safe(panel);
        var clock = Picture(safe, "Clock", Sprite("Modal"), new Vector2(0, -75), new Vector2(255, 118));
        clock.rectTransform.anchorMin = clock.rectTransform.anchorMax = new Vector2(.5f, 1);
        game.matchTimerText = Label(clock.transform, "Time", "03:00", Vector2.zero, new Vector2(235, 92), 38, Cream);
        game.leftScoreText = BuildPlayerHUD(safe, "Player1", "P1 / LEFT", .03f, .47f, LeftColor);
        game.rightScoreText = BuildPlayerHUD(safe, "Player2", "P2 / RIGHT", .53f, .97f, RightColor);
        panel.SetAsFirstSibling(); // Modal panels and pause controls remain above the duel HUD.
    }

    private static TMPro.TextMeshProUGUI BuildPlayerHUD(Transform parent, string name, string title, float minX, float maxX, Color color)
    {
        var background = Picture(parent, name, Sprite("Modal"), new Vector2(0, 125), new Vector2(0, 182));
        background.rectTransform.anchorMin = new Vector2(minX, 0);
        background.rectTransform.anchorMax = new Vector2(maxX, 0);
        var label = Label(background.transform, "Player", title, new Vector2(0, 47), new Vector2(430, 55), 28, color);
        label.rectTransform.anchorMin = new Vector2(0, .5f); label.rectTransform.anchorMax = new Vector2(1, .5f);
        label.rectTransform.sizeDelta = new Vector2(-30, 55);
        var score = Label(background.transform, "Score", "0", new Vector2(0, -28), new Vector2(430, 86), 46, color);
        score.rectTransform.anchorMin = new Vector2(0, .5f); score.rectTransform.anchorMax = new Vector2(1, .5f);
        score.rectTransform.sizeDelta = new Vector2(-30, 86);
        return score;
    }
}
