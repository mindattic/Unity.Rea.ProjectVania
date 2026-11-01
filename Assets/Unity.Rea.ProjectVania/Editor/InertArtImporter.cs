using UnityEditor;

namespace Unity.Rea.ProjectVania.EditorTools
{
    /// <summary>
    /// Imports the rest of U.VAN-US-A16's inert art (RECONSTRUCTION 13: the particles, selectors, banners and sprite
    /// sheets) the same pixel-faithful way <see cref="CharacterArtImporter"/> imports the world's and characters' art:
    /// whole, uncompressed, without mipmaps and point-filtered, so the pixels this ticket keeps are the original's own
    /// and not reshaped by Unity's NPOT-rescaling or compressed texture-format defaults. Nothing loads any of these at
    /// runtime; they exist only so the rebuild still has them.
    /// </summary>
    public sealed class InertArtImporter : AssetPostprocessor
    {
        public const string ResourcesRoot = "Assets/Unity.Rea.ProjectVania/Resources/";

        public static readonly string[] Folders = { "Particles/", "Selectors/", "Banners/", "SpriteSheets/" };

        void OnPreprocessTexture()
        {
            var inFolder = false;
            foreach (var folder in Folders)
            {
                if (assetPath.StartsWith(ResourcesRoot + folder))
                {
                    inFolder = true;
                    break;
                }
            }

            if (!inFolder)
            {
                return;
            }

            CharacterArtImporter.ApplyPixelArt((TextureImporter)assetImporter, readable: false);
        }
    }
}
