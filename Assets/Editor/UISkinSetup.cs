using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Thiết lập sprite trong Assets/UI (import + viền 9-slice) và tạo UISkin.asset (Addressables, group UI).
/// Chạy lại an toàn bất cứ lúc nào (khi designer thay ảnh trong Assets/UI).
/// </summary>
public static class UISkinSetup
{
    private const string Root = "Assets/UI/";
    public const string SkinPath = "Assets/Addressables/UI/UISkin.asset";
    private const string OldSkinPath = "Assets/Resources/UISkin.asset";

    // Ảnh từ cỡ này trở lên nén BC7 (ảnh nền 1920x1080 không nén tốn ~8 MB VRAM mỗi tấm)
    private const int CompressPixelThreshold = 512 * 512;

    // Viền 9-slice (trái, dưới, phải, trên) theo pixel ảnh gốc.
    private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "BackPopup.png", new Vector4(50, 50, 50, 50) },
        { "Black_Frame.png", new Vector4(12, 12, 12, 12) },
        { "Home/Frame_1.png", new Vector4(34, 20, 34, 20) },
        { "Home/Frame_1_Select.png", new Vector4(34, 20, 34, 20) },
        { "Home/Un_Frame_1.png", new Vector4(34, 20, 34, 20) },
        { "Gate/Accept_Frame.png", new Vector4(34, 22, 34, 22) },
        { "Gate/Deny_Frame.png", new Vector4(34, 22, 34, 22) },
        { "Gate/Quarantine_Frame.png", new Vector4(34, 22, 34, 22) },
        { "Gate/Stack_Frame.png", new Vector4(22, 22, 22, 22) },
        { "Gate/Frame_Avt.png", new Vector4(44, 44, 44, 60) },
        { "Cài đặt/Tab.png", new Vector4(20, 20, 20, 20) },
        { "Cài đặt/Tab_Select.png", new Vector4(20, 20, 20, 20) },
        { "Cài đặt/Frame.png", new Vector4(10, 10, 10, 10) },
        { "Cài đặt/Frame_Select.png", new Vector4(10, 10, 10, 10) },
        { "Cài đặt/Select_Frame.png", new Vector4(10, 10, 10, 10) },
        { "Cài đặt/Slider_Value.png", new Vector4(10, 0, 10, 0) },
        { "Loading/Slide_Bar.png", new Vector4(40, 0, 40, 0) },
        { "Loading/Slide_Value.png", new Vector4(9, 0, 9, 0) },
        { "Generated/Frame_Gold.png", new Vector4(8, 8, 8, 8) },
        { "Generated/Frame_GoldBright.png", new Vector4(8, 8, 8, 8) },
        { "Generated/Frame_Dim.png", new Vector4(8, 8, 8, 8) },
        { "Generated/Fill_Gold.png", new Vector4(8, 8, 8, 8) },
        { "Generated/Fill_Dark.png", new Vector4(8, 8, 8, 8) },
        { "Generated/Fill_White.png", new Vector4(8, 8, 8, 8) },
        { "Generated/Pill_White.png", new Vector4(15, 15, 15, 15) },
        { "Generated/Btn_Square.png", new Vector4(10, 10, 10, 10) },
        { "Generated/Frame_Dialog.png", new Vector4(8, 8, 8, 8) },
        { "Generated/Btn_SquareActive.png", new Vector4(10, 10, 10, 10) },
    };

    [MenuItem("Tools/LordSimulation/UI Skin/1. Configure Sprite Import")]
    public static void ConfigureImporters()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/UI" });
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    continue;

                string rel = path.Substring(Root.Length);
                bool isMockup = rel.Contains("Mockup") || rel.Contains("MAIN MENU");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                bool large = width * height >= CompressPixelThreshold;
                importer.textureCompression = isMockup || large ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = Borders.TryGetValue(rel, out Vector4 border) ? border : Vector4.zero;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        Debug.Log($"[UISkin] Đã cấu hình {guids.Length} ảnh trong Assets/UI.");
    }

    [MenuItem("Tools/LordSimulation/UI Skin/2. Create Or Update Skin Asset")]
    public static UISkin CreateOrUpdateSkin()
    {
        EnsureFolder("Assets/Addressables/UI");
        // Bản cũ nằm trong Resources -> chuyển sang thư mục Addressables (giữ GUID)
        if (AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath) == null && AssetDatabase.LoadAssetAtPath<UISkin>(OldSkinPath) != null)
        {
            string error = AssetDatabase.MoveAsset(OldSkinPath, SkinPath);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError("[UISkin] Không chuyển được UISkin: " + error);
        }

        UISkin skin = AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<UISkin>();
            AssetDatabase.CreateAsset(skin, SkinPath);
        }

        skin.popupFrame = S("BackPopup.png");
        skin.boxGold = S("Generated/Frame_Gold.png");
        skin.boxGoldBright = S("Generated/Frame_GoldBright.png");
        skin.boxDim = S("Generated/Frame_Dim.png");
        skin.fillGold = S("Generated/Fill_Gold.png");
        skin.fillDark = S("Generated/Fill_Dark.png");
        skin.fillWhite = S("Generated/Fill_White.png");
        skin.pillWhite = S("Generated/Pill_White.png");
        skin.blackFrame = S("Black_Frame.png");
        skin.squareButton = S("Generated/Btn_Square.png");
        skin.squareButtonActive = S("Generated/Btn_SquareActive.png");
        skin.ornateButton = S("Home/Frame_1.png");
        skin.ornateButtonSelected = S("Home/Frame_1_Select.png");
        skin.ornateButtonDisabled = S("Home/Un_Frame_1.png");
        skin.tab = S("Cài đặt/Tab.png");
        skin.tabSelected = S("Cài đặt/Tab_Select.png");

        skin.titleLine = S("ArrowLeft.png");
        skin.titleLineRight = S("ArrowRight.png");
        skin.rhombus = S("Rhombus_Icon.png");
        skin.closeIcon = S("Generated/Close_Icon.png");
        skin.chevronDown = S("Generated/Chevron_Down.png");
        skin.checkIcon = S("Generated/Check_Icon.png");
        skin.checkboxOn = S("Cài đặt/On.png");
        skin.checkboxOff = S("Cài đặt/off.png");
        skin.toggleOn = S("Rule/On.png");
        skin.toggleOff = S("Rule/Off.png");
        skin.deleteIcon = S("Rule/Delete_Icon.png");
        skin.sliderKnob = S("Cài đặt/Btn_Drag.png");
        skin.sliderFill = S("Cài đặt/Slider_Value.png");
        skin.alertIcon = S("Alert_Icon.png");
        skin.saveIcon = S("Save.png");
        skin.loadIcon = S("Generated/Load_Icon.png");
        skin.logo = S("Loading/Avt.png");

        skin.gatePortraitFrame = S("Gate/Frame_Avt.png");
        skin.gateStackFrame = S("Gate/Stack_Frame.png");
        skin.gateAccept = S("Gate/Accept_Frame.png");
        skin.gateDeny = S("Gate/Deny_Frame.png");
        skin.gateQuarantine = S("Gate/Quarantine_Frame.png");
        skin.portraitAdult = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Texture/Resident/Old/IDLE/Asset 1.png");
        skin.portraitKid = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Texture/Resident/Kid/Idle/Asset 1.png");
        skin.dialogFrame = S("Generated/Frame_Dialog.png");

        skin.iconPause = S("GamePlay/Pause_Icon.png");
        skin.iconSun = S("GamePlay/Sun_Icon.png");
        skin.iconNotice = S("GamePlay/Notice_Icon.png");
        skin.iconLetter = S("GamePlay/Letter_Icon.png");
        skin.iconSetting = S("GamePlay/Setting_Icon.png");
        skin.iconGate = S("GamePlay/Gate_Icon.png");
        skin.iconResearch = S("GamePlay/Researd_Icon.png");
        skin.iconLaw = S("GamePlay/Law_Icon.png");
        skin.iconTax = S("GamePlay/Tax_Icon.png");
        skin.iconBuild = S("GamePlay/Build_Icon.png");
        skin.iconHouse = S("GamePlay/House_Icon.png");
        skin.iconProduct = S("GamePlay/Product_Icon.png");
        skin.iconAgriculture = S("GamePlay/Agriculture_Icon.png");
        skin.iconInfrastructure = S("GamePlay/Infrastructure_Icon.png");
        skin.iconDecor = S("GamePlay/Decor_Icon.png");

        skin.resWood = S("GamePlay/Wood_Icon.png");
        skin.resStone = S("GamePlay/Stone_Icon.png");
        skin.resFood = S("GamePlay/Rice_Icon.png");
        skin.resCoal = S("GamePlay/Coal_Icon.png");
        skin.resGold = S("GamePlay/Gold_Icon.png");
        skin.resMedicine = S("Generated/Res_Medicine.png");

        skin.statPopulation = S("Generated/Stat_Population.png");
        skin.statDisease = S("Generated/Stat_Disease.png");
        skin.statStability = S("Generated/Stat_Stability.png");
        skin.statRiot = S("Generated/Stat_Riot.png");
        skin.statReputation = S("Generated/Stat_Reputation.png");

        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        UISkin.ResetCache();
        Debug.Log("[UISkin] Đã cập nhật " + SkinPath);
        return skin;
    }

    // ==========================================
    // FONT: Noto Serif (tiêu đề / nút) + Inter (nội dung) — SIL OFL, kèm license trong Assets/Fonts
    // ==========================================
    private const string FontFolder = "Assets/Fonts/";

    [MenuItem("Tools/LordSimulation/UI Skin/3. Create Font Assets")]
    public static void CreateFontAssets()
    {
        UISkin skin = LoadSkin();
        skin.titleFont = CreateDynamicFont("NotoSerif-Variable.ttf", "NotoSerif SDF.asset");
        skin.bodyFont = CreateDynamicFont("Inter-Variable.ttf", "Inter SDF.asset");
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        UISkin.ResetCache();
        Debug.Log("[UISkin] Đã tạo font TMP và gán vào UISkin.");
    }

    /// <summary>
    /// Font TMP dạng Dynamic: glyph (kể cả tiếng Việt có dấu) được sinh khi cần, fallback về
    /// LiberationSans cho ký tự font gốc không có (›, ≥, ◇...).
    /// </summary>
    private static TMPro.TMP_FontAsset CreateDynamicFont(string ttf, string assetName)
    {
        string assetPath = FontFolder + assetName;
        TMPro.TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(assetPath);
        if (existing != null)
            return existing;

        Font source = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + ttf);
        if (source == null)
        {
            Debug.LogError("[UISkin] Không thấy font " + FontFolder + ttf);
            return null;
        }

        TMPro.TMP_FontAsset font = TMPro.TMP_FontAsset.CreateFontAsset(source, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
            TMPro.AtlasPopulationMode.Dynamic, true);
        font.name = System.IO.Path.GetFileNameWithoutExtension(assetName);

        AssetDatabase.CreateAsset(font, assetPath);
        font.atlasTextures[0].name = font.name + " Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        font.material.name = font.name + " Material";
        AssetDatabase.AddObjectToAsset(font.material, font);

        TMPro.TMP_FontAsset liberation = TMPro.TMP_Settings.defaultFontAsset;
        if (liberation != null)
            font.fallbackFontAssetTable = new System.Collections.Generic.List<TMPro.TMP_FontAsset> { liberation };

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    public static UISkin LoadSkin()
    {
        UISkin skin = AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath);
        return skin != null ? skin : CreateOrUpdateSkin();
    }

    public static Sprite S(string relativePath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + relativePath);
        if (sprite == null)
            Debug.LogWarning("[UISkin] Không tìm thấy sprite " + Root + relativePath);
        return sprite;
    }
}
