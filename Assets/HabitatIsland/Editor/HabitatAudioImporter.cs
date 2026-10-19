using System;
using UnityEditor;
using UnityEngine;

namespace HabitatIsland.Editor
{
    // Applies only to the selected project audio. Source files remain byte-for-byte intact.
    public sealed class HabitatAudioImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessAudio()
        {
            const string root="Assets/HabitatIsland/Resources/Audio/";
            if(!assetPath.StartsWith(root,StringComparison.Ordinal))return;
            bool ambience=assetPath.StartsWith(root+"Ambience/",StringComparison.Ordinal);
            var importer=(AudioImporter)assetImporter;
            var settings=importer.defaultSampleSettings;
            // Coast actions and Interface/UI cues share immediate, mono playback settings.
            settings.loadType=ambience?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=ambience?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;
            settings.quality=.7f;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData=true;
            importer.defaultSampleSettings=settings;
            importer.forceToMono=!ambience;
            importer.loadInBackground=ambience;
        }
    }
}
