using System.Collections.Generic;

namespace MagicTouch.Gameplay
{
    /// <summary>Random skill order without repeats inside a bag and never the same skill twice in a row.</summary>
    public sealed class BossSkillBag
    {
        private readonly System.Random _random;
        private readonly List<int> _bag = new List<int>();
        private int _count;
        private int _last = -1;

        public BossSkillBag(int seed) { _random = new System.Random(seed); }

        public void Reset()
        {
            _bag.Clear();
            _last = -1;
            _count = 0;
        }

        public int Next(int count)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (_bag.Count == 0 || _count != count) Refill(count);
            int pick = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            _last = pick;
            return pick;
        }

        private void Refill(int count)
        {
            _count = count;
            _bag.Clear();
            for (int i = 0; i < count; i++) _bag.Add(i);
            for (int i = count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
            // The next pick is the last element; keep it from repeating the previous skill.
            if (_bag[_bag.Count - 1] == _last)
            {
                int swap = _random.Next(count - 1);
                (_bag[_bag.Count - 1], _bag[swap]) = (_bag[swap], _bag[_bag.Count - 1]);
            }
        }
    }
}
