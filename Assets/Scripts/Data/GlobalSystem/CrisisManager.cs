using UnityEngine;

/// <summary>Báo cáo chuỗi khủng hoảng mỗi tháng (cho thông báo / UI).</summary>
public struct CrisisReportPayload
{
    public int collected;      // Thi thể được thu gom tháng này
    public int unburied;       // Còn nằm lại (tươi + đang phân hủy)
    public int rotting;        // Đang phân hủy -> sinh Miasma
    public float miasma;                 // Số vùng Miasma có hiệu lực tháng tới
    public float diseasePressureAdded;   // Độ phủ Miasma (xác suất một cư dân nằm trong vùng)
}

/// <summary>
/// GDD 3.7 Crisis Chain: thiếu than → chết cóng → đội thu gom không kịp → xác phân hủy sau 1 tháng
/// sinh khí độc Miasma → dịch bệnh bùng phát → bệnh xá quá tải → bạo loạn.
///
/// GDD v0.3 mục 7.4:
/// - Người chết (mọi nguyên nhân) để lại thi thể.
/// - Mỗi người làm ở Corpse Collection Post (hiện là Trạm gác) thu gom 4 thi thể / tháng.
/// - Thi thể chưa thu gom sau 1 tháng tạo một vùng Miasma bán kính 3 ô, kéo dài 2 tháng;
///   cư dân trong vùng có 20% bị lây Sốt Đỏ mỗi tháng (ResidentDiseaseSystem), vùng không cộng dồn.
/// - Tạm thời chưa có vị trí thi thể: mỗi vùng phủ khoảng <see cref="ZoneCoverage"/> thành phố,
///   độ phủ = 1 − (1 − ZoneCoverage)^số vùng.
/// </summary>
public class CrisisManager : MonoBehaviour
{
    [Header("Thu gom thi thể")]
    [Tooltip("Thu gom nền khi chưa có ai làm (GDD: không ai thu gom thì xác nằm lại)")]
    [SerializeField, Min(0)] private int baseCollectPerMonth = 0;
    [Tooltip("GDD 7.4: mỗi người làm ở Corpse Collection Post thu gom 4 thi thể / tháng")]
    [SerializeField, Min(0)] private int collectPerGuard = 4;

    /// <summary>Tỉ lệ thành phố một vùng Miasma (bán kính 3 ô) phủ tới — xấp xỉ khi chưa có vị trí thi thể.</summary>
    public const float ZoneCoverage = 0.15f;

    [SerializeField] private GlobalSystemManager globalSystemManager;
    [SerializeField] private BuildingManager buildingManager;

    public void Init(GlobalSystemManager global, BuildingManager buildings)
    {
        globalSystemManager = global;
        buildingManager = buildings;
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

        // 1. Xác tươi tháng trước còn nằm lại -> đã đủ 1 tháng -> phân hủy, mỗi xác tạo 1 vùng Miasma
        int newZones = d.corpsesFresh;
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

        // 3. Vùng Miasma kéo dài 2 tháng (tháng tạo + tháng sau); xác còn nằm lại vẫn giữ vùng của nó
        d.miasma = Mathf.Max(newZones + d.miasmaZonesPrev, d.corpsesRotting);
        d.miasmaZonesPrev = newZones;

        report.unburied = d.corpsesFresh + d.corpsesRotting;
        report.rotting = d.corpsesRotting;
        report.miasma = d.miasma;
        report.diseasePressureAdded = GetDiseasePressure(in d);

        if (report.collected > 0 || report.unburied > 0 || report.miasma > 0f)
            GameEvents.Post(EventID.CrisisReport, report);
    }

    /// <summary>Xác suất một cư dân nằm trong ít nhất một vùng Miasma (vùng không cộng dồn).</summary>
    public static float GetMiasmaCoverage(in GlobalSystemData d) =>
        d.miasma <= 0f ? 0f : 1f - Mathf.Pow(1f - ZoneCoverage, d.miasma);

    public static float GetDiseasePressure(in GlobalSystemData d) => GetMiasmaCoverage(in d);

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

    /// <summary>Công trình thu gom thi thể (Trạm gác đóng vai Corpse Collection Post).</summary>
    public static bool IsCollector(BuildingType type) => type == BuildingType.GuardPost;

    /// <summary>Số thi thể công trình này thu gom được mỗi tháng (0 nếu chưa chạy / không có người).</summary>
    public int GetCollectRate(in BuildingData b)
    {
        if (b.buildingState != BuildingState.Active || !b.isOperational)
            return 0;
        return b.buildingType == BuildingType.GuardPost ? b.currentWorkers * collectPerGuard : 0;
    }

    /// <summary>Mô tả ngắn cho popup công trình: thu gom bao nhiêu, thành còn bao nhiêu thi thể.</summary>
    public string DescribeCollection(in BuildingData b)
    {
        if (!IsCollector(b.buildingType))
            return string.Empty;

        int perWorker = collectPerGuard;
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
