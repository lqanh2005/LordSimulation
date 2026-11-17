using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bộ skin chung theo mockup trong Assets/UI (nền tối, viền vàng, chữ kem).
/// Asset nằm ở Assets/Addressables/UI/UISkin.asset (address GameAssets.UISkinKey), nạp ở màn Loading.
/// Tạo / cập nhật asset bằng menu Tools/LordSimulation/UI Skin/Create Or Update Skin Asset.
/// </summary>
[CreateAssetMenu(menuName = "LordSimulation/UI Skin", fileName = "UISkin")]
public class UISkin : ScriptableObject
{
    // ---------- Màu ----------
    public static readonly Color Gold = new Color32(0xB3, 0x8B, 0x4D, 0xFF);
    public static readonly Color GoldBright = new Color32(0xFF, 0xD7, 0x7E, 0xFF);
    public static readonly Color GoldFill = new Color32(0x8D, 0x61, 0x00, 0xFF);
    public static readonly Color Dark = new Color32(0x22, 0x22, 0x22, 0xFF);
    public static readonly Color DarkHud = new Color32(0x28, 0x26, 0x25, 0xF2);
    public static readonly Color Cream = new Color32(0xF3, 0xE6, 0xBC, 0xFF);
    public static readonly Color Tan = new Color32(0xD6, 0xBD, 0x8B, 0xFF);
    public static readonly Color Muted = new Color32(0x8A, 0x84, 0x74, 0xFF);
    public static readonly Color Positive = new Color32(0x3F, 0xC1, 0x4F, 0xFF);
    public static readonly Color Negative = new Color32(0xE0, 0x4B, 0x4B, 0xFF);
    public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.55f);

    // ---------- Khung ----------
    [Header("Khung")]
    public Sprite popupFrame;        // BackPopup: khung lớn vát góc, viền vàng đôi
    public Sprite boxGold;           // hộp tối viền vàng mảnh, bo góc
    public Sprite boxGoldBright;     // như trên, viền vàng sáng (đang chọn)
    public Sprite boxDim;            // hộp xám (khóa / vô hiệu)
    public Sprite fillGold;          // nền vàng đậm (chip đang bật)
    public Sprite fillDark;          // nền tối bo góc, không viền
    public Sprite fillWhite;         // bo góc trắng để tô màu
    public Sprite pillWhite;         // viên thuốc trắng (thanh tiến trình)
    public Sprite blackFrame;        // khung đen bo góc lớn
    public Sprite squareButton;      // nút vuông HUD
    public Sprite squareButtonActive;
    public Sprite ornateButton;      // Frame_1: nút vát hai đầu
    public Sprite ornateButtonSelected;
    public Sprite ornateButtonDisabled;
    public Sprite tab;
    public Sprite tabSelected;

    [Header("Chi tiết")]
    public Sprite titleLine;         // ArrowLeft: gạch tiêu đề
    public Sprite titleLineRight;
    public Sprite rhombus;
    public Sprite closeIcon;
    public Sprite chevronDown;
    public Sprite checkIcon;
    public Sprite checkboxOn;
    public Sprite checkboxOff;
    public Sprite toggleOn;
    public Sprite toggleOff;
    public Sprite deleteIcon;
    public Sprite sliderKnob;
    public Sprite sliderFill;
    public Sprite alertIcon;
    public Sprite saveIcon;
    public Sprite loadIcon;
    public Sprite logo;              // logo game (màn chuyển scene)

    [Header("Cổng (ảnh nền cổng 1920x1080 nạp riêng: GameAssets.GateBackgroundKey)")]
    public Sprite gatePortraitFrame;
    public Sprite gateStackFrame;
    public Sprite gateAccept;
    public Sprite gateDeny;
    public Sprite gateQuarantine;
    public Sprite portraitAdult;
    public Sprite portraitKid;
    public Sprite dialogFrame;       // hộp thoại xác nhận: nền xám viền vàng mảnh

    [Header("Icon HUD")]
    public Sprite iconPause;
    public Sprite iconSun;
    public Sprite iconNotice;
    public Sprite iconLetter;
    public Sprite iconSetting;
    public Sprite iconGate;
    public Sprite iconResearch;
    public Sprite iconLaw;
    public Sprite iconTax;
    public Sprite iconBuild;
    public Sprite iconHouse;
    public Sprite iconProduct;
    public Sprite iconAgriculture;
    public Sprite iconInfrastructure;
    public Sprite iconDecor;

    [Header("Icon tài nguyên")]
    public Sprite resWood;
    public Sprite resStone;
    public Sprite resFood;
    public Sprite resCoal;
    public Sprite resGold;
    public Sprite resMedicine;

    [Header("Icon trạng thái thành trì")]
    public Sprite statPopulation;
    public Sprite statDisease;
    public Sprite statStability;
    public Sprite statRiot;
    public Sprite statReputation;

    [Header("Font (để trống = font mặc định TMP)")]
    public TMP_FontAsset titleFont;
    public TMP_FontAsset bodyFont;

    private static UISkin _instance;
    private static bool _searched;

    public static UISkin Instance
    {
        get
        {
            if (_instance == null && !_searched)
            {
#if UNITY_EDITOR
                _searched = true;
                // Ngoài Play mode (editor tool) Addressables chưa chạy -> đọc thẳng asset
                if (!Application.isPlaying)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:UISkin");
                    if (guids.Length > 0)
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<UISkin>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                    return _instance;
                }
#endif
                // Màn Loading đã nạp trước (GameDataPreloader)
                _instance = GameAssets.GetLoaded<UISkin>(GameAssets.UISkinKey);
#if !UNITY_WEBGL || UNITY_EDITOR
                // Mở thẳng scene khác (không qua Loading): nạp đồng bộ.
                // WebGL không hỗ trợ WaitForCompletion -> chỉ khởi động nạp async, lần gọi sau sẽ có.
                if (_instance == null)
                    _instance = GameAssets.LoadSync<UISkin>(GameAssets.UISkinKey);
                _searched = _instance == null; // lỗi thật thì thôi không thử lại mỗi frame
#else
                if (_instance == null)
                    GameAssets.LoadAsync<UISkin>(GameAssets.UISkinKey);
#endif
            }
            return _instance;
        }
    }

#if UNITY_EDITOR
    /// <summary>Cho editor tool gán lại sau khi tạo asset.</summary>
    public static void ResetCache()
    {
        _instance = null;
        _searched = false;
    }
#endif

    // ==========================================
    // HELPER ÁP STYLE (dùng chung editor + runtime)
    // ==========================================

    /// <summary>Gán sprite 9-slice. pixelsPerUnitMultiplier &gt; 1 làm viền/góc nhỏ lại.</summary>
    public static void SetSliced(Image image, Sprite sprite, Color color, float ppuMultiplier = 1f)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = ppuMultiplier;
        image.preserveAspect = false;
    }

    public static void SetIcon(Image image, Sprite sprite, Color? tint = null)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = tint ?? Color.white;
        image.enabled = sprite != null;
    }

    public static void StyleText(TMP_Text text, Color color, bool title = false)
    {
        if (text == null)
            return;
        text.color = color;
        UISkin skin = Instance;
        TMP_FontAsset font = skin != null ? (title ? skin.titleFont : skin.bodyFont) : null;
        if (font != null)
            text.font = font;
    }

    /// <summary>Đổi màu theo trạng thái nút về dạng tint nhẹ (sprite đã mang màu).</summary>
    public static void SetButtonTint(Selectable selectable)
    {
        if (selectable == null)
            return;
        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.08f, 0.95f, 1f);
        colors.pressedColor = new Color(0.78f, 0.74f, 0.66f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        colors.colorMultiplier = 1f;
        selectable.colors = colors;
    }
}
