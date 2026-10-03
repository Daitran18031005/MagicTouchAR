using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D.Sprites;

namespace MagicTouch.Editor
{
    /// <summary>
    /// Editor tool to automatically configure, slice, and save Pixel Art sprite sheets programmatically.
    /// Bypasses Inspector GUI bugs by forcing TextureImporter to Multiple, Point filter, No Compression,
    /// and generating SpriteRects using UnityEditorInternal.InternalSpriteUtility.
    /// </summary>
    public static class AutoSpriteSlicer
    {
        [MenuItem("Tools/Auto Slice Pixel Art", false, 100)]
        [MenuItem("Assets/Auto Slice Pixel Art", false, 30)]
        public static void SliceSelectedTextures()
        {
            var selectedObjects = Selection.GetFiltered<Texture2D>(SelectionMode.DeepAssets);
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                // If nothing directly selected, try finding textures in current selection path
                if (Selection.activeObject != null)
                {
                    string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                    if (Directory.Exists(path))
                    {
                        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
                        var list = new List<Texture2D>();
                        foreach (string guid in guids)
                        {
                            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                            if (tex != null) list.Add(tex);
                        }
                        selectedObjects = list.ToArray();
                    }
                }
            }

            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Auto Slice Pixel Art",
                    "No Texture2D assets selected!\n\nPlease select one or more sprite sheet textures (or a folder containing them) in the Project window and try again.",
                    "OK"
                );
                return;
            }

            int processedCount = 0;
            int totalSpritesGenerated = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < selectedObjects.Length; i++)
                {
                    Texture2D tex = selectedObjects[i];
                    if (tex == null) continue;

                    string assetPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    EditorUtility.DisplayProgressBar(
                        "Auto Slicing Pixel Art",
                        $"Processing {tex.name} ({i + 1}/{selectedObjects.Length})...",
                        (float)i / selectedObjects.Length
                    );

                    int count = SliceTexture(assetPath);
                    if (count > 0)
                    {
                        processedCount++;
                        totalSpritesGenerated += count;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"<color=green>[AutoSpriteSlicer] Complete! Sliced {processedCount} textures into a total of {totalSpritesGenerated} individual sprites.</color>");
            EditorUtility.DisplayDialog(
                "Auto Slice Complete",
                $"Successfully sliced {processedCount} texture(s) into {totalSpritesGenerated} sprites!\n\nAll textures were configured as:\n- Sprite (Multiple)\n- Filter: Point (no filter)\n- Compression: None\n- Alpha is Transparency: On",
                "Great!"
            );
        }

        [MenuItem("Tools/Auto Slice All Sprites in Sprites Folder", false, 101)]
        public static void SliceAllSpritesInFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning("[AutoSpriteSlicer] No textures found in Assets/Sprites.");
                return;
            }

            int count = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("Slicing All Sprites", path, (float)i / guids.Length);
                    count += SliceTexture(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"<color=green>[AutoSpriteSlicer] Sliced all textures in Assets/Sprites. Total sprites: {count}</color>");
        }

        /// <summary>
        /// Slices a single texture asset based on transparency and saves the SpriteMetaData.
        /// Returns the number of sprites created.
        /// </summary>
        public static int SliceTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return 0;

            // 1. Force optimal 2D Pixel Art TextureImporter settings
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;

            if (importer.spritePixelsPerUnit <= 1f)
            {
                importer.spritePixelsPerUnit = 16f; // Standard pixel art PPU
            }

            // Save settings first so the texture can be read with transparency
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            // 2. Load the readable texture to analyze pixels
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null) return 0;

            // 3. Automatically detect sprite rectangles based on transparency
            // minRectSize: 2 pixels to avoid single stray noise pixels
            Rect[] rects = UnityEditorInternal.InternalSpriteUtility.GenerateAutomaticSpriteRectangles(texture, 2, 0);

            if (rects == null || rects.Length == 0)
            {
                Debug.LogWarning($"[AutoSpriteSlicer] No transparent sprites detected in '{texture.name}'. Falling back to full texture frame.");
                rects = new Rect[] { new Rect(0, 0, texture.width, texture.height) };
            }

            // 4. Build SpriteMetaData array for legacy and modern pipelines
            var metaDataList = new List<SpriteMetaData>(rects.Length);
            var spriteRectList = new List<SpriteRect>(rects.Length);

            for (int i = 0; i < rects.Length; i++)
            {
                Rect r = rects[i];
                string spriteName = $"{texture.name}_{i}";

                // Legacy metadata
                var meta = new SpriteMetaData
                {
                    name = spriteName,
                    rect = r,
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
                metaDataList.Add(meta);

                // Modern 2D Sprite package SpriteRect
                var sRect = new SpriteRect
                {
                    name = spriteName,
                    rect = r,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                };
                spriteRectList.Add(sRect);
            }

            // 5. Apply via 2D Sprite DataProviderFactory (Unity 2D Sprite Package)
            try
            {
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                if (dataProvider != null)
                {
                    dataProvider.InitSpriteEditorDataProvider();
                    dataProvider.SetSpriteRects(spriteRectList.ToArray());
                    dataProvider.Apply();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AutoSpriteSlicer] 2D DataProvider fallback: {ex.Message}");
            }

            // 6. Also apply to importer.spritesheet directly for broad engine compatibility
#pragma warning disable CS0618
            importer.spritesheet = metaDataList.ToArray();
#pragma warning restore CS0618

            // 7. Save and reimport with new sprites
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[AutoSpriteSlicer] Sliced '{texture.name}' -> {rects.Length} sprites (Mode: Multiple, Filter: Point, Uncompressed).");
            return rects.Length;
        }
    }
}
