using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ Cài đặt theo Setting_Mockup (5 tab: Hiển thị / Âm thanh / Game-UI / Hỗ trợ / Điều khiển).
/// Lưu bằng PlayerPrefs. UI tự dựng lúc chạy trên canvas HUD (hoặc canvas overlay bất kỳ ở Home).
/// </summary>
public class SettingsPopup : MonoBehaviour
{
    public const string PrefVSync = "Settings.VSync";
    public const string PrefFpsLimit = "Settings.FpsLimit";
    private const int DefaultFps = 60;

    private static SettingsPopup _instance;

    private UIWidgets.Window _window;
    private readonly List<Button> _tabs = new List<Button>();
    private readonly List<RectTransform> _pages = new List<RectTransform>();
    private int _currentTab;

    // Điều khiển cần làm mới khi mở lại / khôi phục mặc định
    private TMP_Dropdown _resolution;
    private Toggle _windowed, _fullscreen, _vsync;
    private Slider _fps, _master, _music, _sfx;
    private readonly List<Resolution> _resolutions = new List<Resolution>();

    public bool IsOpen => _window != null && UIPopupAnim.IsVisible(_window.overlay.gameObject);

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

    /// <summary>Áp V-Sync / giới hạn FPS đã lưu ngay khi game khởi động.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedDisplaySettings()
    {
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(PrefVSync, 0);
        Application.targetFrameRate = PlayerPrefs.GetInt(PrefFpsLimit, DefaultFps);
    }

    public void Show()
    {
        EnsureUI();
        if (_window == null)
            return;
        UIPopupAnim.Show(_window.overlay.gameObject);
        _window.overlay.SetAsLastSibling();
        Refresh();
        SelectTab(_currentTab);
    }

    public void Hide()
    {
        if (_window != null)
            UIPopupAnim.Hide(_window.overlay.gameObject);
    }

    private void OnDestroy()
    {
        if (_window != null && _window.overlay != null)
            Destroy(_window.overlay.gameObject);
    }

    // ==========================================
    // GIÁ TRỊ
    // ==========================================

    private void Refresh()
    {
        _windowed.SetIsOnWithoutNotify(!Screen.fullScreen);
        _fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
        _vsync.SetIsOnWithoutNotify(QualitySettings.vSyncCount > 0);
        _fps.SetValueWithoutNotify(PlayerPrefs.GetInt(PrefFpsLimit, DefaultFps));
        _fps.onValueChanged.Invoke(_fps.value);
        _master.SetValueWithoutNotify(MusicManager.MasterVolume * 100f);
        _master.onValueChanged.Invoke(_master.value);
        _music.SetValueWithoutNotify(MusicManager.MusicVolume * 100f);
        _music.onValueChanged.Invoke(_music.value);
        _sfx.SetValueWithoutNotify(MusicManager.SfxVolume * 100f);
        _sfx.onValueChanged.Invoke(_sfx.value);
        RefreshResolutions();
    }

    private void RefreshResolutions()
    {
        _resolutions.Clear();
        var options = new List<string>();
        int current = 0;
        foreach (Resolution r in Screen.resolutions)
        {
            string label = $"{r.width} x {r.height}";
            if (options.Contains(label))
                continue;
            if (r.width == Screen.width && r.height == Screen.height)
                current = options.Count;
            options.Add(label);
            _resolutions.Add(r);
        }
        if (options.Count == 0)
        {
            options.Add($"{Screen.width} x {Screen.height}");
            _resolutions.Add(new Resolution { width = Screen.width, height = Screen.height });
        }

        _resolution.ClearOptions();
        _resolution.AddOptions(options);
        _resolution.SetValueWithoutNotify(current);
        _resolution.RefreshShownValue();
    }

    private void OnResolutionChanged(int index)
    {
        if ((uint)index >= (uint)_resolutions.Count)
            return;
        Resolution r = _resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
    }

    private void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        _windowed.SetIsOnWithoutNotify(!fullscreen);
        _fullscreen.SetIsOnWithoutNotify(fullscreen);
    }

    private static void SetVSync(bool on)
    {
        QualitySettings.vSyncCount = on ? 1 : 0;
        PlayerPrefs.SetInt(PrefVSync, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void SetFps(float value)
    {
        int fps = Mathf.RoundToInt(value);
        Application.targetFrameRate = fps;
        PlayerPrefs.SetInt(PrefFpsLimit, fps);
        PlayerPrefs.Save();
    }

    private static void SetVolume(string key, float percent)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(percent / 100f));
        PlayerPrefs.Save();
        AudioListener.volume = MusicManager.MasterVolume;
        if (MusicManager.Instance != null)
            MusicManager.Instance.ApplyVolumes();
    }

    private void AskRestoreDefaults()
    {
        ConfirmDialog.Ask(ConfirmDialog.Icon.Save, "Xác nhận khôi phục cài đặt", "Khôi phục", RestoreDefaults);
    }

    private void RestoreDefaults()
    {
        PlayerPrefs.DeleteKey(MusicManager.PrefMaster);
        PlayerPrefs.DeleteKey(MusicManager.PrefMusic);
        PlayerPrefs.DeleteKey(MusicManager.PrefSfx);
        PlayerPrefs.DeleteKey(PrefVSync);
        PlayerPrefs.DeleteKey(PrefFpsLimit);
        PlayerPrefs.Save();

        ApplySavedDisplaySettings();
        Screen.fullScreen = true;
        AudioListener.volume = MusicManager.MasterVolume;
        if (MusicManager.Instance != null)
            MusicManager.Instance.ApplyVolumes();
        Refresh();
    }

    private void ReplayTutorial()
    {
        TutorialHints.Restart();
        Hide();
    }

    // ==========================================
    // DỰNG UI (toạ độ theo Setting_Mockup 1514x817)
    // ==========================================

    private const float ContentX = 120f;
    private const float ContentY = 250f;
    private const float ContentWidth = 1273f;
    private const float ContentHeight = 504f;
    private const float RowStride = 113f;
    private const float FirstRowY = 96f;
    private const float ControlX = 700f;
    private const float ControlWidth = 454f;

    private void EnsureUI()
    {
        if (_window != null)
            return;

        Canvas canvas = RuntimeUIKit.FindHudCanvas();
        if (canvas == null)
            return;

        _window = UIWidgets.CreateWindow(canvas.transform, "SettingsPopup", new Vector2(1514f, 817f), "Cài đặt", Hide, 63f);

        string[] tabNames = { "Hiển thị", "Âm thanh", "Game / UI", "Hỗ trợ", "Điều khiển" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            int index = i;
            Button tab = UIWidgets.TabButton(_window.frame, "Tab_" + i, tabNames[i], () => SelectTab(index));
            UIWidgets.TopLeft((RectTransform)tab.transform, 120f + i * 270f, 150f, 194f, 64f);
            _tabs.Add(tab);
        }

        RectTransform content = UIWidgets.Box(_window.frame, "Content");
        UIWidgets.TopLeft(content, ContentX, ContentY, ContentWidth, ContentHeight);

        _pages.Add(BuildDisplayPage(content));
        _pages.Add(BuildAudioPage(content));
        _pages.Add(BuildGamePage(content));
        _pages.Add(BuildHelpPage(content));
        _pages.Add(BuildControlsPage(content));

        _window.overlay.gameObject.SetActive(false);
    }

    private void SelectTab(int index)
    {
        _currentTab = index;
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool on = i == index;
            UIWidgets.SetTabSelected(_tabs[i], on);
            _pages[i].gameObject.SetActive(on);
        }
    }

    private static RectTransform Page(RectTransform content, string name)
    {
        RectTransform page = UIWidgets.Rect(content, name);
        UIWidgets.Stretch(page);
        return page;
    }

    /// <summary>Nhãn hàng bên trái (căn giữa trong cột 450px như mockup).</summary>
    private static void RowLabel(RectTransform page, int row, string text)
    {
        TextMeshProUGUI label = UIWidgets.Text(page, "Label_" + row, text, 24f, UISkin.Cream, TextAlignmentOptions.Center, true);
        UIWidgets.TopLeft(label.rectTransform, 0f, FirstRowY + row * RowStride - 20f, 450f, 40f);
    }

    private static float RowY(int row) => FirstRowY + row * RowStride;

    private RectTransform BuildDisplayPage(RectTransform content)
    {
        RectTransform page = Page(content, "Page_Display");

        RowLabel(page, 0, "Độ phân giải");
        _resolution = UIWidgets.Dropdown(page, "Dd_Resolution", 16f, 260f);
        UIWidgets.TopLeft((RectTransform)_resolution.transform, ControlX, RowY(0) - 23f, ControlWidth, 46f);
        _resolution.onValueChanged.AddListener(OnResolutionChanged);

        RowLabel(page, 1, "Chế độ màn hình");
        _windowed = CheckboxWithCaption(page, "Window", ControlX + 126f, RowY(1), v => { if (v) SetFullscreen(false); else _windowed.SetIsOnWithoutNotify(true); });
        _fullscreen = CheckboxWithCaption(page, "Fullscreen", ControlX + 263f, RowY(1), v => { if (v) SetFullscreen(true); else _fullscreen.SetIsOnWithoutNotify(true); });

        RowLabel(page, 2, "V-Sync");
        _vsync = UIWidgets.Checkbox(page, "Chk_VSync", false, SetVSync);
        UIWidgets.TopLeft((RectTransform)_vsync.transform, ControlX + 263f, RowY(2) - 20f, 40f, 40f);

        RowLabel(page, 3, "Giới hạn FPS");
        _fps = UIWidgets.Slider(page, "Sld_Fps", 30f, 120f, DefaultFps, true, v => Mathf.RoundToInt(v).ToString(), SetFps, "30", "120");
        UIWidgets.TopLeft((RectTransform)_fps.transform, ControlX - 16f, RowY(3) - 12f, 464f, 24f);
        return page;
    }

    private static Toggle CheckboxWithCaption(RectTransform page, string caption, float x, float centerY, UnityEngine.Events.UnityAction<bool> onChange)
    {
        TextMeshProUGUI text = UIWidgets.Text(page, "Caption_" + caption, caption, 16f, UISkin.Cream, TextAlignmentOptions.Center, true);
        UIWidgets.TopLeft(text.rectTransform, x - 40f, centerY - 52f, 120f, 24f);
        Toggle toggle = UIWidgets.Checkbox(page, "Chk_" + caption, false, onChange);
        UIWidgets.TopLeft((RectTransform)toggle.transform, x, centerY - 18f, 40f, 40f);
        return toggle;
    }

    private RectTransform BuildAudioPage(RectTransform content)
    {
        RectTransform page = Page(content, "Page_Audio");
        RowLabel(page, 0, "Âm lượng tổng");
        _master = VolumeSlider(page, 0, MusicManager.PrefMaster);
        RowLabel(page, 1, "Nhạc nền");
        _music = VolumeSlider(page, 1, MusicManager.PrefMusic);
        RowLabel(page, 2, "Hiệu ứng");
        _sfx = VolumeSlider(page, 2, MusicManager.PrefSfx);
        return page;
    }

    private static Slider VolumeSlider(RectTransform page, int row, string key)
    {
        Slider slider = UIWidgets.Slider(page, "Sld_" + key, 0f, 100f, 100f, true, v => $"{Mathf.RoundToInt(v)}%",
            v => SetVolume(key, v), "0", "100");
        UIWidgets.TopLeft((RectTransform)slider.transform, ControlX - 16f, RowY(row) - 12f, 464f, 24f);
        return slider;
    }

    private RectTransform BuildGamePage(RectTransform content)
    {
        RectTransform page = Page(content, "Page_Game");

        RowLabel(page, 0, "Hướng dẫn chơi");
        Button replay = UIWidgets.BoxButton(page, "Btn_Tutorial", "Xem lại hướng dẫn", 18f, ReplayTutorial);
        UIWidgets.TopLeft((RectTransform)replay.transform, ControlX, RowY(0) - 26f, ControlWidth, 52f);

        RowLabel(page, 1, "Cài đặt mặc định");
        Button restore = UIWidgets.BoxButton(page, "Btn_Restore", "Khôi phục mặc định", 18f, AskRestoreDefaults);
        UIWidgets.TopLeft((RectTransform)restore.transform, ControlX, RowY(1) - 26f, ControlWidth, 52f);
        return page;
    }

    private static RectTransform BuildHelpPage(RectTransform content)
    {
        RectTransform page = Page(content, "Page_Help");
        TextMeshProUGUI text = UIWidgets.Text(page, "Txt_Help",
            "<color=#FFD77E>Mục tiêu</color>: sống sót qua mùa đông đầu tiên mà thành không sụp đổ.\n\n" +
            "• Kiểm duyệt người tị nạn ở Cổng: Cho vào / Từ chối / Cách ly — mỗi lựa chọn đều có cái giá.\n" +
            "• Dự trữ Than trước mùa đông, thiếu sưởi dân sẽ chết cóng.\n" +
            "• Mỗi Giếng nước cấp nước cho 15 người; thiếu giếng dân sẽ khát và uống nước bẩn.\n" +
            "• Thi thể không thu gom sinh Miasma → dịch bệnh → bạo loạn.\n" +
            "• Ban hành Luật để tự động hoá việc kiểm duyệt ở Cổng.\n" +
            "• Bấm vào thông báo để tới ngay nơi đang có vấn đề.",
            19f, UISkin.Cream, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.lineSpacing = 12f;
        UIWidgets.TopLeft(text.rectTransform, 80f, 60f, ContentWidth - 160f, ContentHeight - 100f);
        return page;
    }

    private static readonly (string key, string action)[] Hotkeys =
    {
        ("WASD / giữ chuột giữa", "Di chuyển camera"),
        ("Lăn chuột", "Phóng to / thu nhỏ"),
        ("Space", "Tạm dừng"),
        ("Tab", "Đổi tốc độ x1 / x2 / x3"),
        ("1 – 6", "Nhóm xây dựng"),
        ("Delete / X", "Chế độ phá dỡ"),
        ("G", "Tới Cổng"),
        ("H", "Về trung tâm"),
        ("T", "Cây nghiên cứu"),
        ("E", "Luật (Sắc lệnh)"),
        ("F5", "Lưu nhanh"),
        ("Esc", "Đóng cửa sổ / Tạm dừng"),
    };

    private static RectTransform BuildControlsPage(RectTransform content)
    {
        RectTransform page = Page(content, "Page_Controls");
        int perColumn = (Hotkeys.Length + 1) / 2;
        for (int i = 0; i < Hotkeys.Length; i++)
        {
            int col = i / perColumn;
            int row = i % perColumn;
            float x = 90f + col * 580f;
            float y = 50f + row * 66f;

            TextMeshProUGUI key = UIWidgets.Text(page, "Key_" + i, Hotkeys[i].key, 19f, UISkin.GoldBright, TextAlignmentOptions.Left, true);
            UIWidgets.TopLeft(key.rectTransform, x, y, 250f, 36f);
            TextMeshProUGUI action = UIWidgets.Text(page, "Action_" + i, Hotkeys[i].action, 19f, UISkin.Cream, TextAlignmentOptions.Left);
            UIWidgets.TopLeft(action.rectTransform, x + 260f, y, 290f, 36f);
        }
        return page;
    }
}
