using UnityEngine;

/// <summary>
/// Kiểm tra điều kiện thua / thắng sau mỗi tháng mô phỏng:
/// - Thua — Tuyệt chủng: dân số về 0.
/// - Thua — Bạo loạn: thanh bạo loạn chạm ngưỡng liên tục N tháng.
/// - Thắng — sống qua N mùa đông với dân số >= ngưỡng (chỉ 1 lần, cờ lưu trong save).
/// Kiểm tra trễ tới LateUpdate để mọi hệ thống (chết đói, bệnh, sinh sản…) xử lý xong tháng đó.
/// </summary>
public class GameOverChecker : MonoBehaviour
{
    [SerializeField] private GlobalSystemManager globalSystemManager;
    [SerializeField] private ResidentManager residentManager;

    [Header("Bạo loạn")]
    [Tooltip("Thanh bạo loạn >= giá trị này thì tính là đang bạo loạn")]
    [SerializeField, Range(1f, 100f)] private float riotThreshold = 100f;
    [Tooltip("Số tháng liên tiếp ở mức bạo loạn trước khi thua (1 = thua ngay cuối tháng chạm đỉnh)")]
    [SerializeField, Min(1)] private int riotMonthsToLose = 1;   // GDD: Riot 100% = Lose

    [Header("Chiến thắng (GDD MVP: sống sót qua mùa đông đầu tiên)")]
    [Tooltip("Số mùa đông phải vượt qua. Game bắt đầu Năm 1 Tháng 1, mùa đông là tháng 10-12 -> sang Năm 2 là đã qua 1 mùa đông")]
    [SerializeField, Min(1)] private int wintersToSurvive = 1;
    [Tooltip("Dân số tối thiểu khi kết thúc mùa đông (0 = không yêu cầu). Nếu thiếu, game tiếp tục và thắng ngay khi đủ")]
    [SerializeField, Min(0)] private int minPopulationToWin = 20;

    private bool _pendingCheck;
    private int _riotMonths;

    public int RiotMonths => _riotMonths;
    public int RiotMonthsToLose => riotMonthsToLose;
    public int WintersToSurvive => wintersToSurvive;
    public int MinPopulationToWin => minPopulationToWin;

    /// <summary>Đã vượt đủ số mùa đông yêu cầu (chưa xét dân số).</summary>
    public bool HasSurvivedWinters(in GlobalSystemData data) => data.currentYear - 1 >= wintersToSurvive;

    private void Awake()
    {
        if (globalSystemManager == null)
            globalSystemManager = FindFirstObjectByType<GlobalSystemManager>();
        if (residentManager == null)
            residentManager = FindFirstObjectByType<ResidentManager>();
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnMonthChanged(object param)
    {
        if (param is MonthChangedPayload payload && payload.isSimulationTick)
            _pendingCheck = true;
    }

    private void OnLoadCompleted(object param)
    {
        _pendingCheck = false;
        _riotMonths = 0;
    }

    private void LateUpdate()
    {
        if (!_pendingCheck)
            return;
        _pendingCheck = false;

        GamePlayController gameplay = GamePlayController.Instance;
        if (gameplay == null || globalSystemManager == null)
            return;
        if (!gameplay.IsIn(GamePlayStateID.Playing) && !gameplay.IsIn(GamePlayStateID.Paused))
            return;

        GameOverReason reason = Evaluate();
        if (reason != GameOverReason.None)
        {
            gameplay.TriggerGameOver(reason);
            return;
        }

        if (IsVictoryReached())
            gameplay.TriggerVictory();
    }

    private bool IsVictoryReached()
    {
        ref GlobalSystemData data = ref globalSystemManager.GetGlobalDataRef();
        if (data.victoryAchieved)
            return false;

        return HasSurvivedWinters(in data) && data.totalPopulation >= minPopulationToWin;
    }

    private GameOverReason Evaluate()
    {
        ref GlobalSystemData data = ref globalSystemManager.GetGlobalDataRef();

        if (data.totalPopulation <= 0 && CountAliveSamples() == 0)
            return GameOverReason.Extinction;

        if (data.riotRiskMeter >= riotThreshold)
            _riotMonths++;
        else
            _riotMonths = 0;

        return _riotMonths >= riotMonthsToLose ? GameOverReason.Riot : GameOverReason.None;
    }

    private int CountAliveSamples()
    {
        if (residentManager == null)
            return 0;

        int alive = 0;
        for (int i = 0; i < residentManager.activeCount; i++)
        {
            if (residentManager.GetResidentRef(i).isAlive)
                alive++;
        }
        return alive;
    }
}
