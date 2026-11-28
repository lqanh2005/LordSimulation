using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum NoticeLevel : byte
{
    Info = 0,       // Dùng mẫu Notice_Reminder
    Warning = 1,    // Dùng mẫu Notice_Warning
    Critical = 2    // Mẫu Warning tô đỏ đậm, hiện lâu hơn (GDD 7.3: Disease Outbreak, Riot Critical…)
}

/// <summary>
/// Toast thông báo góc trái màn hình. Dùng 2 mẫu Notice_Reminder / Notice_Warning có sẵn trong
/// GameplayHUD/Notifications; nếu scene không có thì tự dựng panel đơn giản.
/// Nghe các sự kiện gameplay và biến chúng thành câu thông báo dễ hiểu.
/// Chạy LateUpdate sau GameOverChecker để đọc đúng số tháng bạo loạn.
/// </summary>
[DefaultExecutionOrder(100)]
public class NotificationFeed : MonoBehaviour
{
    private const float DefaultDuration = 6f;
    private const float DedupeSeconds = 3f;
    private const int MaxVisible = 5;
    private const float RowHeight = 52f;
    private const float RowSpacing = 12f;
    private const float MinWidth = 260f;
    private const float MaxWidth = 720f;
    private const float TextLeft = 30f;
    private const float TextRight = 48f;

    public static NotificationFeed Instance { get; private set; }

    /// <summary>Một mục trong lịch sử thông báo (cửa sổ Thông báo ở nút chuông).</summary>
    public struct HistoryEntry
    {
        public string message;
        public NoticeLevel level;
        public int year;
        public int month;
        public System.Action onClick;
    }

    private const int MaxHistory = 50;
    private readonly List<HistoryEntry> _history = new List<HistoryEntry>();

    /// <summary>Lịch sử thông báo, mới nhất ở đầu danh sách.</summary>
    public IReadOnlyList<HistoryEntry> History => _history;

    private sealed class Notice
    {
        public RectTransform rect;
        public float expireAt;
    }

    private readonly List<Notice> _active = new List<Notice>();
    private readonly Dictionary<string, float> _recentKeys = new Dictionary<string, float>();
    private readonly int[] _deathsThisFrame = new int[5];   // theo DeathCause
    private bool _hasPendingDeaths;
    private bool _pendingMonthReport;
    private int _lastInfected;
    private WeatherEvent _lastWeather;

    private RectTransform _container;
    private GameObject _infoTemplate;
    private GameObject _warningTemplate;
    private PlacementManager _placement;
    private GlobalSystemManager _global;
    private EdictManager _edicts;
    private GameOverChecker _gameOver;

    public static void EnsureInstance(PlacementManager placement)
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("NotificationFeed");
        NotificationFeed feed = go.AddComponent<NotificationFeed>();
        feed._placement = placement;
    }

    /// <summary>Hiện một thông báo. <paramref name="key"/> dùng để chống lặp trong vài giây.</summary>
    /// <summary>Hiện thông báo. <paramref name="onClick"/>: bấm vào thông báo để tới nơi liên quan (GDD 7.3).</summary>
    public static void Show(string message, NoticeLevel level = NoticeLevel.Info, string key = null, System.Action onClick = null)
    {
        if (Instance != null)
            Instance.Push(message, level, key, onClick);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.BuildingPlaceFailed, OnPlaceFailed);
        GameEvents.Listen(EventID.BuildingCompleted, OnBuildingCompleted);
        GameEvents.Listen(EventID.EdictTriggered, OnEdictTriggered);
        GameEvents.Listen(EventID.ResearchCompleted, OnResearchCompleted);
        GameEvents.Listen(EventID.HungerResolved, OnHunger);
        GameEvents.Listen(EventID.ThirstResolved, OnThirst);
        GameEvents.Listen(EventID.ColderResolved, OnCold);
        GameEvents.Listen(EventID.ResidentsDied, OnResidentsDied);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Listen(EventID.MerchantArrived, OnMerchantArrived);
        GameEvents.Listen(EventID.MerchantLeft, OnMerchantLeft);
        GameEvents.Listen(EventID.WaitingYardReport, OnWaitingYardReport);
        GameEvents.Listen(EventID.CrisisReport, OnCrisisReport);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.BuildingPlaceFailed, OnPlaceFailed);
        GameEvents.Unlisten(EventID.BuildingCompleted, OnBuildingCompleted);
        GameEvents.Unlisten(EventID.EdictTriggered, OnEdictTriggered);
        GameEvents.Unlisten(EventID.ResearchCompleted, OnResearchCompleted);
        GameEvents.Unlisten(EventID.HungerResolved, OnHunger);
        GameEvents.Unlisten(EventID.ThirstResolved, OnThirst);
        GameEvents.Unlisten(EventID.ColderResolved, OnCold);
        GameEvents.Unlisten(EventID.ResidentsDied, OnResidentsDied);
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Unlisten(EventID.MerchantArrived, OnMerchantArrived);
        GameEvents.Unlisten(EventID.MerchantLeft, OnMerchantLeft);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnWaitingYardReport);
        GameEvents.Unlisten(EventID.CrisisReport, OnCrisisReport);
    }

    private void OnCrisisReport(object param)
    {
        if (!(param is CrisisReportPayload p))
            return;

        if (p.rotting > 0)
            Push($"{p.rotting} thi thể chưa được thu gom đang phân hủy — cần thêm người ở Trạm gác thu gom", NoticeLevel.Critical, "corpses",
                () => UIActions.OpenBuildCategory("Hạ tầng"));
        if (p.miasma >= 1f)
            Push($"{Mathf.RoundToInt(p.miasma)} vùng Miasma: khoảng {Mathf.RoundToInt(p.diseasePressureAdded * 100f)}% dân sống trong vùng, 20%/tháng nhiễm Sốt Đỏ",
                p.diseasePressureAdded >= 0.3f ? NoticeLevel.Critical : NoticeLevel.Warning, "miasma");
    }

    private void OnWaitingYardReport(object param)
    {
        if (!(param is WaitingYardReportPayload p))
            return;

        if (p.left > 0)
            Push($"{p.left} người trong hàng chờ bỏ đi vì đói (giảm uy tín)", NoticeLevel.Warning, "yard-left", UIActions.OpenGate);
        if (p.died > 0)
            Push($"{p.died} người chết ngoài tường thành (uy tín giảm, thi thể sát tường)", NoticeLevel.Warning, "yard-died", UIActions.OpenGate);
        if (p.hungry > 0)
            Push($"Hàng ngoài tường đang đói: {p.hungry} người chờ quá 1 tháng", NoticeLevel.Warning, "yard-hungry", UIActions.OpenGate);
        if (p.newlyInfected > 0)
            Push($"Bệnh lây trong hàng chờ: thêm {p.newlyInfected} người nhiễm", NoticeLevel.Warning, "yard-infect", UIActions.OpenGate);

    }

    private void OnMerchantArrived(object param)
    {
        if (!(param is bool live) || !live)
            return;

        PlayerContain contain = FindFirstObjectByType<PlayerContain>();
        int months = contain != null && contain.merchantManager != null ? contain.merchantManager.MonthsLeft : 0;
        Push($"Thương nhân đã tới Cổng thành, ở lại {months} tháng", NoticeLevel.Info, "merchant", UIActions.OpenMerchant);
    }

    private void OnMerchantLeft(object param)
    {
        if (param is bool live && live)
            Push("Thương nhân đã rời Cổng thành", NoticeLevel.Info, "merchant-left");
    }

    // ==========================================
    // SỰ KIỆN -> THÔNG BÁO
    // ==========================================

    private void OnPlaceFailed(object param)
    {
        if (!(param is BuildingPlaceFailedPayload p))
            return;

        string name = GetBuildingName(p.type);
        string msg = p.reason switch
        {
            BuildingPlaceFailReason.NotEnoughResources => $"Không đủ tài nguyên xây {name}: thiếu {DescribeMissing(p.missingWood, p.missingGold)}",
            BuildingPlaceFailReason.Blocked => $"Không thể xây {name} ở đây: vị trí bị chặn",
            BuildingPlaceFailReason.AlreadyBuilt => $"{name} chỉ được xây một lần",
            BuildingPlaceFailReason.CapacityFull => "Đã đạt giới hạn số công trình",
            BuildingPlaceFailReason.NotResearched => $"Cần nghiên cứu \"{GetResearchName(ResearchCatalog.GetRequiredResearch(p.type))}\" để xây {name}",
            _ => $"Không thể xây {name}"
        };

        Push(msg, NoticeLevel.Warning, "place:" + p.type + ":" + p.reason,
            p.reason == BuildingPlaceFailReason.NotResearched ? UIActions.OpenResearch : (System.Action)null);
    }

    private void OnBuildingCompleted(object param)
    {
        if (!(param is ushort id) || _placement == null || _placement.buildingManager == null)
            return;

        int index = _placement.buildingManager.FindBuildingIndexById(id);
        if (index < 0)
            return;

        BuildingType type = _placement.buildingManager.GetBuildingRef(index).buildingType;
        if (type == BuildingType.Road)
            return; // Vẽ đường từng ô -> không báo từng ô

        Push($"Đã xây xong {GetBuildingName(type)}", NoticeLevel.Info, "built:" + id, () => UIActions.FocusBuilding(id));
        if (type == BuildingType.CityHall && RoadNetwork.Enabled)
            Push("Từ giờ công trình sản xuất phải nối đường tới Tòa thị chính để hoạt động", NoticeLevel.Warning, "road-rule");
    }

    private void OnEdictTriggered(object param)
    {
        if (!(param is ushort ruleId))
            return;

        string name = $"#{ruleId}";
        EdictManager edicts = ResolveEdicts();
        if (edicts != null)
        {
            int index = edicts.FindIndexById(ruleId);
            if (index >= 0 && !string.IsNullOrEmpty(edicts.GetEdictRef(index).ruleName))
                name = edicts.GetEdictRef(index).ruleName;
        }

        Push($"Sắc lệnh \"{name}\" đã được thi hành", NoticeLevel.Info, "edict:" + ruleId, UIActions.OpenEdicts);
    }

    private void OnResearchCompleted(object param)
    {
        if (!(param is ResearchID id))
            return;

        Push($"Nghiên cứu hoàn thành: {GetResearchName(id)}", NoticeLevel.Info, "research:" + id, UIActions.OpenResearch);
    }

    private void OnHunger(object param)
    {
        if (!(param is float rate) || rate <= 0f)
            return;

        Push($"Thiếu lương thực: {Mathf.RoundToInt(rate * 100f)}% dân bị đói", NoticeLevel.Warning, "hunger", () => UIActions.OpenBuildCategory("Nông nghiệp"));
    }

    private void OnThirst(object param)
    {
        if (!(param is float rate) || rate <= 0f)
            return;

        string extra = "";
        GlobalSystemManager global = HUDLocator.ResolveGlobalSystem(null);
        if (global != null)
        {
            int need = WaterRules.WellsNeeded(global.GetGlobalDataRef().totalPopulation, global.CurrentWaterCapacity);
            if (need > 0)
                extra = $" — cần thêm {need} Giếng nước";
        }

        // Khát nặng (>= 1/3 dân) là nguy cấp: khát 2 tháng liền có thể chết
        NoticeLevel level = rate >= 0.34f ? NoticeLevel.Critical : NoticeLevel.Warning;
        Push($"Thiếu nước: {Mathf.RoundToInt(rate * 100f)}% dân bị khát{extra}", level, "thirst", () => UIActions.OpenBuildCategory("Hạ tầng"));
    }

    private void OnCold(object param)
    {
        if (!(param is float rate) || rate <= 0f)
            return;

        Push($"Thiếu sưởi ấm: {Mathf.RoundToInt(rate * 100f)}% dân bị lạnh", NoticeLevel.Warning, "cold");
    }

    private void OnResidentsDied(object param)
    {
        if (!(param is ResidentsDiedPayload p) || p.count <= 0)
            return;

        _deathsThisFrame[(int)p.cause] += p.count;
        _hasPendingDeaths = true;
    }

    private void OnMonthChanged(object param)
    {
        if (param is MonthChangedPayload p && p.isSimulationTick)
            _pendingMonthReport = true;
    }

    private void OnLoadCompleted(object param)
    {
        ClearAll();
        GlobalSystemManager global = ResolveGlobal();
        _lastInfected = global != null ? global.GetGlobalDataRef().totalInfected : 0;
        _lastWeather = global != null ? global.GetGlobalDataRef().currentWeather : WeatherEvent.Clear;
    }

    private void LateUpdate()
    {
        if (_hasPendingDeaths)
            FlushDeaths();

        if (_pendingMonthReport)
        {
            _pendingMonthReport = false;
            ReportMonthlyStatus();
        }

        float now = Time.unscaledTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].rect == null || now >= _active[i].expireAt)
                Remove(_active[i]);
        }
    }

    private void FlushDeaths()
    {
        _hasPendingDeaths = false;
        for (int i = 0; i < _deathsThisFrame.Length; i++)
        {
            int count = _deathsThisFrame[i];
            _deathsThisFrame[i] = 0;
            if (count <= 0)
                continue;

            string cause = (DeathCause)i switch
            {
                DeathCause.Starvation => "chết đói",
                DeathCause.Cold => "chết rét",
                DeathCause.Disease => "chết vì bệnh",
                DeathCause.Thirst => "chết khát",
                _ => "qua đời vì tuổi già"
            };
            NoticeLevel level = (DeathCause)i == DeathCause.OldAge ? NoticeLevel.Info : NoticeLevel.Warning;
            Push($"{count} cư dân đã {cause}", level, "death:" + i);
        }
    }

    /// <summary>Cảnh báo tổng hợp cuối tháng: dịch bệnh, kho sắp cạn, bạo loạn.</summary>
    private void ReportMonthlyStatus()
    {
        GlobalSystemManager global = ResolveGlobal();
        if (global == null)
            return;

        ref GlobalSystemData d = ref global.GetGlobalDataRef();
        int pop = Mathf.Max(0, d.totalPopulation);

        // Bão tuyết (GDD 3.7 giai đoạn 1)
        if (d.currentWeather == WeatherEvent.Blizzard && _lastWeather != WeatherEvent.Blizzard)
            Push($"Bão tuyết ập tới ({d.weatherDuration} tháng): nhiệt độ {d.environmentTemperature:0}°C, than tiêu hao tăng mạnh",
                NoticeLevel.Critical, "blizzard", () => UIActions.OpenBuildCategory("Sản xuất"));
        _lastWeather = d.currentWeather;

        // Dịch bệnh
        // GDD 7.4: từ 3 người Infected trong thành -> Critical "Dịch bùng phát"
        if (d.totalInfected >= ResidentDiseaseSystem.OutbreakInfectedThreshold)
            Push($"Dịch bùng phát: {d.totalInfected} người đang mắc Sốt Đỏ"
                    + (ResidentDiseaseSystem.LastInfirmaryOverloaded ? " — Bệnh xá quá tải" : ""), NoticeLevel.Critical, "disease");
        else if (d.totalInfected > _lastInfected)
            Push($"Có thêm ca bệnh mới ({d.totalInfected} người đang bệnh)", NoticeLevel.Info, "disease");
        _lastInfected = d.totalInfected;

        // Lương thực: không đủ cho tháng tới
        SeasonType nextSeason = d.currentMonth == 12 ? SeasonType.Spring : (SeasonType)((d.currentMonth) / 3);
        int foodNeed = pop * (nextSeason == SeasonType.Winter ? 2 : 1);
        if (pop > 0 && d.stockFood < foodNeed)
            Push($"Lương thực sắp cạn: còn {d.stockFood}, tháng tới cần {foodNeed}", NoticeLevel.Info, "food-low", () => UIActions.OpenBuildCategory("Nông nghiệp"));

        // Nước: dân sắp vượt sức cấp nước của Giếng (chưa khát nhưng sắp)
        GlobalSystemManager waterGlobal = HUDLocator.ResolveGlobalSystem(null);
        if (pop > 0 && waterGlobal != null)
        {
            int waterCapacity = waterGlobal.CurrentWaterCapacity;
            if (pop <= waterCapacity && pop >= waterCapacity - 2)
                Push($"Nước sắp không đủ: {pop}/{waterCapacity} người — xây thêm Giếng nước", NoticeLevel.Info, "water-low", () => UIActions.OpenBuildCategory("Hạ tầng"));
        }

        // Than: mùa đông đang tới / đang là mùa đông
        if (pop > 0 && nextSeason == SeasonType.Winter && d.stockCoal < pop)
            Push($"Than sắp hết: còn {d.stockCoal}, mùa đông cần {pop}/tháng", NoticeLevel.Info, "coal-low", () => UIActions.OpenBuildCategory("Sản xuất"));

        // Thuốc khi có bệnh
        if (d.totalInfected > 0 && d.stockMedicine <= 0)
            Push("Hết thuốc men: người bệnh không được chữa trị", NoticeLevel.Warning, "medicine");

        ReportHomelessAndIdleBuildings(pop);
        ReportObjective(in d);

        // Bạo loạn
        GameOverChecker checker = ResolveGameOver();
        if (checker != null && checker.RiotMonths > 0)
        {
            int left = checker.RiotMonthsToLose - checker.RiotMonths;
            Push(left > 0
                    ? $"Bạo loạn đã bùng nổ! Còn {left} tháng để dẹp yên trước khi bị lật đổ"
                    : "Bạo loạn đã bùng nổ!", NoticeLevel.Critical, "riot-critical", UIActions.OpenEdicts);
        }
        else if (d.riotRiskMeter >= StabilityRules.RiotCriticalThreshold)
        {
            // GDD 9.6: Riot >= 70 -> Critical
            Push($"Bạo loạn sắp nổ ra: {Mathf.RoundToInt(d.riotRiskMeter)}%", NoticeLevel.Critical, "riot", UIActions.OpenEdicts);
        }
    }

    /// <summary>GDD 7.3 Warning: "Homeless", "Building Missing Workers" — bấm để tới nơi xử lý.</summary>
    private void ReportHomelessAndIdleBuildings(int population)
    {
        PlayerContain contain = FindFirstObjectByType<PlayerContain>();
        if (contain == null)
            return;

        ResidentManager residents = contain.residentManager;
        if (residents != null)
        {
            int alive = 0, homeless = 0;
            for (int i = 0; i < residents.activeCount; i++)
            {
                ref ResidentData r = ref residents.GetResidentRef(i);
                if (!r.isAlive)
                    continue;
                alive++;
                if (r.assignedHouseID < 0)
                    homeless++;
            }

            if (homeless > 0 && alive > 0)
            {
                int scaled = Mathf.Max(homeless, Mathf.RoundToInt(homeless * (float)Mathf.Max(population, alive) / alive));
                Push($"{scaled} cư dân vô gia cư: dễ bệnh, mất vui và chết rét mùa đông", NoticeLevel.Warning, "homeless",
                    () => UIActions.OpenBuildCategory("Nhà ở"));
            }
        }

        BuildingManager buildings = contain.buildingManager;
        if (buildings != null)
        {
            int idle = 0;
            ushort firstIdle = 0;
            for (int i = 0; i < buildings.activeCount; i++)
            {
                ref BuildingData b = ref buildings.GetBuildingRef(i);
                if (b.maxWorkers == 0 || b.currentWorkers > 0 || !b.isOperational
                    || b.buildingState != BuildingState.Active)
                    continue;
                idle++;
                if (firstIdle == 0)
                    firstIdle = b.buildingID;
            }

            if (idle > 0)
            {
                ushort target = firstIdle;
                Push($"{idle} công trình không có công nhân", NoticeLevel.Warning, "idle-buildings",
                    () => UIActions.FocusBuilding(target));
            }
        }
    }

    /// <summary>Nhắc mục tiêu chiến thắng theo mùa (GDD: sống qua mùa đông đầu tiên).</summary>
    private void ReportObjective(in GlobalSystemData d)
    {
        GameOverChecker checker = ResolveGameOver();
        if (checker == null || d.victoryAchieved)
            return;

        int minPop = checker.MinPopulationToWin;
        if (checker.HasSurvivedWinters(in d))
        {
            if (d.totalPopulation < minPop)
                Push($"Đã qua mùa đông! Cần thêm {minPop - d.totalPopulation} dân để thắng", NoticeLevel.Info, "objective");
            return;
        }

        if (d.currentMonth == 7)
            Push("Còn 3 tháng tới mùa đông: hãy tích trữ than và lương thực", NoticeLevel.Info, "objective");
        else if (d.currentMonth == 10)
            Push("Mùa đông đã tới! Trụ vững tới hết Tháng 12", NoticeLevel.Warning, "objective");
    }

    private void ShowStartObjective()
    {
        GlobalSystemManager global = ResolveGlobal();
        GameOverChecker checker = ResolveGameOver();
        if (global == null || checker == null)
            return;

        ref GlobalSystemData d = ref global.GetGlobalDataRef();
        if (d.victoryAchieved || checker.HasSurvivedWinters(in d))
            return;

        string pop = checker.MinPopulationToWin > 0 ? $", ≥{checker.MinPopulationToWin} dân" : "";
        string winters = checker.WintersToSurvive > 1 ? $"{checker.WintersToSurvive} mùa đông" : "mùa đông đầu";
        Push($"Mục tiêu: sống qua {winters}{pop}", NoticeLevel.Info, "objective-start");
    }

    // ==========================================
    // HIỂN THỊ
    // ==========================================

    private const float CriticalDuration = 12f;
    private static readonly Color CriticalColor = new Color32(0xC0, 0x39, 0x2B, 0xF5);

    private void Push(string message, NoticeLevel level, string key, System.Action onClick = null)
    {
        if (string.IsNullOrEmpty(message))
            return;

        float now = Time.unscaledTime;
        if (!string.IsNullOrEmpty(key))
        {
            if (_recentKeys.TryGetValue(key, out float last) && now - last < DedupeSeconds)
                return;
            _recentKeys[key] = now;
        }

        GlobalSystemManager global = ResolveGlobal();
        _history.Insert(0, new HistoryEntry
        {
            message = message,
            level = level,
            year = global != null ? global.GetGlobalDataRef().currentYear : 0,
            month = global != null ? global.GetGlobalDataRef().currentMonth : 0,
            onClick = onClick
        });
        if (_history.Count > MaxHistory)
            _history.RemoveAt(_history.Count - 1);
        NoticeWindow.RefreshIfOpen();

        if (!EnsureContainer())
            return;

        if (onClick != null)
            message += "  <b>›</b>";

        RectTransform rect = CreateNoticeView(level, message);
        if (rect == null)
            return;

        float duration = level == NoticeLevel.Critical ? CriticalDuration : DefaultDuration;
        var notice = new Notice { rect = rect, expireAt = now + duration };
        Button close = rect.GetComponentInChildren<Button>(true);
        if (close != null)
        {
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(() => Remove(notice));
        }

        if (onClick != null)
        {
            // Bấm vào thân thông báo -> tới nơi liên quan rồi tắt thông báo
            Button body = rect.GetComponent<Button>();
            if (body == null)
                body = rect.gameObject.AddComponent<Button>();
            body.targetGraphic = rect.GetComponent<Image>();
            body.onClick.RemoveAllListeners();
            body.onClick.AddListener(() =>
            {
                onClick();
                Remove(notice);
            });
        }

        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayAlert(level);

        _active.Insert(0, notice);
        while (_active.Count > MaxVisible)
            Remove(_active[_active.Count - 1]);

        Relayout();
    }

    private void Remove(Notice notice)
    {
        if (!_active.Remove(notice))
            return;
        if (notice.rect != null)
            Destroy(notice.rect.gameObject);
        Relayout();
    }

    private void ClearAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].rect != null)
                Destroy(_active[i].rect.gameObject);
        }
        _active.Clear();
        _recentKeys.Clear();
    }

    private void Relayout()
    {
        float y = 0f;
        for (int i = 0; i < _active.Count; i++)
        {
            if (_active[i].rect == null)
                continue;
            _active[i].rect.anchoredPosition = new Vector2(0f, y);
            y -= RowHeight + RowSpacing;
        }
    }

    private RectTransform CreateNoticeView(NoticeLevel level, string message)
    {
        GameObject template = level == NoticeLevel.Info ? _infoTemplate : _warningTemplate;
        GameObject go = template != null ? Instantiate(template, _container) : CreateFallbackNotice(level);
        go.name = "Notice";
        go.SetActive(true);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);

        TMP_Text text = go.transform.Find("Txt_Message")?.GetComponent<TMP_Text>();
        if (text == null)
            text = go.GetComponentInChildren<TMP_Text>(true);

        if (level == NoticeLevel.Critical)
        {
            Image bg = go.GetComponent<Image>();
            if (bg != null)
            {
                // Nền tối của skin nhân màu sẽ ra đỏ bẩn -> đổi sang nền trắng bo góc rồi tô đỏ
                UISkin skin = UISkin.Instance;
                if (skin != null && skin.fillWhite != null)
                    UISkin.SetSliced(bg, skin.fillWhite, CriticalColor);
                else
                    bg.color = CriticalColor;
            }
            if (text != null)
            {
                text.color = Color.white;
                text.fontStyle |= FontStyles.Bold;
            }
        }
        if (text != null)
        {
            text.text = message;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            float width = Mathf.Clamp(text.GetPreferredValues(message).x + TextLeft + TextRight, MinWidth, MaxWidth);
            rect.sizeDelta = new Vector2(width, RowHeight);
            RectTransform tr = text.rectTransform;
            tr.sizeDelta = new Vector2(width - TextLeft - TextRight, tr.sizeDelta.y);
        }

        return rect;
    }

    private GameObject CreateFallbackNotice(NoticeLevel level)
    {
        GameObject go = new GameObject("Notice", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_container, false);
        go.GetComponent<Image>().color = level == NoticeLevel.Warning
            ? new Color32(0xE8, 0xC4, 0xC4, 0xF2)
            : new Color32(0xE6, 0xDD, 0xC8, 0xF2);

        TextMeshProUGUI text = RuntimeUIKit.CreateText(go.transform, "Txt_Message", 16f, FontStyles.Normal, TextAlignmentOptions.Left);
        RectTransform tr = text.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
        tr.pivot = new Vector2(0f, 0.5f);
        tr.anchoredPosition = new Vector2(TextLeft, 0f);
        tr.sizeDelta = new Vector2(200f, 30f);

        Image close = RuntimeUIKit.CreateButton(go.transform, "Btn_Close", "X", () => { });
        RectTransform cr = close.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f);
        cr.pivot = new Vector2(1f, 0.5f);
        cr.anchoredPosition = new Vector2(-14f, 0f);
        cr.sizeDelta = new Vector2(30f, 30f);
        return go;
    }

    private bool EnsureContainer()
    {
        if (_container != null)
            return true;

        GameObject existing = GameObject.Find("GameplayHUD/Notifications");
        if (existing != null)
        {
            _container = (RectTransform)existing.transform;
            Transform info = _container.Find("Notice_Reminder");
            Transform warning = _container.Find("Notice_Warning");
            if (info != null)
            {
                _infoTemplate = info.gameObject;
                _infoTemplate.SetActive(false);
            }
            if (warning != null)
            {
                _warningTemplate = warning.gameObject;
                _warningTemplate.SetActive(false);
            }
            return true;
        }

        Canvas canvas = RuntimeUIKit.FindHudCanvas();
        if (canvas == null)
            return false;

        GameObject go = new GameObject("Notifications", typeof(RectTransform));
        _container = (RectTransform)go.transform;
        _container.SetParent(canvas.transform, false);
        _container.anchorMin = _container.anchorMax = new Vector2(0f, 1f);
        _container.pivot = new Vector2(0f, 1f);
        _container.anchoredPosition = new Vector2(52f, -384f);
        _container.sizeDelta = new Vector2(MaxWidth, 400f);
        return true;
    }

    /// <summary>Gọi khi scene load để ẩn 2 mẫu thông báo tĩnh ngay từ đầu.</summary>
    private void Start()
    {
        EnsureContainer();
        GlobalSystemManager global = ResolveGlobal();
        _lastInfected = global != null ? global.GetGlobalDataRef().totalInfected : 0;
        _lastWeather = global != null ? global.GetGlobalDataRef().currentWeather : WeatherEvent.Clear;
        ShowStartObjective();
    }

    // ==========================================
    // TIỆN ÍCH
    // ==========================================

    private string GetBuildingName(BuildingType type)
    {
        BuildingConfig config = _placement != null && _placement.database != null ? _placement.database.GetConfig(type) : null;
        return config != null && !string.IsNullOrEmpty(config.displayName)
            ? config.displayName
            : ResearchCatalog.GetBuildingName(type);
    }

    private static string GetResearchName(ResearchID id)
    {
        ResearchNode node = ResearchCatalog.Get(id);
        return node != null ? node.displayName : id.ToString();
    }

    private static string DescribeMissing(int wood, int gold)
    {
        if (wood > 0 && gold > 0)
            return $"{wood} gỗ, {gold} vàng";
        if (wood > 0)
            return $"{wood} gỗ";
        if (gold > 0)
            return $"{gold} vàng";
        return "tài nguyên";
    }

    private GlobalSystemManager ResolveGlobal()
    {
        if (_global == null)
            _global = HUDLocator.ResolveGlobalSystem(null);
        return _global;
    }

    private EdictManager ResolveEdicts()
    {
        if (_edicts == null)
            _edicts = HUDLocator.ResolveEdictManager(null);
        return _edicts;
    }

    private GameOverChecker ResolveGameOver()
    {
        if (_gameOver == null)
            _gameOver = FindFirstObjectByType<GameOverChecker>();
        return _gameOver;
    }
}
