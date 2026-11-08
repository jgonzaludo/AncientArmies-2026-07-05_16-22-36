using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// editor tasks for the ui fonts, run through the scenario file trigger ("task=UiFontTasks.ImportTmp" then "task=UiFontTasks.Build")
public static class UiFontTasks
{
    const string Dir = "Assets/Art/UI/Fonts/Resources/";   // resources, so the hud can load them by name

    // brings in the tmp essentials (shaders, default settings) the first time
    public static string ImportTmp()
    {
        var pkg = Path.GetFullPath("Library/PackageCache");
        string found = null;
        foreach (var d in Directory.GetDirectories(pkg, "com.unity.ugui@*")) { var p = Path.Combine(d, "Package Resources", "TMP Essential Resources.unitypackage"); if (File.Exists(p)) found = p; }
        if (found == null) return "no essentials package found";
        AssetDatabase.ImportPackage(found, false);
        return "importing " + found;
    }

    // static sdf atlases: sampling 90, padding 9, 2048, latin only
    public static string Build()
    {
        var sb = new StringBuilder();
        foreach (var name in new[] { "BarlowSemiCondensed-Medium", "BarlowSemiCondensed-SemiBold", "BarlowSemiCondensed-Bold", "Cinzel-Bold" })
        {
            string ttf = Dir + name + ".ttf", asset = Dir + name + " SDF.asset";
            AssetDatabase.ImportAsset(ttf);
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) { sb.AppendLine("MISSING " + ttf); continue; }
            if (File.Exists(asset)) AssetDatabase.DeleteAsset(asset);
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            AssetDatabase.CreateAsset(fa, asset);
            fa.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTexture.name = name + " Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
            var chars = new StringBuilder();
            for (int c = 32; c < 127; c++) chars.Append((char)c);
            chars.Append("·×—–•…");   // middot, multiply and a few punctuation marks
            fa.TryAddCharacters(chars.ToString(), out string missing);
            fa.atlasPopulationMode = AtlasPopulationMode.Static;   // baked, no runtime atlas growth on a phone
            EditorUtility.SetDirty(fa); EditorUtility.SetDirty(fa.material);
            sb.AppendLine(name + " glyphs=" + fa.characterTable.Count + (string.IsNullOrEmpty(missing) ? "" : " missing=" + missing));
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }
}
