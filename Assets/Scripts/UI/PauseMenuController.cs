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
    [SerializeField] private GlobalSystemManager globalSystemManager;
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

    private GameSpeed resumeSpeed = GameSpeed.Speed1x;

    public bool IsOpen => root != null && root.activeSelf;

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
        root.SetActive(true);
        SetStatus(string.Empty, infoColor);

        globalSystemManager = HUDLocator.ResolveGlobalSystem(globalSystemManager);
        if (globalSystemManager != null)
        {
            GameSpeed current = globalSystemManager.GetGlobalDataRef().currentGameSpeed;
            resumeSpeed = current == GameSpeed.Pause ? GameSpeed.Speed1x : current;
            globalSystemManager.SetGameSpeed(GameSpeed.Pause);
        }
    }

    public void Resume()
    {
        if (!IsOpen)
            return;

        root.SetActive(false);

        if (globalSystemManager != null)
            globalSystemManager.SetGameSpeed(resumeSpeed);
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

        SetStatus("Đang lưu...", infoColor);
        manager.SaveGame();
    }

    public void LoadGame()
    {
        SaveLoadManager manager = ResolveSaveLoad();
        if (manager == null)
        {
            SetStatus("Không tìm thấy hệ thống lưu game.", errorColor);
            return;
        }

        if (!manager.HasSaveFile())
        {
            SetStatus("Chưa có bản lưu nào.", errorColor);
            return;
        }

        SetStatus("Đang tải...", infoColor);
        manager.LoadGame();
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

        globalSystemManager = HUDLocator.ResolveGlobalSystem(globalSystemManager);
        if (globalSystemManager != null)
        {
            GameSpeed loaded = globalSystemManager.GetGlobalDataRef().currentGameSpeed;
            resumeSpeed = loaded == GameSpeed.Pause ? GameSpeed.Speed1x : loaded;
            globalSystemManager.SetGameSpeed(GameSpeed.Pause);
        }

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
            SetStatus("Cài đặt chưa sẵn có.", infoColor);
            return;
        }

        onSettings.Invoke();
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;

        if (GameController.Instance != null)
            GameController.Instance.LoadScene(mainMenuSceneName);
        else
            SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ExitGame()
    {
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
