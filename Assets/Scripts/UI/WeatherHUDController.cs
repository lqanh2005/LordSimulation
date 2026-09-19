using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeatherHUDController : MonoBehaviour
{
    [Header("Tham Chiếu Hệ Thống Toàn Cầu")]
    [SerializeField] private GlobalSystemManager globalSystemManager;

    [Header("UI Thành Phần (Gắn trên Weather_Badge)")]
    [SerializeField] private Image imgWeatherIcon;         // Icon Thời Tiết
    [SerializeField] private TMP_Text txtTemperature;      // "20.0°C" (Tự đổi màu âm/dương độ)
    [SerializeField] private TMP_Text txtWeatherStatus;     // "Quang đãng" hoặc "Bão tuyết (còn 2 th.)"

    [Header("Bộ Icon Thời Tiết (Kéo Sprite tương ứng vào đây)")]
    [SerializeField] private Sprite iconClear;             // Icon Trời quang
    [SerializeField] private Sprite iconBlizzard;          // Icon Bão tuyết
    [SerializeField] private Sprite iconHeatwave;          // Icon Sóng nhiệt
    [SerializeField] private Sprite iconToxicFog;          // Icon Sương độc

    private void Start()
    {
        if (globalSystemManager == null)
        {
            globalSystemManager = FindObjectOfType<GlobalSystemManager>();
        }

        if (globalSystemManager != null)
        {
            GameEvents.Unlisten(EventID.OnWeatherChanged, OnWeatherChanged);
            GameEvents.Listen(EventID.OnWeatherChanged, OnWeatherChanged);

            ref GlobalSystemData data = ref globalSystemManager.GetGlobalDataRef();
            HandleWeatherChanged(data.currentWeather, data.environmentTemperature);
        }
    }

    private void OnDestroy()
    {
        GameEvents.Unlisten(EventID.OnWeatherChanged, OnWeatherChanged);
    }

    private void OnWeatherChanged(object param)
    {
        WeatherChangedPayload payload = (WeatherChangedPayload)param;
        HandleWeatherChanged(payload.weather, payload.temperature);
    }

    // ==========================================
    // LOGIC CẬP NHẬT GIAO DIỆN THỜI TIẾT
    // ==========================================
    private void HandleWeatherChanged(WeatherEvent weather, float temperature)
    {
        // 1. Tự động đổi Icon Sprite tương ứng
        if (imgWeatherIcon != null)
        {
            imgWeatherIcon.sprite = weather switch
            {
                WeatherEvent.Clear => iconClear,
                WeatherEvent.Blizzard => iconBlizzard,
                WeatherEvent.Heatwave => iconHeatwave,
                WeatherEvent.ToxicFog => iconToxicFog,
                _ => iconClear
            };
        }

        // 2. Cập nhật Text Nhiệt Độ & Đổi màu theo độ lạnh/nóng
        if (txtTemperature != null)
        {
            if (temperature <= -15.0f)
            {
                // Rét buốt cực hạn (Màu đỏ cảnh báo tử vong)
                txtTemperature.text = $"<color=#e74c3c>{temperature:F1}°C</color>";
            }
            else if (temperature < 0.0f)
            {
                // Băng tuyết âm độ (Màu xanh buốt giá)
                txtTemperature.text = $"<color=#5dade2>{temperature:F1}°C</color>";
            }
            else if (temperature > 32.0f)
            {
                // Nắng nóng gay gắt (Màu cam lửa)
                txtTemperature.text = $"<color=#e67e22>{temperature:F1}°C</color>";
            }
            else
            {
                // Bình thường ôn hòa
                txtTemperature.text = $"{temperature:F1}°C";
            }
        }

        // 3. Cập nhật Trạng thái thời tiết và đếm lùi thời hạn
        if (txtWeatherStatus != null)
        {
            byte duration = 0;
            if (globalSystemManager != null)
            {
                duration = globalSystemManager.GetGlobalDataRef().weatherDuration;
            }

            string durationText = duration > 0 ? $" (còn {duration} th.)" : "";

            string statusText = weather switch
            {
                WeatherEvent.Clear => "Quang đãng",
                WeatherEvent.Blizzard => $"<color=#e74c3c>Bão tuyết{durationText}</color>",
                WeatherEvent.Heatwave => $"<color=#e67e22>Sóng nhiệt{durationText}</color>",
                WeatherEvent.ToxicFog => $"<color=#9b59b6>Sương độc{durationText}</color>",
                _ => "Bình thường"
            };

            txtWeatherStatus.text = statusText;
        }
    }
}
