using System;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>The scene objects of one boss in the solo run (definition + the objects that play it).</summary>
    [Serializable]
    public sealed class BossStage
    {
        public BossDefinition boss;
        public GameObject root;
        public BossVisual visual;
        public BossSkillCaster caster;
        [Tooltip("A component that implements IBossEntrance (BossEntrance, DullahanEntrance).")]
        public MonoBehaviour entrance;
    }
}
