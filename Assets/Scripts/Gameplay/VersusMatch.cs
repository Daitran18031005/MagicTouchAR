using System;

namespace MagicTouch.Gameplay
{
    public enum GameMode { Solo, Versus }

    /// <summary>Pure round state; caller supplies Unity's scaled time so pause freezes the deadline.</summary>
    public sealed class VersusMatch
    {
        private readonly int[] _scores = new int[2];
        private double _deadline;
        public int DurationSeconds { get; private set; } = 180;
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public int LeftScore => _scores[0];
        public int RightScore => _scores[1];
        public int Winner => LeftScore == RightScore ? -1 : (LeftScore > RightScore ? 0 : 1);
        public static int ValidMinutes(int minutes) => minutes == 5 || minutes == 7 ? minutes : 3;
        public static int PlayerForScreenX(float x, float width) => x < width * .5f ? 0 : 1;

        public void Reset(int minutes)
        {
            DurationSeconds = ValidMinutes(minutes) * 60;
            _scores[0] = _scores[1] = 0;
            Started = Finished = false;
        }
        public void Start(double now)
        {
            if (Started || Finished) return;
            _deadline = now + DurationSeconds;
            Started = true;
        }
        public double Remaining(double now) => Finished ? 0 : (Started ? Math.Max(0, _deadline - now) : DurationSeconds);
        public bool CanScore(double now) => Started && !Finished && now < _deadline;
        public bool TryAward(int player, int points, double now)
        {
            if (player < 0 || player > 1 || points <= 0 || !CanScore(now)) return false;
            _scores[player] += points;
            return true;
        }
        public void Finish() { Finished = true; }
    }
}
