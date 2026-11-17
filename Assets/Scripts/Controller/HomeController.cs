using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeController : MonoBehaviour
{
    [Header("Nút Menu")]
    [SerializeField] private Button btnNewGame;
    [SerializeField] private Button btnContinue;
    [SerializeField] private Button btnLoad;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnQuit;

    [Header("Dự Phòng Khi Không Có GameController")]
    [SerializeField] private string gameplaySceneName = "GamePlayScene";

    private void Awake()
    {
        if (btnNewGame != null) btnNewGame.onClick.AddListener(NewGame);
        if (btnContinue != null) btnContinue.onClick.AddListener(ContinueGame);
        if (btnLoad != null) btnLoad.onClick.AddListener(OpenLoad);
        if (btnSettings != null) btnSettings.onClick.AddListener(SettingsPopup.Open);
        if (btnQuit != null) btnQuit.onClick.AddListener(QuitGame);
    }

    private void Start()
    {
        RefreshButtons();
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.SaveCompleted, OnSaveChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.SaveCompleted, OnSaveChanged);
    }

    private void OnSaveChanged(object param)
    {
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        if (btnContinue != null)
            btnContinue.interactable = HasSaveFile();
        if (btnLoad != null)
            btnLoad.interactable = HasSaveFile();
    }

    public void NewGame()
    {
        if (GameController.Instance != null)
            GameController.Instance.StartNewGame();
        else
            SceneManager.LoadScene(gameplaySceneName);
    }

    public void ContinueGame()
    {
        if (!HasSaveFile())
            return;

        if (GameController.Instance != null)
            GameController.Instance.ContinueGame();
        else
            SceneManager.LoadScene(gameplaySceneName);
    }

    /// <summary>Mở màn Tải game (chọn ô lưu) rồi vào gameplay với ô đó.</summary>
    public void OpenLoad()
    {
        if (!HasSaveFile())
            return;

        SaveLoadWindow.Open(SaveLoadWindow.Mode.Load, slot =>
        {
            GameController.Instance.saveLoadManager.SetActiveSlot(slot);
            GameController.Instance.ContinueGame();
        });
    }

    public void QuitGame()
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

    private static bool HasSaveFile()
    {
        return GameController.Instance != null && GameController.Instance.HasSaveFile;
    }
}
