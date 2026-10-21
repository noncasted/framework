using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Import settings are defined by the folder: manual edits in the inspector are overwritten on reimport.
    public sealed class AudioImportPostprocessor : AssetPostprocessor
    {
        private static string SoundsFolder => FrameworkSettings.SoundsFolder + "/";
        private static string MusicFolder => FrameworkSettings.MusicFolder + "/";

        // Music can also ship as ready mp3 bytes "*.mp3.bytes" (EncodedAudio) and bypass this importer: for WebGL Unity
        // re-encodes any clip to AAC of at least ~100 kbps.
        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;

            if (string.IsNullOrEmpty(FrameworkSettings.MusicFolder) == false && assetPath.StartsWith(MusicFolder))
            {
                // Decompress On Load would unpack the whole track to PCM, so music stays compressed in memory.
                Apply(importer, false, new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.CompressedInMemory,
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.45f,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                });
            }
            else if (assetPath.StartsWith(SoundsFolder))
            {
                // Short effects are decompressed in advance to play without delay.
                Apply(importer, true, new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.5f,
                    sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate,
                    sampleRateOverride = 22050,
                });
            }
        }

        private static void Apply(AudioImporter importer, bool forceToMono, AudioImporterSampleSettings settings)
        {
            importer.forceToMono = forceToMono;
            importer.loadInBackground = true;

            // On WebGL Play() does not load the clip by itself and stays silent: the data is loaded with the asset from the bundle.
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;

            // Platform overrides would take precedence over the common settings.
            importer.ClearSampleSettingOverride("WebGL");
            importer.ClearSampleSettingOverride("Standalone");
        }
    }
}
