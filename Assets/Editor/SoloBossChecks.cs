using System;
using System.Collections;
using System.Collections.Generic;
using MagicTouch.Gameplay;
using MagicTouch.Recognition;
using UnityEditor;
using UnityEngine;

namespace MagicTouch.Editor
{
    /// <summary>Opt-in checks invoked from the Editor bridge. Never runs on reload or saves a scene.</summary>
    public static class SoloBossChecks
    {
        private const string BossFolder = "Assets/Sprites/bosses/castle/";
        private static readonly string[] Definitions =
        {
            BossFolder + "dullahan_guard/DullahanGuard.asset",
            BossFolder + "ember_drake_tyrant/EmberDrake.asset"
        };

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[SoloBossChecks] " + message);
        }

        public static void ValidateModel()
        {
            var p = new SoloBossProgress(60f, new[] { 100, 200 }, 10);
            Require(p.MaximumHealth == 300 && p.SegmentCount == 2, "Expected 300 total HP in two segments.");
            Require(p.RecordPop(true) == SoloBossHit.None && p.Elapsed == 0f, "Boss targets cannot advance the minion wave.");
            Require(!p.AdvanceSurvive(59f) && p.Phase == SoloBossPhase.Minions && Mathf.Approximately(p.Fill, 59f / 60f), "Wave ended early.");
            Require(p.AdvanceSurvive(5f) && p.Phase == SoloBossPhase.Clearing && p.Fill == 1f, "The wave must end exactly once, then wait for a clear field.");
            Require(!p.AdvanceSurvive(5f), "The wave ended twice.");
            p.BeginBoss();
            Require(p.Phase == SoloBossPhase.Clearing, "The boss cannot arrive before the field is clear.");
            p.BeginSummon();
            Require(p.Phase == SoloBossPhase.Summoning && p.Health == 300, "Clearing must lead to summoning without damaging the boss.");
            p.BeginBoss();
            Require(p.RecordPop(false) == SoloBossHit.None && p.Health == 300, "Ordinary enemy cleanup must not damage boss.");
            for (int i = 1; i <= 9; i++)
                Require(p.RecordPop(true) == SoloBossHit.Damaged && p.CurrentSegment == 0 && p.Health == 300 - i * 10,
                    "Red layer must absorb the first 100 HP, 10 per pop.");
            Require(p.RecordPop(true) == SoloBossHit.SegmentCleared && p.CurrentSegment == 1 && p.SegmentFill(0) == 0f
                && p.SegmentFill(1) == 1f, "Clearing red must reveal an untouched green layer.");
            for (int i = 1; i <= 19; i++)
                Require(p.RecordPop(true) == SoloBossHit.Damaged && p.CurrentSegment == 1, "Green layer must take 20 pops.");
            Require(p.RecordPop(true) == SoloBossHit.Defeated && p.Phase == SoloBossPhase.Dying && p.Health == 0, "30 boss pops must kill the boss.");
            Require(p.RecordPop(true) == SoloBossHit.None && p.Health == 0, "Health underflow.");
            p.CompleteDeath();
            Require(p.Phase == SoloBossPhase.Victory, "Death must finish before victory.");
            p.Reset();
            Require(p.Health == 300 && p.Elapsed == 0f && p.Fill == 0 && p.Phase == SoloBossPhase.Minions && p.SegmentFill(0) == 1f,
                "Replay must reset the entire stage.");

            var single = new SoloBossProgress(3f, new[] { 200 }, 10); // a mini boss: one layer
            single.AdvanceSurvive(3f); single.BeginSummon(); single.BeginBoss();
            for (int i = 0; i < 19; i++) Require(single.RecordPop(true) == SoloBossHit.Damaged, "Single-layer boss damage.");
            Require(single.RecordPop(true) == SoloBossHit.Defeated, "Single-layer boss must end without a segment-cleared step.");

            var bag = new BossSkillBag(1234);
            Require(bag.Next(0) == -1 && bag.Next(1) == 0, "Bag edge cases.");
            bag.Reset();
            int last = -1;
            for (int round = 0; round < 40; round++)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < 5; i++)
                {
                    int pick = bag.Next(5);
                    Require(pick >= 0 && pick < 5 && pick != last, "A skill repeated back to back.");
                    Require(seen.Add(pick), "A skill repeated inside one bag.");
                    last = pick;
                }
            }
            Debug.Log("[SoloBossChecks] PASS: timed wave, clear-the-field gate, red+green layers, single layer, reset, skill bag.");
        }

        /// <summary>Checks every boss data asset against the animation set it points at.</summary>
        public static void ValidateDefinition()
        {
            int checkedBosses = 0;
            foreach (var path in Definitions)
            {
                var def = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
                if (def == null) { Require(path != Definitions[Definitions.Length - 1], "EmberDrake.asset is missing."); continue; }
                Require(def.Animations != null, path + " has no animation set.");
                foreach (var segment in def.Segments)
                {
                    Require(def.Animations.Get(segment.restClip) != null, "Missing rest clip " + segment.restClip);
                    Require(segment.skills.Length > 0, def.name + ": segment " + segment.name + " has no skills.");
                    foreach (var skill in segment.skills)
                    {
                        var boss = def.Animations.Get(skill.BossClip);
                        Require(boss != null && boss.Length > skill.ImpactFrame, "Skill clip too short: " + skill.name);
                        var fx = def.Animations.Get(skill.FxClip);
                        Require(fx != null && fx.Length > skill.FxHitFrame, "Skill fx clip too short: " + skill.name);
                        Require(skill.PlayerHeightMultiple > 0f, "Skill effect must be sized from the player's height: " + skill.name);
                    }
                }
                foreach (var key in new[] { def.HurtClip, def.DeathClip, def.RoarClip, def.WhirlwindClip, def.PillarClip })
                    Require(string.IsNullOrEmpty(key) || def.Animations.Get(key) != null, "Missing clip " + key);
                foreach (var clip in def.Animations.Clips)
                    foreach (var sprite in clip.frames)
                        Require(sprite != null && sprite.texture != null, "Null sprite in clip " + clip.key);
                Require(def.SurviveSeconds >= 1f, def.name + ": no survive time.");
                checkedBosses++;
                Debug.Log($"[SoloBossChecks] {def.name}: {def.Segments.Length} segment(s), {def.SegmentHealths().Length} layer(s), {def.Animations.Clips.Length} clips, survive {def.SurviveSeconds}s.");
            }
            var ember = AssetDatabase.LoadAssetAtPath<BossDefinition>(Definitions[Definitions.Length - 1]);
            int total = 0;
            foreach (var s in ember.Segments) total += s.health;
            Require(total == 300 && ember.Segments.Length == 2, "Expected red 100 + green 200 for Ember.");
            Require(ember.Segments[0].skills.Length == 3 && ember.Segments[1].skills.Length == 5, "Ember red uses 3 claw skills, green all 5.");
            Require(ember.RoarSound != null, "Roar sound is not assigned.");
            Debug.Log($"[SoloBossChecks] PASS: {checkedBosses} boss definition(s) ok.");
        }

        /// <summary>Plays the whole run in Play Mode with the waves skipped: every stage, the checkpoint and the victory.</summary>
        public static IEnumerator ValidateRuntime()
        {
            Require(Application.isPlaying, "Runtime checks require Play Mode.");
            var gm = GameManager.Instance;
            var boss = gm.GetComponent<SoloBossEncounter>();
            var spawner = EnemySpawner.Instance;
            var menu = MagicTouch.UI.MenuManager.Instance;
            Require(boss != null && boss.Boss != null, "Scene encounter is not installed.");
            if (menu != null) menu.ReturnToMainMenu();
            gm.ConfigureMode(GameMode.Solo);
            gm.isGameActive = true;
            gm.StartNewGame();
            gm.StartGame();
            Require(boss.StageIndex == 0 && boss.Attempt == 1 && boss.CheckpointStage == 0, "A new run must start at stage 0, attempt 1.");

            for (int stage = 0; stage < boss.StageCount; stage++)
            {
                bool lastStage = stage == boss.StageCount - 1;
                spawner.StopSpawning();
                spawner.ClearAllEnemies();
                Require(boss.StageIndex == stage, "Wrong stage " + boss.StageIndex + ", expected " + stage);
                Require(boss.Progress.Phase == SoloBossPhase.Minions && boss.Progress.Fill < .5f, "A stage must open with its timed wave.");
                int definitionHealth = boss.Progress.MaximumHealth;

                // The wave ends by time; the boss only comes once the field is empty.
                boss.Progress.AdvanceSurvive(boss.Progress.RequiredSeconds);
                Require(boss.Progress.Phase == SoloBossPhase.Clearing, "The wave must lead to the clear-the-field phase.");
                float deadline = Time.realtimeSinceStartup + 40;
                while (!boss.Fighting && Time.realtimeSinceStartup < deadline) yield return null;
                Require(boss.Fighting && boss.Progress.Phase == SoloBossPhase.Boss, "Boss did not arrive and start fighting at stage " + stage);
                Require(EnemySpawner.ActiveEnemies.Count == boss.Boss.BalloonsAround, "Expected the boss's surrounding balloons.");

                // Balloons must change symbol on a timer (checked once, on the main boss).
                if (lastStage)
                {
                    var before = new List<GestureSymbol>();
                    foreach (var e in EnemySpawner.ActiveEnemies) before.Add(e.AssignedSymbol);
                    deadline = Time.realtimeSinceStartup + boss.Boss.BalloonRerollSeconds + 3f;
                    int changed = 0;
                    while (changed < before.Count - 1 && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        changed = 0;
                        for (int i = 0; i < EnemySpawner.ActiveEnemies.Count && i < before.Count; i++)
                            if (EnemySpawner.ActiveEnemies[i].AssignedSymbol != before[i]) changed++;
                    }
                    Require(changed >= before.Count - 2, "Boss balloons did not change symbol on the timer.");
                }

                // A duplicate Pop call must not deal damage twice.
                int damage = boss.Boss.DamagePerBalloon;
                var first = EnemySpawner.ActiveEnemies[0];
                first.Pop(); first.Pop();
                Require(boss.Progress.Health == definitionHealth - damage, "Duplicate damage detected.");
                gm.currentState = GameState.Paused;
                Time.timeScale = 0;
                Vector3 position = EnemySpawner.ActiveEnemies[0].transform.position;
                yield return new WaitForSecondsRealtime(.3f);
                Require(EnemySpawner.ActiveEnemies[0].transform.position == position && boss.Progress.Health == definitionHealth - damage,
                    "Paused boss encounter advanced.");
                gm.currentState = GameState.Playing;
                Time.timeScale = 1;

                // Take the boss down.
                int guard = 0;
                while (boss.Progress.Phase == SoloBossPhase.Boss && guard++ < 200)
                {
                    yield return null;
                    yield return null;
                    if (EnemySpawner.ActiveEnemies.Count > 0) EnemySpawner.ActiveEnemies[0].Pop();
                }
                Require(boss.Progress.Health == 0, "Boss was not defeated at stage " + stage);

                if (lastStage)
                {
                    deadline = Time.realtimeSinceStartup + 10;
                    while (!gm.SoloBossVictory && Time.realtimeSinceStartup < deadline) yield return null;
                    Require(gm.SoloBossVictory && gm.currentState == GameState.GameOver, "Victory did not finish.");
                }
                else
                {
                    deadline = Time.realtimeSinceStartup + 15;
                    while (boss.StageIndex == stage && Time.realtimeSinceStartup < deadline) yield return null;
                    Require(boss.StageIndex == stage + 1 && boss.CheckpointStage == stage + 1 && !gm.SoloBossVictory,
                        "A prelude boss must open the next stage and save the checkpoint, never win the game.");
                }
            }

            gm.RestartGame(); gm.StartGame();
            Require(boss.StageIndex == 0 && boss.Attempt == 1 && boss.Progress.Phase == SoloBossPhase.Minions, "Replay after victory must be a fresh run.");
            if (menu != null) menu.ReturnToMainMenu();
            else { gm.currentState = GameState.MainMenu; spawner.StopSpawning(); spawner.ClearAllEnemies(); }
            yield return null;
            gm.ConfigureMode(GameMode.Versus);
            gm.isGameActive = true; gm.StartNewGame(); gm.StartGame();
            spawner.StopSpawning(); spawner.ClearAllEnemies();
            spawner.SpawnEnemy().SetSymbol(GestureSymbol.V);
            gm.OnPlayerSymbolRecognized(0, GestureSymbol.V);
            Require(boss.Progress.Elapsed == 0f && boss.Progress.Phase == SoloBossPhase.Minions, "Versus unexpectedly advances the solo run.");
            if (menu != null) menu.ReturnToMainMenu();
            gm.ConfigureMode(GameMode.Solo);
            Debug.Log("[SoloBossChecks] RUNTIME PASS: timed wave, clear gate, every boss stage, checkpoint, duplicate guard, pause, victory only after the main boss, fresh replay, versus isolation.");
        }

        /// <summary>5 attempts per run: a retry resumes at the checkpoint, the 5th loss ends the run, any restart from outside is fresh.</summary>
        public static IEnumerator ValidateAttempts()
        {
            Require(Application.isPlaying, "Runtime checks require Play Mode.");
            var gm = GameManager.Instance;
            var boss = gm.GetComponent<SoloBossEncounter>();
            var spawner = EnemySpawner.Instance;
            var menu = MagicTouch.UI.MenuManager.Instance;
            if (menu != null) menu.ReturnToMainMenu();
            gm.ConfigureMode(GameMode.Solo);
            gm.isGameActive = true; gm.StartNewGame(); gm.StartGame();
            spawner.StopSpawning(); spawner.ClearAllEnemies();

            // Attempt 1 lost before any prelude boss: attempt 2 restarts at stage 0.
            LoseAllLives(gm);
            Require(gm.currentState == GameState.GameOver && boss.AttemptsLeft == 4, "Losing 3 hearts must end attempt 1 with 4 left.");
            gm.RestartGame(); gm.StartGame();
            yield return null;
            Require(boss.Attempt == 2 && boss.StageIndex == 0 && gm.lives == 3, "Attempt 2 must start at stage 0 with full hearts.");

            if (boss.StageCount > 1)
            {
                spawner.StopSpawning(); spawner.ClearAllEnemies();
                boss.Progress.AdvanceSurvive(boss.Progress.RequiredSeconds);
                float deadline = Time.realtimeSinceStartup + 40;
                while (!boss.Fighting && Time.realtimeSinceStartup < deadline) yield return null;
                Require(boss.Fighting, "The prelude boss did not arrive.");
                int guard = 0;
                while (boss.StageIndex == 0 && guard++ < 400)
                {
                    yield return null;
                    if (EnemySpawner.ActiveEnemies.Count > 0 && boss.Fighting) EnemySpawner.ActiveEnemies[0].Pop();
                }
                deadline = Time.realtimeSinceStartup + 15;
                while (boss.StageIndex == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Require(boss.StageIndex == 1 && boss.CheckpointStage == 1, "Beating the prelude boss must save the checkpoint.");
                spawner.StopSpawning(); spawner.ClearAllEnemies();

                LoseAllLives(gm);
                Require(boss.AttemptsLeft == 3, "Attempt 2 lost: 3 left.");
                gm.RestartGame(); gm.StartGame();
                yield return null;
                Require(boss.Attempt == 3 && boss.StageIndex == 1 && boss.Progress.Phase == SoloBossPhase.Minions,
                    "After the checkpoint a retry must restart at the next stage's wave.");
            }

            // Burn the attempts until the last one is running, then lose it: that loss is final.
            while (boss.Attempt < SoloBossEncounter.MaxAttempts)
            {
                spawner.StopSpawning(); spawner.ClearAllEnemies();
                LoseAllLives(gm);
                gm.RestartGame(); gm.StartGame(); yield return null;
            }
            spawner.StopSpawning(); spawner.ClearAllEnemies();
            LoseAllLives(gm);
            Require(gm.currentState == GameState.GameOver && boss.Attempt == SoloBossEncounter.MaxAttempts && boss.AttemptsLeft == 0,
                $"The 5th loss must end the run (state {gm.currentState}, attempt {boss.Attempt}, left {boss.AttemptsLeft}).");
            gm.RestartGame(); gm.StartGame();
            yield return null;
            Require(boss.Attempt == 1 && boss.StageIndex == 0 && boss.CheckpointStage == 0, "After the run ended a restart must be a fresh run.");

            // A restart that was not preceded by a loss (Pause > Restart, menu) is fresh too.
            LoseAllLives(gm);
            gm.RestartGame(); gm.StartGame();
            yield return null;
            Require(boss.Attempt == 2, "Retry after a loss counts as attempt 2.");
            gm.currentState = GameState.Playing;
            gm.RestartGame(); gm.StartGame();
            yield return null;
            Require(boss.Attempt == 1 && boss.StageIndex == 0, "Restarting mid-round must begin a fresh run.");
            if (menu != null) menu.ReturnToMainMenu();
            Debug.Log("[SoloBossChecks] ATTEMPTS PASS: 5 attempts, checkpoint resume, final loss, fresh restarts.");
        }

        /// <summary>Steps a check coroutine from the editor loop (nested enumerators included) and logs the outcome.</summary>
        public static void Run(IEnumerator check, string name)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(check);
            void Step()
            {
                try
                {
                    while (stack.Count > 0)
                    {
                        if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                        var inner = stack.Peek().Current as IEnumerator;
                        if (inner != null) { stack.Push(inner); continue; }
                        return;
                    }
                    EditorApplication.update -= Step;
                }
                catch (Exception e)
                {
                    EditorApplication.update -= Step;
                    Debug.LogError($"[SoloBossChecks] {name} FAIL: {e}");
                }
            }
            EditorApplication.update += Step;
        }

        /// <summary>Warning ring: green, yellow, red in order; a pop before red cancels and hides it; a pop at red does not; it never lingers.</summary>
        public static IEnumerator ValidateRing()
        {
            Require(Application.isPlaying, "Runtime checks require Play Mode.");
            float ready = Time.realtimeSinceStartup + 20;
            while ((GameManager.Instance == null || EnemySpawner.Instance == null) && Time.realtimeSinceStartup < ready) yield return null;
            var gm = GameManager.Instance;
            Require(gm != null, "GameManager did not start.");
            var boss = gm.GetComponent<SoloBossEncounter>();
            var menu = MagicTouch.UI.MenuManager.Instance;
            if (menu != null) menu.ReturnToMainMenu();
            gm.ConfigureMode(GameMode.Solo);
            gm.isGameActive = true; gm.StartNewGame(); gm.StartGame();
            EnemySpawner.Instance.StopSpawning(); EnemySpawner.Instance.ClearAllEnemies();
            boss.Progress.AdvanceSurvive(boss.Progress.RequiredSeconds);
            float deadline = Time.realtimeSinceStartup + 40;
            while (!boss.Fighting && Time.realtimeSinceStartup < deadline) yield return null;
            Require(boss.Fighting, "Boss never started fighting.");
            BossSkillCaster caster = null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<BossSkillCaster>()) caster = c;
            Require(caster != null, "No active caster.");
            var renderer = caster.Marker;
            Require(renderer != null, "Warning ring was not built.");
            var ring = renderer.gameObject;

            // Round 1: watch the colours go green, yellow, red and strike.
            var seen = new List<string>();
            deadline = Time.realtimeSinceStartup + 15;
            while (!ring.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            Require(ring.activeSelf, "Ring never appeared.");
            while (ring.activeSelf && Time.realtimeSinceStartup < deadline)
            {
                string name = ColorName(renderer.color);
                if (seen.Count == 0 || seen[seen.Count - 1] != name) seen.Add(name);
                yield return null;
            }
            Debug.Log("[SoloBossChecks] ring colours: " + string.Join(" > ", seen));
            Require(string.Join(",", seen) == "green,yellow,red", "Ring colours were " + string.Join(",", seen));

            // Round 2: pop a boss balloon while green/yellow: the ring must vanish and no skill may strike.
            deadline = Time.realtimeSinceStartup + 15;
            while (!ring.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Require(ring.activeSelf && ColorName(renderer.color) == "green", "Expected the green ring.");
            EnemySpawner.ActiveEnemies[0].Pop();
            yield return null;
            Require(!ring.activeSelf && !caster.IsCasting, "Popping on green must cancel the skill and hide the ring.");
            int lives = gm.lives;

            // Round 3: pop on red: too late, the skill lands, the ring still goes away, nothing sticks.
            deadline = Time.realtimeSinceStartup + 15;
            while ((!ring.activeSelf || ColorName(renderer.color) != "red") && Time.realtimeSinceStartup < deadline) yield return null;
            Require(ring.activeSelf, "Second ring never reached red.");
            Require(EnemySpawner.ActiveEnemies.Count > 0, "No boss balloon left to pop.");
            EnemySpawner.ActiveEnemies[0].Pop();
            yield return null;
            Require(caster.IsCasting, "Popping on red must not cancel the skill.");
            yield return new WaitForSecondsRealtime(1.5f);
            Require(!ring.activeSelf, "Ring stayed on after the strike.");
            deadline = Time.realtimeSinceStartup + 6;
            while (caster.IsCasting && Time.realtimeSinceStartup < deadline) yield return null;
            Require(!caster.IsCasting, "Caster stayed stuck in a cast after a pop on red.");
            Debug.Log($"[SoloBossChecks] RING PASS: green>yellow>red, cancel on green, no cancel on red, nothing stuck (lives {lives} -> {gm.lives}).");
            if (menu != null) menu.ReturnToMainMenu();
        }

        private static IEnumerator _pumped;

        /// <summary>Runs a check coroutine from the Editor update loop (Play Mode only); an exception is logged and stops it.</summary>
        public static void Pump(IEnumerator routine)
        {
            _pumped = routine;
            EditorApplication.update -= PumpStep;
            EditorApplication.update += PumpStep;
        }

        private static void PumpStep()
        {
            try { if (_pumped != null && _pumped.MoveNext()) return; }
            catch (Exception e) { Debug.LogError(e); }
            _pumped = null;
            EditorApplication.update -= PumpStep;
        }

        /// <summary>
        /// Plays to the final boss and saves a screenshot every <paramref name="interval"/> s while its entrance runs.
        /// The file name carries the time since the entrance started and the last entrance event tag.
        /// </summary>
        public static IEnumerator CaptureEntrance(string directory, int stage, float interval = .2f)
        {
            Require(Application.isPlaying, "Capture requires Play Mode.");
            float ready = Time.realtimeSinceStartup + 20;
            while ((GameManager.Instance == null || EnemySpawner.Instance == null) && Time.realtimeSinceStartup < ready) yield return null;
            var gm = GameManager.Instance;
            var boss = gm.GetComponent<SoloBossEncounter>();
            var menu = MagicTouch.UI.MenuManager.Instance;
            if (menu != null) menu.ReturnToMainMenu();
            gm.ConfigureMode(GameMode.Solo);
            gm.isGameActive = true; gm.StartNewGame(); gm.StartGame();
            for (int s = 0; s < stage; s++)
            {
                EnemySpawner.Instance.StopSpawning(); EnemySpawner.Instance.ClearAllEnemies();
                boss.Progress.AdvanceSurvive(boss.Progress.RequiredSeconds);
                float wait = Time.realtimeSinceStartup + 40;
                while (!boss.Fighting && Time.realtimeSinceStartup < wait) yield return null;
                while (boss.Progress.Phase == SoloBossPhase.Boss) { if (EnemySpawner.ActiveEnemies.Count > 0) EnemySpawner.ActiveEnemies[0].Pop(); yield return null; yield return null; }
                wait = Time.realtimeSinceStartup + 15;
                while (boss.StageIndex == s && Time.realtimeSinceStartup < wait) yield return null;
            }
            var entrance = UnityEngine.Object.FindFirstObjectByType<MagicTouch.Juice.BossEntrance>(FindObjectsInactive.Include);
            Require(entrance != null, "No BossEntrance in the scene.");
            string tag = "-";
            Action<string> onTag = t => { tag = t; Debug.Log($"[SoloBossChecks] entrance tag {t} at {Time.realtimeSinceStartup:F2}"); };
            entrance.Tagged += onTag;
            System.IO.Directory.CreateDirectory(directory);
            var cam = Camera.main;
            int width = 540, height = Mathf.RoundToInt(width / cam.aspect);
            var rt = new RenderTexture(width, height, 24);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            EnemySpawner.Instance.StopSpawning(); EnemySpawner.Instance.ClearAllEnemies();
            boss.Progress.AdvanceSurvive(boss.Progress.RequiredSeconds);
            float deadline = Time.realtimeSinceStartup + 40;
            while (!boss.Progress.Phase.Equals(SoloBossPhase.Summoning) && !boss.Progress.Phase.Equals(SoloBossPhase.Boss) && Time.realtimeSinceStartup < deadline) yield return null;
            float start = Time.realtimeSinceStartup, next = 0f;
            int index = 0;
            while (!boss.Fighting && Time.realtimeSinceStartup < start + 30)
            {
                gm.lives = 3;
                if (Time.realtimeSinceStartup - start >= next)
                {
                    next += interval;
                    var previous = cam.targetTexture;
                    cam.targetTexture = rt; cam.Render(); cam.targetTexture = previous;
                    RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(); RenderTexture.active = null;
                    System.IO.File.WriteAllBytes($"{directory}/e{index++:000}_{Time.realtimeSinceStartup - start:00.0}_{tag}.png", tex.EncodeToPNG());
                }
                yield return null;
            }
            entrance.Tagged -= onTag;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log($"[SoloBossChecks] ENTRANCE CAPTURE done: {index} shots in {directory}, fighting={boss.Fighting}");
            if (menu != null) menu.ReturnToMainMenu();
        }

        /// <summary>Plays to the first boss fight and saves a screenshot every 0.2 s while a skill is cast (ring colour in the file name).</summary>
        public static IEnumerator CaptureBoss(string directory, int stage, int seconds, int popsFirst = 0, float interval = .2f, bool always = false)
        {
            Require(Application.isPlaying, "Capture requires Play Mode.");
            float ready = Time.realtimeSinceStartup + 20;
            while ((GameManager.Instance == null || EnemySpawner.Instance == null) && Time.realtimeSinceStartup < ready) yield return null;
            var gm = GameManager.Instance;
            var boss = gm.GetComponent<SoloBossEncounter>();
            var menu = MagicTouch.UI.MenuManager.Instance;
            if (menu != null) menu.ReturnToMainMenu();
            gm.ConfigureMode(GameMode.Solo);
            gm.isGameActive = true; gm.StartNewGame(); gm.StartGame();
            for (int s = 0; s < stage; s++)
            {
                EnemySpawner.Instance.StopSpawning(); EnemySpawner.Instance.ClearAllEnemies();
                boss.Progress.AdvanceSurvive(boss.Progress.RequiredSeconds);
                float wait = Time.realtimeSinceStartup + 40;
                while (!boss.Fighting && Time.realtimeSinceStartup < wait) yield return null;
                while (boss.Progress.Phase == SoloBossPhase.Boss) { if (EnemySpawner.ActiveEnemies.Count > 0) EnemySpawner.ActiveEnemies[0].Pop(); yield return null; yield return null; }
                wait = Time.realtimeSinceStartup + 15;
                while (boss.StageIndex == s && Time.realtimeSinceStartup < wait) yield return null;
            }
            EnemySpawner.Instance.StopSpawning(); EnemySpawner.Instance.ClearAllEnemies();
            boss.Progress.AdvanceSurvive(boss.Progress.RequiredSeconds);
            float deadline = Time.realtimeSinceStartup + 40;
            while (!boss.Fighting && Time.realtimeSinceStartup < deadline) yield return null;
            Require(boss.Fighting, "Boss never started fighting.");
            for (int pops = 0; pops < popsFirst; pops++)
            {
                yield return new WaitForSecondsRealtime(.35f);
                if (EnemySpawner.ActiveEnemies.Count > 0) EnemySpawner.ActiveEnemies[0].Pop();
            }
            BossSkillCaster caster = null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<BossSkillCaster>()) caster = c;
            System.IO.Directory.CreateDirectory(directory);
            var cam = Camera.main;
            int width = 1000, height = Mathf.RoundToInt(width / cam.aspect);
            var rt = new RenderTexture(width, height, 24);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            float end = Time.realtimeSinceStartup + seconds, next = 0f;
            int index = 0;
            while (Time.realtimeSinceStartup < end)
            {
                gm.lives = 3; // keep the round alive while watching
                if ((always || caster.IsCasting) && Time.realtimeSinceStartup >= next)
                {
                    next = Time.realtimeSinceStartup + interval;
                    string phase = caster.Marker.gameObject.activeSelf ? ColorName(caster.Marker.color) : (caster.IsStriking ? "strike" : (caster.IsCasting ? "cast" : "rest"));
                    var previous = cam.targetTexture;
                    cam.targetTexture = rt; cam.Render(); cam.targetTexture = previous;
                    RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(); RenderTexture.active = null;
                    System.IO.File.WriteAllBytes($"{directory}/s{index++:000}_{phase}.png", tex.EncodeToPNG());
                }
                yield return null;
            }
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log($"[SoloBossChecks] CAPTURE done: {index} shots in {directory}");
            if (menu != null) menu.ReturnToMainMenu();
        }

        private static string ColorName(Color c) => c.g > .8f && c.r < .5f ? "green" : c.r > .9f && c.g > .7f ? "yellow" : c.r > .9f ? "red" : "other";

        private static void LoseAllLives(GameManager gm)
        {
            for (int i = 0; i < 3 && gm.currentState == GameState.Playing; i++) gm.LoseLife();
        }

        /// <summary>Losing a life in a plain Solo round flashes red, shakes the camera and puts it back exactly.</summary>
        public static IEnumerator ValidateLifeLoss()
        {
            Require(Application.isPlaying, "Runtime checks require Play Mode.");
            var gm = GameManager.Instance;
            var menu = MagicTouch.UI.MenuManager.Instance;
            if (menu != null) menu.ReturnToMainMenu();
            gm.ConfigureMode(GameMode.Solo);
            gm.isGameActive = true; gm.StartNewGame(); gm.StartGame();
            EnemySpawner.Instance.StopSpawning(); EnemySpawner.Instance.ClearAllEnemies();
            var cam = Camera.main;
            Vector3 basePosition = cam.transform.position;
            int lives = gm.lives;
            gm.LoseLife();
            Require(gm.lives == lives - 1, "Life was not lost.");
            yield return new WaitForSecondsRealtime(.12f);
            Require(cam.transform.position != basePosition, "Camera did not shake.");
            yield return new WaitForSecondsRealtime(.5f);
            Require(cam.transform.position == basePosition, "Camera was not restored after the shake.");
            if (menu != null) menu.ReturnToMainMenu();
            Debug.Log("[SoloBossChecks] LIFE-LOSS PASS: life lost, camera shook and was restored.");
        }
    }
}
