using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SeasonClockHUD : MonoBehaviour
{
    [Header("Tham Chiếu Hệ Thống Toàn Cầu")]
    [SerializeField] private GlobalSystemManager globalSystemManager;

    [Header("Vòng Mùa")]
    [SerializeField] private RectTransform ringPivot;      // Phần xoay (vòng 4 màu)
    [SerializeField] private SeasonRingGraphic ring;
    [SerializeField] private Graphic imgSeasonDot;         // Chấm giữa đổi màu theo mùa
    [SerializeField, Min(0f)] private float rotateSmoothing = 8f;

    [Header("Text")]
    [SerializeField] private TMP_Text txtDayAndSeason;     // "Ngày 12, Mùa Xuân"
    [SerializeField] private TMP_Text txtMonthAndYear;     // "Tháng 3 - Năm 1"
    [SerializeField, Min(1)] private int daysPerMonth = 30;

    private float currentAngle;
    private bool hasAngle;
    private int shownDay = -1;
    private int shownMonth = -1;
    private int shownYear = -1;
    private float nextResolveTime;

    private void OnEnable()
    {
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnLoadCompleted(object param)
    {
        hasAngle = false;
    }

    private void Update()
    {
        if (!TryResolve()) return;

        ref GlobalSystemData data = ref globalSystemManager.GetGlobalDataRef();
        float monthProgress = Mathf.Clamp01(data.timeTickProgress);
        int month = Mathf.Clamp(data.currentMonth, 1, 12);

        UpdateRing(month, monthProgress);

        int day = Mathf.Clamp(Mathf.FloorToInt(monthProgress * daysPerMonth) + 1, 1, daysPerMonth);
        if (day != shownDay || month != shownMonth || data.currentYear != shownYear)
        {
            shownDay = day;
            shownMonth = month;
            shownYear = data.currentYear;
            RefreshTexts(day, month, data.currentYear, data.CurrentSeason);
        }
    }

    private void UpdateRing(int month, float monthProgress)
    {
        if (ringPivot == null) return;

        float targetAngle = (month - 1 + monthProgress) / 12f * 360f;
        if (!hasAngle || rotateSmoothing <= 0f)
        {
            currentAngle = targetAngle;
            hasAngle = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-rotateSmoothing * Time.unscaledDeltaTime);
            currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, t);
        }

        ringPivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    private void RefreshTexts(int day, int month, int year, SeasonType season)
    {
        string seasonName = season switch
        {
            SeasonType.Spring => "Xuân",
            SeasonType.Summer => "Hạ",
            SeasonType.Autumn => "Thu",
            SeasonType.Winter => "Đông",
            _ => ""
        };

        string dayText = $"Ngày {day}, Mùa {seasonName}";

        if (txtMonthAndYear != null)
        {
            txtMonthAndYear.text = $"Tháng {month} - Năm {year}";
            if (txtDayAndSeason != null)
                txtDayAndSeason.text = dayText;
        }
        else if (txtDayAndSeason != null)
        {
            txtDayAndSeason.text = $"{dayText} - Năm {year}";
        }

        if (imgSeasonDot != null && ring != null)
            imgSeasonDot.color = ring.GetSeasonColor(season);
    }

    private bool TryResolve()
    {
        if (globalSystemManager != null) return true;
        if (Time.unscaledTime < nextResolveTime) return false;

        nextResolveTime = Time.unscaledTime + 1f;
        globalSystemManager = HUDLocator.ResolveGlobalSystem(null);
        return globalSystemManager != null;
    }
}
