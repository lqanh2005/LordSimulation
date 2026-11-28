using UnityEngine;

/// <summary>
/// Chạy Lệnh khẩn cấp theo tháng (GDD v0.3 mục 9.4): đếm ngược hiệu lực, hậu quả trễ, cháy nhà khi đốt gỗ,
/// rồi xét ngưỡng khủng hoảng để mở thẻ thông báo có lựa chọn. Xử lý ở LateUpdate sau tick tháng
/// (giống CrisisManager) để đọc được kết quả của mọi hệ thống trong tháng.
/// </summary>
public class EmergencyManager : MonoBehaviour
{
    private GlobalSystemManager _global;
    private PlayerContain _contain;
    private bool _pendingTick;

    // Số liệu tháng vừa qua để xét ngưỡng
    private int _coldDeaths;
    private bool _corpsesAboutToRot;
    private bool _yardStarving;

    public void Init(PlayerContain contain)
    {
        _contain = contain;
        _global = contain.globalSystemManager;
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.ResidentsDied, OnResidentsDied);
        GameEvents.Listen(EventID.CrisisReport, OnCrisisReport);
        GameEvents.Listen(EventID.WaitingYardReport, OnYardReport);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Unlisten(EventID.ResidentsDied, OnResidentsDied);
        GameEvents.Unlisten(EventID.CrisisReport, OnCrisisReport);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnYardReport);
    }

    private void OnMonthChanged(object param)
    {
        if (param is MonthChangedPayload p && p.isSimulationTick)
        {
            // Hết tháng mà chưa chọn -> "Không làm gì"
            EmergencyOrders.ExpireOpen();
            _pendingTick = true;
        }
    }

    private void OnResidentsDied(object param)
    {
        if (param is ResidentsDiedPayload p && p.cause == DeathCause.Cold)
            _coldDeaths += p.count;
    }

    private void OnCrisisReport(object param)
    {
        if (param is CrisisReportPayload p)
            _corpsesAboutToRot = p.unburied > p.rotting;   // còn xác tươi chưa thu gom -> tháng sau thành Miasma
    }

    private void OnYardReport(object param)
    {
        if (param is WaitingYardReportPayload p)
            _yardStarving = p.died > 0 || p.hungry > 0;
    }

    private void LateUpdate()
    {
        if (!_pendingTick)
            return;
        _pendingTick = false;
        if (_global == null && _contain != null)
            _global = _contain.globalSystemManager;
        if (_global == null)
            return;

        ProcessMonthly(ref _global.GetGlobalDataRef());
        _coldDeaths = 0;
        _corpsesAboutToRot = false;
        _yardStarving = false;
    }

    private void ProcessMonthly(ref GlobalSystemData d)
    {
        EmergencyState s = EmergencyOrders.State;
        bool winter = d.CurrentSeason == SeasonType.Winter;

        // 1. Đếm ngược hiệu lực
        if (s.doubleShiftMonths > 0) s.doubleShiftMonths--;
        if (s.gateClosedMonths > 0)
        {
            s.gateClosedMonths--;
            if (s.gateClosedMonths == 0)
                NotificationFeed.Show("Cổng thành mở lại", NoticeLevel.Info, "gate-open", UIActions.OpenGate);
        }
        if (s.reliefMonths > 0) s.reliefMonths--;
        if (s.martialLawMonths > 0) s.martialLawMonths--;
        if (s.cremationMonths > 0) s.cremationMonths--;
        if (s.minersMoodMonths > 0) s.minersMoodMonths--;
        if (!winter)
        {
            s.burnWood = false;   // Đốt gỗ / dồn người chỉ tới hết mùa đông
            if (s.crowding)
            {
                s.crowding = false;
                _contain?.residentManager?.RequestReassign();
            }
        }

        // 2. Hậu quả trễ
        if (s.burnWood && Random.value < EmergencyOrders.HouseFireChance)
            BurnAHouse();
        if (s.crowding)
            ReportCrowdOutbreak();
        for (int i = 0; i < EmergencyState.MaxPending; i++)
        {
            if (s.pendingEvent[i] == EmergencyEvent.None)
                continue;
            if (s.pendingMonths[i] > 0)
                s.pendingMonths[i]--;
            if (s.pendingMonths[i] > 0 || s.openEvent != EmergencyEvent.None)
                continue;   // Đang có thẻ khác mở: để tháng sau
            EmergencyEvent e = s.pendingEvent[i];
            s.pendingEvent[i] = EmergencyEvent.None;
            if (e == EmergencyEvent.MinersDemandRaise)
                KillMiners();
            EmergencyOrders.Offer(e);
        }

        // 3. Xét ngưỡng khủng hoảng (một thẻ mỗi lần, không mời lại cùng sự kiện hai tháng liền)
        if (s.openEvent != EmergencyEvent.None)
            return;
        EmergencyEvent trigger = EmergencyEvent.None;
        if (d.riotRiskMeter >= StabilityRules.RiotCriticalThreshold && s.martialLawMonths == 0)
            trigger = EmergencyEvent.RiotImminent;
        else if (winter && !s.burnWood && s.doubleShiftMonths == 0 && d.stockCoal < MarketSystem.GetMonthlyNeed(ResourceType.Coal, in d))
            trigger = EmergencyEvent.CoalRunningOut;
        else if (_coldDeaths > 0 && winter && !s.crowding)
            trigger = EmergencyEvent.FrozenInTents;
        else if (_corpsesAboutToRot && d.corpsesFresh > 0)
            trigger = EmergencyEvent.UncollectedCorpses;
        else if (d.totalInfected >= ResidentDiseaseSystem.OutbreakInfectedThreshold && s.gateClosedMonths == 0)
            trigger = EmergencyEvent.Outbreak;
        else if (_yardStarving && s.reliefMonths == 0 && EmergencyOrders.YardReliefFood() > 0)
            trigger = EmergencyEvent.YardStarving;

        if (trigger != EmergencyEvent.None && trigger != s.lastOffered)
            EmergencyOrders.Offer(trigger);
        else if (trigger == EmergencyEvent.None)
            s.lastOffered = EmergencyEvent.None;
    }

    /// <summary>Đốt gỗ: mỗi tháng 5% một căn nhà bốc cháy — "Lều số 4 cháy, gia đình ông Bram mất chỗ ở".</summary>
    private void BurnAHouse()
    {
        if (_contain == null || _contain.buildingManager == null || _contain.residentManager == null)
            return;
        BuildingManager bm = _contain.buildingManager;
        int pick = -1, seen = 0;
        for (int i = 0; i < bm.activeCount; i++)
        {
            ref BuildingData b = ref bm.GetBuildingRef(i);
            if ((b.buildingType == BuildingType.Tent || b.buildingType == BuildingType.House) && b.currentOccupancy > 0
                && Random.Range(0, ++seen) == 0)
                pick = i;
        }
        if (pick < 0)
            return;

        BuildingData house = bm.GetBuildingRef(pick);
        string owner = "một gia đình";
        ResidentManager rm = _contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
        {
            if (rm.allResidents[i].isAlive && rm.allResidents[i].assignedHouseID == (short)house.buildingID)
            {
                owner = "gia đình " + NameDatabase.GetFullName(rm.allResidents[i].firstNameID, rm.allResidents[i].lastNameID);
                break;
            }
        }

        PlacementManager placement = FindFirstObjectByType<PlacementManager>();
        if (placement == null || !placement.DemolishBuilding(house.buildingID, false))
            return;
        string kind = house.buildingType == BuildingType.Tent ? "Lều" : "Nhà gỗ";
        NotificationFeed.Show($"Vì đốt gỗ sưởi ấm: {kind} số {house.buildingID} cháy, {owner} mất chỗ ở", NoticeLevel.Warning, "house-fire");
    }

    /// <summary>Dồn người: nhà quá sức chứa có người bệnh -> "Ổ dịch ở Nhà gỗ số N".</summary>
    private void ReportCrowdOutbreak()
    {
        if (_contain == null || _contain.buildingManager == null || _contain.residentManager == null)
            return;
        BuildingManager bm = _contain.buildingManager;
        ResidentManager rm = _contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
        {
            ref ResidentData r = ref rm.allResidents[i];
            if (!r.isAlive || r.healthStatus != HealthStatus.ActiveInfected || r.assignedHouseID < 0)
                continue;
            int idx = bm.FindBuildingIndexById((ushort)r.assignedHouseID);
            if (idx < 0)
                continue;
            ref BuildingData b = ref bm.GetBuildingRef(idx);
            if (b.currentOccupancy > b.maxOccupancy)
            {
                NotificationFeed.Show($"Vì dồn người vào nhà gỗ: ổ dịch ở Nhà gỗ số {b.buildingID}", NoticeLevel.Critical, "crowd-outbreak");
                return;
            }
        }
    }

    /// <summary>Ca làm kép: 1–2 thợ mỏ chết vì sập hầm hoặc kiệt sức (có tên).</summary>
    private void KillMiners()
    {
        if (_contain == null || _contain.residentManager == null || _global == null)
            return;
        ResidentManager rm = _contain.residentManager;
        int toKill = Random.Range(1, 3);
        string names = null;
        int killed = 0;
        for (int i = 0; i < rm.activeCount && killed < toKill; i++)
        {
            ref ResidentData r = ref rm.allResidents[i];
            if (!r.isAlive || r.professionType != ProfessionType.Miner)
                continue;
            r.isAlive = false;
            killed++;
            string n = NameDatabase.GetFullName(r.firstNameID, r.lastNameID);
            names = names == null ? n : names + " và " + n;
        }
        if (killed == 0)
            return;
        ref GlobalSystemData d = ref _global.GetGlobalDataRef();
        d.totalPopulation = Mathf.Max(0, d.totalPopulation - killed);
        CrisisManager.AddCorpses(killed);
        rm.NotifyPopulationChanged();
        NotificationFeed.Show($"Vì ca làm kép: thợ mỏ {names} chết trong hầm mỏ", NoticeLevel.Critical, "miner-death");
    }
}
