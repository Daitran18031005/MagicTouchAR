using UnityEditor;
using UnityEngine;

namespace MagicTouch.Editor
{
    /// <summary>
    /// Long music tracks stream from disk instead of being decompressed whole into RAM.
    /// Short effects (.wav, GameVictory, boss roar) keep Unity's default import.
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        private static readonly string[] MusicFiles =
        {
            "menu.ogg", "boss_appear.ogg", "boss_combat.ogg", "CombatClockMusic.ogg"
        };

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/")) return;
            string file = System.IO.Path.GetFileName(assetPath);
            if (System.Array.IndexOf(MusicFiles, file) < 0) return;

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
        }
    }
}
