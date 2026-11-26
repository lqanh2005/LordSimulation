#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif
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

    public void TriggerGameOver()
    {
        if (!IsIn(GamePlayStateID.Init))
            ChangeState(GamePlayStateID.GameOver);
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
        if (IsIn(GamePlayStateID.GameOver))
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

#if UNITY_EDITOR
    // ==========================================
    // TEST CƯ DÂN (chỉ Editor, bấm trong Play mode) — logic ở ResidentStressTest
    // ==========================================

    [System.NonSerialized, ShowInInspector, BoxGroup("Test cư dân"), LabelText("Số cư dân"), PropertyRange(1, 5000)]
    private int testCount = 200;

    [System.NonSerialized, ShowInInspector, BoxGroup("Test cư dân"), LabelText("Hiện hình tất cả (bỏ giới hạn)")]
    private bool testShowAll;

    [System.NonSerialized, ShowInInspector, BoxGroup("Test cư dân"), LabelText("Đủ lứa tuổi (trẻ em / người già)")]
    private bool testMixedAges = true;

    [System.NonSerialized, ShowInInspector, BoxGroup("Test cư dân"), LabelText("Toàn người khoẻ mạnh")]
    private bool testHealthy = true;

    [System.NonSerialized, ShowInInspector, BoxGroup("Test cư dân"), LabelText("Cấp đủ lương thực + than")]
    private bool testFeed = true;

    [ShowInInspector, BoxGroup("Test cư dân"), LabelText("Dân đi tự do (bỏ lịch đi làm / về nhà)"), EnableIf(nameof(CanTest))]
    private bool TestFreeRoam
    {
        get => Application.isPlaying && playerContain != null && playerContain.residentManager != null
               && playerContain.residentManager.DebugFreeRoam;
        set
        {
            if (CanTest)
                playerContain.residentManager.DebugFreeRoam = value;
        }
    }

    [ShowInInspector, BoxGroup("Test cư dân"), LabelText("Tốc độ đi bộ"), PropertyRange(0.1f, 5f), EnableIf(nameof(CanTest))]
    private float TestWalkSpeed
    {
        get => playerContain != null && playerContain.residentManager != null ? playerContain.residentManager.CommuteSpeed : 0.8f;
        set
        {
            if (CanTest)
                playerContain.residentManager.CommuteSpeed = value;
        }
    }

    private bool CanTest => Application.isPlaying && playerContain != null && IsIn(GamePlayStateID.Playing);

    [BoxGroup("Test cư dân"), Button("Spawn cư dân", ButtonSizes.Large), EnableIf(nameof(CanTest))]
    public void TestSpawnResidents()
    {
        ResidentManager rm = playerContain.residentManager;
        if (testShowAll)
            rm.MaxVisualAgents = Mathf.Max(rm.MaxVisualAgents, rm.activeCount + testCount);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int added = ResidentStressTest.Spawn(playerContain, testCount, testMixedAges, testHealthy);
        ResidentStressTest.Reassign(playerContain);
        sw.Stop();
        if (testFeed)
            ResidentStressTest.FeedEveryone(playerContain);
        Debug.Log($"[Test cư dân] Spawn {added} người trong {sw.Elapsed.TotalMilliseconds:0.0} ms. Dân {rm.activeCount}, hình {rm.ActiveVisuals.Count}/{rm.MaxVisualAgents}.");
    }

    [BoxGroup("Test cư dân"), Button("Xây khu nhà + chỗ làm mẫu"), EnableIf(nameof(CanTest))]
    public void TestBuildTown(int moiLoai = 2) =>
        Debug.Log("[Test cư dân] " + ResidentStressTest.BuildTown(playerContain, moiLoai));

    [BoxGroup("Test cư dân"), Button("Tua 1 tháng"), EnableIf(nameof(CanTest))]
    public void TestAdvanceMonth()
    {
        if (testFeed)
            ResidentStressTest.FeedEveryone(playerContain);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        playerContain.globalSystemManager.DebugAdvanceMonth();
        Debug.Log($"[Test cư dân] Tính 1 tháng: {sw.Elapsed.TotalMilliseconds:0.0} ms.");
    }

    [BoxGroup("Test cư dân"), Button("Kiểm tra logic", ButtonSizes.Large), EnableIf(nameof(CanTest))]
    public void TestValidateLogic()
    {
        string report = ResidentStressTest.Validate(playerContain, out int errors);
        if (errors > 0)
            Debug.LogError(report);
        else
            Debug.Log(report);
    }

    [BoxGroup("Test cư dân"), Button("Đo hiệu năng (50 → 2000 người)", ButtonSizes.Large), EnableIf(nameof(CanTest))]
    public void TestBenchmark() =>
        StartCoroutine(ResidentStressTest.Benchmark(playerContain, new[] { 50, 100, 200, 500, 1000, 2000 }, testShowAll, 4f, Debug.Log));

    [BoxGroup("Test cư dân"), Button("Xoá cư dân test"), EnableIf(nameof(CanTest))]
    public void TestClearResidents() =>
        Debug.Log($"[Test cư dân] Đã xoá {ResidentStressTest.Clear(playerContain)} cư dân test.");
#endif
}
