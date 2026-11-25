using UnityEditor;
using UnityEngine;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Owns the generated white placeholder sprite used by every greybox object.
    ///
    /// The builtin "Sprites.asset" bundle is not reliably reachable through
    /// Resources.GetBuiltinResource in every Unity version, and passing an array
    /// to that generic method does not compile. Writing a real PNG asset is
    /// version-proof and survives scene save and reload.
    ///
    /// SpriteRenderer colors tint this white quad, so one asset covers ducks,
    /// platforms and coins during greyboxing. Swap in real art later by
    /// reassigning the Sprite field on each renderer.
    /// </summary>
    public static class PlaceholderArt
    {
        private const string SpritePath = "Assets/Art/Placeholder/WhiteSquare.png";
        private const int TextureSize = 16;

        private static Sprite _cached;

        /// <summary>
        /// The shared placeholder sprite, creating the PNG asset on first use.
        /// Returns null only if asset creation itself fails, which is logged.
        /// </summary>
        public static Sprite Sprite
        {
            get
            {
                if (_cached != null) return _cached;

                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
                if (existing != null)
                {
                    _cached = existing;
                    return existing;
                }

                _cached = CreateSpriteAsset();
                return _cached;
            }
        }

        private static Sprite CreateSpriteAsset()
        {
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);

            var pixels = new Color32[TextureSize * TextureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string dir = System.IO.Path.GetDirectoryName(SpritePath);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllBytes(SpritePath, png);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

            // 16 px at 16 pixels-per-unit is exactly 1 world unit.
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = TextureSize;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null)
            {
                Debug.LogError($"[DuckKing] Failed to create placeholder sprite at {SpritePath}.");
            }

            return sprite;
        }
    }
}