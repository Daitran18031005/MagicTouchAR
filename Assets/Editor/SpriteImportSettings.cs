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
                // Balloon sway frames replace the static balloon sprite in place, so they keep its 256 px canvas, centre pivot and 128 PPU.
                if (assetPath.StartsWith("Assets/Sprites/Balloons/sway/"))
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 128f;
                    importer.spritePivot = new Vector2(.5f, .5f);
                }
                if (importer.spriteImportMode != SpriteImportMode.Multiple)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 128f;
                    importer.filterMode = FilterMode.Bilinear;
                }
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                // Preserve the authored pixel grid for every boss sheet and frame.
                // Same for the pixel-art creep sheets (Assets/Sprites/Enemies/<creep>/...).
                bool creepArt = assetPath.StartsWith("Assets/Sprites/Enemies/") && assetPath.IndexOf('/', "Assets/Sprites/Enemies/".Length) > 0;
                if (creepArt) { importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false; }
                if (assetPath.StartsWith("Assets/Sprites/bosses/"))
                {
                    importer.filterMode = FilterMode.Point;
                    importer.maxTextureSize = 4096; // boss sheets are up to 4096 px wide; never downscale them
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.crunchedCompression = false;
                    importer.anisoLevel = 0;
                }
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
