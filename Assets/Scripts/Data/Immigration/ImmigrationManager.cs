using UnityEngine;

public class ImmigrationManager : MonoBehaviour
{
    public const int MAX_WAITING = ImmigrationRules.MaxWaiting;

    [Header("Hang cho cong thanh")]
    public ResidentData[] waitingApplicants = new ResidentData[MAX_WAITING];
    public int waitingCount = 0;

    [SerializeField] private ResidentManager residentManager;
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private GlobalSystemManager globalSystemManager;
    [SerializeField] private EdictManager edictManager;
    [SerializeField] private ImmigrationPopupUI popupUi;
    public ImmigrationGate gate;
    private bool _suppressArrivals;
    private int _lastArrivalYear = -1;
    private byte _lastArrivalMonth;

    public int InspectIndex { get; private set; }

    public bool HasApplicants => waitingCount > 0;

    public void Init()
    {
        ResolveDependencies();
        if (popupUi == null)
            popupUi = GetComponent<ImmigrationPopupUI>() ?? gameObject.AddComponent<ImmigrationPopupUI>();
        popupUi.Bind(this);
        EnsureGate();
        gate.Bind(this);
        if (waitingCount == 0)
            EnqueueMonthlyArrivals(SeasonType.Spring);
        else
            gate.SetWaitingCount(waitingCount);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnMonthChanged(object param)
    {
        if (_suppressArrivals)
            return;

        MonthChangedPayload payload = (MonthChangedPayload)param;
        if (!payload.isSimulationTick)
            return;
        if (payload.year == _lastArrivalYear && payload.month == _lastArrivalMonth)
            return;

        _lastArrivalYear = payload.year;
        _lastArrivalMonth = payload.month;
        ProcessQuarantineUpkeep();
        ProcessWaitingYard();
        EnqueueMonthlyArrivals(payload.season);
    }

    /// <summary>
    /// Áp lực hàng chờ mỗi tháng (GDD 3.1.7): ăn lương thực của thành, ai không được ăn thì đói;
    /// bệnh lây trong hàng; hàng đông gây bất ổn; đói lâu thì bỏ đi (mất uy tín).
    /// </summary>
    private void ProcessWaitingYard()
    {
        if (waitingCount <= 0)
            return;

        var report = new WaitingYardReportPayload();

        // 1. Ăn uống: phục vụ theo thứ tự hàng, hết lương thực thì người phía sau đói
        int stockFood = globalSystemManager != null ? globalSystemManager.GetGlobalDataRef().stockFood : 0;
        int canFeed = ImmigrationRules.YardFoodPerPerson > 0 ? stockFood / ImmigrationRules.YardFoodPerPerson : waitingCount;
        int fed = Mathf.Min(waitingCount, canFeed);
        report.foodEaten = fed * ImmigrationRules.YardFoodPerPerson;
        if (report.foodEaten > 0 && globalSystemManager != null)
            globalSystemManager.ConsumeAvailable(ResourceType.Food, report.foodEaten);

        // 2. Đếm người mang bệnh trước khi lây (tránh lây dây chuyền trong cùng tháng)
        int carriers = 0;
        DiseaseType yardDisease = DiseaseType.None;
        for (int i = 0; i < waitingCount; i++)
        {
            ref ResidentData a = ref waitingApplicants[i];
            if (a.healthStatus == HealthStatus.ActiveInfected || a.healthStatus == HealthStatus.Incubating)
            {
                carriers++;
                if (yardDisease == DiseaseType.None)
                    yardDisease = a.diseaseType;
            }
        }
        float infectChance = ImmigrationRules.GetYardInfectChance(carriers, waitingCount);

        for (int i = 0; i < waitingCount; i++)
        {
            ref ResidentData a = ref waitingApplicants[i];

            if (i < fed)
            {
                a.hungerMonths = 0;
            }
            else
            {
                if (a.hungerMonths < byte.MaxValue)
                    a.hungerMonths++;
                a.happiness = Mathf.Max(0f, a.happiness - ImmigrationRules.YardHungerHappinessPenalty);
                report.hungry++;
            }

            switch (a.healthStatus)
            {
                case HealthStatus.Healthy:
                    if (infectChance > 0f && Random.value < infectChance)
                    {
                        ResidentDiseaseRules.Infect(ref a,
                            yardDisease != DiseaseType.None ? yardDisease : ResidentDiseaseRules.RollRandomDisease());
                        report.newlyInfected++;
                    }
                    break;

                case HealthStatus.Incubating:
                    // Chờ lâu thì bệnh lộ ra -> thanh tra dễ phát hiện hơn, nhưng đã kịp lây
                    if (a.IncubationMonths < byte.MaxValue)
                        a.IncubationMonths++;
                    if (a.IncubationMonths >= ResidentDiseaseRules.GetIncubationLength(a.diseaseType))
                    {
                        ResidentDiseaseRules.ActivateDisease(ref a);
                        report.newlySymptomatic++;
                    }
                    break;

                case HealthStatus.ActiveInfected:
                    // Phát bệnh trong hàng chờ: không có thầy thuốc -> có thể chết (trước đây bệnh mãi không đổi)
                    float deathChance = ResidentDiseaseRules.GetDeathChance(a.diseaseType) * ImmigrationRules.YardSickDeathMultiplier;
                    if (a.hungerMonths > 0)
                        deathChance += ImmigrationRules.YardSickHungryDeathBonus;
                    if (Random.value < deathChance)
                        a.isAlive = false;
                    break;
            }
        }

        // 3. Bất ổn do hàng đông + người đói
        report.riotAdded = ImmigrationRules.GetYardRiot(waitingCount, report.hungry);
        if (report.riotAdded > 0f && globalSystemManager != null)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            global.riotRiskMeter = Mathf.Min(100f, global.riotRiskMeter + report.riotAdded);
        }

        // 4. Chết bệnh -> thi thể ở Cổng (vào chuỗi khủng hoảng); đói quá lâu -> bỏ đi, tiếng xấu lan ra ngoài
        for (int i = waitingCount - 1; i >= 0; i--)
        {
            if (!waitingApplicants[i].isAlive)
            {
                RemoveAt(i);
                report.died++;
                continue;
            }

            if (waitingApplicants[i].hungerMonths < ImmigrationRules.YardLeaveHungerMonths)
                continue;

            RemoveAt(i);
            report.left++;
        }
        CrisisManager.AddCorpses(report.died);
        if (report.left > 0 && globalSystemManager != null)
            globalSystemManager.ModifyReputation(-report.left * ImmigrationRules.YardLeaveReputationPenalty);

        report.waiting = waitingCount;
        if (report.left > 0 || report.died > 0)
            NotifyQueueChanged();
        GameEvents.Post(EventID.WaitingYardReport, report);
    }

    /// <summary>
    /// Xoá người ở vị trí index, dồn những người phía sau lên (giữ đúng thứ tự hàng).
    /// InspectIndex vẫn trỏ đúng người đang xét nếu người bị xoá đứng trước họ.
    /// </summary>
    private void RemoveAt(int index)
    {
        if ((uint)index >= (uint)waitingCount)
            return;

        int tail = waitingCount - index - 1;
        if (tail > 0)
            System.Array.Copy(waitingApplicants, index + 1, waitingApplicants, index, tail);
        waitingCount--;
        waitingApplicants[waitingCount] = default;

        if (index < InspectIndex)
            InspectIndex--;
        InspectIndex = waitingCount > 0 ? Mathf.Clamp(InspectIndex, 0, waitingCount - 1) : 0;
    }

    private void ResolveDependencies()
    {
        if (GamePlayController.Instance == null || GamePlayController.Instance.playerContain == null)
            return;

        PlayerContain contain = GamePlayController.Instance.playerContain;
        if (residentManager == null)
            residentManager = contain.residentManager;
        if (buildingManager == null)
            buildingManager = contain.buildingManager;
        if (globalSystemManager == null)
            globalSystemManager = contain.globalSystemManager;
        if (edictManager == null)
            edictManager = contain.edictManager;
        if (contain.immigrationManager == null)
            contain.immigrationManager = this;
    }

    private void EnsureGate()
    {
        if (gate != null)
            return;

        gate = FindObjectOfType<ImmigrationGate>();
        if (gate != null)
            return;

        // Cổng vòm tạm (2 cột + xà ngang + mái) — chưa có art. Vị trí do ImmigrationGate tự đặt
        // theo GridController.GateCell (mép bản đồ, đầu lối mòn) khi bản đồ sinh / tải xong.
        GameObject go = new GameObject("CityGate");
        go.transform.SetParent(transform, false);
        Sprite block = CreateGateSprite();
        Color wood = new Color(0.45f, 0.3f, 0.18f);
        Color roof = new Color(0.62f, 0.22f, 0.16f);
        AddGatePart(go.transform, "PostLeft", block, wood, new Vector2(-0.42f, 0.55f), new Vector2(0.14f, 1.1f), 0);
        AddGatePart(go.transform, "PostRight", block, wood, new Vector2(0.42f, 0.55f), new Vector2(0.14f, 1.1f), 0);
        AddGatePart(go.transform, "Beam", block, wood, new Vector2(0f, 1.08f), new Vector2(1.06f, 0.14f), 1);
        AddGatePart(go.transform, "Roof", block, roof, new Vector2(0f, 1.24f), new Vector2(1.2f, 0.18f), 2);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.offset = new Vector2(0f, 0.65f);
        col.size = new Vector2(1.1f, 1.4f);
        gate = go.AddComponent<ImmigrationGate>();
    }

    private static void AddGatePart(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, int orderOffset)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = orderOffset;   // offset; ImmigrationGate cộng thêm thứ tự theo độ sâu ô
    }

    private static Sprite CreateGateSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        // FullRect để dùng được drawMode Sliced (kéo giãn theo size mà không scale transform)
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 8f, 0, SpriteMeshType.FullRect);
    }

    public void EnqueueMonthlyArrivals(SeasonType season)
    {
        float reputation = 0f;
        if (globalSystemManager != null)
            reputation = globalSystemManager.GetGlobalDataRef().reputation;
        int arrivals = ImmigrationRules.RollArrivalCount(season, reputation);
        int added = 0;
        int nextId = NextApplicantId();
        for (int i = 0; i < arrivals && waitingCount < MAX_WAITING; i++)
        {
            waitingApplicants[waitingCount] = ImmigrationRules.CreateApplicant(nextId++);
            waitingCount++;
            added++;
        }

        if (added <= 0)
            return;

        InspectIndex = Mathf.Clamp(InspectIndex, 0, waitingCount - 1);
        NotifyQueueChanged();
    }

    public void SetSuppressArrivals(bool suppress)
    {
        _suppressArrivals = suppress;
    }

    public void RefreshAfterLoad()
    {
        _interrogated.Clear();
        if (globalSystemManager != null)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            _lastArrivalYear = global.currentYear;
            _lastArrivalMonth = global.currentMonth;
        }

        InspectIndex = 0;
        if (gate == null)
            EnsureGate();
        NotifyQueueChanged();
    }

    public void OpenInspection()
    {
        if (waitingCount <= 0)
        {
            if (popupUi != null)
                popupUi.ShowEmpty();
            return;
        }

        InspectIndex = Mathf.Clamp(InspectIndex, 0, waitingCount - 1);
        ShowCurrent();
    }

    public bool TryGetCurrent(out ResidentData applicant)
    {
        if (waitingCount <= 0 || (uint)InspectIndex >= (uint)waitingCount)
        {
            applicant = default;
            return false;
        }

        applicant = waitingApplicants[InspectIndex];
        return true;
    }

    public RuleAction GetSuggestedAction(in ResidentData applicant)
    {
        if (edictManager == null || globalSystemManager == null)
            return RuleAction.Admit;

        if (EdictRules.TryGetSuggestedAction(
                edictManager.allEdicts,
                edictManager.activeCount,
                in applicant,
                in globalSystemManager.GetGlobalDataRef(),
                out RuleAction action,
                out _))
            return action;

        return RuleAction.Admit;
    }

    public void AcceptCurrent()
    {
        if (!TryGetCurrent(out ResidentData applicant))
            return;

        CollectEntryTax(ref applicant, out int tax);
        if (!Admit(in applicant, false))
            return;

        if (tax > 0 && globalSystemManager != null)
            globalSystemManager.ModifyGold(tax);

        RemoveCurrent();
    }

    public void DenyCurrent()
    {
        if (!TryGetCurrent(out ResidentData applicant))
            return;

        if (globalSystemManager != null)
            globalSystemManager.ModifyReputation(ImmigrationRules.GetDenyReputationChange(
                in applicant, globalSystemManager.GetGlobalDataRef().CurrentSeason));

        RemoveCurrent();
    }

    public void QuarantineCurrent()
    {
        if (!TryGetCurrent(out ResidentData applicant))
            return;

        // Không còn chỗ trong Trạm cách ly -> không cho "cách ly" (trước đây người đó lọt vào thành như dân thường)
        if (FindFreeQuarantineWard() < 0)
        {
            ConfirmDialog.Alert("Không còn chỗ trong Trạm cách ly.\nHãy xây thêm Trạm cách ly, hoặc chọn Cho vào / Từ chối.");
            return;
        }

        CollectEntryTax(ref applicant, out int tax);
        applicant.quarantineMonths = ResolveQuarantineMonths(in applicant);
        if (!Admit(in applicant, true))
            return;

        if (tax > 0 && globalSystemManager != null)
            globalSystemManager.ModifyGold(tax);
        RemoveCurrent();
    }

    /// <summary>Thời hạn cách ly: lấy từ sắc lệnh Cách ly đang khớp (nếu có), không thì mặc định 3 tháng.</summary>
    private byte ResolveQuarantineMonths(in ResidentData applicant)
    {
        if (edictManager != null && globalSystemManager != null
            && EdictRules.TryResolveImmigration(
                edictManager.allEdicts,
                edictManager.activeCount,
                in applicant,
                in globalSystemManager.GetGlobalDataRef(),
                out RuleActionData action,
                out _,
                out _)
            && action.type == RuleActionType.ImmigrationQuarantine
            && action.param0 >= 1f)
            return (byte)Mathf.Clamp(Mathf.RoundToInt(action.param0), 1, 24);

        return ImmigrationRules.DefaultQuarantineMonths;
    }

    /// <summary>Có Trạm cách ly nào còn chỗ không (để nút Cách ly / sắc lệnh biết trước).</summary>
    public bool HasQuarantineSpace => FindFreeQuarantineWard() >= 0;

    private int FindFreeQuarantineWard()
    {
        if (buildingManager == null)
            ResolveDependencies();
        if (buildingManager == null)
            return -1;

        for (int i = 0; i < buildingManager.activeCount; i++)
        {
            ref BuildingData building = ref buildingManager.GetBuildingRef(i);
            if (building.buildingType == BuildingType.QuarantineWard
                && ResidentAssignmentRules.HasHousingSlot(in building))
                return i;
        }

        return -1;
    }

    private bool Admit(in ResidentData applicant, bool quarantine)
    {
        if (residentManager == null)
            ResolveDependencies();
        if (residentManager == null)
            return false;

        ResidentData admitted = applicant;
        // Cấp số hiệu mới lớn hơn mọi cư dân + người chờ: trẻ sinh ra có thể đã lấy trùng số của người đang chờ
        admitted.residentID = NextApplicantId();
        int index = residentManager.AddResident(in admitted);
        if (index < 0)
            return false;

        if (quarantine)
            TryAssignQuarantine(index);

        if (globalSystemManager != null)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            global.totalPopulation++;
        }

        residentManager.NotifyPopulationChanged();
        return true;
    }

    private void CollectEntryTax(ref ResidentData applicant, out int tax)
    {
        tax = ImmigrationRules.GetEntryTax(in applicant, ResolveTariffPercent(in applicant));
        if (tax > 0)
            ImmigrationRules.PayTax(ref applicant, tax);
    }

    private float ResolveTariffPercent(in ResidentData applicant)
    {
        if (edictManager == null || globalSystemManager == null)
            return ImmigrationRules.DefaultTariffPercent;

        if (!EdictRules.TryResolveImmigration(
                edictManager.allEdicts,
                edictManager.activeCount,
                in applicant,
                in globalSystemManager.GetGlobalDataRef(),
                out RuleActionData action,
                out _,
                out _))
            return ImmigrationRules.DefaultTariffPercent;

        float tariff = EdictRules.GetTariffPercent(in action);
        return tariff > 0f ? tariff : ImmigrationRules.DefaultTariffPercent;
    }

    /// <summary>
    /// Chi phí + đếm ngược cách ly mỗi tháng. Lương thực của người cách ly đã nằm trong tiêu thụ chung
    /// của dân số (GlobalSystemManager) nên ở đây chỉ trừ thuốc, tránh ăn hai lần.
    /// Hết hạn: người khoẻ được ra nhập thành (ResidentAssignmentSystem chuyển nhà), người bệnh ở lại cách ly.
    /// </summary>
    private void ProcessQuarantineUpkeep()
    {
        if (residentManager == null || buildingManager == null)
            ResolveDependencies();
        if (residentManager == null || buildingManager == null || globalSystemManager == null)
            return;

        int quarantined = 0;
        for (int i = 0; i < residentManager.activeCount; i++)
        {
            ref ResidentData r = ref residentManager.GetResidentRef(i);
            if (!r.isAlive)
                continue;
            if (r.quarantineMonths > 0)
                r.quarantineMonths--;
            if (ResidentAssignmentRules.IsHousedInQuarantine(
                    in r, buildingManager.allBuildings, buildingManager.activeCount))
                quarantined++;
        }

        if (quarantined > 0)
        {
            float scale = ResidentProfessionEffectSystem.GetSampleScale(
                residentManager.allResidents, residentManager.activeCount, globalSystemManager);
            globalSystemManager.ConsumeAvailable(
                ResourceType.Medicine, Mathf.RoundToInt(quarantined * scale * ImmigrationRules.QuarantineMedicinePerMonth));
        }
    }

    private void TryAssignQuarantine(int residentIndex)
    {
        if (buildingManager == null)
            return;

        ref ResidentData resident = ref residentManager.GetResidentRef(residentIndex);
        int best = FindFreeQuarantineWard();
        if (best < 0)
            return;

        ref BuildingData ward = ref buildingManager.GetBuildingRef(best);
        ResidentAssignmentRules.AssignHousing(ref resident, ward.buildingID);
        ward.currentOccupancy++;
    }

    private void RemoveCurrent()
    {
        if (waitingCount <= 0)
            return;

        _interrogated.Remove(waitingApplicants[InspectIndex].residentID);

        // Dồn hàng: người kế tiếp lên đúng vị trí vừa xét (trước đây lấy người cuối hàng chen lên)
        RemoveAt(InspectIndex);

        NotifyQueueChanged();
        if (waitingCount > 0)
            ShowCurrent();
        else if (popupUi != null)
            popupUi.ShowEmpty();
    }

    /// <summary>Vẽ lại người đang xét trên popup Cổng (khi hàng chờ đổi lúc popup đang mở).</summary>
    public void RefreshCurrent()
    {
        if (waitingCount <= 0)
            return;
        InspectIndex = Mathf.Clamp(InspectIndex, 0, waitingCount - 1);
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (popupUi == null || !TryGetCurrent(out ResidentData applicant))
            return;

        RuleAction suggested = ExplainSuggestion(in applicant, out string reason);
        if (suggested == RuleAction.EscortToDesk)
            suggested = Interrogate(in applicant, ref reason);
        ResearchManager research = GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
            ? GamePlayController.Instance.playerContain.researchManager
            : null;
        popupUi.ShowApplicant(in applicant, suggested, waitingCount, reason, research != null && research.HasThermometer);
    }

    // Người đã bị áp giải thẩm vấn (theo residentID) — chỉ trả quyền lực 1 lần / người. Không lưu save.
    private readonly System.Collections.Generic.HashSet<int> _interrogated = new System.Collections.Generic.HashSet<int>();

    /// <summary>
    /// Sắc lệnh "Áp giải tới bàn thẩm vấn": tốn điểm Quyền lực để tra hỏi kỹ, lộ tình trạng bệnh thật
    /// (kể cả đang ủ bệnh, chưa có triệu chứng). Đổi gợi ý theo kết quả: mang bệnh -> Cách ly, khoẻ -> Cho vào.
    /// </summary>
    private RuleAction Interrogate(in ResidentData applicant, ref string reason)
    {
        if (globalSystemManager == null)
            return RuleAction.EscortToDesk;

        ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
        if (!_interrogated.Contains(applicant.residentID))
        {
            int cost = ResolveEscortCost(in applicant);
            if (global.authorityPoints < cost)
            {
                reason += $"\nKhông đủ quyền lực để áp giải (cần {cost}, còn {global.authorityPoints}) — hãy tự kiểm tra.";
                return RuleAction.EscortToDesk;
            }

            global.authorityPoints = (ushort)(global.authorityPoints - cost);
            _interrogated.Add(applicant.residentID);
            GameEvents.Post(EventID.ResourcesChanged);
        }

        bool carrier = applicant.healthStatus == HealthStatus.Incubating
            || applicant.healthStatus == HealthStatus.ActiveInfected;
        string disease = ResidentInfoPopup.GetDiseaseName(applicant.diseaseType);
        reason += applicant.healthStatus switch
        {
            HealthStatus.Incubating => $"\nThẩm vấn: đang ủ bệnh {disease}, chưa lộ triệu chứng!",
            HealthStatus.ActiveInfected => $"\nThẩm vấn: đang mắc {disease}.",
            HealthStatus.Treated => "\nThẩm vấn: đang hồi phục sau bệnh, không còn lây.",
            _ => "\nThẩm vấn: khoẻ mạnh, không mang bệnh."
        };
        reason += $" (quyền lực còn {global.authorityPoints})";
        return carrier ? RuleAction.Quarantine : RuleAction.Admit;
    }

    private int ResolveEscortCost(in ResidentData applicant)
    {
        if (edictManager != null && globalSystemManager != null
            && EdictRules.TryResolveImmigration(
                edictManager.allEdicts,
                edictManager.activeCount,
                in applicant,
                in globalSystemManager.GetGlobalDataRef(),
                out RuleActionData action,
                out _,
                out _)
            && action.type == RuleActionType.ImmigrationEscort)
            return Mathf.Max(0, Mathf.RoundToInt(action.param0));
        return 0;
    }

    /// <summary>GDD 3.5: "Why was this NPC processed?" — sắc lệnh nào khớp (ưu tiên trên → dưới) và vì sao.</summary>
    public RuleAction ExplainSuggestion(in ResidentData applicant, out string reason)
    {
        if (edictManager == null || globalSystemManager == null || edictManager.activeCount <= 0)
        {
            reason = "Chưa có sắc lệnh nhập cư — bạn tự quyết định.";
            return RuleAction.Admit;
        }

        if (!EdictRules.TryResolveImmigration(
                edictManager.allEdicts,
                edictManager.activeCount,
                in applicant,
                in globalSystemManager.GetGlobalDataRef(),
                out RuleActionData action,
                out int index,
                out bool usedElse))
        {
            reason = "Không sắc lệnh nào khớp với người này → mặc định Cho vào.";
            return RuleAction.Admit;
        }

        ref EdictRuleData rule = ref edictManager.GetEdictRef(index);
        var sb = new System.Text.StringBuilder(128);
        string name = string.IsNullOrEmpty(rule.ruleName) ? $"#{rule.ruleID}" : rule.ruleName;
        sb.Append("Sắc lệnh \"").Append(name).Append("\" (ưu tiên ").Append(rule.executionOrder + 1).Append(") ");
        sb.Append(usedElse
            ? "— không luật nào khớp, dùng nhánh Else vì không thoả: "
            : "khớp: ");
        for (int i = 0; i < rule.ConditionCount; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(EdictRuleCatalog.DescribeCondition(in rule.conditions[i], i == 0).Replace('\t', ' '));
        }
        sb.Append(" → ").Append(EdictRuleCatalog.DescribeAction(in action));
        reason = sb.ToString();
        return EdictRules.ToRuleAction(action.type);
    }

    private void NotifyQueueChanged()
    {
        if (gate != null)
            gate.SetWaitingCount(waitingCount);
        GameEvents.Post(EventID.ImmigrationQueueChanged, waitingCount);
    }

    private int NextApplicantId()
    {
        int maxId = 0;
        if (residentManager != null)
            maxId = residentManager.PeekMaxResidentId();
        for (int i = 0; i < waitingCount; i++)
        {
            if (waitingApplicants[i].residentID > maxId)
                maxId = waitingApplicants[i].residentID;
        }

        return maxId + 1;
    }
}
