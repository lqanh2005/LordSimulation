using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ Cài đặt (mở từ Pause Menu): âm lượng tổng / nhạc / hiệu ứng, toàn màn hình, xem lại hướng dẫn.
/// Lưu bằng PlayerPrefs. UI tự dựng lúc chạy, nằm trên cùng canvas HUD.
/// </summary>
public class SettingsPopup : MonoBehaviour
{
    private const float Step = 0.1f;

    private static SettingsPopup _instance;

    private Canvas _canvas;
    private RectTransform _panel;
    private TextMeshProUGUI _master, _music, _sfx, _fullscreen;

    public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

    public static void Open()
    {
        if (_instance == null)
            _instance = new GameObject("SettingsPopup").AddComponent<SettingsPopup>();
        _instance.Show();
    }

    public static void CloseIfOpen()
    {
        if (_instance != null && _instance.IsOpen)
            _instance.Hide();
    }

    public static bool AnyOpen => _instance != null && _instance.IsOpen;

    public void Show()
    {
        EnsureUI();
        if (_panel == null)
            return;
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        _master.text = $"Âm lượng tổng: <b>{Pct(MusicManager.MasterVolume)}</b>";
        _music.text = $"Nhạc nền: <b>{Pct(MusicManager.MusicVolume)}</b>";
        _sfx.text = $"Hiệu ứng: <b>{Pct(MusicManager.SfxVolume)}</b>";
        _fullscreen.text = Screen.fullScreen ? "Toàn màn hình: Bật" : "Toàn màn hình: Tắt";
    }

    private static string Pct(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

    private void Change(string key, float fallback, float delta)
    {
        float value = Mathf.Clamp01(Mathf.Round((PlayerPrefs.GetFloat(key, fallback) + delta) * 10f) / 10f);
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        AudioListener.volume = MusicManager.MasterVolume;
        if (MusicManager.Instance != null)
            MusicManager.Instance.ApplyVolumes();
        Refresh();
    }

    private void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
        Refresh();
    }

    private void ReplayTutorial()
    {
        TutorialHints.Restart();
        Hide();
    }

    private void EnsureUI()
    {
        if (_panel != null)
            return;

        _canvas = RuntimeUIKit.FindHudCanvas();
        if (_canvas == null)
            return;

        _panel = RuntimeUIKit.CreatePanel(_canvas.transform, "SettingsPanel", 420f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.anchoredPosition = Vector2.zero;
        TextMeshProUGUI title = RuntimeUIKit.CreateHeader(_panel, Hide);
        title.text = "Cài đặt";

        _master = VolumeRow("Master", MusicManager.PrefMaster, 1f);
        _music = VolumeRow("Music", MusicManager.PrefMusic, 0.7f);
        _sfx = VolumeRow("Sfx", MusicManager.PrefSfx, 0.8f);

        RectTransform screenRow = RuntimeUIKit.CreateRow(_panel, "Fullscreen", 36f);
        Image fs = RuntimeUIKit.CreateButton(screenRow, "Toggle", "Toàn màn hình", ToggleFullscreen);
        _fullscreen = fs.GetComponentInChildren<TextMeshProUGUI>();

        RectTransform tutorialRow = RuntimeUIKit.CreateRow(_panel, "Tutorial", 36f);
        RuntimeUIKit.CreateButton(tutorialRow, "Replay", "Xem lại hướng dẫn", ReplayTutorial);

        _panel.gameObject.SetActive(false);
    }

    private TextMeshProUGUI VolumeRow(string name, string key, float fallback)
    {
        RectTransform row = RuntimeUIKit.CreateRow(_panel, name, 34f);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.childForceExpandWidth = false;

        TextMeshProUGUI label = RuntimeUIKit.CreateText(row, "Label", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        Image minus = RuntimeUIKit.CreateButton(row, "Minus", "−", () => Change(key, fallback, -Step));
        minus.gameObject.AddComponent<LayoutElement>().preferredWidth = 44f;
        Image plus = RuntimeUIKit.CreateButton(row, "Plus", "+", () => Change(key, fallback, Step));
        plus.gameObject.AddComponent<LayoutElement>().preferredWidth = 44f;
        return label;
    }
}
