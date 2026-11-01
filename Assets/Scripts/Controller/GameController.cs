using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public static GameController Instance;
    public MusicManager musicManager;
    public SaveLoadManager saveLoadManager;

    [Header("Tên Scene")]
    [SerializeField] private string homeSceneName = "HomeScene";
    [SerializeField] private string gameplaySceneName = "GamePlayScene";

    [Header("Chuyển Scene")]
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float minLoadingSeconds = 1f;

    private StateMachine<GameController, AppStateID> stateMachine;
    private GameStartMode pendingStartMode = GameStartMode.Auto;

    public string HomeSceneName => homeSceneName;
    public string GameplaySceneName => gameplaySceneName;
    public float MinLoadingSeconds => minLoadingSeconds;
    public AppStateID CurrentState => stateMachine != null ? stateMachine.CurrentId : AppStateID.None;
    public bool HasSaveFile => saveLoadManager != null && saveLoadManager.HasSaveFile();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.targetFrameRate = 60;
        if (saveLoadManager != null)
            saveLoadManager.Init();

        BuildStateMachine();
        stateMachine.ChangeState(ResolveInitialState());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        stateMachine?.Tick(Time.unscaledDeltaTime);
    }

    private void BuildStateMachine()
    {
        stateMachine = new StateMachine<GameController, AppStateID>(this);
        stateMachine.Register(AppStateID.Loading, new AppLoadingState());
        stateMachine.Register(AppStateID.Home, new AppHomeState());
        stateMachine.Register(AppStateID.GamePlay, new AppGamePlayState());
        stateMachine.StateChanged += OnStateChanged;
    }

    private AppStateID ResolveInitialState()
    {
        string active = SceneManager.GetActiveScene().name;
        if (active == homeSceneName)
            return AppStateID.Home;
        if (active == gameplaySceneName)
            return AppStateID.GamePlay;
        return AppStateID.Loading;
    }

    private void OnStateChanged(AppStateID from, AppStateID to)
    {
        GameEvents.Post(EventID.AppStateChanged, new AppStateChangedPayload { from = from, to = to });
    }

    public bool IsIn(AppStateID id) => stateMachine != null && stateMachine.IsIn(id);

    public bool ChangeState(AppStateID id)
    {
        if (stateMachine == null)
            return false;

        if (Initiate.IsFading)
        {
            Debug.LogWarning($"[GameController] Đang chuyển scene, bỏ qua yêu cầu sang {id}.");
            return false;
        }

        return stateMachine.ChangeState(id);
    }

    public void GoHome()
    {
        ChangeState(AppStateID.Home);
    }

    public void StartNewGame()
    {
        pendingStartMode = GameStartMode.NewGame;
        if (!ChangeState(AppStateID.GamePlay))
            pendingStartMode = GameStartMode.Auto;
    }

    public void ContinueGame()
    {
        if (!HasSaveFile)
            return;

        pendingStartMode = GameStartMode.Continue;
        if (!ChangeState(AppStateID.GamePlay))
            pendingStartMode = GameStartMode.Auto;
    }

    public GameStartMode ConsumeStartMode()
    {
        GameStartMode mode = pendingStartMode;
        pendingStartMode = GameStartMode.Auto;
        return mode;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void LoadScene(string sceneName)
    {
        Initiate.Fade(sceneName, fadeColor, fadeSpeed);
    }

    public void LoadSceneIfNeeded(string sceneName)
    {
        if (SceneManager.GetActiveScene().name != sceneName)
            LoadScene(sceneName);
    }
}
