using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Popup kết thúc ván: dùng chung cho THẤT BẠI (Game Over) và CHIẾN THẮNG.
/// - Thua: Chơi lại / Tải bản lưu / Về menu.
/// - Thắng: Tiếp tục chơi / Chơi lại / Về menu.
/// </summary>
public class GameOverPopupController : MonoBehaviour
{
    private const float ButtonStep = 88f;
    private const float WindowBottomPadding = 30f;

    [Header("Tham Chiếu")]
    [SerializeField] private SaveLoadManager saveLoadManager;

    [Header("Popup")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform window;
    [SerializeField] private TMP_Text txtTitle;
    [SerializeField] private TMP_Text txtReason;
    [SerializeField] private TMP_Text txtStats;
    [SerializeField] private Button btnContinue;
    [SerializeField] private Button btnRestart;
    [SerializeField] private Button btnLoad;
    [SerializeField] private Button btnMainMenu;

    [Header("Bố cục")]
    [Tooltip("Vị trí Y (anchoredPosition) của nút đầu tiên")]
    [SerializeField] private float firstButtonY = -245f;

    [Header("Cấu Hình")]
    [SerializeField] private string mainMenuSceneName = "HomeScene";

    private readonly List<Button> _visibleButtons = new List<Button>(4);

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (btnContinue != null) btnContinue.onClick.AddListener(ContinuePlaying);
        if (btnRestart != null) btnRestart.onClick.AddListener(Restart);
        if (btnLoad != null) btnLoad.onClick.AddListener(LoadLastSave);
        if (btnMainMenu != null) btnMainMenu.onClick.AddListener(GoToMainMenu);

        if (root != null)
            root.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.GameOver, OnGameOver);
        GameEvents.Listen(EventID.Victory, OnVictory);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.GameOver, OnGameOver);
        GameEvents.Unlisten(EventID.Victory, OnVictory);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnGameOver(object param)
    {
        GameOverPayload payload = param is GameOverPayload p ? p : default;
        ShowGameOver(payload);
    }

    private void OnVictory(object param)
    {
        VictoryPayload payload = param is VictoryPayload p ? p : default;
        ShowVictory(payload);
    }

    private void OnLoadCompleted(object param)
    {
        if (IsOpen)
            root.SetActive(false);
    }

    public void ShowGameOver(in GameOverPayload payload)
    {
        if (!Open())
            return;

        SetText(txtTitle, "THẤT BẠI");
        SetText(txtReason, GetReasonText(payload.reason));
        SetText(txtStats, $"Thành trì trụ được tới Tháng {payload.month}, Năm {payload.year}");

        SaveLoadManager manager = ResolveSaveLoad();
        if (btnLoad != null)
            btnLoad.interactable = manager != null && manager.HasSaveFile();

        LayoutButtons(btnRestart, btnLoad, btnMainMenu);
    }

    public void ShowVictory(in VictoryPayload payload)
    {
        if (!Open())
            return;

        string winters = payload.wintersSurvived > 1 ? $"{payload.wintersSurvived} mùa đông" : "mùa đông đầu tiên";
        SetText(txtTitle, "CHIẾN THẮNG");
        SetText(txtReason, $"Thành trì đã sống sót qua {winters}. Dân chúng tin tưởng vào lãnh chúa!");
        SetText(txtStats, $"Dân số: {payload.population} · Tháng {payload.month}, Năm {payload.year}");

        LayoutButtons(btnContinue, btnRestart, btnMainMenu);
    }

    private bool Open()
    {
        if (root == null)
            return false;

        transform.SetAsLastSibling();
        root.SetActive(true);
        return true;
    }

    /// <summary>Chỉ hiện các nút được truyền vào, xếp dọc từ trên xuống và co cửa sổ theo số nút.</summary>
    private void LayoutButtons(params Button[] buttons)
    {
        _visibleButtons.Clear();
        foreach (Button b in buttons)
        {
            if (b != null)
                _visibleButtons.Add(b);
        }

        Button[] all = { btnContinue, btnRestart, btnLoad, btnMainMenu };
        foreach (Button b in all)
        {
            if (b != null)
                b.gameObject.SetActive(_visibleButtons.Contains(b));
        }

        float y = firstButtonY;
        float lastBottom = -firstButtonY;
        foreach (Button b in _visibleButtons)
        {
            RectTransform rt = (RectTransform)b.transform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
            lastBottom = -y + rt.sizeDelta.y;
            y -= ButtonStep;
        }

        if (window != null)
            window.sizeDelta = new Vector2(window.sizeDelta.x, lastBottom + WindowBottomPadding);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private static string GetReasonText(GameOverReason reason) => reason switch
    {
        GameOverReason.Extinction => "Toàn bộ cư dân đã chết. Thành trì giờ chỉ còn là phế tích.",
        GameOverReason.Riot => "Dân chúng nổi dậy bạo loạn và lật đổ lãnh chúa.",
        _ => "Thành trì đã sụp đổ."
    };

    public void ContinuePlaying()
    {
        if (root != null)
            root.SetActive(false);

        if (GamePlayController.Instance != null)
            GamePlayController.Instance.ContinueAfterVictory();
    }

    public void Restart()
    {
        if (GameController.Instance != null)
        {
            GameController.Instance.RestartNewGame();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadLastSave()
    {
        SaveLoadManager manager = ResolveSaveLoad();
        if (manager == null || !manager.HasSaveFile())
            return;

        // GamePlayController tự chuyển GameOver -> Playing khi LoadCompleted
        manager.LoadGame();
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

    private SaveLoadManager ResolveSaveLoad()
    {
        if (saveLoadManager != null)
            return saveLoadManager;
        if (GameController.Instance != null)
            saveLoadManager = GameController.Instance.saveLoadManager;
        return saveLoadManager;
    }
}
