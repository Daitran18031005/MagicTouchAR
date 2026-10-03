using UnityEditor;
using UnityEngine;

namespace MagicTouch.Editor
{
    /// <summary>
    /// Automatically configures newly added PNG graphics under Assets/Sprites
    /// as 2D Sprites with optimal settings for pixel/arcade rendering.
    /// </summary>
    public class SpriteImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Sprites"))
            {
                TextureImporter importer = (TextureImporter)assetImporter;
                importer.textureType = TextureImporterType.Sprite;
                if (importer.spriteImportMode != SpriteImportMode.Multiple)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 128f;
                    importer.filterMode = FilterMode.Bilinear;
                }
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
            }
        }

        [MenuItem("Tools/Magic Touch/Reimport All Sprites")]
        public static void ReimportAllSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log($"[SpriteImportSettings] Reimported {guids.Length} sprites as 2D Sprites.");
        }
    }
}
