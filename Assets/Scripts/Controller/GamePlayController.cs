using UnityEngine;

public class GamePlayController : Singleton<GamePlayController>
{
    public PlayerContain playerContain;
    public GameScene scene;

    private StateMachine<GamePlayController, GamePlayStateID> stateMachine;

    public GamePlayStateID State => stateMachine != null ? stateMachine.CurrentId : GamePlayStateID.None;
    public GlobalSystemManager GlobalSystem => playerContain != null ? playerContain.globalSystemManager : null;

    protected override void OnAwake()
    {
        stateMachine = new StateMachine<GamePlayController, GamePlayStateID>(this);
        stateMachine.Register(GamePlayStateID.Init, new GamePlayInitState());
        stateMachine.Register(GamePlayStateID.Playing, new GamePlayPlayingState());
        stateMachine.Register(GamePlayStateID.Paused, new GamePlayPausedState());
        stateMachine.Register(GamePlayStateID.GameOver, new GamePlayOverState());
        stateMachine.Register(GamePlayStateID.Victory, new GamePlayVictoryState());
        stateMachine.StateChanged += OnStateChanged;
    }

    private void OnEnable()
    {
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void Start()
    {
        stateMachine?.ChangeState(GamePlayStateID.Init);
    }

    private void Update()
    {
        stateMachine?.Tick(Time.unscaledDeltaTime);
    }

    public bool IsIn(GamePlayStateID id) => stateMachine != null && stateMachine.IsIn(id);

    public bool ChangeState(GamePlayStateID id) => stateMachine != null && stateMachine.ChangeState(id);

    public void Pause()
    {
        if (IsIn(GamePlayStateID.Playing))
            ChangeState(GamePlayStateID.Paused);
    }

    public void Resume()
    {
        if (IsIn(GamePlayStateID.Paused))
            ChangeState(GamePlayStateID.Playing);
    }

    public GameSpeed ResolveSaveSpeed(GameSpeed current)
    {
        if (!IsIn(GamePlayStateID.Paused))
            return current;

        GamePlayPausedState paused = stateMachine.GetState<GamePlayPausedState>(GamePlayStateID.Paused);
        return paused != null ? paused.SpeedBeforePause : current;
    }

    public GameOverReason LastGameOverReason { get; private set; }

    public void TriggerGameOver(GameOverReason reason)
    {
        if (IsIn(GamePlayStateID.Init) || IsIn(GamePlayStateID.GameOver))
            return;

        LastGameOverReason = reason;
        ChangeState(GamePlayStateID.GameOver);
    }

    /// <summary>Đạt mục tiêu MVP: đánh dấu đã thắng (lưu vào save) và mở popup chiến thắng.</summary>
    public void TriggerVictory()
    {
        if (!IsIn(GamePlayStateID.Playing) && !IsIn(GamePlayStateID.Paused))
            return;

        GlobalSystemManager global = GlobalSystem;
        if (global != null)
            global.GetGlobalDataRef().victoryAchieved = true;

        ChangeState(GamePlayStateID.Victory);
    }

    /// <summary>Chơi tiếp sau khi thắng (endless).</summary>
    public void ContinueAfterVictory()
    {
        if (IsIn(GamePlayStateID.Victory))
            ChangeState(GamePlayStateID.Playing);
    }

    public bool InitializeSession()
    {
        if (playerContain == null)
        {
            Debug.LogError("[GamePlayController] Chưa gán PlayerContain.");
            return false;
        }

        GameController app = GameController.Instance;
        GameStartMode mode = app != null ? app.ConsumeStartMode() : GameStartMode.Auto;
        SaveLoadManager saveLoad = app != null ? app.saveLoadManager : null;
        bool loadSave = mode != GameStartMode.NewGame && saveLoad != null && saveLoad.HasSaveFile();

        ResidentSocialSystem.ResetStats();

        if (!loadSave)
            playerContain.globalSystemManager.InitializeNewGame();

        playerContain.Init();

        bool isNewGame = !loadSave;
        if (loadSave && !saveLoad.LoadGame())
        {
            playerContain.globalSystemManager.InitializeNewGame();
            isNewGame = true;
        }

        if (isNewGame)
            playerContain.residentManager.SpawnStartingResidents();

        return true;
    }

    private void OnLoadCompleted(object param)
    {
        if (IsIn(GamePlayStateID.GameOver) || IsIn(GamePlayStateID.Victory))
        {
            ChangeState(GamePlayStateID.Playing);
            return;
        }

        if (!IsIn(GamePlayStateID.Paused))
            return;

        GamePlayPausedState paused = stateMachine.GetState<GamePlayPausedState>(GamePlayStateID.Paused);
        if (paused != null)
            paused.CaptureAndPause(this);
    }

    private void OnStateChanged(GamePlayStateID from, GamePlayStateID to)
    {
        GameEvents.Post(EventID.GamePlayStateChanged, new GamePlayStateChangedPayload { from = from, to = to });
    }
}
