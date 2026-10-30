using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MagicTouch.Gameplay;
using UnityEditor;
using UnityEngine;

namespace MagicTouch.Editor
{
    /// <summary>
    /// Builds the animation sets of the castle creeps (4x4 sheet: idle, idle_cracked, hurt, death) with BossAnimationImporter,
    /// and creates Assets/Resources/CreepSpawnTable.asset once. An existing table is never overwritten, so tuned odds survive.
    /// </summary>
    public static class CreepAssetBuilder
    {
        public static readonly string[] CreepIds = { "goblin_crossbowman", "wrathful_knight", "toxic_slime" };
        private const string Root = "Assets/Sprites/Enemies/";
        private const string TablePath = "Assets/Resources/CreepSpawnTable.asset";
        private static readonly string[] ClipKeys = { "idle", "idle_cracked", "hurt", "death" };

        [MenuItem("Tools/Magic Touch/Creeps/Build Creep Assets")]
        public static void BuildAll()
        {
            var defs = new List<CreepDefinition>();
            foreach (string id in CreepIds)
            {
                string folder = Root + id + "/";
                string manifest = folder + id + "_clips.json";
                File.WriteAllText(manifest, ManifestFor(folder, id), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(manifest);
                // Reimport so SpriteImportSettings applies Point filter / no compression to the sheet.
                AssetDatabase.ImportAsset(folder + id + "_sheet_4x4.png", ImportAssetOptions.ForceUpdate);
                var set = BossAnimationImporter.Build(manifest, folder + id + "_Animations.asset");
                defs.Add(new CreepDefinition { id = id, animations = set, weight = 1f });
            }

            var table = AssetDatabase.LoadAssetAtPath<CreepSpawnTable>(TablePath);
            if (table == null)
            {
                Directory.CreateDirectory("Assets/Resources");
                table = ScriptableObject.CreateInstance<CreepSpawnTable>();
                table.Set(defs.ToArray(), DefaultRules());
                AssetDatabase.CreateAsset(table, TablePath);
            }
            else
            {
                table.Set(defs.ToArray(), table.Rules);
                EditorUtility.SetDirty(table);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CreepAssetBuilder] Built " + defs.Count + " creeps into " + TablePath);
        }

        private static CreepDifficultyRule[] DefaultRules() => new[]
        {
            Rule("Easy", .20f, 2, (2, 1f)),
            Rule("Normal", .25f, 3, (2, 70f), (3, 30f)),
            Rule("Hard", .35f, 4, (2, 50f), (3, 30f), (4, 20f)),
        };

        private static CreepDifficultyRule Rule(string label, float chance, int max, params (int balloons, float weight)[] weights)
        {
            var list = new BalloonCountWeight[weights.Length];
            for (int i = 0; i < weights.Length; i++) list[i] = new BalloonCountWeight { balloons = weights[i].balloons, weight = weights[i].weight };
            return new CreepDifficultyRule { label = label, multiBalloonChance = chance, maxBalloons = max, multiWeights = list };
        }

        private static string ManifestFor(string folder, string id)
        {
            string source = File.ReadAllText(folder + id + ".json");
            var sb = new StringBuilder();
            sb.Append("{\"ppu\":128,\"sheets\":[{\"file\":\"").Append(id).Append("_sheet_4x4.png\",\"cell_w\":64,\"cell_h\":64,\"columns\":4,\"pivot_x\":0.5,\"pivot_y\":0.5,\"clips\":[");
            for (int c = 0; c < ClipKeys.Length; c++)
            {
                string key = ClipKeys[c];
                // "idle": { "frames": [...], "duration_ms": [150, 150, ...], "loop": true }
                var match = Regex.Match(source, "\"" + key + "\":\\s*\\{[^}]*?\"duration_ms\":\\s*\\[([^\\]]*)\\]");
                if (!match.Success) throw new InvalidDataException(id + ": no duration_ms for " + key);
                if (c > 0) sb.Append(',');
                sb.Append("{\"key\":\"").Append(key).Append("\",\"frames\":[");
                for (int i = 0; i < 4; i++) sb.Append(i > 0 ? "," : "").Append(c * 4 + i);
                sb.Append("],\"frame_ms\":[").Append(Regex.Replace(match.Groups[1].Value, "\\s+", "")).Append("],\"loop\":")
                  .Append(key.StartsWith("idle") ? "true" : "false").Append('}');
            }
            sb.Append("]}]}");
            return sb.ToString();
        }
    }
}
