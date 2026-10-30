using System.Collections.Generic;
using System.IO;
using MagicTouch.Gameplay;
using UnityEditor;
using UnityEngine;

namespace MagicTouch.Editor
{
    /// <summary>Collects Assets/Sprites/Balloons/sway/balloon_&lt;colour&gt;_sway_&lt;n&gt;.png into Assets/Resources/BalloonSwaySet.asset (existing tuning is kept).</summary>
    public static class BalloonSwayBuilder
    {
        [System.Serializable] private class Meta { public float[] bodyOffsetPx; }

        private const string Folder = "Assets/Sprites/Balloons/sway";
        private const string AssetPath = "Assets/Resources/BalloonSwaySet.asset";

        [MenuItem("Tools/MagicTouch/Sprites/Build Balloon Sway Set")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var byColor = new SortedDictionary<string, SortedDictionary<int, Sprite>>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EnsureImportSettings(path);
                string[] part = Path.GetFileNameWithoutExtension(path).Split('_'); // balloon, <colour>, sway, <n>
                if (part.Length != 4 || part[0] != "balloon" || part[2] != "sway" || !int.TryParse(part[3], out int n)) continue;
                if (!byColor.TryGetValue(part[1], out var frames)) byColor[part[1]] = frames = new SortedDictionary<int, Sprite>();
                frames[n] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var entries = new List<BalloonSwaySet.Entry>();
            foreach (var pair in byColor)
                entries.Add(new BalloonSwaySet.Entry { color = pair.Key, frames = new List<Sprite>(pair.Value.Values).ToArray() });

            Directory.CreateDirectory("Assets/Resources");
            var set = AssetDatabase.LoadAssetAtPath<BalloonSwaySet>(AssetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<BalloonSwaySet>(); AssetDatabase.CreateAsset(set, AssetPath); }
            var offsets = new float[6];
            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(Folder + "/sway-meta.json"));
            if (meta?.bodyOffsetPx != null) for (int i = 0; i < offsets.Length && i < meta.bodyOffsetPx.Length; i++) offsets[i] = meta.bodyOffsetPx[i] / 128f; // 128 px = 1 unit
            set.Set(entries.ToArray(), offsets);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BalloonSwayBuilder] {entries.Count} colours -> {AssetPath}");
        }

        /// <summary>The frames replace the static 256 px balloon sprite, so they need its settings: one sprite, centre pivot, 128 PPU.</summary>
        private static void EnsureImportSettings(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.spriteImportMode == SpriteImportMode.Single && importer.spritePixelsPerUnit == 128f && settings.spriteMeshType == SpriteMeshType.FullRect
                && importer.spritePivot == new Vector2(.5f, .5f)) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.spritePivot = new Vector2(.5f, .5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
