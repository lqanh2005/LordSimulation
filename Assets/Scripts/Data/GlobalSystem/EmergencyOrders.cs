using System.IO;
using UnityEngine;

/// <summary>Sự kiện mở ra Lệnh khẩn cấp (GDD v0.3 mục 9.4) + sự kiện nối tiếp (hậu quả trễ có lựa chọn).</summary>
public enum EmergencyEvent : byte
{
    None = 0,
    RiotImminent = 1,        // "Bạo loạn sắp nổ ra" (Riot ≥ 70)
    CoalRunningOut = 2,      // "Kho than sắp cạn" (mùa đông, Coal dưới 1 tháng)
    FrozenInTents = 3,       // "Có người chết cóng ở Lều"
    UncollectedCorpses = 4,  // "Thi thể chưa được thu gom" (sắp thành Miasma)
    Outbreak = 5,            // "Dịch bùng trong thành"
    YardStarving = 6,        // "Người ngoài tường đang chết đói"
    MinersDemandRaise = 10,  // Hậu quả "Ca làm kép": thợ mỏ đòi tăng lương
    MemorialDemand = 11,     // Hậu quả "Đốt xác tập thể": gia đình đòi tưởng niệm
    LeaderArrested = 12      // Hậu quả "Thiết quân luật": người cầm đầu bị bắt
}

public enum EmergencyOrder : byte
{
    None = 0,
    BurnWood = 1, DoubleShift = 2, CrowdHouses = 3, MassCremation = 4, CloseGate = 5, YardRelief = 6, MartialLaw = 7,
    AgreeRaise = 10, RefuseRaise = 11, BuildMemorial = 12, IgnoreMemorial = 13, ReleaseLeader = 14, JailLeader = 15
}

/// <summary>Trạng thái Lệnh khẩn cấp, lưu cuối file save (v16).</summary>
public class EmergencyState
{
    public const int MaxPending = 8;

    public EmergencyEvent openEvent;
    public EmergencyEvent lastOffered;
    public byte[] uses = new byte[16];           // Số lần đã dùng mỗi lệnh (tiền lệ: giá Happiness +50% mỗi lần)

    public bool burnWood;                        // Đốt gỗ sưởi ấm (tới hết mùa đông)
    public bool crowding;                        // Dồn người vào nhà gỗ (tới hết mùa đông)
    public byte doubleShiftMonths;               // Ca làm kép ở mỏ
    public byte gateClosedMonths;                // Đóng cổng
    public byte reliefMonths;                    // Phát chẩn hàng ngoài tường
    public byte martialLawMonths;                // Happiness −10 trong 3 tháng sau Thiết quân luật
    public byte cremationMonths;                 // Happiness cả thành −15 sau Đốt xác tập thể
    public byte minersMoodMonths;                // Thợ mỏ bất mãn (ca kép / từ chối tăng lương)
    public float martialMood, cremationMood, crowdMood, minersMood;
    public float nextWaveMultiplier = 1f;        // Hậu quả trễ lên đợt di cư kế tiếp (Đóng cổng ×0,7, Phát chẩn ×1,3)
    public bool nextWavePoor;

    public EmergencyEvent[] pendingEvent = new EmergencyEvent[MaxPending];   // Hậu quả trễ (thông báo sau 1–3 tháng)
    public byte[] pendingMonths = new byte[MaxPending];

    public void Write(BinaryWriter w)
    {
        w.Write((byte)openEvent);
        w.Write((byte)lastOffered);
        w.Write(uses);
        w.Write(burnWood); w.Write(crowding);
        w.Write(doubleShiftMonths); w.Write(gateClosedMonths); w.Write(reliefMonths);
        w.Write(martialLawMonths); w.Write(cremationMonths); w.Write(minersMoodMonths);
        w.Write(martialMood); w.Write(cremationMood); w.Write(crowdMood); w.Write(minersMood);
        w.Write(nextWaveMultiplier); w.Write(nextWavePoor);
        for (int i = 0; i < MaxPending; i++)
        {
            w.Write((byte)pendingEvent[i]);
            w.Write(pendingMonths[i]);
        }
    }

    public static EmergencyState Read(BinaryReader r)
    {
        var s = new EmergencyState
        {
            openEvent = (EmergencyEvent)r.ReadByte(),
            lastOffered = (EmergencyEvent)r.ReadByte(),
            uses = r.ReadBytes(16),
            burnWood = r.ReadBoolean(), crowding = r.ReadBoolean(),
            doubleShiftMonths = r.ReadByte(), gateClosedMonths = r.ReadByte(), reliefMonths = r.ReadByte(),
            martialLawMonths = r.ReadByte(), cremationMonths = r.ReadByte(), minersMoodMonths = r.ReadByte(),
            martialMood = r.ReadSingle(), cremationMood = r.ReadSingle(), crowdMood = r.ReadSingle(), minersMood = r.ReadSingle(),
            nextWaveMultiplier = r.ReadSingle(), nextWavePoor = r.ReadBoolean()
        };
        for (int i = 0; i < MaxPending; i++)
        {
            s.pendingEvent[i] = (EmergencyEvent)r.ReadByte();
            s.pendingMonths[i] = r.ReadByte();
        }
        return s;
    }
}

/// <summary>Một lựa chọn trên thẻ thông báo: tên lệnh, giá ngay (hiện rõ), gợi ý hậu quả trễ (không nói cụ thể).</summary>
public struct EmergencyOption
{
    public EmergencyOrder order;
    public string label;
    public string gain;
    public string cost;
    public string hint;
}

/// <summary>
/// Lệnh khẩn cấp GDD v0.3 mục 9.4 — không nằm trong menu riêng mà hiện ra qua thông báo sự kiện khi khủng hoảng
/// chạm ngưỡng, kèm 2–3 lựa chọn và luôn có "Không làm gì". Mỗi lệnh cứu thành ngay nhưng trả giá hai lần:
/// giá ngay (ghi rõ) và hậu quả trễ (1–3 tháng sau, thành thông báo mới). Dùng lại cùng một lệnh: giá Happiness +50%.
/// Thời gian vẫn trôi khi thẻ đang mở; hết tháng mà chưa chọn thì tự động "Không làm gì".
/// </summary>
public static class EmergencyOrders
{
    public static EmergencyState State = new EmergencyState();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetStatics() => State = new EmergencyState();

    // ===== Số GDD bảng 9.4 (giá trị tạm) =====
    public const float WoodPerCoal = 2f;
    public const float WoodHeatEfficiency = 0.8f;        // Nhà kém ấm hơn 20%
    public const float HouseFireChance = 0.05f;
    public const float DoubleShiftBonus = 1.5f;
    public const float DoubleShiftMood = -15f;
    public const float CrowdCapacity = 1.5f;
    public const float CrowdMood = -10f;
    public const float CremationMood = -15f;
    public const float CremationReputation = -5f;
    public const float CloseGateReputation = -15f;
    public const byte CloseGateMonths = 2;
    public const float CloseGateWaveMultiplier = 0.7f;
    public const float ReliefWaveMultiplier = 1.3f;
    public const float MartialLawRiot = -30f;
    public const float MartialLawRiotRepeat = -15f;      // Lần sau dùng chỉ còn Riot −15
    public const float MartialLawMood = -10f;
    public const byte MartialLawMonths = 3;
    public const float PrecedentStep = 0.5f;

    public const int RaiseGold = 40;                     // Đồng ý tăng lương thợ mỏ (trả một lần)
    public const float RefuseRaiseMood = -10f;
    public const int MemorialGold = 60, MemorialWood = 40;
    public const float IgnoreMemorialRiot = 10f;
    public const float ReleaseLeaderRiot = 5f;
    public const float JailLeaderReputation = -3f;

    public static float Precedent(EmergencyOrder order) => 1f + PrecedentStep * State.uses[(int)order];

    // ===== Truy vấn cho các hệ thống khác =====
    public static bool GateClosed => State.gateClosedMonths > 0;
    public static bool BurnWoodActive => State.burnWood;
    public static bool CrowdingActive => State.crowding;
    public static bool YardReliefActive => State.reliefMonths > 0;

    public static float GetProductionMultiplier(BuildingType type) =>
        type == BuildingType.Mine && State.doubleShiftMonths > 0 ? DoubleShiftBonus : 1f;

    /// <summary>Sức chứa nhà khi đang Dồn người (nhà gỗ / nhà lớn chứa tới 150%).</summary>
    public static int GetHousingCapacity(in BuildingData b)
    {
        if (State.crowding && (b.buildingType == BuildingType.House || b.buildingType == BuildingType.LargeHouse))
            return Mathf.FloorToInt(b.maxOccupancy * CrowdCapacity);
        return b.maxOccupancy;
    }

    /// <summary>Điều chỉnh mục tiêu Happiness do lệnh khẩn cấp (đã nhân tiền lệ).</summary>
    public static float GetHappinessModifier(in ResidentData r, bool inCrowdedHouse)
    {
        float mod = 0f;
        if (State.martialLawMonths > 0) mod += State.martialMood;
        if (State.cremationMonths > 0) mod += State.cremationMood;
        if (State.crowding && inCrowdedHouse) mod += State.crowdMood;
        if (State.minersMoodMonths > 0 && r.professionType == ProfessionType.Miner) mod += State.minersMood;
        return mod;
    }

    /// <summary>Hậu quả trễ lên đợt di cư kế tiếp (dùng một lần).</summary>
    public static float ConsumeWaveMultiplier(out bool poorer)
    {
        float m = State.nextWaveMultiplier > 0f ? State.nextWaveMultiplier : 1f;
        poorer = State.nextWavePoor;
        State.nextWaveMultiplier = 1f;
        State.nextWavePoor = false;
        return m;
    }

    // ===== Nội dung thẻ =====
    public static string GetTitle(EmergencyEvent e) => e switch
    {
        EmergencyEvent.RiotImminent => "Bạo loạn sắp nổ ra",
        EmergencyEvent.CoalRunningOut => "Kho than sắp cạn",
        EmergencyEvent.FrozenInTents => "Có người chết cóng ở Lều",
        EmergencyEvent.UncollectedCorpses => "Thi thể chưa được thu gom",
        EmergencyEvent.Outbreak => "Dịch bùng trong thành",
        EmergencyEvent.YardStarving => "Người ngoài tường đang chết đói",
        EmergencyEvent.MinersDemandRaise => "Thợ mỏ đòi tăng lương",
        EmergencyEvent.MemorialDemand => "Gia đình người chết đòi được tưởng niệm",
        EmergencyEvent.LeaderArrested => "Một người cầm đầu bạo loạn bị bắt",
        _ => ""
    };

    public static string GetDescription(EmergencyEvent e) => e switch
    {
        EmergencyEvent.RiotImminent => "Dân chúng tụ tập trước Tòa thị chính. Chỉ còn ít thời gian trước khi bạo loạn lật đổ chính quyền.",
        EmergencyEvent.CoalRunningOut => "Than trong kho không đủ sưởi qua tháng tới. Lều sẽ lạnh trước tiên.",
        EmergencyEvent.FrozenInTents => "Đêm qua có người chết cóng trong Lều tạm.",
        EmergencyEvent.UncollectedCorpses => "Thi thể nằm lại chưa ai thu gom; sang tháng sẽ phân hủy thành Miasma.",
        EmergencyEvent.Outbreak => "Sốt Đỏ đang lan trong thành, người mới vào có thể mang thêm mầm bệnh.",
        EmergencyEvent.YardStarving => "Người chờ ngoài tường không có gì ăn, đã có người chết đói.",
        EmergencyEvent.MinersDemandRaise => "Sau vụ tai nạn ở mỏ, thợ mỏ còn lại đòi được trả thêm.",
        EmergencyEvent.MemorialDemand => "Vì đã đốt xác tập thể, gia đình người chết đòi một nơi tưởng niệm.",
        EmergencyEvent.LeaderArrested => "Sau Thiết quân luật, lính gác bắt được một người cầm đầu.",
        _ => ""
    };

    public static bool IsFollowUp(EmergencyEvent e) => (byte)e >= 10;

    public static EmergencyOption[] GetOptions(EmergencyEvent e)
    {
        switch (e)
        {
            case EmergencyEvent.RiotImminent:
                return new[] { Opt(EmergencyOrder.MartialLaw, "Thiết quân luật",
                    $"Riot {(State.uses[(int)EmergencyOrder.MartialLaw] > 0 ? MartialLawRiotRepeat : MartialLawRiot):0} ngay",
                    $"Happiness {MartialLawMood * Precedent(EmergencyOrder.MartialLaw):0} trong {MartialLawMonths} tháng") };
            case EmergencyEvent.CoalRunningOut:
                return new[]
                {
                    Opt(EmergencyOrder.BurnWood, "Đốt gỗ sưởi ấm", "2 gỗ thay 1 than tới hết mùa đông", "Nhà kém ấm hơn 20%; hết gỗ để xây"),
                    Opt(EmergencyOrder.DoubleShift, "Ca làm kép ở mỏ", "Than +50% trong 1 tháng",
                        $"Happiness thợ mỏ {DoubleShiftMood * Precedent(EmergencyOrder.DoubleShift):0}")
                };
            case EmergencyEvent.FrozenInTents:
                return new[] { Opt(EmergencyOrder.CrowdHouses, "Dồn người vào nhà gỗ", "Nhà gỗ chứa tới 150%, người ở Lều được vào ở",
                    $"Happiness người trong nhà bị dồn {CrowdMood * Precedent(EmergencyOrder.CrowdHouses):0}") };
            case EmergencyEvent.UncollectedCorpses:
                return new[] { Opt(EmergencyOrder.MassCremation, "Đốt xác tập thể", "Xóa ngay thi thể và Miasma",
                    $"Happiness cả thành {CremationMood * Precedent(EmergencyOrder.MassCremation):0}; Reputation {CremationReputation:0}") };
            case EmergencyEvent.Outbreak:
                return new[] { Opt(EmergencyOrder.CloseGate, $"Đóng cổng {CloseGateMonths} tháng", "Chặn nguồn bệnh từ ngoài",
                    $"Reputation {CloseGateReputation:0}; hàng ngoài tường không được vào, thương nhân không tới") };
            case EmergencyEvent.YardStarving:
            {
                int food = YardReliefFood();
                return new[] { Opt(EmergencyOrder.YardRelief, "Phát chẩn", "Người chờ không đói trong 1 tháng", $"Lương thực −{food}") };
            }
            case EmergencyEvent.MinersDemandRaise:
                return new[]
                {
                    Opt(EmergencyOrder.AgreeRaise, "Đồng ý", "Thợ mỏ yên lòng", $"Vàng −{RaiseGold}"),
                    Opt(EmergencyOrder.RefuseRaise, "Từ chối", "Không tốn vàng", $"Happiness thợ mỏ {RefuseRaiseMood:0} trong 2 tháng")
                };
            case EmergencyEvent.MemorialDemand:
                return new[]
                {
                    Opt(EmergencyOrder.BuildMemorial, "Xây đài tưởng niệm", "Gia đình được an ủi", $"Vàng −{MemorialGold}, Gỗ −{MemorialWood}"),
                    Opt(EmergencyOrder.IgnoreMemorial, "Phớt lờ", "Không tốn gì", $"Riot +{IgnoreMemorialRiot:0}")
                };
            case EmergencyEvent.LeaderArrested:
                return new[]
                {
                    Opt(EmergencyOrder.ReleaseLeader, "Thả", "Dân bớt căm phẫn chính quyền", $"Riot +{ReleaseLeaderRiot:0}"),
                    Opt(EmergencyOrder.JailLeader, "Giam", "Răn đe kẻ khác", $"Reputation {JailLeaderReputation:0}")
                };
            default:
                return System.Array.Empty<EmergencyOption>();
        }
    }

    private static EmergencyOption Opt(EmergencyOrder order, string label, string gain, string cost) => new EmergencyOption
    {
        order = order, label = label, gain = gain, cost = cost,
        hint = (byte)order < 10 ? "Dân sẽ nhớ chuyện này." : null
    };

    private static GlobalSystemManager Global =>
        GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
            ? GamePlayController.Instance.playerContain.globalSystemManager
            : null;

    public static int YardReliefFood()
    {
        PlayerContain c = GamePlayController.Instance != null ? GamePlayController.Instance.playerContain : null;
        int waiting = c != null && c.immigrationManager != null ? c.immigrationManager.waitingCount : 0;
        GlobalSystemManager g = Global;
        int perCapita = g != null ? GlobalSystemManager.GetFoodPerCapita(g.GetGlobalDataRef().CurrentSeason) : 1;
        return waiting * perCapita;
    }

    // ===== Mở / chọn =====
    public static void Offer(EmergencyEvent e)
    {
        State.openEvent = e;
        State.lastOffered = e;
        NotificationFeed.Show($"{GetTitle(e)} — cần quyết định", NoticeLevel.Critical, "emergency-" + (int)e, EmergencyCard.Open);
        EmergencyCard.Open();
    }

    /// <summary>Hết tháng mà chưa chọn -> tự động "Không làm gì".</summary>
    public static void ExpireOpen()
    {
        if (State.openEvent == EmergencyEvent.None)
            return;
        // Sự kiện nối tiếp có hai lựa chọn: bỏ qua = lựa chọn "không chiều" (Từ chối / Phớt lờ / Giam)
        EmergencyEvent e = State.openEvent;
        if (e == EmergencyEvent.MinersDemandRaise) Choose(EmergencyOrder.RefuseRaise);
        else if (e == EmergencyEvent.MemorialDemand) Choose(EmergencyOrder.IgnoreMemorial);
        else if (e == EmergencyEvent.LeaderArrested) Choose(EmergencyOrder.JailLeader);
        else Choose(EmergencyOrder.None);
    }

    public static void Choose(EmergencyOrder order)
    {
        GlobalSystemManager g = Global;
        State.openEvent = EmergencyEvent.None;
        EmergencyCard.Close();
        if (order == EmergencyOrder.None || g == null)
            return;

        ref GlobalSystemData d = ref g.GetGlobalDataRef();
        float precedent = Precedent(order);
        switch (order)
        {
            case EmergencyOrder.BurnWood:
                State.burnWood = true;
                break;
            case EmergencyOrder.DoubleShift:
                State.doubleShiftMonths = 1;
                State.minersMoodMonths = 1;
                State.minersMood = DoubleShiftMood * precedent;
                Schedule(EmergencyEvent.MinersDemandRaise, (byte)Random.Range(1, 3));
                break;
            case EmergencyOrder.CrowdHouses:
                State.crowding = true;
                State.crowdMood = CrowdMood * precedent;
                MoveTentDwellers();
                break;
            case EmergencyOrder.MassCremation:
                d.corpsesFresh = 0;
                d.corpsesRotting = 0;
                d.miasma = 0f;
                d.miasmaZonesPrev = 0;
                State.cremationMonths = 1;
                State.cremationMood = CremationMood * precedent;
                g.ModifyReputation(CremationReputation);
                Schedule(EmergencyEvent.MemorialDemand, (byte)Random.Range(1, 3));
                break;
            case EmergencyOrder.CloseGate:
                State.gateClosedMonths = CloseGateMonths;
                g.ModifyReputation(CloseGateReputation);
                State.nextWaveMultiplier *= CloseGateWaveMultiplier;   // Tin lan ra: đợt di cư kế tiếp nhỏ đi 30%
                break;
            case EmergencyOrder.YardRelief:
                g.ConsumeAvailable(ResourceType.Food, YardReliefFood());
                State.reliefMonths = 1;
                State.nextWaveMultiplier *= ReliefWaveMultiplier;      // Tin lan ra: đợt sau đông hơn 30%, nhiều người nghèo hơn
                State.nextWavePoor = true;
                break;
            case EmergencyOrder.MartialLaw:
            {
                float riot = State.uses[(int)EmergencyOrder.MartialLaw] > 0 ? MartialLawRiotRepeat : MartialLawRiot;
                d.riotRiskMeter = Mathf.Clamp(d.riotRiskMeter + riot, 0f, 100f);
                State.martialLawMonths = MartialLawMonths;
                State.martialMood = MartialLawMood * precedent;
                Schedule(EmergencyEvent.LeaderArrested, 1);
                break;
            }
            case EmergencyOrder.AgreeRaise:
                g.ModifyGold(-RaiseGold);
                break;
            case EmergencyOrder.RefuseRaise:
                State.minersMoodMonths = 2;
                State.minersMood = RefuseRaiseMood;
                break;
            case EmergencyOrder.BuildMemorial:
                g.ModifyGold(-MemorialGold);
                g.ConsumeAvailable(ResourceType.Wood, MemorialWood);
                break;
            case EmergencyOrder.IgnoreMemorial:
                d.riotRiskMeter = Mathf.Min(100f, d.riotRiskMeter + IgnoreMemorialRiot);
                break;
            case EmergencyOrder.ReleaseLeader:
                d.riotRiskMeter = Mathf.Min(100f, d.riotRiskMeter + ReleaseLeaderRiot);
                break;
            case EmergencyOrder.JailLeader:
                g.ModifyReputation(JailLeaderReputation);
                break;
        }

        if (State.uses[(int)order] < byte.MaxValue)
            State.uses[(int)order]++;
        GameEvents.Post(EventID.ResourcesChanged);
    }

    private static void Schedule(EmergencyEvent e, byte months)
    {
        for (int i = 0; i < EmergencyState.MaxPending; i++)
        {
            if (State.pendingEvent[i] != EmergencyEvent.None)
                continue;
            State.pendingEvent[i] = e;
            State.pendingMonths[i] = months;
            return;
        }
    }

    /// <summary>Dồn người: người ở Lều bỏ Lều để được xếp lại vào nhà gỗ (sức chứa 150%).</summary>
    private static void MoveTentDwellers()
    {
        PlayerContain c = GamePlayController.Instance != null ? GamePlayController.Instance.playerContain : null;
        if (c == null || c.residentManager == null || c.buildingManager == null)
            return;
        for (int i = 0; i < c.residentManager.activeCount; i++)
        {
            ref ResidentData r = ref c.residentManager.allResidents[i];
            if (!r.isAlive || r.assignedHouseID < 0)
                continue;
            int idx = c.buildingManager.FindBuildingIndexById((ushort)r.assignedHouseID);
            if (idx >= 0 && c.buildingManager.GetBuildingRef(idx).buildingType == BuildingType.Tent)
                ResidentAssignmentRules.ClearHousingAssignment(ref r);
        }
        c.residentManager.RequestReassign();
    }
}
