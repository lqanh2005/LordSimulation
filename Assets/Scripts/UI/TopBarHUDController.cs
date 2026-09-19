using System;
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

    private void Start()
    {
        if (globalSystemManager == null)
        {
            globalSystemManager = FindObjectOfType<GlobalSystemManager>();
        }

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

        // 2. Cập nhật tài nguyên & dân sinh
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
        if (txtPopulation != null) txtPopulation.text = $"{data.totalPopulation}";
        if (txtHappiness != null) txtHappiness.text = $"{data.overallHappiness:F0}%";
    }

    // ==========================================
    // HÀM FORMAT SỐ (1.5K, 2M theo yêu cầu PM)
    // ==========================================
    public static string FormatNumber(int value)
    {
        if (value >= 1_000_000)
        {
            return (value / 1_000_000f).ToString("0.#") + "M";
        }
        if (value >= 1_000)
        {
            return (value / 1_000f).ToString("0.#") + "K";
        }
        return value.ToString();
    }
}
