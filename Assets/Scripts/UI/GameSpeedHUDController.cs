using UnityEngine;
using UnityEngine.UI;

public class GameSpeedHUDController : MonoBehaviour
{
    [Header("Tham Chiếu Hệ Thống Toàn Cầu")]
    [SerializeField] private GlobalSystemManager globalSystemManager;

    [Header("Nút Tốc Độ")]
    [SerializeField] private Button btnPause;
    [SerializeField] private Button btnSpeed1;
    [SerializeField] private Button btnSpeed2;
    [SerializeField] private Button btnSpeed3;

    [Header("Màu Nút")]
    [SerializeField] private Color normalColor = new Color32(0xD3, 0xD7, 0xDA, 0xFF);
    [SerializeField] private Color activeColor = new Color32(0xF2, 0xC9, 0x4C, 0xFF);

    private GameSpeed lastRunningSpeed = GameSpeed.Speed1x;
    private GameSpeed shownSpeed = (GameSpeed)255;
    private float nextResolveTime;

    private void Start()
    {
        if (btnPause != null) btnPause.onClick.AddListener(TogglePause);
        if (btnSpeed1 != null) btnSpeed1.onClick.AddListener(() => SetSpeed(GameSpeed.Speed1x));
        if (btnSpeed2 != null) btnSpeed2.onClick.AddListener(() => SetSpeed(GameSpeed.Speed2x));
        if (btnSpeed3 != null) btnSpeed3.onClick.AddListener(() => SetSpeed(GameSpeed.Speed3x));
    }

    private void Update()
    {
        if (!TryResolve()) return;

        GameSpeed speed = globalSystemManager.GetGlobalDataRef().currentGameSpeed;
        if (speed != shownSpeed)
            Highlight(speed);
    }

    public void TogglePause()
    {
        if (!TryResolve()) return;

        GameSpeed current = globalSystemManager.GetGlobalDataRef().currentGameSpeed;
        SetSpeed(current == GameSpeed.Pause ? lastRunningSpeed : GameSpeed.Pause);
    }

    public void SetSpeed(GameSpeed speed)
    {
        if (!TryResolve()) return;

        if (speed != GameSpeed.Pause)
            lastRunningSpeed = speed;

        globalSystemManager.SetGameSpeed(speed);
        Highlight(speed);
    }

    private void Highlight(GameSpeed speed)
    {
        shownSpeed = speed;
        Tint(btnPause, speed == GameSpeed.Pause);
        Tint(btnSpeed1, speed == GameSpeed.Speed1x);
        Tint(btnSpeed2, speed == GameSpeed.Speed2x);
        Tint(btnSpeed3, speed == GameSpeed.Speed3x);
    }

    private void Tint(Button button, bool active)
    {
        if (button != null && button.targetGraphic != null)
            button.targetGraphic.color = active ? activeColor : normalColor;
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
