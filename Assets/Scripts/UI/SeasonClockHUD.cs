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
    [SerializeField] private TMP_Text txtDayAndSeason;     // "Buổi sáng, Mùa Xuân"
    [SerializeField] private TMP_Text txtMonthAndYear;     // "Tháng 3 - Năm 1"

    [Header("Kiểu mockup: \"Mùa Xuân, Năm 1\" + thanh tiến trình mùa")]
    [SerializeField] private Image seasonProgressFill;     // Image Filled ngang, 0..1 trong 3 tháng của mùa
    [SerializeField] private TMP_Text txtSeasonAndYear;    // "Mùa Xuân, Năm 1"
    [Tooltip("Icon mặt trời — đổi màu theo vòng ngày–đêm. Để trống thì tự tìm con tên Icon_Season")]
    [SerializeField] private Image sunIcon;
    private bool sunIconSearched;

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
        if (seasonProgressFill != null)
            seasonProgressFill.fillAmount = ((month - 1) % 3 + monthProgress) / 3f;

        // Không đếm "ngày thứ mấy" (1 tháng = 1 vòng ngày–đêm, xem DayCycle) — chỉ hiện buổi trong ngày
        float hour = DayCycle.GetHour(in data);
        DayPhase phase = DayCycle.GetPhase(hour, data.CurrentSeason);
        UpdateSunIcon(DayCycle.GetDarkness(hour, data.CurrentSeason));
        if ((int)phase != shownDay || month != shownMonth || data.currentYear != shownYear)
        {
            shownDay = (int)phase;
            shownMonth = month;
            shownYear = data.currentYear;
            RefreshTexts(phase, month, data.currentYear, data.CurrentSeason);
        }
    }

    /// <summary>Biểu tượng mặt trời đổi màu theo giờ: trắng ban ngày -> cam lúc chạng vạng -> xanh nhạt (trăng) ban đêm.</summary>
    private void UpdateSunIcon(float darkness)
    {
        if (sunIcon == null)
        {
            if (sunIconSearched)
                return;
            sunIconSearched = true;
            Transform t = transform.Find("Icon_Season");
            sunIcon = t != null ? t.GetComponent<Image>() : null;
            if (sunIcon == null)
                return;
        }

        Color dusk = new Color(1f, 0.62f, 0.3f, 1f);
        Color night = new Color(0.62f, 0.72f, 1f, 0.75f);
        sunIcon.color = darkness < 0.5f
            ? Color.Lerp(Color.white, dusk, darkness * 2f)
            : Color.Lerp(dusk, night, (darkness - 0.5f) * 2f);
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

    private void RefreshTexts(DayPhase phase, int month, int year, SeasonType season)
    {
        string seasonName = season switch
        {
            SeasonType.Spring => "Xuân",
            SeasonType.Summer => "Hạ",
            SeasonType.Autumn => "Thu",
            SeasonType.Winter => "Đông",
            _ => ""
        };

        string phaseName = DayCycle.GetPhaseName(phase);
        string dayText = $"{phaseName}, Mùa {seasonName}";

        if (txtSeasonAndYear != null)
            txtSeasonAndYear.text = $"Mùa {seasonName}, Năm {year}";

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
