using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum NoticeLevel : byte
{
    Info = 0,       // Dùng mẫu Notice_Reminder
    Warning = 1     // Dùng mẫu Notice_Warning
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
    private const float RowHeight = 56f;
    private const float RowSpacing = 10f;
    private const float MinWidth = 260f;
    private const float MaxWidth = 720f;
    private const float TextLeft = 68f;
    private const float TextRight = 56f;

    public static NotificationFeed Instance { get; private set; }

    private sealed class Notice
    {
        public RectTransform rect;
        public float expireAt;
    }

    private readonly List<Notice> _active = new List<Notice>();
    private readonly Dictionary<string, float> _recentKeys = new Dictionary<string, float>();
    private readonly int[] _deathsThisFrame = new int[4];
    private bool _hasPendingDeaths;
    private bool _pendingMonthReport;
    private int _lastInfected;

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
    public static void Show(string message, NoticeLevel level = NoticeLevel.Info, string key = null)
    {
        if (Instance != null)
            Instance.Push(message, level, key);
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
        GameEvents.Listen(EventID.ColderResolved, OnCold);
        GameEvents.Listen(EventID.ResidentsDied, OnResidentsDied);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Listen(EventID.MerchantArrived, OnMerchantArrived);
        GameEvents.Listen(EventID.MerchantLeft, OnMerchantLeft);
        GameEvents.Listen(EventID.WaitingYardReport, OnWaitingYardReport);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.BuildingPlaceFailed, OnPlaceFailed);
        GameEvents.Unlisten(EventID.BuildingCompleted, OnBuildingCompleted);
        GameEvents.Unlisten(EventID.EdictTriggered, OnEdictTriggered);
        GameEvents.Unlisten(EventID.ResearchCompleted, OnResearchCompleted);
        GameEvents.Unlisten(EventID.HungerResolved, OnHunger);
        GameEvents.Unlisten(EventID.ColderResolved, OnCold);
        GameEvents.Unlisten(EventID.ResidentsDied, OnResidentsDied);
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Unlisten(EventID.MerchantArrived, OnMerchantArrived);
        GameEvents.Unlisten(EventID.MerchantLeft, OnMerchantLeft);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnWaitingYardReport);
    }

    private void OnWaitingYardReport(object param)
    {
        if (!(param is WaitingYardReportPayload p))
            return;

        if (p.left > 0)
            Push($"{p.left} người trong hàng chờ bỏ đi vì đói (giảm uy tín)", NoticeLevel.Warning, "yard-left");
        if (p.hungry > 0)
            Push($"Không đủ lương thực cho hàng chờ: {p.hungry} người bị đói", NoticeLevel.Warning, "yard-hungry");
        if (p.newlyInfected > 0)
            Push($"Bệnh lây trong hàng chờ: thêm {p.newlyInfected} người nhiễm", NoticeLevel.Warning, "yard-infect");
        if (p.riotAdded >= 1f)
            Push($"Hàng chờ quá đông ({p.waiting}/{ImmigrationRules.MaxWaiting}): bạo loạn +{p.riotAdded:0.#}", NoticeLevel.Warning, "yard-crowd");
        else if (p.waiting > 0 && p.foodEaten > 0 && p.waiting * 2 >= ImmigrationRules.MaxWaiting)
            Push($"Hàng chờ đã ăn {p.foodEaten} lương thực tháng này", NoticeLevel.Info, "yard-food");
    }

    private void OnMerchantArrived(object param)
    {
        if (!(param is bool live) || !live)
            return;

        PlayerContain contain = FindFirstObjectByType<PlayerContain>();
        int months = contain != null && contain.merchantManager != null ? contain.merchantManager.MonthsLeft : 0;
        Push($"Thương nhân đã tới Cổng thành, ở lại {months} tháng", NoticeLevel.Info, "merchant");
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

        Push(msg, NoticeLevel.Warning, "place:" + p.type + ":" + p.reason);
    }

    private void OnBuildingCompleted(object param)
    {
        if (!(param is ushort id) || _placement == null || _placement.buildingManager == null)
            return;

        int index = _placement.buildingManager.FindBuildingIndexById(id);
        if (index < 0)
            return;

        BuildingType type = _placement.buildingManager.GetBuildingRef(index).buildingType;
        Push($"Đã xây xong {GetBuildingName(type)}", NoticeLevel.Info, "built:" + id);
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

        Push($"Sắc lệnh \"{name}\" đã được thi hành", NoticeLevel.Info, "edict:" + ruleId);
    }

    private void OnResearchCompleted(object param)
    {
        if (!(param is ResearchID id))
            return;

        Push($"Nghiên cứu hoàn thành: {GetResearchName(id)}", NoticeLevel.Info, "research:" + id);
    }

    private void OnHunger(object param)
    {
        if (!(param is float rate) || rate <= 0f)
            return;

        Push($"Thiếu lương thực: {Mathf.RoundToInt(rate * 100f)}% dân bị đói", NoticeLevel.Warning, "hunger");
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

        // Dịch bệnh
        float infectedPct = pop > 0 ? d.totalInfected * 100f / pop : 0f;
        if (infectedPct >= 10f)
            Push($"Dịch bệnh bùng phát: {Mathf.RoundToInt(infectedPct)}% dân đang nhiễm bệnh", NoticeLevel.Warning, "disease");
        else if (d.totalInfected > _lastInfected)
            Push($"Có thêm ca bệnh mới ({d.totalInfected} người đang bệnh)", NoticeLevel.Info, "disease");
        _lastInfected = d.totalInfected;

        // Lương thực: không đủ cho tháng tới
        SeasonType nextSeason = d.currentMonth == 12 ? SeasonType.Spring : (SeasonType)((d.currentMonth) / 3);
        int foodNeed = pop * (nextSeason == SeasonType.Winter ? 2 : 1);
        if (pop > 0 && d.stockFood < foodNeed)
            Push($"Lương thực sắp cạn: còn {d.stockFood}, tháng tới cần {foodNeed}", NoticeLevel.Info, "food-low");

        // Than: mùa đông đang tới / đang là mùa đông
        if (pop > 0 && nextSeason == SeasonType.Winter && d.stockCoal < pop)
            Push($"Than sắp hết: còn {d.stockCoal}, mùa đông cần {pop}/tháng", NoticeLevel.Info, "coal-low");

        // Thuốc khi có bệnh
        if (d.totalInfected > 0 && d.stockMedicine <= 0)
            Push("Hết thuốc men: người bệnh không được chữa trị", NoticeLevel.Warning, "medicine");

        ReportObjective(in d);

        // Bạo loạn
        GameOverChecker checker = ResolveGameOver();
        if (checker != null && checker.RiotMonths > 0)
        {
            int left = checker.RiotMonthsToLose - checker.RiotMonths;
            Push(left > 0
                    ? $"Bạo loạn đã bùng nổ! Còn {left} tháng để dẹp yên trước khi bị lật đổ"
                    : "Bạo loạn đã bùng nổ!", NoticeLevel.Warning, "riot");
        }
        else if (d.riotRiskMeter >= 75f)
        {
            Push($"Dân chúng bất mãn: nguy cơ bạo loạn {Mathf.RoundToInt(d.riotRiskMeter)}%", NoticeLevel.Warning, "riot");
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

    private void Push(string message, NoticeLevel level, string key)
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

        if (!EnsureContainer())
            return;

        RectTransform rect = CreateNoticeView(level, message);
        if (rect == null)
            return;

        var notice = new Notice { rect = rect, expireAt = now + DefaultDuration };
        Button close = rect.GetComponentInChildren<Button>(true);
        if (close != null)
        {
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(() => Remove(notice));
        }

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
        GameObject template = level == NoticeLevel.Warning ? _warningTemplate : _infoTemplate;
        GameObject go = template != null ? Instantiate(template, _container) : CreateFallbackNotice(level);
        go.name = "Notice";
        go.SetActive(true);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);

        TMP_Text text = go.transform.Find("Txt_Message")?.GetComponent<TMP_Text>();
        if (text == null)
            text = go.GetComponentInChildren<TMP_Text>(true);
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
        tr.anchoredPosition = new Vector2(TextLeft - 40f, 0f);
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
