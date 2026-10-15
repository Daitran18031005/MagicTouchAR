using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MagicTouch.Tracking
{
    /// <summary>
    /// Machine-local settings from the optional <c>.env</c> file in the project root (see <c>.env.example</c>),
    /// and the Python interpreter used for the hand tracker. Never falls back to a system Python.
    /// </summary>
    public static class ProjectEnv
    {
        public const string SetupHint =
            "Chạy Tools\\setup_python_env.bat (cần Python 3.11) để tạo .venv trong thư mục dự án, " +
            "hoặc đặt PYTHON_EXE trong file .env ở thư mục gốc dự án (xem .env.example). " +
            "Unity không dùng Python hệ thống.";

        private static Dictionary<string, string> _values;

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        // A new Play session re-reads .env, so edits apply without restarting the Editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlay() => _values = null;

        /// <summary>Value of <paramref name="key"/> from .env, or <paramref name="fallback"/> when missing or empty.</summary>
        public static string Get(string key, string fallback = "")
        {
            if (_values == null) _values = Load(Path.Combine(ProjectRoot, ".env"));
            return _values.TryGetValue(key, out string value) && value.Length > 0 ? value : fallback;
        }

        public static int GetInt(string key, int fallback)
        {
            string raw = Get(key);
            if (raw.Length == 0) return fallback;
            if (int.TryParse(raw, out int value)) return value;
            Debug.LogWarning($"[ProjectEnv] {key}={raw} trong .env không phải số nguyên; dùng mặc định {fallback}.");
            return fallback;
        }

        /// <summary>
        /// Interpreter for Tools/mediapipe_streamer.py: PYTHON_EXE from .env, then the project's .venv.
        /// Returns false with a Vietnamese error message when neither exists.
        /// </summary>
        public static bool TryResolvePython(out string pythonPath, out string error)
        {
            pythonPath = null;
            error = null;
            string root = ProjectRoot;

            string configured = Get("PYTHON_EXE");
            if (configured.Length > 0)
            {
                string full = Path.IsPathRooted(configured) ? configured : Path.Combine(root, configured);
                if (File.Exists(full)) { pythonPath = full; return true; }
                error = $"PYTHON_EXE trong .env trỏ tới file không tồn tại: {configured}. Sửa đường dẫn hoặc để trống để dùng .venv. {SetupHint}";
                return false;
            }

            string[] venvCandidates = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer
                ? new[] { Path.Combine(root, ".venv", "Scripts", "pythonw.exe"), Path.Combine(root, ".venv", "Scripts", "python.exe") }
                : new[] { Path.Combine(root, ".venv", "bin", "python3"), Path.Combine(root, ".venv", "bin", "python") };
            foreach (string candidate in venvCandidates)
            {
                if (File.Exists(candidate)) { pythonPath = candidate; return true; }
            }

            error = $"Không tìm thấy Python cho hand tracker (không có PYTHON_EXE trong .env và không có .venv). {SetupHint}";
            return false;
        }

        private static Dictionary<string, string> Load(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return values;
            try
            {
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0])
                        value = value.Substring(1, value.Length - 2);
                    values[key] = value;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProjectEnv] Không đọc được .env: {ex.Message}");
            }
            return values;
        }
    }
}
