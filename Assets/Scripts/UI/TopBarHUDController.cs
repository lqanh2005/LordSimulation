using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TopBarHUDController : MonoBehaviour
{
    [Header("Tham Chiếu Hệ Thống Toàn Cầu")]
    [SerializeField] private GlobalSystemManager globalSystemManager;

    [Header("Thời Gian & Mùa (Góc Trái TopBar)")]
    [SerializeField] private TMP_Text txtDateAndSeason;    // "Năm 1 - T3 (Mùa Xuân)"
    [SerializeField] private Image imgSeasonIcon;          // Icon Mùa Xuân/Hạ/Thu/Đông
    [SerializeField] private Sprite iconSpring;            // Icon Mùa Xuân
    [SerializeField] private Sprite iconSummer;            // Icon Mùa Hạ
    [SerializeField] private Sprite iconAutumn;            // Icon Mùa Thu
    [SerializeField] private Sprite iconWinter;            // Icon Mùa Đông

    [Header("Kho Tài Nguyên (Góc Phải TopBar)")]
    [SerializeField] private TMP_Text txtGold;             // Vàng (Gold)
    [SerializeField] private TMP_Text txtFood;             // Lương thực (Food)
    [SerializeField] private TMP_Text txtCoal;             // Than (Coal)
    [SerializeField] private TMP_Text txtWood;             // Gỗ (Wood)
    [SerializeField] private TMP_Text txtIron;             // Sắt (Iron)
    [SerializeField] private TMP_Text txtMedicine;         // Thuốc men (Medicine)

    [Header("Chỉ Số Dân Sinh (Ở Giữa TopBar)")]
    [SerializeField] private TMP_Text txtPopulation;       // "Dân: 120"
    [SerializeField] private TMP_Text txtHappiness;        // "Vui: 75%"
    [SerializeField] private TMP_Text txtInfection;        // "Dịch bệnh: 3%"

    [Header("Thanh Đo Bạo Loạn")]
    [SerializeField] private Image imgRiotFill;            // Image Type = Filled
    [SerializeField] private Slider sliderRiot;            // Dùng thay Image nếu thích
    [SerializeField] private TMP_Text txtRiot;             // "Bạo loạn: 35%"
    [SerializeField] private Gradient riotGradient = CreateDefaultRiotGradient();
    [SerializeField, Range(0f, 100f)] private float riotWarningThreshold = 70f;

    private void Start()
    {
        globalSystemManager = HUDLocator.ResolveGlobalSystem(globalSystemManager);

        if (globalSystemManager != null)
        {
            GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
            GameEvents.Unlisten(EventID.ResourcesChanged, OnResourcesChanged);
            GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
            GameEvents.Listen(EventID.ResourcesChanged, OnResourcesChanged);

            RefreshAllUI();
        }
        else
        {
            Debug.LogWarning("[TopBarHUDController] Không tìm thấy GlobalSystemManager trong Scene!");
        }
    }

    private void OnDestroy()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Unlisten(EventID.ResourcesChanged, OnResourcesChanged);
    }

    private void OnMonthChanged(object param)
    {
        MonthChangedPayload payload = (MonthChangedPayload)param;
        HandleMonthChanged(payload.year, payload.month, payload.season);
    }

    private void OnResourcesChanged(object param)
    {
        UpdateResourceDisplay();
    }

    private void HandleMonthChanged(int year, byte month, SeasonType season)
    {
        string seasonName = season switch
        {
            SeasonType.Spring => "Xuân",
            SeasonType.Summer => "Hạ",
            SeasonType.Autumn => "Thu",
            SeasonType.Winter => "Đông",
            _ => ""
        };

        if (txtDateAndSeason != null)
        {
            txtDateAndSeason.text = $"Năm {year} - Th.{month} ({seasonName})";
        }

        // Cập nhật Sprite Icon Mùa tương ứng
        if (imgSeasonIcon != null)
        {
            imgSeasonIcon.sprite = season switch
            {
                SeasonType.Spring => iconSpring,
                SeasonType.Summer => iconSummer,
                SeasonType.Autumn => iconAutumn,
                SeasonType.Winter => iconWinter,
                _ => null
            };
        }
    }

    // ==========================================
    // LÀM MỚI TOÀN BỘ TOPBAR
    // ==========================================

    public void RefreshAllUI()
    {
        if (globalSystemManager == null) return;

        ref GlobalSystemData data = ref globalSystemManager.GetGlobalDataRef();

        // 1. Cập nhật ngày tháng và icon mùa
        HandleMonthChanged(data.currentYear, data.currentMonth, data.CurrentSeason);

        // 2. Cập nhật tài nguyên, dân sinh & bạo loạn
        UpdateResourceDisplay();
    }

    private void UpdateResourceDisplay()
    {
        if (globalSystemManager == null) return;

        ref GlobalSystemData data = ref globalSystemManager.GetGlobalDataRef();

        // Cập nhật các kho tài nguyên (Format 1.5K, 2M)
        if (txtGold != null) txtGold.text = FormatNumber(data.treasuryGold);
        if (txtFood != null) txtFood.text = FormatNumber(data.stockFood);
        if (txtCoal != null) txtCoal.text = FormatNumber(data.stockCoal);
        if (txtWood != null) txtWood.text = FormatNumber(data.stockWood);
        if (txtIron != null) txtIron.text = FormatNumber(data.stockIron);
        if (txtMedicine != null) txtMedicine.text = FormatNumber(data.stockMedicine);

        // Cập nhật chỉ số dân sinh
        if (txtPopulation != null) txtPopulation.text = FormatNumber(data.totalPopulation);
        if (txtHappiness != null) txtHappiness.text = $"{data.overallHappiness:F0}%";
        if (txtInfection != null)
        {
            float infectedPercent = data.totalPopulation > 0 ? data.totalInfected * 100f / data.totalPopulation : 0f;
            txtInfection.text = $"{infectedPercent:F0}%";
        }

        UpdateRiotMeter(data.riotRiskMeter);
    }

    private void UpdateRiotMeter(float riotRisk)
    {
        float percent = Mathf.Clamp(riotRisk, 0f, 100f);
        float normalized = percent / 100f;
        Color color = riotGradient != null ? riotGradient.Evaluate(normalized) : Color.white;

        if (imgRiotFill != null)
        {
            imgRiotFill.fillAmount = normalized;
            imgRiotFill.color = color;
        }

        if (sliderRiot != null)
        {
            sliderRiot.minValue = 0f;
            sliderRiot.maxValue = 1f;
            sliderRiot.SetValueWithoutNotify(normalized);

            if (sliderRiot.fillRect != null && sliderRiot.fillRect.TryGetComponent(out Image fill))
            {
                fill.color = color;
            }
        }

        if (txtRiot != null)
        {
            string value = $"{percent:F0}%";
            txtRiot.text = percent >= riotWarningThreshold
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{value}</color>"
                : value;
        }
    }

    private static Gradient CreateDefaultRiotGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.30f, 0.80f, 0.35f), 0f),
                new GradientColorKey(new Color(0.95f, 0.80f, 0.20f), 0.5f),
                new GradientColorKey(new Color(0.90f, 0.25f, 0.20f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }

    // ==========================================
    // HÀM FORMAT SỐ (1.5K, 2M theo yêu cầu PM)
    // ==========================================
    public static string FormatNumber(int value)
    {
        long abs = Math.Abs((long)value);
        string sign = value < 0 ? "-" : string.Empty;

        if (abs >= 999_950)
        {
            return sign + (abs / 1_000_000f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }
        if (abs >= 1_000)
        {
            return sign + (abs / 1_000f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
