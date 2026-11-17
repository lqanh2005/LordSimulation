using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn các nút HUD chưa có listener cố định: Cổng → mở kiểm duyệt, Chuông → cửa sổ Thông báo.
/// Gắn trên GameplayHUD (UISkinApplier tự thêm).
/// </summary>
public class HUDButtonBinder : MonoBehaviour
{
    [SerializeField] private Button btnGate;
    [SerializeField] private Button btnNotices;

    private void Awake()
    {
        if (btnGate != null)
            btnGate.onClick.AddListener(UIActions.OpenGate);
        if (btnNotices != null)
            btnNotices.onClick.AddListener(NoticeWindow.Toggle);
    }
}
