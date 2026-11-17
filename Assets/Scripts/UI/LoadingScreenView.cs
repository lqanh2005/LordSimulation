using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thanh tiến trình màn Loading (Loading_Mockup): theo tiến trình khởi tạo dữ liệu thật
/// của GameDataPreloader (đọc cài đặt, bản lưu, giao diện, phông chữ, bản đồ, âm thanh).
/// </summary>
public class LoadingScreenView : MonoBehaviour
{
    [SerializeField] private Image fill;               // Image Filled ngang
    [SerializeField] private TMP_Text txtPercent;
    [SerializeField] private TMP_Text txtStatus;       // "Đang nạp bản đồ..."
    [SerializeField, Min(0.1f)] private float fillSpeed = 2.5f;   // tối đa bao nhiêu % thanh mỗi giây

    private float _shown;

    private void Start()
    {
        _shown = 0f;
        Apply(0f, null);
    }

    private void Update()
    {
        GameDataPreloader preloader = GameController.Instance != null ? GameController.Instance.Preloader : null;
        float target = preloader != null ? preloader.Progress : 1f;

        // Chạy mượt về tiến trình thật, không bao giờ vượt quá nó
        _shown = Mathf.MoveTowards(_shown, target, fillSpeed * Time.unscaledDeltaTime);
        Apply(_shown, preloader != null ? preloader.Status : null);
    }

    private void Apply(float progress, string status)
    {
        if (fill != null)
            fill.fillAmount = progress;
        if (txtPercent != null)
            txtPercent.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        if (txtStatus != null && !string.IsNullOrEmpty(status) && txtStatus.text != status)
            txtStatus.text = status;
    }
}
