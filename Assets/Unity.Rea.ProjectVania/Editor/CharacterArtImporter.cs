using UnityEditor;
using UnityEngine;

namespace Unity.Rea.ProjectVania.EditorTools
{
    /// <summary>
    /// Imports the original's character strips (RECONSTRUCTION 4.8, 13) the way they are drawn: whole, unscaled (Leo's run
    /// is 257 wide and must not be rounded to a power of two), uncompressed, without mipmaps and point-filtered, so every
    /// source pixel lands on whole screen pixels. Settings are set here in code rather than by hand in the Inspector;
    /// <see cref="CharacterArt"/> cuts the frames at run time from animations.json.
    /// </summary>
    public sealed class CharacterArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Unity.Rea.ProjectVania/Resources/" + CharacterArt.ResourceFolder + "/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            ApplyPixelArt((TextureImporter)assetImporter, readable: false);
        }

        /// <summary>The original's art as XNA drew it: whole, unscaled, uncompressed, no mipmaps, point-filtered.</summary>
        public static void ApplyPixelArt(TextureImporter importer, bool readable)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.isReadable = readable;
        }
    }

    /// <summary>
    /// Imports the world's art (RECONSTRUCTION 5, 13) as <see cref="CharacterArtImporter.ApplyPixelArt"/> does: the
    /// 1920 x 1200 backdrop is drawn at its original size and must not be scaled. The tileset is also readable, because
    /// Core paints the grid's cells from its pixels (<see cref="MapView"/>).
    /// </summary>
    public sealed class WorldArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Unity.Rea.ProjectVania/Resources/" + MapView.ResourceFolder + "/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            CharacterArtImporter.ApplyPixelArt((TextureImporter)assetImporter, readable: name.StartsWith(MapView.TilesetPrefix));
        }
    }
}
