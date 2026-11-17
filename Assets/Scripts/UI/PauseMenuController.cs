using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class PauseMenuController : MonoBehaviour
{
    [Header("Tham Chiếu")]
    [SerializeField] private SaveLoadManager saveLoadManager;
    [SerializeField] private PlacementManager placementManager;

    [Header("Popup")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnResume;
    [SerializeField] private Button btnSave;
    [SerializeField] private Button btnLoad;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnMainMenu;
    [SerializeField] private Button btnExit;
    [SerializeField] private TMP_Text txtStatus;

    [Header("Cấu Hình")]
    [SerializeField] private string mainMenuSceneName = "HomeScene";
    [SerializeField] private bool toggleWithEscape = true;

    [Header("Màu Thông Báo")]
    [SerializeField] private Color infoColor = new Color32(0x6B, 0x6F, 0x73, 0xFF);
    [SerializeField] private Color successColor = new Color32(0x4C, 0xAF, 0x50, 0xFF);
    [SerializeField] private Color errorColor = new Color32(0xE5, 0x48, 0x48, 0xFF);

    [Header("Sự Kiện")]
    [SerializeField] private UnityEvent onSettings = new UnityEvent();

    public bool IsOpen => UIPopupAnim.IsVisible(root);

    /// <summary>GameHotkeys tắt cờ này để tự xử lý Esc (đóng cửa sổ trước, rồi mới mở Pause).</summary>
    public bool ToggleWithEscape
    {
        get => toggleWithEscape;
        set => toggleWithEscape = value;
    }

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(Resume);
        if (btnResume != null) btnResume.onClick.AddListener(Resume);
        if (btnSave != null) btnSave.onClick.AddListener(SaveGame);
        if (btnLoad != null) btnLoad.onClick.AddListener(LoadGame);
        if (btnSettings != null) btnSettings.onClick.AddListener(OpenSettings);
        if (btnMainMenu != null) btnMainMenu.onClick.AddListener(GoToMainMenu);
        if (btnExit != null) btnExit.onClick.AddListener(ExitGame);

        if (root != null)
            root.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.SaveCompleted, OnSaveCompleted);
        GameEvents.Listen(EventID.SaveFailed, OnSaveFailed);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Listen(EventID.LoadFailed, OnLoadFailed);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.SaveCompleted, OnSaveCompleted);
        GameEvents.Unlisten(EventID.SaveFailed, OnSaveFailed);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Unlisten(EventID.LoadFailed, OnLoadFailed);
    }

    private void Update()
    {
        if (!toggleWithEscape || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (IsOpen)
            Resume();
        else if (!IsPlacing())
            Show();
    }

    public void Show()
    {
        if (root == null || IsOpen)
            return;

        transform.SetAsLastSibling();
        UIPopupAnim.Show(root);
        SetStatus(string.Empty, infoColor);

        if (GamePlayController.Instance != null)
            GamePlayController.Instance.Pause();
    }

    public void Resume()
    {
        if (!IsOpen)
            return;

        UIPopupAnim.Hide(root);

        if (GamePlayController.Instance != null)
            GamePlayController.Instance.Resume();
    }

    public void Toggle()
    {
        if (IsOpen)
            Resume();
        else
            Show();
    }

    // ==========================================
    // LƯU / TẢI GAME
    // ==========================================
    public void SaveGame()
    {
        SaveLoadManager manager = ResolveSaveLoad();
        if (manager == null)
        {
            SetStatus("Không tìm thấy hệ thống lưu game.", errorColor);
            return;
        }

        // Chọn ô lưu trong cửa sổ Lưu game (SaveGame_Mockup)
        SetStatus(string.Empty, infoColor);
        SaveLoadWindow.Open(SaveLoadWindow.Mode.Save);
    }

    public void LoadGame()
    {
        SaveLoadManager manager = ResolveSaveLoad();
        if (manager == null)
        {
            SetStatus("Không tìm thấy hệ thống lưu game.", errorColor);
            return;
        }

        if (manager.GetLatestSlot() < 0)
        {
            SetStatus("Chưa có bản lưu nào.", errorColor);
            return;
        }

        SaveLoadWindow.Open(SaveLoadWindow.Mode.Load, slot =>
        {
            SetStatus("Đang tải...", infoColor);
            manager.LoadFromSlot(slot);
        });
    }

    private void OnSaveCompleted(object param)
    {
        if (IsOpen)
            SetStatus("Đã lưu game.", successColor);
    }

    private void OnSaveFailed(object param)
    {
        if (IsOpen)
            SetStatus($"Lưu game thất bại: {param}", errorColor);
    }

    private void OnLoadCompleted(object param)
    {
        if (!IsOpen)
            return;

        SetStatus("Đã tải game.", successColor);
    }

    private void OnLoadFailed(object param)
    {
        if (IsOpen)
            SetStatus($"Tải game thất bại: {param}", errorColor);
    }

    private SaveLoadManager ResolveSaveLoad()
    {
        if (GameController.Instance != null && GameController.Instance.saveLoadManager != null)
            return GameController.Instance.saveLoadManager;

        if (saveLoadManager == null)
        {
            saveLoadManager = FindFirstObjectByType<SaveLoadManager>();
            if (saveLoadManager != null)
                saveLoadManager.Init();
        }

        return saveLoadManager;
    }

    // ==========================================
    // CÀI ĐẶT / MENU CHÍNH / THOÁT
    // ==========================================
    public void OpenSettings()
    {
        if (onSettings.GetPersistentEventCount() == 0)
        {
            SettingsPopup.Open();
            return;
        }

        onSettings.Invoke();
    }

    public void GoToMainMenu()
    {
        if (GameController.Instance != null)
        {
            GameController.Instance.GoHome();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ExitGame()
    {
        if (GameController.Instance != null)
        {
            GameController.Instance.QuitGame();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private bool IsPlacing()
    {
        if (placementManager == null)
            placementManager = FindFirstObjectByType<PlacementManager>();

        return placementManager != null && placementManager.isActiveAndEnabled && placementManager.isPlacing;
    }

    private void SetStatus(string message, Color color)
    {
        if (txtStatus == null)
            return;

        txtStatus.text = message;
        txtStatus.color = color;
    }
}
