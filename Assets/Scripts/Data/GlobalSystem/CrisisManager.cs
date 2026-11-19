using UnityEngine;

/// <summary>Báo cáo chuỗi khủng hoảng mỗi tháng (cho thông báo / UI).</summary>
public struct CrisisReportPayload
{
    public int collected;      // Thi thể được thu gom tháng này
    public int unburied;       // Còn nằm lại (tươi + đang phân hủy)
    public int rotting;        // Đang phân hủy -> sinh Miasma
    public float miasma;
    public float diseasePressureAdded;
}

/// <summary>
/// GDD 3.7 Crisis Chain: thiếu than → chết cóng → đội thu gom không kịp → xác phân hủy sau 1 tháng
/// sinh khí độc Miasma → dịch bệnh bùng phát → bệnh xá quá tải → bạo loạn.
///
/// - Người chết (mọi nguyên nhân) để lại thi thể.
/// - Mỗi tháng thu gom được: nền + theo số lính gác và thầy thuốc đang làm việc.
/// - Thi thể để quá 1 tháng (qua 1 lần tick) mới phân hủy sinh Miasma; Miasma giảm dần theo thời gian.
/// - Miasma cộng vào áp lực dịch bệnh (GlobalSystemManager) và gây bất ổn nhẹ.
/// </summary>
public class CrisisManager : MonoBehaviour
{
    [Header("Thu gom thi thể")]
    [SerializeField, Min(0)] private int baseCollectPerMonth = 2;
    [Tooltip("Mỗi lính gác đang làm việc thu gom thêm bao nhiêu thi thể / tháng")]
    [SerializeField, Min(0)] private int collectPerGuard = 3;
    [SerializeField, Min(0)] private int collectPerDoctor = 1;

    [Header("Miasma")]
    [SerializeField, Min(0f)] private float miasmaPerRottingCorpse = 0.5f;
    [SerializeField, Range(0f, 1f)] private float miasmaDecayPerMonth = 0.3f;
    [Tooltip("Áp lực dịch bệnh cộng thêm cho mỗi điểm Miasma")]
    [SerializeField, Min(0f)] private float diseasePressurePerMiasma = 0.004f;
    [SerializeField, Min(0f)] private float riotPerMiasma = 0.05f;

    [SerializeField] private GlobalSystemManager globalSystemManager;
    [SerializeField] private BuildingManager buildingManager;

    /// <summary>Hệ số Miasma → áp lực dịch bệnh, đọc bởi GlobalSystemManager.</summary>
    public static float DiseaseFactor { get; private set; } = 0.004f;

    public void Init(GlobalSystemManager global, BuildingManager buildings)
    {
        globalSystemManager = global;
        buildingManager = buildings;
        DiseaseFactor = diseasePressurePerMiasma;
        _newCorpses = 0;   // static: xoá số dư từ ván trước
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.ResidentsDied, OnResidentsDied);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.ResidentsDied, OnResidentsDied);
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    // Người chết từ lần xử lý trước — chưa đủ 1 tháng nên chưa phân hủy. Không lưu save: luôn được
    // xử lý ngay trong LateUpdate của frame có tick tháng (mọi cái chết trong tháng đều xảy ra lúc tick).
    private static int _newCorpses;
    private bool _pendingTick;

    /// <summary>Báo có thi thể mới (cư dân chết, người chết trong hàng chờ ở Cổng…).</summary>
    public static void AddCorpses(int count)
    {
        if (count > 0)
            _newCorpses += count;
    }

    private void OnResidentsDied(object param)
    {
        if (param is ResidentsDiedPayload p)
            AddCorpses(p.count);
    }

    private void OnMonthChanged(object param)
    {
        if (param is MonthChangedPayload p && p.isSimulationTick)
            _pendingTick = true;
    }

    private void OnLoadCompleted(object param)
    {
        _newCorpses = 0;
        _pendingTick = false;
    }

    /// <summary>
    /// Xử lý trễ tới LateUpdate để gom đủ người chết của tháng (đói/rét/bệnh trước tick, sinh sản/tuổi già trong tick).
    /// Thi thể tươi của tháng TRƯỚC chưa thu gom mới chuyển sang phân hủy (GDD: phân hủy sau 1 tháng).
    /// </summary>
    private void LateUpdate()
    {
        if (!_pendingTick || globalSystemManager == null)
            return;
        _pendingTick = false;

        ref GlobalSystemData d = ref globalSystemManager.GetGlobalDataRef();
        var report = new CrisisReportPayload();

        // 1. Xác tươi tháng trước còn nằm lại -> đã đủ 1 tháng -> phân hủy; người chết tháng này là xác tươi
        d.corpsesRotting += d.corpsesFresh;
        d.corpsesFresh = _newCorpses;
        _newCorpses = 0;

        // 2. Thu gom: ưu tiên thi thể đang phân hủy
        int capacity = GetCollectCapacity();
        int fromRotting = Mathf.Min(capacity, d.corpsesRotting);
        d.corpsesRotting -= fromRotting;
        capacity -= fromRotting;
        int fromFresh = Mathf.Min(capacity, d.corpsesFresh);
        d.corpsesFresh -= fromFresh;
        report.collected = fromRotting + fromFresh;

        // 3. Miasma: giảm dần rồi cộng theo số xác đang phân hủy
        d.miasma = d.miasma * (1f - miasmaDecayPerMonth) + d.corpsesRotting * miasmaPerRottingCorpse;
        if (d.miasma < 0.05f)
            d.miasma = 0f;

        if (d.miasma > 0f)
            d.riotRiskMeter = Mathf.Min(100f, d.riotRiskMeter + d.miasma * riotPerMiasma);

        report.unburied = d.corpsesFresh + d.corpsesRotting;
        report.rotting = d.corpsesRotting;
        report.miasma = d.miasma;
        report.diseasePressureAdded = GetDiseasePressure(in d);

        if (report.collected > 0 || report.unburied > 0 || report.miasma > 0f)
            GameEvents.Post(EventID.CrisisReport, report);
    }

    /// <summary>Áp lực dịch bệnh do Miasma (GlobalSystemManager cộng vào mỗi tháng).</summary>
    public static float GetDiseasePressure(in GlobalSystemData d) => d.miasma * DiseaseFactor;

    private int GetCollectCapacity()
    {
        int capacity = baseCollectPerMonth;
        if (buildingManager == null)
            return capacity;

        for (int i = 0; i < buildingManager.activeCount; i++)
            capacity += GetCollectRate(in buildingManager.GetBuildingRef(i));
        return capacity;
    }

    /// <summary>Tổng số thi thể có thể thu gom mỗi tháng (nền + mọi công trình).</summary>
    public int TotalCollectPerMonth => GetCollectCapacity();

    public int BaseCollectPerMonth => baseCollectPerMonth;

    /// <summary>Công trình này có tham gia thu gom thi thể không (Trạm gác, Phòng khám).</summary>
    public static bool IsCollector(BuildingType type) =>
        type == BuildingType.GuardPost || type == BuildingType.Clinic;

    /// <summary>Số thi thể công trình này thu gom được mỗi tháng (0 nếu chưa chạy / không có người).</summary>
    public int GetCollectRate(in BuildingData b)
    {
        if (b.buildingState != BuildingState.Active || !b.isOperational)
            return 0;
        return b.buildingType switch
        {
            BuildingType.GuardPost => b.currentWorkers * collectPerGuard,
            BuildingType.Clinic => b.currentWorkers * collectPerDoctor,
            _ => 0
        };
    }

    /// <summary>Mô tả ngắn cho popup công trình: thu gom bao nhiêu, thành còn bao nhiêu thi thể.</summary>
    public string DescribeCollection(in BuildingData b)
    {
        if (!IsCollector(b.buildingType))
            return string.Empty;

        int perWorker = b.buildingType == BuildingType.GuardPost ? collectPerGuard : collectPerDoctor;
        int rate = GetCollectRate(in b);
        int pending = 0;
        if (globalSystemManager != null)
        {
            ref GlobalSystemData d = ref globalSystemManager.GetGlobalDataRef();
            pending = d.corpsesFresh + d.corpsesRotting;
        }

        var sb = new System.Text.StringBuilder(160);
        sb.Append("Thu gom thi thể: <b>").Append(rate).Append("/tháng</b>");
        sb.Append(" <size=13>(").Append(perWorker).Append(" mỗi người)</size>\n");
        sb.Append("Cả thành: ").Append(TotalCollectPerMonth).Append("/tháng · ");
        if (pending > 0)
            sb.Append("<color=#E86A5A>còn ").Append(pending).Append(" thi thể chưa thu gom</color>");
        else
            sb.Append("<color=#5CCB6A>không còn thi thể</color>");
        return sb.ToString();
    }
}
