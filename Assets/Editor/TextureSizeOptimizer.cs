using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

/// <summary>
/// Giới hạn cỡ import theo kích thước hiển thị thật + xếp atlas chặt hơn. Không sửa file ảnh gốc, chạy lại an toàn.
/// Mốc: zoom gần nhất (camera size 2, 1080p) = 270 px / unit, zoom mặc định = 154 px / unit.
/// </summary>
public static class TextureSizeOptimizer
{
    private const string TextureRoot = "Assets/Texture";
    private const string AtlasRoot = "Assets/Texture/Atlas/";

    [MenuItem("LordSim/Art/8. Optimize Texture Sizes")]
    public static void Run()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            int max = GetMaxSize(path);
            if (max <= 0 || !(AssetImporter.GetAtPath(path) is TextureImporter ti) || ti.maxTextureSize == max)
                continue;
            ti.maxTextureSize = max;
            ti.SaveAndReimport();
            changed++;
        }

        // Ảnh cảnh quan thu rất nhỏ khi zoom xa -> cần mipmap để khỏi nhiễu
        SetupAtlas("Atlas_Map", true);
        SetupAtlas("Atlas_Buildings", false);
        SetupAtlas("Atlas_Farm", false);
        SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget, false);
        Debug.Log($"[TextureSizeOptimizer] Đổi cỡ {changed} ảnh, đã đóng gói lại atlas.");
    }

    /// <summary>Cỡ tối đa cho 1 ảnh, 0 = không quản lý.</summary>
    public static int GetMaxSize(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).Normalize(NormalizationForm.FormC);

        if (path.StartsWith("Assets/Texture/Farm/"))
            return name == "Farm" ? 1024 : 512; // art nông trại hiển thị thu nhỏ 1/2
        if (path.StartsWith("Assets/Texture/Building/") || name == "CoalCave")
            return 1024;
        if (!path.StartsWith("Assets/Texture/Map/"))
            return 0;

        switch (name)
        {
            case "GrassTile":
            case "DirtTile":
            case "GrassDecor":
            case "map_Mockup":
                return 0;
            case "tòa":
            case "tòa bé":
            case "full tòa":
            case "đá quanh":
            case "đất":
            case "đất chắn":
            case "cổng":
                return 512;
            case "bốt quan sát":
            case "CẦU":
                return 512;
            case "BaseDirt":
                return 2048; // ảnh gốc chỉ 1145 px, giữ nguyên
        }
        if (name.StartsWith("cột"))
            return 256;
        return name.StartsWith("tường") ? 512 : 1024;
    }

    private static void SetupAtlas(string name, bool mipmaps)
    {
        if (!(AssetImporter.GetAtPath(AtlasRoot + name + ".spriteatlasv2") is SpriteAtlasImporter ai))
        {
            Debug.LogWarning($"[TextureSizeOptimizer] Không thấy atlas {name}");
            return;
        }

        SpriteAtlasPackingSettings packing = ai.packingSettings;
        packing.enableTightPacking = true;
        packing.enableRotation = false;
        packing.padding = mipmaps ? 8 : 4; // mipmap cần khoảng cách lớn hơn để không lem
        ai.packingSettings = packing;

        SpriteAtlasTextureSettings tex = ai.textureSettings;
        tex.generateMipMaps = mipmaps;
        tex.filterMode = FilterMode.Bilinear;
        ai.textureSettings = tex;

        // Trang 2048 thay vì 4096: phần trống ở trang cuối ít lãng phí hơn
        TextureImporterPlatformSettings platform = ai.GetPlatformSettings("DefaultTexturePlatform");
        platform.maxTextureSize = 2048;
        ai.SetPlatformSettings(platform);
        EditorUtility.SetDirty(ai);
        ai.SaveAndReimport();
    }
}
