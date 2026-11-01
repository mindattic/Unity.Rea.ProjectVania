using UnityEditor;
using UnityEngine;

namespace Unity.Rea.ProjectVania.EditorTools
{
    /// <summary>
    /// Imports the GUI's art (RECONSTRUCTION 9, 13) whole and uncompressed, without mipmaps. XNA's SpriteBatch sampled
    /// linearly, which matters only where the GUI stretches a picture: <c>menu1</c> (543 x 742) is drawn over the whole
    /// 800 x 600 screen, <c>console1</c> (608 x 203) and <c>red</c> (1 x 1) over the console's 800 x 200, and the portrait's
    /// photo <c>portrait_1</c> (84 x 94) over its 99 x 110 frame, and the editor's <c>tilepalette_background</c> (300 x 300) over
    /// 300 x 295, so they are bilinear-filtered as the original showed them;
    /// everything else is drawn at its own size and is point-filtered like the world's art. Set here in code rather than
    /// by hand in the Inspector.
    /// </summary>
    public sealed class GuiArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Unity.Rea.ProjectVania/Resources/" + MainMenuView.ResourceFolder + "/";

        /// <summary>The GUI pictures the original stretched.</summary>
        public static readonly string[] Stretched = { "menu1", "console1", "red", "portrait_1", "tilepalette_background" };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            importer.filterMode = System.Array.IndexOf(Stretched, name) >= 0 ? FilterMode.Bilinear : FilterMode.Point;
        }
    }

    /// <summary>
    /// Imports the cue table's waves (RECONSTRUCTION 11) as the original's uncompressed 16-bit PCM, decompressed and
    /// preloaded, so a cue starts on the frame it is asked for.
    /// </summary>
    public sealed class CueAudioImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Unity.Rea.ProjectVania/Resources/" + CuePlayer.ResourceFolder + "/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            var sample = importer.defaultSampleSettings;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = AudioCompressionFormat.PCM;
            sample.preloadAudioData = true;
            importer.defaultSampleSettings = sample;
        }
    }

    /// <summary>
    /// Imports the console's two songs (RECONSTRUCTION 11, the original's <c>Content\Audio\Music\*.mp3</c>, 7 MB and 5 MB)
    /// compressed and streamed from disk, as XNA's MediaPlayer streamed its <c>Song</c>s, so neither sits decoded in memory.
    /// </summary>
    public sealed class SongAudioImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Unity.Rea.ProjectVania/Resources/" + SongPlayer.ResourceFolder + "/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            var sample = importer.defaultSampleSettings;
            sample.loadType = AudioClipLoadType.Streaming;
            sample.compressionFormat = AudioCompressionFormat.Vorbis;
            sample.quality = 0.7f;
            sample.preloadAudioData = false;
            importer.defaultSampleSettings = sample;
        }
    }
}
