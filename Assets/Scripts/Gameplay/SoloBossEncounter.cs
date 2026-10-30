using System.Collections;
using System.Collections.Generic;
using MagicTouch.Juice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MagicTouch.Gameplay
{
    /// <summary>
    /// Solo run: a chain of stages, each = survive a timed minion wave (the bar fills in real time), clear the field,
    /// then a boss arrives, the bar is refilled with its health (layers, e.g. red then green) and the boss attacks while
    /// the player pops its balloons. The prelude bosses come first; the main boss (the fields below) is last and only
    /// beating it wins. A run has <see cref="MaxAttempts"/> attempts (3 hearts each); after a prelude boss falls a retry
    /// resumes at the next stage. Boss specifics live in a BossDefinition.
    /// </summary>
    [RequireComponent(typeof(GameManager))]
    public sealed class SoloBossEncounter : MonoBehaviour
    {
        public const int MaxAttempts = 5;

        [Header("Boss (the main boss, last stage)")]
        [SerializeField] private BossDefinition _boss;
        [Tooltip("Bosses fought before the main boss, in order. Each one starts its own timed minion wave.")]
        [SerializeField] private BossStage[] _preludeBosses = System.Array.Empty<BossStage>();
        [Header("Scene references")]
        [SerializeField] private EnemySpawner _spawner;
        [SerializeField] private GameObject _bossRoot;
        [SerializeField] private BossVisual _visual;
        [SerializeField] private BossSkillCaster _caster;
        [SerializeField] private BossEntrance _entrance;
        [SerializeField] private GameObject _hud;
        [Tooltip("Base bar layer: the minion progress, then the last boss segment.")]
        [SerializeField] private RectTransform _fill;
        [SerializeField] private Image _fillImage;
        [Tooltip("Layers for boss segments 0..N-2, top layer first.")]
        [SerializeField] private RectTransform[] _overlayFills = System.Array.Empty<RectTransform>();
        [SerializeField] private Image[] _overlayImages = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text _label;
        [SerializeField] private RectTransform _marker;

        private GameManager _game;
        private SoloBossProgress _progress;
        private readonly List<EnemyBalloon> _observed = new List<EnemyBalloon>();
        private EnemyBalloon[] _targets;
        private float[] _rerollTimers;
        private float[] _shown;
        private float _clock;
        private float _refill;
        private bool _running;
        private bool _transitionQueued;
        private bool _fighting;
        private BossStage[] _stages;
        private IBossEntrance _activeEntrance;
        private int _stage;
        private int _startStage;
        private int _checkpointStage;
        private int _attempt = 1;
        private bool _retryPending;
        private int _shownSecond = -1;

        public SoloBossProgress Progress => _progress;
        public BossDefinition Boss => _boss;
        public bool Fighting => _fighting;
        public int StageIndex => _stage;
        public int StageCount => _stages != null ? _stages.Length : 1;
        public int Attempt => _attempt;
        /// <summary>Attempts left after the one in progress (or just lost); 0 means the run is over for good.</summary>
        public int AttemptsLeft => Mathf.Max(0, MaxAttempts - _attempt);
        /// <summary>The stage a retry restarts from: 0, or the stage after the last prelude boss that fell.</summary>
        public int CheckpointStage => _checkpointStage;

        private void Awake()
        {
            _game = GetComponent<GameManager>();
            _game.RoundReset += OnRoundReset;
            _game.RoundStarted += BeginRound;
            _game.OnGameOver += EndRound;
            if (_spawner != null) _spawner.EnemySpawned += Observe;
            // The serialized single-boss fields are the main boss; prelude bosses come before it.
            var main = new BossStage { boss = _boss, root = _bossRoot, visual = _visual, caster = _caster, entrance = _entrance };
            _stages = new BossStage[_preludeBosses.Length + 1];
            for (int i = 0; i < _preludeBosses.Length; i++) _stages[i] = _preludeBosses[i];
            _stages[_stages.Length - 1] = main;
            foreach (var stage in _stages) ConfigureStage(stage);
            AbandonRun();
        }

        private void ConfigureStage(BossStage stage)
        {
            if (stage == null || stage.boss == null) return;
            stage.visual.Bind(stage.boss.Animations);
            stage.visual.transform.localScale = Vector3.one * stage.boss.Scale;
            if (stage.visual.Body != null) stage.visual.Body.sortingOrder = stage.boss.SortingOrder;
            stage.caster.Configure(stage.boss, stage.visual, _game);
        }

        /// <summary>Makes a stage the active one: the fields used everywhere below always describe it.</summary>
        private void SelectStage(int index)
        {
            _stage = Mathf.Clamp(index, 0, _stages.Length - 1);
            var stage = _stages[_stage];
            _boss = stage.boss;
            _bossRoot = stage.root;
            _visual = stage.visual;
            _caster = stage.caster;
            _activeEntrance = stage.entrance as IBossEntrance;
            _progress = new SoloBossProgress(_boss.SurviveSeconds, _boss.SegmentHealths(), _boss.DamagePerBalloon);
            _targets = new EnemyBalloon[_boss.BalloonsAround];
            _rerollTimers = new float[_targets.Length];
            _shown = new float[_progress.SegmentCount];
            for (int i = 0; i < _overlayImages.Length && i < _boss.Segments.Length; i++)
                if (_overlayImages[i] != null) _overlayImages[i].color = _boss.Segments[i].barColor;
            var flag = _marker != null ? _marker.GetComponent<Image>() : null;
            var idle = _boss.Animations != null ? _boss.Animations.Get(_boss.Segments[0].restClip) : null;
            if (flag != null && idle != null && idle.Length > 0) flag.sprite = idle.frames[0];
        }

        /// <summary>A new round after a loss resumes the run (next attempt); any other new round starts a fresh run.</summary>
        private void OnRoundReset()
        {
            if (_retryPending)
            {
                _attempt++;
                _startStage = _checkpointStage;
                _retryPending = false;
                ResetRound();
            }
            else AbandonRun();
        }

        private void AbandonRun()
        {
            _retryPending = false;
            _attempt = 1;
            _checkpointStage = 0;
            _startStage = 0;
            ResetRound();
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.RoundReset -= OnRoundReset;
                _game.RoundStarted -= BeginRound;
                _game.OnGameOver -= EndRound;
            }
            if (_spawner != null) _spawner.EnemySpawned -= Observe;
            UnsubscribeEnemies();
        }

        private void UnsubscribeEnemies()
        {
            foreach (var enemy in _observed) if (enemy != null) enemy.Popped -= OnPopped;
            _observed.Clear();
        }

        private void ResetRound()
        {
            if (_stages == null) return;
            StopAllCoroutines();
            UnsubscribeEnemies();
            foreach (var stage in _stages)
            {
                if (stage.caster != null) stage.caster.Stop();
                (stage.entrance as IBossEntrance)?.Cancel();
                if (stage.root != null) stage.root.SetActive(false);
            }
            MusicDirector.Instance?.EndBossMode();
            SelectStage(_startStage);
            _running = false;
            _transitionQueued = false;
            _fighting = false;
            _clock = 0;
            _refill = 0;
            _shownSecond = -1;
            _hud.SetActive(false);
            _visual.ResetVisual(_boss.Segments[0].restClip);
            RefreshHUD(true);
        }

        private void BeginRound()
        {
            if (_game.IsVersus || _boss == null) return;
            _running = true;
            _hud.SetActive(true);
            foreach (var enemy in EnemySpawner.ActiveEnemies) Observe(enemy);
            RefreshHUD(true);
        }

        private void EndRound()
        {
            bool soloRound = _running;
            _running = false;
            StopAllCoroutines();
            UnsubscribeEnemies();
            _fighting = false;
            if (_caster != null) _caster.Stop();
            _activeEntrance?.Cancel();
            MusicDirector.Instance?.EndBossMode();
            _hud.SetActive(false);
            if (!_game.SoloBossVictory) _bossRoot.SetActive(false);
            // A lost solo round with attempts left is retried from the checkpoint; victory or the last loss ends the run.
            _retryPending = soloRound && !_game.SoloBossVictory && _attempt < MaxAttempts;
        }

        private void Observe(EnemyBalloon enemy)
        {
            if (_game.IsVersus || enemy == null || _observed.Contains(enemy)) return;
            _observed.RemoveAll(item => item == null);
            _observed.Add(enemy);
            enemy.Popped += OnPopped;
        }

        private void OnPopped(EnemyBalloon enemy)
        {
            enemy.Popped -= OnPopped;
            _observed.Remove(enemy);
            if (!_running || _game.IsVersus || _game.currentState != GameState.Playing) return;
            if (!enemy.IsBossBalloon || !_fighting) return;
            var hit = _progress.RecordPop(true);
            if (hit != SoloBossHit.None)
            {
                _caster.Interrupt();
                if (hit == SoloBossHit.SegmentCleared)
                {
                    int next = _progress.CurrentSegment;
                    _visual.SetRest(_boss.Segments[next].restClip);
                    _caster.SetSegment(next);
                }
                _visual.Hurt(_boss.HurtClip, !_caster.IsCasting);
            }
            RefreshHUD(false);
            if (!_transitionQueued && _progress.Phase == SoloBossPhase.Dying)
            {
                _transitionQueued = true;
                StartCoroutine(DefeatBoss());
            }
        }

        private IEnumerator SummonBoss()
        {
            _spawner.StopSpawning();
            // The field is already empty; this only catches a balloon that slipped in on the same frame.
            yield return null;
            _spawner.ClearAllEnemies();
            _progress.BeginSummon();
            MusicDirector.Instance?.BeginBossMode();
            yield return WaitForCombatSeconds(.8f);
            _progress.BeginBoss();
            _refill = 0;
            RefreshHUD(true);
            Vector3 hover = HoverPosition();
            var player = PlayerBounds();
            _bossRoot.SetActive(true);
            _visual.ResetVisual(_boss.Segments[0].restClip);
            bool entrance = _boss.HasEntrance && _activeEntrance != null;
            if (entrance) _activeEntrance.Prepare(_boss, _visual, hover, player);
            else
            {
                _bossRoot.transform.position = hover;
                _visual.SetVisible(true);
            }
            if (entrance && _boss.EntranceBeforeRefill)
            {
                yield return _activeEntrance.Run(_boss, _visual, hover, player, IsPlaying);
                RefreshHUD(false);
            }

            // The bar is refilled layer by layer before the fight starts (a stage may rise while it fills).
            float elapsed = 0;
            while (elapsed < _boss.RefillSeconds)
            {
                if (IsPlaying())
                {
                    elapsed += Time.unscaledDeltaTime;
                    _refill = Mathf.Clamp01(elapsed / _boss.RefillSeconds);
                    if (entrance) _activeEntrance.SetRise(_refill);
                }
                yield return null;
            }
            _refill = 1;
            if (entrance && !_boss.EntranceBeforeRefill)
            {
                _activeEntrance.SetRise(1f);
                yield return _activeEntrance.Run(_boss, _visual, hover, player, IsPlaying);
            }
            _fighting = true;
            _transitionQueued = false;
            RefreshHUD(false);
            MusicDirector.Instance?.PlayBossCombat();
            RefillBalloons();
            for (int i = 0; i < _rerollTimers.Length; i++)
                _rerollTimers[i] = _boss.BalloonRerollSeconds * (.35f + .65f * (i + 1f) / (_rerollTimers.Length + 1f));
            _caster.Begin(_progress.CurrentSegment);
            StartCoroutine(BossRoutine());
        }

        private IEnumerator BossRoutine()
        {
            while (_running && _fighting)
            {
                if (IsPlaying())
                {
                    RefillBalloons();
                    float dt = Time.unscaledDeltaTime;
                    for (int i = 0; i < _targets.Length; i++)
                    {
                        if (_targets[i] == null) continue;
                        _rerollTimers[i] -= dt;
                        if (_rerollTimers[i] > 0) continue;
                        _rerollTimers[i] = _boss.BalloonRerollSeconds;
                        _spawner.RerollSymbol(_targets[i]);
                    }
                }
                yield return null;
            }
        }

        private void RefillBalloons()
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] != null) continue;
                var target = _spawner.SpawnEnemy();
                if (target == null) continue;
                target.ConfigureBossBalloon();
                _targets[i] = target;
                _rerollTimers[i] = _boss.BalloonRerollSeconds;
                PlaceBalloon(i);
            }
        }

        private void PlaceBalloon(int index)
        {
            var target = _targets[index];
            if (target == null || Camera.main == null) return;
            var cam = Camera.main;
            float depth = Mathf.Abs(cam.transform.position.z);
            // Side lanes keep every gesture readable around the boss.
            bool left = index % 2 == 0;
            int laneCount = left ? (_targets.Length + 1) / 2 : _targets.Length / 2;
            float row = laneCount > 1 ? (float)(index / 2) / (laneCount - 1) : .5f;
            float x = left ? _boss.BalloonLaneX : 1f - _boss.BalloonLaneX;
            Vector3 viewport = new Vector3(x,
                Mathf.Lerp(_boss.BalloonLaneY.x, _boss.BalloonLaneY.y, row) + Mathf.Sin(_clock * 1.4f + index) * .012f, depth);
            Vector3 position = cam.ViewportToWorldPoint(viewport);
            position.z = 0;
            target.transform.position = position;
        }

        private Vector3 HoverPosition()
        {
            var cam = Camera.main;
            if (cam == null) return new Vector3(0f, 2f, 0f);
            var p = cam.ViewportToWorldPoint(new Vector3(_boss.HoverViewport.x, _boss.HoverViewport.y, Mathf.Abs(cam.transform.position.z)));
            p.z = 0f;
            // The boss always stays above the player's head, whatever the hover point or scale.
            var wizard = _game.playerWizardSprite;
            if (wizard != null) p.y = Mathf.Max(p.y, wizard.bounds.max.y + .25f + _boss.BodyHalfHeight * _boss.Scale);
            return p;
        }

        /// <summary>World bounds of the player; its bottom is the floor the entrance effects stand on.</summary>
        private Bounds PlayerBounds()
        {
            var wizard = _game.playerWizardSprite;
            return wizard != null ? wizard.bounds : new Bounds(new Vector3(0f, -2.2f, 0f), new Vector3(1.8f, 2f, 0f));
        }

        private IEnumerator DefeatBoss()
        {
            yield return null;
            _spawner.StopSpawning();
            _spawner.ClearAllEnemies();
            _fighting = false;
            _caster.Stop();
            bool last = _stage >= _stages.Length - 1;
            if (last) MusicDirector.Instance?.FadeOut();
            _visual.Die(_boss.DeathClip);
            if (!last) _label.text = _boss.DefeatedText + "   CHECKPOINT";
            // The final boss drops to the floor where the player stands; a stage boss goes down with its stage.
            float deathSeconds = _visual.Duration(_boss.DeathClip) + .45f;
            if (_activeEntrance != null) yield return _activeEntrance.Defeat(_boss, _visual, PlayerBounds(), deathSeconds, IsPlaying);
            else yield return WaitForCombatSeconds(deathSeconds);
            _progress.CompleteDeath();
            if (last)
            {
                _game.CompleteSoloBossRound();
                yield break;
            }

            // A prelude boss fell: save the checkpoint and go on to the next stage's wave.
            _checkpointStage = _stage + 1;
            yield return WaitForCombatSeconds(.6f);
            _activeEntrance?.Cancel();
            _bossRoot.SetActive(false);
            MusicDirector.Instance?.EndBossMode();
            MusicDirector.Instance?.PlayCombat();
            SelectStage(_stage + 1);
            _visual.ResetVisual(_boss.Segments[0].restClip);
            _transitionQueued = false;
            _clock = 0;
            _refill = 0;
            _shownSecond = -1;
            RefreshHUD(true);
            _spawner.StartSpawning();
        }

        private static string Clock(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            return $"{total / 60}:{total % 60:00}";
        }

        private bool IsPlaying() => _game.currentState == GameState.Playing;

        private IEnumerator WaitForCombatSeconds(float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                if (IsPlaying()) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void Update()
        {
            // A script reload in Play Mode wipes the runtime state (Awake does not run again): wait for the next round.
            if (_boss == null || _game == null || _progress == null) return;
            // Menu navigation is owned by MenuManager; abandon the encounter without restarting tracking.
            if (_game.currentState == GameState.MainMenu)
            {
                if (_running || _bossRoot.activeSelf || _retryPending || _attempt != 1 || _checkpointStage != 0) AbandonRun();
                return;
            }
            if (!_running) return;
            UpdateHud(Time.unscaledDeltaTime);
            if (!IsPlaying()) return;
            float dt = Time.unscaledDeltaTime;
            _clock += dt;
            if (_bossRoot.activeSelf) _visual.Tick(dt);
            if (_progress.Phase == SoloBossPhase.Minions)
            {
                if (_progress.AdvanceSurvive(dt))
                {
                    _spawner.StopSpawning(); // the wave is over; the player now has to empty the field
                    RefreshHUD(false);
                }
                else if (Mathf.FloorToInt(_progress.Elapsed) != _shownSecond) RefreshHUD(false);
            }
            else if (_progress.Phase == SoloBossPhase.Clearing && !_transitionQueued && EnemySpawner.ActiveEnemies.Count == 0)
            {
                _transitionQueued = true;
                StartCoroutine(SummonBoss());
            }
            if (_fighting)
            {
                _caster.Tick(dt);
                for (int i = 0; i < _targets.Length; i++) PlaceBalloon(i);
            }
        }

        private void UpdateHud(float dt)
        {
            int last = _progress.SegmentCount - 1;
            bool boss = _progress.Phase == SoloBossPhase.Boss || _progress.Phase == SoloBossPhase.Dying;
            float baseTarget = boss ? _progress.SegmentFill(last) : _progress.Fill;
            float step = dt * 1.4f;
            _shown[last] = Mathf.MoveTowards(_shown[last], baseTarget, step);
            for (int i = 0; i < last; i++)
            {
                float target = boss ? _progress.SegmentFill(i) * _refill : 0f;
                _shown[i] = Mathf.MoveTowards(_shown[i], target, i == 0 && _refill < 1f ? dt / _boss.RefillSeconds * 1.2f : step);
            }
            ApplyFills();
        }

        private void ApplyFills()
        {
            int last = _progress.SegmentCount - 1;
            _fill.anchorMax = new Vector2(_shown[last], 1);
            for (int i = 0; i < _overlayFills.Length; i++) // layers this boss does not have stay empty
                if (_overlayFills[i] != null) _overlayFills[i].anchorMax = new Vector2(i < last ? _shown[i] : 0f, 1);
            bool wave = _progress.Phase == SoloBossPhase.Minions || _progress.Phase == SoloBossPhase.Clearing || _progress.Phase == SoloBossPhase.Summoning;
            int current = wave ? last
                : Mathf.Min(_progress.CurrentSegment, last);
            float markerX = current == last || _refill < 1f ? _shown[last] : _shown[current];
            _marker.anchorMin = _marker.anchorMax = new Vector2(markerX, .5f);
        }

        private void RefreshHUD(bool immediate)
        {
            var phase = _progress.Phase;
            bool wave = phase == SoloBossPhase.Minions || phase == SoloBossPhase.Clearing || phase == SoloBossPhase.Summoning;
            int last = _progress.SegmentCount - 1;
            _fillImage.color = wave ? _boss.MinionBarColor : _boss.Segments[last].barColor;
            _shownSecond = Mathf.FloorToInt(_progress.Elapsed);
            string text = phase == SoloBossPhase.Minions ? $"{_boss.MinionText}   {Clock(_progress.Elapsed)} / {Clock(_progress.RequiredSeconds)}"
                : phase == SoloBossPhase.Clearing ? $"{_boss.MinionText}   CLEAR THE FIELD!"
                : phase == SoloBossPhase.Summoning || !_fighting && phase == SoloBossPhase.Boss ? _boss.IntroText
                : phase == SoloBossPhase.Dying ? _boss.DefeatedText
                : $"{_boss.DisplayName}   {_progress.Health} / {_progress.MaximumHealth} HP";
            _label.text = $"{text}   ATTEMPT {_attempt}/{MaxAttempts}";
            if (!immediate) return;
            bool boss = _progress.Phase == SoloBossPhase.Boss || _progress.Phase == SoloBossPhase.Dying;
            _shown[last] = boss ? _progress.SegmentFill(last) : _progress.Fill;
            for (int i = 0; i < last; i++) _shown[i] = boss ? _progress.SegmentFill(i) * _refill : 0f;
            ApplyFills();
        }
    }
}
