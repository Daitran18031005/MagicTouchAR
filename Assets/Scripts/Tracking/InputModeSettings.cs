using System;

namespace MagicTouch.Tracking
{
    public enum InputMode
    {
        AR,
        MouseSimulation
    }

    /// <summary>
    /// Machine-local input mode, so switching AR / Mouse Simulation never dirties SampleScene.
    /// Order: EditorPrefs (set by Tools > Magic Touch > Switch Input) → INPUT_MODE in .env →
    /// no override (each component keeps its serialized scene value). Mouse Simulation exists
    /// only in the Editor, so player builds always resolve to AR.
    /// </summary>
    public static class InputModeSettings
    {
        public const string EditorPrefsKey = "MagicTouch.InputMode";

        /// <summary>The local override, or null to keep the scene's serialized value.</summary>
        public static InputMode? Override
        {
            get
            {
#if UNITY_EDITOR
                if (TryParse(UnityEditor.EditorPrefs.GetString(EditorPrefsKey, ""), out InputMode fromPrefs)) return fromPrefs;
                if (TryParse(ProjectEnv.Get("INPUT_MODE"), out InputMode fromEnv)) return fromEnv;
                return null;
#else
                return InputMode.AR;
#endif
            }
        }

        /// <summary>Accepts "ar" or "mouse" / "mousesimulation" (case-insensitive).</summary>
        public static bool TryParse(string value, out InputMode mode)
        {
            mode = InputMode.AR;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.Trim();
            if (v.Equals("ar", StringComparison.OrdinalIgnoreCase)) { mode = InputMode.AR; return true; }
            if (v.Equals("mouse", StringComparison.OrdinalIgnoreCase) || v.Equals("mousesimulation", StringComparison.OrdinalIgnoreCase))
            {
                mode = InputMode.MouseSimulation;
                return true;
            }
            return false;
        }

        public static string ToConfigValue(InputMode mode) => mode == InputMode.MouseSimulation ? "mouse" : "ar";
    }
}
