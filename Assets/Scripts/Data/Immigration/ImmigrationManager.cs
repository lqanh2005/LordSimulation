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
        // GDD v0.3 mục 4.1: người di cư đến theo đợt (Xuân tháng 2, Hạ tháng 5, Thu tháng 8), không có sẵn từ đầu
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
        ProcessWaitingYard(payload.season);
        EnqueueWaves(payload.year, payload.month);
        ProcessGateClerk();
        if (waitingCount > 0)
            InspectIndex = FamilyStart(Mathf.Clamp(InspectIndex, 0, waitingCount - 1));
    }

    /// <summary>
    /// Hàng chờ ngoài tường mỗi tháng (GDD v0.3 mục 4.5). Thành không nuôi người chờ:
    /// - Chờ từ 1 tháng: mỗi tháng 10% chết đói (mùa đông ×2). Mỗi người chết: Reputation −2, để lại thi thể sát tường.
    /// - Chờ từ 2 tháng: một phần bỏ đi (Reputation −1 mỗi người).
    /// - Sốt Đỏ lây trong hàng: β 10% mỗi người bệnh × mức lây (ủ bệnh 0,3; phát bệnh 1,0).
    /// </summary>
    private void ProcessWaitingYard(SeasonType season)
    {
        if (waitingCount <= 0)
            return;

        var report = new WaitingYardReportPayload();

        // 1. Mức lây trong hàng (trạng thái đầu tháng)
        float survive = 1f;
        for (int i = 0; i < waitingCount; i++)
            survive *= 1f - ImmigrationRules.YardBeta * ResidentDiseaseSystem.GetInfectiousness(in waitingApplicants[i]);
        float infectChance = Mathf.Clamp01(1f - survive);

        for (int i = 0; i < waitingCount; i++)
        {
            ref ResidentData a = ref waitingApplicants[i];

            // Lệnh khẩn cấp "Phát chẩn" (GDD 9.4): tháng này người chờ không đói
            bool relief = EmergencyOrders.YardReliefActive;
            int waited = relief ? 0 : a.hungerMonths;
            if (!relief && a.hungerMonths < byte.MaxValue)
                a.hungerMonths++;
            if (!relief && a.hungerMonths >= 1)
                report.hungry++;

            // Chết đói / chết cóng
            float starve = ImmigrationRules.GetYardStarveChance(waited, season);
            if (starve > 0f && Random.value < starve)
            {
                a.isAlive = false;
                continue;
            }

            switch (a.healthStatus)
            {
                case HealthStatus.Healthy:
                    float chance = Mathf.Clamp01(infectChance * ResidentDiseaseSystem.GetPersonalMultiplier(in a));
                    if (chance > 0f && Random.value < chance)
                    {
                        ResidentDiseaseRules.Infect(ref a, DiseaseType.RedFever);
                        report.newlyInfected++;
                    }
                    break;

                case HealthStatus.Incubating:
                    if (a.IncubationMonths < byte.MaxValue)
                        a.IncubationMonths++;
                    if (a.IncubationMonths >= ResidentDiseaseRules.GetIncubationLength(a.diseaseType))
                    {
                        ResidentDiseaseRules.ActivateDisease(ref a);
                        report.newlySymptomatic++;
                    }
                    break;

                case HealthStatus.ActiveInfected:
                    // Không ai chữa: 15% chết / 25% hồi phục mỗi tháng (nhân hệ số đói, tuổi, mùa đông)
                    float death = ResidentDiseaseSystem.UntreatedDeath * ResidentDiseaseSystem.HungryDeathMultiplier;
                    if (season == SeasonType.Winter) death *= ResidentDiseaseSystem.ColdDeathMultiplier;
                    AgeGroupType age = a.GetAgeGroup();
                    if (age == AgeGroupType.Elderly) death *= ResidentDiseaseSystem.ElderlyDeathMultiplier;
                    else if (age == AgeGroupType.Child) death *= ResidentDiseaseSystem.ChildDeathMultiplier;
                    float roll = Random.value;
                    if (roll < death)
                        a.isAlive = false;
                    else if (roll < death + ResidentDiseaseSystem.UntreatedRecover)
                        ResidentDiseaseRules.BeginTreatment(ref a);
                    break;

                case HealthStatus.Treated:
                    if (a.recoveryMonths < byte.MaxValue)
                        a.recoveryMonths++;
                    if (a.recoveryMonths >= ResidentDiseaseRules.GetRecoveryLength(a.diseaseType))
                    {
                        ResidentDiseaseRules.ClearDisease(ref a);
                        a.immunityMonths = ResidentDiseaseSystem.ImmunityMonths;
                    }
                    break;
            }
        }

        // 2. Người chết -> thi thể sát tường (vào chuỗi Miasma); chờ quá lâu -> một phần bỏ đi
        for (int i = waitingCount - 1; i >= 0; i--)
        {
            if (!waitingApplicants[i].isAlive)
            {
                RemoveAt(i);
                report.died++;
                continue;
            }

            if (waitingApplicants[i].hungerMonths >= ImmigrationRules.YardLeaveAfterMonths
                && Random.value < ImmigrationRules.YardLeaveChance)
            {
                RemoveAt(i);
                report.left++;
            }
        }
        CrisisManager.AddCorpses(report.died);
        if (globalSystemManager != null && (report.left > 0 || report.died > 0))
            globalSystemManager.ModifyReputation(
                report.died * StabilityRules.YardDeathReputation + report.left * StabilityRules.YardLeaveReputation);

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

    /// <summary>
    /// GDD v0.3 mục 4.1 + 9.6: đợt thường (tháng 2 / 5 / 8) và đợt tị nạn khi có thiên tai (bão tuyết).
    /// Số người = gốc × 1,3^(năm−1) × (0,6 + 0,8 × Rep/100) × (1,5 nếu thiên tai).
    /// </summary>
    private readonly System.Collections.Generic.List<ResidentData> _familyBuffer = new System.Collections.Generic.List<ResidentData>(16);

    public void EnqueueWaves(int year, byte month)
    {
        if (globalSystemManager == null)
            ResolveDependencies();
        float reputation = StabilityRules.ReputationStart;
        EntryTaxLevel tax = StabilityRules.DefaultEntryTax;
        bool disaster = false;
        if (globalSystemManager != null)
        {
            ref GlobalSystemData g = ref globalSystemManager.GetGlobalDataRef();
            reputation = g.reputation;
            tax = g.entryTaxLevel;
            disaster = g.disasterWaveBoost;
            g.disasterWaveBoost = false;
        }

        int added = 0;
        Vector2Int regular = StabilityRules.GetBaseWave(month);
        if (regular.y > 0)
        {
            // Hậu quả trễ của Lệnh khẩn cấp: Đóng cổng -> đợt sau nhỏ đi 30%; Phát chẩn -> đông hơn 30%, nghèo hơn
            float mult = EmergencyOrders.ConsumeWaveMultiplier(out bool poorer);
            int size = Mathf.RoundToInt(StabilityRules.RollWaveSize(regular, year, reputation, false) * mult);
            added += EnqueueApplicants(size, poorer ? EntryTaxLevel.Harsh : tax, false);   // ít người giàu như khi thuế hà khắc
        }
        if (disaster)
            added += EnqueueApplicants(StabilityRules.RollWaveSize(StabilityRules.RefugeeWave, year, reputation, true), tax, true);

        if (added > 0)
            NotificationFeed.Show(disaster
                    ? $"Đợt tị nạn tới Cổng: {added} người đang chờ ngoài tường"
                    : $"Đợt di cư tới Cổng: {added} người đang chờ ngoài tường",
                NoticeLevel.Info, "wave", UIActions.OpenGate);
    }

    /// <summary>Thêm N người vào hàng chờ (test / đợt di cư). Trả số người thực sự được thêm.</summary>
    public int EnqueueApplicants(int count, EntryTaxLevel tax, bool refugee)
    {
        // GDD v0.3 mục 4.1: người di cư đến theo gia đình 1–5 người (trung bình ~2,5); mỗi gia đình là một lượt duyệt
        int added = 0;
        int nextId = NextApplicantId();
        _familyBuffer.Clear();
        while (added + _familyBuffer.Count < count)
        {
            int size = Mathf.Min(FamilyRules.RollFamilySize(), count - added - _familyBuffer.Count);
            FamilyRules.CreateFamily(_familyBuffer, ref nextId, size, tax, refugee);
        }
        // GDD 6.6: đang có tin đồn thị trường -> một số gia đình kể lại ở Cổng (note "Tin đồn thị trường")
        bool rumor = globalSystemManager != null && globalSystemManager.GetGlobalDataRef().rumorInMonths > 0;
        for (int i = 0; i < _familyBuffer.Count && rumor; i++)
        {
            ResidentData m = _familyBuffer[i];
            bool head = i == 0 || _familyBuffer[i - 1].familyID != m.familyID || m.familyID == 0;
            if (head && Random.value < MarketSystem.RumorNoteChance)
            {
                m.rolledNotes |= (byte)NoteFlags.MarketRumor;
                _familyBuffer[i] = m;
            }
        }
        for (int i = 0; i < _familyBuffer.Count && waitingCount < MAX_WAITING; i++)
        {
            waitingApplicants[waitingCount] = _familyBuffer[i];
            waitingCount++;
            added++;
        }
        // Mảng đầy giữa chừng: cắt mất phần cuối của một gia đình thì vẫn coi phần còn lại là một nhà

        if (added <= 0)
            return 0;

        InspectIndex = FamilyStart(Mathf.Clamp(InspectIndex, 0, waitingCount - 1));
        NotifyQueueChanged();
        return added;
    }

    public void SetSuppressArrivals(bool suppress)
    {
        _suppressArrivals = suppress;
    }

    public void RefreshAfterLoad()
    {
        int maxFamily = 0;
        for (int i = 0; i < waitingCount; i++)
            maxFamily = Mathf.Max(maxFamily, waitingApplicants[i].familyID);
        if (residentManager != null)
            for (int i = 0; i < residentManager.activeCount; i++)
                maxFamily = Mathf.Max(maxFamily, residentManager.allResidents[i].familyID);
        FamilyRules.EnsureFamilyIdAbove(maxFamily);
        RecentAutoDecisions.Clear();
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

        InspectIndex = FamilyStart(Mathf.Clamp(InspectIndex, 0, waitingCount - 1));
        ShowCurrent();
    }

    /// <summary>Chủ hộ của gia đình đang được xét.</summary>
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

    /// <summary>Gia đình đang được xét (đoạn liên tiếp trong hàng chờ).</summary>
    public bool TryGetCurrentFamily(out ApplicantFamily family)
    {
        family = default;
        if (waitingCount <= 0 || (uint)InspectIndex >= (uint)waitingCount)
            return false;
        family = GetFamilyAt(InspectIndex);
        return family.IsValid;
    }

    public ApplicantFamily GetFamilyAt(int index)
    {
        FamilyRules.GetFamilyRange(waitingApplicants, waitingCount, index, out int start, out int length);
        return new ApplicantFamily(waitingApplicants, start, length, FamilyRules.CurrentProfileLevel);
    }

    private int FamilyStart(int index)
    {
        FamilyRules.GetFamilyRange(waitingApplicants, waitingCount, index, out int start, out _);
        return start;
    }

    public void AcceptCurrent() => DecideCurrent(RuleAction.Admit, 0);
    public void DenyCurrent() => DecideCurrent(RuleAction.Deny, 0);
    public void QuarantineCurrent() => DecideCurrent(RuleAction.Quarantine, 0);

    /// <summary>
    /// GDD v0.3 mục 4.3: phán quyết áp dụng cho cả gia đình. ruleOrder = 0 là duyệt tay, n là sắc lệnh ưu tiên n
    /// (ghi vào cư dân để trả lời "Why was this NPC processed?"). Trả false nếu không thực hiện được.
    /// </summary>
    private bool DecideCurrent(RuleAction decision, byte ruleOrder, RuleActionData ruleAction = default)
    {
        if (!TryGetCurrentFamily(out ApplicantFamily family))
            return false;

        switch (decision)
        {
            case RuleAction.Admit when EmergencyOrders.GateClosed:
            case RuleAction.Quarantine when EmergencyOrders.GateClosed:
                if (ruleOrder == 0)
                    ConfirmDialog.Alert("Cổng đang đóng (Lệnh khẩn cấp): hàng ngoài tường không được vào.\nChỉ có thể Từ chối.");
                return false;

            case RuleAction.Admit:
            {
                float reputation = GetAdmitReputation(in family);
                int tax = 0;
                for (int i = 0; i < family.count; i++)
                {
                    ResidentData member = family[i];
                    CollectEntryTax(ref member, out int t);
                    tax += t;
                    member.processedByRule = ruleOrder;
                    Admit(in member, false);
                }
                if (globalSystemManager != null)
                {
                    if (tax > 0)
                        globalSystemManager.ModifyGold(tax);
                    globalSystemManager.ModifyReputation(reputation);
                }
                break;
            }

            case RuleAction.Deny:
                if (globalSystemManager != null)
                    globalSystemManager.ModifyReputation(FamilyRules.HasChild(in family)
                        ? StabilityRules.DenyChildReputation
                        : StabilityRules.DenyReputation);
                break;

            case RuleAction.Quarantine:
            {
                // Cả nhà vào trạm (sức chứa tính theo người) — không đủ chỗ thì không cách ly được
                if (GetFreeQuarantineSlots() < family.count)
                {
                    if (ruleOrder == 0)
                        ConfirmDialog.Alert($"Trạm cách ly không đủ chỗ cho cả nhà ({family.count} người).\nHãy xây thêm Trạm cách ly, hoặc chọn Cho vào / Từ chối.");
                    return false;
                }

                byte months = ruleAction.type == RuleActionType.ImmigrationQuarantine && ruleAction.param0 >= 1f
                    ? (byte)Mathf.Clamp(Mathf.RoundToInt(ruleAction.param0), 1, 24)
                    : ImmigrationRules.DefaultQuarantineMonths;
                int tax = 0;
                for (int i = 0; i < family.count; i++)
                {
                    ResidentData member = family[i];
                    CollectEntryTax(ref member, out int t);
                    tax += t;
                    member.quarantineMonths = months;
                    member.processedByRule = ruleOrder;
                    Admit(in member, true);
                }
                if (tax > 0 && globalSystemManager != null)
                    globalSystemManager.ModifyGold(tax);
                break;
            }

            default:
                return false;
        }

        residentManager?.NotifyPopulationChanged();
        RemoveCurrent();
        return true;
    }

    /// <summary>GDD 9.6: Admit một gia đình +1 Reputation (+2 nếu có note sức khoẻ hoặc Nghèo kiệt).</summary>
    public static float GetAdmitReputation(in ApplicantFamily family)
    {
        NoteFlags notes = FamilyRules.GetFamilyNotes(in family);
        return (notes & (FamilyRules.HealthNotes | NoteFlags.Destitute)) != 0
            ? StabilityRules.AdmitNeedyReputation
            : StabilityRules.AdmitReputation;
    }

    /// <summary>Có Trạm cách ly nào còn chỗ không (để nút Cách ly / sắc lệnh biết trước).</summary>
    public bool HasQuarantineSpace => FindFreeQuarantineWard() >= 0;

    private int GetFreeQuarantineSlots()
    {
        if (buildingManager == null)
            ResolveDependencies();
        if (buildingManager == null)
            return 0;
        int free = 0;
        for (int i = 0; i < buildingManager.activeCount; i++)
        {
            ref BuildingData b = ref buildingManager.GetBuildingRef(i);
            if (b.buildingType == BuildingType.QuarantineWard && ResidentAssignmentRules.HasHousingSlot(in b))
                free += b.maxOccupancy - b.currentOccupancy;
        }
        return free;
    }

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
        admitted.hungerMonths = 0;   // Trong hàng chờ hungerMonths = số tháng đã chờ; vào thành thì tính lại đói theo kho
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

        return true;
    }

    private void CollectEntryTax(ref ResidentData applicant, out int tax)
    {
        tax = ImmigrationRules.GetEntryTax(in applicant, ResolveTariffPercent(in applicant));
        if (tax > 0)
            ImmigrationRules.PayTax(ref applicant, tax);
    }

    /// <summary>GDD 6.4: thuế nhập thành là mức chung của thành (5 mức), không theo sắc lệnh.</summary>
    private float ResolveTariffPercent(in ResidentData applicant)
    {
        EntryTaxLevel level = globalSystemManager != null
            ? globalSystemManager.GetGlobalDataRef().entryTaxLevel
            : StabilityRules.DefaultEntryTax;
        return StabilityRules.GetEntryTaxPercent(level);
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

        // Xoá cả gia đình; dồn hàng: gia đình kế tiếp lên đúng vị trí vừa xét
        FamilyRules.GetFamilyRange(waitingApplicants, waitingCount, InspectIndex, out int start, out int length);
        for (int i = 0; i < length; i++)
            RemoveAt(start);
        InspectIndex = waitingCount > 0 ? FamilyStart(Mathf.Clamp(start, 0, waitingCount - 1)) : 0;

        NotifyQueueChanged();
        // Chỉ vẽ lại khi màn Cổng đang mở (Gate Clerk duyệt ngầm không được bật màn Cổng lên)
        if (popupUi == null || !popupUi.IsOpen)
            return;
        if (waitingCount > 0)
            ShowCurrent();
        else
            popupUi.ShowEmpty();
    }

    /// <summary>Vẽ lại người đang xét trên popup Cổng (khi hàng chờ đổi lúc popup đang mở).</summary>
    public void RefreshCurrent()
    {
        if (waitingCount <= 0)
            return;
        InspectIndex = FamilyStart(Mathf.Clamp(InspectIndex, 0, waitingCount - 1));
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (popupUi == null || !TryGetCurrentFamily(out ApplicantFamily family))
            return;

        RuleAction suggested = ExplainSuggestion(in family, out string reason);
        popupUi.ShowFamily(in family, suggested, waitingCount, CountFamilies(), reason);
    }

    public int CountFamilies()
    {
        int families = 0;
        for (int i = 0; i < waitingCount;)
        {
            FamilyRules.GetFamilyRange(waitingApplicants, waitingCount, i, out _, out int length);
            families++;
            i += Mathf.Max(1, length);
        }
        return families;
    }

    public RuleAction ExplainSuggestion(in ResidentData applicant, out string reason) =>
        ExplainSuggestion(ApplicantFamily.Single(in applicant, FamilyRules.CurrentProfileLevel), out reason);

    /// <summary>
    /// GDD 8.4 "Why was this NPC processed?": rule nào khớp (ưu tiên trên → dưới, rule đầu tiên khớp được chạy)
    /// và vì sao. Không khớp rule nào -> MANUAL REVIEW (người chơi tự duyệt).
    /// </summary>
    public RuleAction ExplainSuggestion(in ApplicantFamily family, out string reason)
    {
        if (!TryResolveFamily(in family, out RuleActionData action, out int index, out bool usedElse))
        {
            reason = edictManager == null || edictManager.activeCount <= 0
                ? "Chưa có sắc lệnh nhập cư — bạn tự quyết định."
                : "Không sắc lệnh nào khớp với gia đình này → duyệt tay.";
            return RuleAction.ManualReview;
        }

        ref EdictRuleData rule = ref edictManager.GetEdictRef(index);
        var sb = new System.Text.StringBuilder(128);
        string name = string.IsNullOrEmpty(rule.ruleName) ? $"#{rule.ruleID}" : rule.ruleName;
        sb.Append("Sắc lệnh \"").Append(name).Append("\" (ưu tiên ").Append(rule.executionOrder + 1).Append(") ");
        sb.Append(usedElse ? "— không luật nào khớp, dùng nhánh Else vì không thoả: " : "khớp: ");
        sb.Append(EdictRuleCatalog.DescribeLogic(in rule));
        sb.Append(" → ").Append(EdictRuleCatalog.DescribeAction(in action));
        reason = sb.ToString();
        return EdictRules.ToRuleAction(action.type);
    }

    private bool TryResolveFamily(in ApplicantFamily family, out RuleActionData action, out int index, out bool usedElse)
    {
        action = default;
        index = -1;
        usedElse = false;
        if (edictManager == null || globalSystemManager == null || edictManager.activeCount <= 0)
            return false;
        return EdictRules.TryResolveImmigration(
            edictManager.allEdicts, edictManager.activeCount, in family,
            in globalSystemManager.GetGlobalDataRef(), out action, out index, out usedElse);
    }

    // ==========================================
    // GATE CLERK (GDD v0.3 mục 4.6)
    // ==========================================
    public const int FamiliesPerClerk = 4;
    public const int ClerkSlotsTier1 = 1;

    /// <summary>Các quyết định tự động gần đây (hiện ở màn Cổng: "đã được rule xử lý").</summary>
    public readonly System.Collections.Generic.List<string> RecentAutoDecisions = new System.Collections.Generic.List<string>(8);

    /// <summary>Số Clerk đang làm (0 nếu chưa nghiên cứu Bộ luật hoặc không có ai rảnh để giao việc).</summary>
    public int ActiveClerks => ResolveGateClerk() ? ClerkSlotsTier1 : 0;

    /// <summary>
    /// Chỗ Gate Clerk (mở cùng Bộ luật, Tier 1): giao cho một người lớn đang không có việc. Người đó được tính là có việc
    /// (không bị trừ Happiness thất nghiệp). Không còn ai rảnh thì chỗ trống và không duyệt tự động.
    /// </summary>
    public bool ResolveGateClerk()
    {
        if (globalSystemManager == null || residentManager == null)
            ResolveDependencies();
        if (globalSystemManager == null || residentManager == null || !EdictRules.EdictsUnlocked)
            return false;

        ref GlobalSystemData g = ref globalSystemManager.GetGlobalDataRef();
        int current = -1;
        int candidate = -1;
        for (int i = 0; i < residentManager.activeCount; i++)
        {
            ref ResidentData r = ref residentManager.allResidents[i];
            if (!IsClerkCandidate(in r))
                continue;
            if (r.residentID == g.gateClerkResidentID) { current = i; break; }
            if (candidate < 0) candidate = i;
        }

        int chosen = current >= 0 ? current : candidate;
        g.gateClerkResidentID = chosen >= 0 ? residentManager.allResidents[chosen].residentID : 0;
        return chosen >= 0;
    }

    public static bool IsClerkCandidate(in ResidentData r) =>
        ResidentAssignmentRules.CanWork(in r) && r.assignedWorkID < 0
        && !ResidentAssignmentRules.NeedsQuarantineHousing(in r);

    /// <summary>Mỗi Clerk xử lý tối đa 4 gia đình / tháng theo rule. MANUAL REVIEW hoặc không khớp rule thì để lại cho người chơi.</summary>
    public int ProcessGateClerk()
    {
        int capacity = EmergencyOrders.GateClosed ? 0 : ActiveClerks * FamiliesPerClerk;
        if (capacity <= 0 || waitingCount <= 0)
            return 0;

        RecentAutoDecisions.Clear();
        int processed = 0;
        int index = 0;
        while (processed < capacity && index < waitingCount)
        {
            ApplicantFamily family = GetFamilyAt(index);
            if (!TryResolveFamily(in family, out RuleActionData action, out int ruleIndex, out _)
                || action.type == RuleActionType.ImmigrationManualReview)
            {
                index = family.start + family.count;   // để duyệt tay
                continue;
            }

            RuleAction decision = EdictRules.ToRuleAction(action.type);
            ref EdictRuleData rule = ref edictManager.GetEdictRef(ruleIndex);
            byte order = (byte)Mathf.Min(254, rule.executionOrder + 1);
            string label = $"{FamilyLabel(in family)} → {ImmigrationRules.ActionLabel(decision)} (sắc lệnh {order}: {rule.ruleName})";

            InspectIndex = family.start;
            if (!DecideCurrent(decision, order, action))
            {
                index = family.start + family.count;   // vd. trạm cách ly hết chỗ -> để duyệt tay
                continue;
            }

            processed++;
            RecentAutoDecisions.Add(label);
            index = family.start;   // hàng đã dồn lên
        }

        InspectIndex = waitingCount > 0 ? FamilyStart(0) : 0;
        if (processed > 0)
            NotificationFeed.Show($"Gate Clerk đã xử lý {processed} gia đình theo sắc lệnh", NoticeLevel.Info, "gate-clerk", UIActions.OpenGate);
        return processed;
    }

    public static string FamilyLabel(in ApplicantFamily family) =>
        $"Nhà {NameDatabase.GetLastName(family.Head.lastNameID)} ({family.count} người)";

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
