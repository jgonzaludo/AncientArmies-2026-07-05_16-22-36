using System.IO;
using UnityEditor;

// Idempotent import settings for the four formation-banner sprites
// (Patch6Integration pattern): Sprite (2D and UI), single, alpha-is-
// transparency, no mipmaps, bilinear, mobile-friendly compression. Runs on
// domain reload and via the menu item; logs to Logs/banner_sprite_import.log.
[InitializeOnLoad]
public static class BannerSpriteImport
{
    private static readonly string[] Textures =
    {
        "Assets/Art/UI/Banners/Resources/UI_Banner_Red_Melee.png",
        "Assets/Art/UI/Banners/Resources/UI_Banner_Blue_Melee.png",
        "Assets/Art/UI/Banners/Resources/UI_Banner_Red_Ranged.png",
        "Assets/Art/UI/Banners/Resources/UI_Banner_Blue_Ranged.png",
    };
    private const string LogPath = "Logs/banner_sprite_import.log";

    static BannerSpriteImport()
    {
        EditorApplication.delayCall += RunOnce;
    }

    [MenuItem("Ancient Armies/Run Banner Sprite Import")]
    public static void RunOnce()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Banner sprite import {System.DateTime.Now:HH:mm:ss} ===");
        foreach (var path in Textures)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { sb.AppendLine($"missing: {path}"); continue; }
            bool ok = imp.textureType == TextureImporterType.Sprite &&
                      imp.spriteImportMode == SpriteImportMode.Single &&
                      imp.alphaIsTransparency && !imp.mipmapEnabled &&
                      imp.filterMode == UnityEngine.FilterMode.Bilinear &&
                      imp.textureCompression == TextureImporterCompression.Compressed;
            if (ok) { sb.AppendLine($"ok: {Path.GetFileName(path)}"); continue; }
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = UnityEngine.FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.spritePixelsPerUnit = 100f;
            imp.SaveAndReimport();
            sb.AppendLine($"APPLIED: {Path.GetFileName(path)}");
        }
        File.AppendAllText(LogPath, sb.ToString());
    }
}
