using System;
using System.Collections.Generic;
using System.IO;
using MagicTouch.Gameplay;
using UnityEditor;
using UnityEngine;

namespace MagicTouch.Editor
{
    /// <summary>
    /// Builds a BossAnimationSet from a boss clip manifest (JSON written by the sprite pipeline):
    /// grid sheets in, named clips of Sprites out. Sprites are created on the whole cell with a centre pivot so a boss
    /// never jitters between poses. Adding a boss = run this on its manifest, then fill in a BossDefinition.
    /// </summary>
    public static class BossAnimationImporter
    {
        [Serializable] private sealed class Manifest { public float ppu = 100f; public SheetEntry[] sheets; }
        [Serializable] private sealed class SheetEntry { public string file; public int cell_w; public int cell_h; public int columns; public float pivot_x = .5f; public float pivot_y = .5f; public ClipEntry[] clips; }
        [Serializable] private sealed class ClipEntry { public string key; public int[] frames; public int ms = 100; public int[] frame_ms; public bool loop; }

        [MenuItem("Tools/Magic Touch/Boss/Build Animation Set From Selected Manifest")]
        private static void BuildFromSelection()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".json")) { Debug.LogError("[BossAnimationImporter] Select a boss clip manifest (.json)."); return; }
            Build(path, Path.ChangeExtension(path, null) + "_Animations.asset");
        }

        public static BossAnimationSet Build(string manifestPath, string assetPath)
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (manifest?.sheets == null || manifest.sheets.Length == 0) throw new InvalidOperationException("Manifest has no sheets.");
            string folder = Path.GetDirectoryName(manifestPath).Replace('\\', '/');

            var set = AssetDatabase.LoadAssetAtPath<BossAnimationSet>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<BossAnimationSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }
            else
            {
                foreach (var old in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    if (old is Sprite) { AssetDatabase.RemoveObjectFromAsset(old); UnityEngine.Object.DestroyImmediate(old, true); }
            }

            var clips = new List<BossClip>();
            foreach (var sheet in manifest.sheets)
            {
                string texturePath = folder + "/" + sheet.file;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null) throw new InvalidOperationException("Missing sheet texture: " + texturePath);
                foreach (var entry in sheet.clips)
                {
                    var clip = new BossClip
                    {
                        key = entry.key,
                        loop = entry.loop,
                        frames = new Sprite[entry.frames.Length],
                        seconds = new float[entry.frames.Length]
                    };
                    for (int i = 0; i < entry.frames.Length; i++)
                    {
                        int index = entry.frames[i];
                        int column = index % sheet.columns;
                        int row = index / sheet.columns;
                        var rect = new Rect(column * sheet.cell_w, texture.height - (row + 1) * sheet.cell_h, sheet.cell_w, sheet.cell_h);
                        var sprite = Sprite.Create(texture, rect, new Vector2(sheet.pivot_x, sheet.pivot_y), manifest.ppu, 0, SpriteMeshType.FullRect);
                        sprite.name = $"{entry.key}_{i:00}";
                        AssetDatabase.AddObjectToAsset(sprite, set);
                        clip.frames[i] = sprite;
                        int ms = entry.frame_ms != null && i < entry.frame_ms.Length ? entry.frame_ms[i] : entry.ms;
                        clip.seconds[i] = Mathf.Max(10, ms) / 1000f;
                    }
                    clips.Add(clip);
                }
            }
            set.SetClips(clips.ToArray());
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            Debug.Log($"[BossAnimationImporter] {assetPath}: {clips.Count} clips.");
            return set;
        }
    }
}
