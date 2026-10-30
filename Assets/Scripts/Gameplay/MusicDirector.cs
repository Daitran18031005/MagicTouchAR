using UnityEngine;

namespace MagicTouch.Gameplay
{
    public enum MusicTrack { None, Menu, Creep, BossAppear, BossCombat }

    /// <summary>
    /// Single owner of background music: menu, normal combat, boss arrival and boss combat, with crossfades.
    /// MenuManager and GameManager route their BGM calls here; when no clips are assigned they fall back to
    /// their old behaviour. Boss mode keeps a pause/resume from jumping back to the normal combat track.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        private const string MusicVolumeKey = "BGM_Volume";

        [SerializeField] private AudioClip _menu;
        [SerializeField] private AudioClip _creep;
        [SerializeField] private AudioClip _bossAppear;
        [SerializeField] private AudioClip _bossCombat;
        [SerializeField, Min(.05f)] private float _fadeSeconds = 1f;

        private AudioSource[] _sources;
        private readonly float[] _level = new float[2];
        private readonly float[] _target = new float[2];
        private int _active;
        private MusicTrack _track;
        private MusicTrack _bossTrack;
        private bool _bossMode;
        private float _master = .35f;

        public static MusicDirector Instance { get; private set; }
        public MusicTrack Track => _track;
        public bool BossMode => _bossMode;
        public bool HasClips => _menu != null && _creep != null;

        private void Awake()
        {
            Instance = this;
            _master = PlayerPrefs.GetFloat(MusicVolumeKey, .35f);
            _sources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Music_" + i, typeof(AudioSource));
                go.transform.SetParent(transform, false);
                var source = go.GetComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0f;
                _sources[i] = source;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Plays the track that matches a clip MenuManager asks for. Returns false when it does not own that clip.</summary>
        public bool TryPlay(AudioClip clip)
        {
            if (clip == null || !HasClips) return false;
            if (clip == _menu) { _bossMode = false; Play(MusicTrack.Menu); return true; }
            if (clip == _creep) { PlayCombat(); return true; }
            return false;
        }

        /// <summary>Normal combat music, or the boss track while a boss fight is under way.</summary>
        public void PlayCombat() => Play(_bossMode ? _bossTrack : MusicTrack.Creep);

        public void BeginBossMode()
        {
            _bossMode = true;
            _bossTrack = MusicTrack.BossAppear;
            Play(MusicTrack.BossAppear);
        }

        public void PlayBossCombat()
        {
            _bossMode = true;
            _bossTrack = MusicTrack.BossCombat;
            Play(MusicTrack.BossCombat);
        }

        public void EndBossMode() => _bossMode = false;

        public void FadeOut()
        {
            _target[0] = _target[1] = 0f;
            _track = MusicTrack.None;
        }

        public void StopAll()
        {
            _bossMode = false;
            _track = MusicTrack.None;
            if (_sources == null) return;
            for (int i = 0; i < 2; i++)
            {
                _sources[i].Stop();
                _level[i] = _target[i] = 0f;
            }
        }

        public void SetPaused(bool paused)
        {
            if (_sources == null) return;
            foreach (var source in _sources)
            {
                if (paused) source.Pause();
                else source.UnPause();
            }
        }

        public void SetMaster(float volume) => _master = Mathf.Clamp01(volume);

        private void Play(MusicTrack track)
        {
            var clip = ClipFor(track);
            if (clip == null) return;
            if (track == _track && _sources[_active].isPlaying) return;
            int next = 1 - _active;
            var source = _sources[next];
            source.Stop();
            source.clip = clip;
            source.loop = track != MusicTrack.BossAppear;
            source.volume = 0f;
            source.Play();
            _level[next] = 0f;
            _target[next] = 1f;
            _target[_active] = 0f;
            _active = next;
            _track = track;
        }

        private AudioClip ClipFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Menu: return _menu;
                case MusicTrack.Creep: return _creep;
                case MusicTrack.BossAppear: return _bossAppear != null ? _bossAppear : _bossCombat;
                case MusicTrack.BossCombat: return _bossCombat != null ? _bossCombat : _creep;
                default: return null;
            }
        }

        private void Update()
        {
            if (_sources == null) return;
            float step = Time.unscaledDeltaTime / _fadeSeconds;
            for (int i = 0; i < 2; i++)
            {
                _level[i] = Mathf.MoveTowards(_level[i], _target[i], step);
                _sources[i].volume = _master * _level[i];
                if (_level[i] <= 0f && _target[i] <= 0f && _sources[i].isPlaying) _sources[i].Stop();
            }
        }
    }
}
