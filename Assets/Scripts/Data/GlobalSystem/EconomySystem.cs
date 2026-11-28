using System.Collections.Generic;
using UnityEngine;

/// <summary>Báo cáo dòng tiền tháng gần nhất (đã quy ra dân thật) — cho cửa sổ Ngân khố / thông báo.</summary>
public struct EconomyReport
{
    public int wagesDue;          // Tổng lương phải trả
    public int wagesPaid;         // Đã trả (kể cả trả nợ cũ)
    public int incomeTax;         // Thuế thu nhập khấu trừ tại nguồn
    public int householdFees;     // Hộ trả tiền ăn + sưởi
    public int subsidy;           // Thành bù cho hộ thiếu tiền (cấp phát)
    public int newDebt;           // Nợ lương phát sinh tháng này
    public int totalDebt;         // Tổng nợ lương còn lại
    public int debtors;           // Số người (mẫu) đang bị nợ
    public int strikers;          // Số người (mẫu) đang đình công
    public int cutHouseholds;     // Số hộ bị cắt khẩu phần
    public int netGold;           // Kho vàng thay đổi vì kinh tế hộ
    public bool hasTaxOffice;
    public float strikeRate;      // Tỉ lệ lao động đang đình công (0–1)
}

/// <summary>
/// Kinh tế hộ gia đình GDD v0.3 mục 6.4, tính mỗi tháng (trước Happiness / Riot):
/// 1. Kho vàng trả lương cho người có việc theo nghề × mức lương. Thiếu vàng: trả mọi người cùng tỷ lệ, phần thiếu thành nợ.
/// 2. Thuế thu nhập khấu trừ tại nguồn (cần Tax Office = Tòa thị chính đã xây).
/// 3. Hộ (cùng nhà; vô gia cư theo gia đình) gộp lương ròng, trả tiền ăn theo đầu người và tiền sưởi mùa đông.
///    Thiếu thì lấy Wealth; vẫn thiếu thì theo chính sách: Cấp phát (thành chịu lỗ) / Cắt khẩu phần (hộ bị đói).
/// 4. Phần dư cộng vào Wealth của hộ.
/// Nợ lương 1 tháng: −10 Happiness; 2 tháng liên tiếp: đình công (hiệu suất 0, −20); đình công 2 tháng: Riot +10.
/// </summary>
public static class EconomySystem
{
    public static EconomyReport LastReport { get; private set; }

    private static float _foodFee = EconomyRules.FoodFeePerPerson, _heatFee = EconomyRules.HeatFeePerPerson;
    /// <summary>Tiền ăn / sưởi mỗi người tháng gần nhất (theo giá thị trường).</summary>
    public static float LastFoodFee => _foodFee;
    public static float LastHeatFee => _heatFee;

    // Hệ số Happiness lương thực tế theo residentID (chỉ tồn tại giữa EconomySystem và ResidentSocialSystem trong cùng tick)
    private static readonly Dictionary<int, float> RealWageModifier = new Dictionary<int, float>(256);

    private struct Household
    {
        public float netIncome;
        public int members;
        public int heated;      // Số người ở nhà được sưởi (không tính vô gia cư / trạm cách ly)
        public int wealth;
        public bool hasEarner;
    }

    private static readonly Dictionary<long, Household> Households = new Dictionary<long, Household>(128);
    private static readonly float[] NetIncome = new float[ResidentManager.MAX_RESIDENTS];
    private static readonly BuildingType[] TypeById = new BuildingType[ushort.MaxValue + 1];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetStatics()
    {
        LastReport = default;
        RealWageModifier.Clear();
    }

    public static bool TryGetRealWageModifier(int residentId, out float modifier) =>
        RealWageModifier.TryGetValue(residentId, out modifier);

    /// <summary>Có được trả lương tháng này không (có việc / Gate Clerk, không đình công).</summary>
    public static bool IsEarner(in ResidentData r, int gateClerkId) =>
        r.isAlive && !r.IsStriking
        && ResidentAssignmentRules.CanWork(in r)
        && (r.assignedWorkID >= 0 || (gateClerkId != 0 && r.residentID == gateClerkId));

    public static float GetGrossWage(in ResidentData r, WageLevel level) =>
        EconomyRules.GetBaseWage(r.professionType) * EconomyRules.GetWageMultiplier(level);

    private static long HouseholdKey(in ResidentData r, int index)
    {
        if (r.assignedHouseID >= 0) return (1L << 40) | (ushort)r.assignedHouseID;
        if (r.familyID != 0) return (2L << 40) | (uint)r.familyID;
        return (3L << 40) | (uint)index;
    }

    public static EconomyReport ProcessMonthly(
        ResidentData[] residents,
        int count,
        BuildingData[] buildings,
        int buildingCount,
        GlobalSystemManager global)
    {
        RealWageModifier.Clear();
        var report = new EconomyReport();
        if (residents == null || global == null)
        {
            LastReport = report;
            return report;
        }

        ref GlobalSystemData g = ref global.GetGlobalDataRef();
        FillBuildingTypes(buildings, buildingCount);
        report.hasTaxOffice = global.CurrentBuildingEffects.hasTaxOffice;

        int alive = 0;
        for (int i = 0; i < count; i++)
            if (residents[i].isAlive) alive++;
        if (alive == 0)
        {
            LastReport = report;
            return report;
        }
        float scale = g.totalPopulation > alive ? (float)g.totalPopulation / alive : 1f;
        bool winter = g.CurrentSeason == SeasonType.Winter;
        float taxRate = report.hasTaxOffice ? EconomyRules.GetIncomeTaxRate(g.incomeTaxLevel) : 0f;
        // GDD 6.6: tiền ăn và tiền sưởi hộ trả tính theo giá thị trường hiện tại
        float foodFee = EconomyRules.FoodFeePerPerson * MarketSystem.GetPriceRatio(ResourceType.Food, in g);
        float heatFee = EconomyRules.HeatFeePerPerson * MarketSystem.GetPriceRatio(ResourceType.Coal, in g);
        _foodFee = foodFee;
        _heatFee = heatFee;
        int clerkId = g.gateClerkResidentID;

        // 1. Lương phải trả (mẫu)
        float due = 0f;
        for (int i = 0; i < count; i++)
        {
            NetIncome[i] = 0f;
            if (IsEarner(in residents[i], clerkId))
                due += GetGrossWage(in residents[i], g.wageLevel);
        }

        float gold = g.treasuryGold;
        float dueMacro = due * scale;
        float ratio = dueMacro <= 0f ? 1f : Mathf.Clamp01(gold / dueMacro);
        float paidMacro = dueMacro * ratio;
        gold -= paidMacro;
        report.wagesDue = Mathf.RoundToInt(dueMacro);

        int employed = 0;
        for (int i = 0; i < count; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;
            bool earner = IsEarner(in r, clerkId);
            if (earner || r.IsStriking)
                employed++;
            if (!earner)
                continue;
            float gross = GetGrossWage(in r, g.wageLevel);
            float paid = gross * ratio;
            NetIncome[i] = paid;
            int owed = Mathf.CeilToInt(gross - paid - 0.001f);
            if (owed > 0)
            {
                r.wageDebt = (ushort)Mathf.Min(ushort.MaxValue, r.wageDebt + owed);
                report.newDebt += owed;
            }
        }

        // 2. Còn vàng thì trả nợ cũ, lần lượt từng người (trả hết nợ cho một người -> người đó đi làm lại)
        for (int i = 0; i < count && gold >= 1f; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive || r.wageDebt == 0)
                continue;
            float need = r.wageDebt * scale;
            float pay = Mathf.Min(need, gold);
            int paidSample = Mathf.FloorToInt(pay / scale + 0.001f);
            if (paidSample <= 0)
                break;
            gold -= paidSample * scale;
            r.wageDebt = (ushort)(r.wageDebt - paidSample);
            NetIncome[i] += paidSample;
            paidMacro += paidSample * scale;
        }
        report.wagesPaid = Mathf.RoundToInt(paidMacro);

        // 3. Thuế thu nhập khấu trừ tại nguồn
        float taxSample = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = NetIncome[i] * taxRate;
            NetIncome[i] -= t;
            taxSample += t;
        }
        gold += taxSample * scale;
        report.incomeTax = Mathf.RoundToInt(taxSample * scale);

        // 4. Hộ gia đình: gộp lương ròng, trả tiền ăn + sưởi
        Households.Clear();
        for (int i = 0; i < count; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;
            long key = HouseholdKey(in r, i);
            Households.TryGetValue(key, out Household h);
            h.members++;
            h.netIncome += NetIncome[i];
            h.wealth += r.wealth;
            if (NetIncome[i] > 0f) h.hasEarner = true;
            if (winter && IsHeatedHome(in r)) h.heated++;
            Households[key] = h;
        }

        float feesSample = 0f, subsidySample = 0f;
        var cutKeys = new HashSet<long>();
        var leftover = new Dictionary<long, float>(Households.Count);   // >0: dư cộng Wealth; <0: lấy từ Wealth
        foreach (var kv in Households)
        {
            Household h = kv.Value;
            float cost = h.members * foodFee + h.heated * heatFee;
            float fromIncome = Mathf.Min(cost, h.netIncome);
            float shortfall = cost - fromIncome;
            float fromWealth = Mathf.Min(shortfall, h.wealth);
            shortfall -= fromWealth;
            feesSample += fromIncome + fromWealth;
            leftover[kv.Key] = (h.netIncome - fromIncome) - fromWealth;

            if (shortfall > 0.001f)
            {
                if (g.feedPolicy == FeedPolicy.Subsidize)
                    subsidySample += shortfall;
                else
                    cutKeys.Add(kv.Key);
            }
        }
        gold += feesSample * scale;
        report.householdFees = Mathf.RoundToInt(feesSample * scale);
        report.subsidy = Mathf.RoundToInt(subsidySample * scale);
        report.cutHouseholds = cutKeys.Count;

        // 5. Ghi Wealth, cắt khẩu phần, lương thực tế; nợ lương -> đình công
        int cutPeople = 0;
        int marchers = 0;
        string strikeName = null;
        ProfessionType strikeProfession = ProfessionType.None;
        int newStrikers = 0;
        for (int i = 0; i < count; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;
            long key = HouseholdKey(in r, i);
            Household h = Households[key];

            float delta = leftover[key] / Mathf.Max(1, h.members);
            int next = r.wealth + (delta >= 0f ? Mathf.FloorToInt(delta) : -Mathf.CeilToInt(-delta));
            r.wealth = (byte)Mathf.Clamp(next, 0, ResidentSocialRules.MaxWealth);

            if (cutKeys.Contains(key))
            {
                cutPeople++;
                if (r.hungerMonths == 0)
                    r.hungerMonths = 1;   // Kho còn lương thực nhưng hộ không được ăn
            }

            // GDD 9.6: lương thực tế r = lương ròng hộ ÷ chi phí sinh hoạt (hộ không ai có lương: chỉ chịu phạt thất nghiệp / đói)
            float cost = h.members * foodFee + h.heated * heatFee;
            if (h.hasEarner && cost > 0f)
                RealWageModifier[r.residentID] = EconomyRules.GetRealWageModifier(h.netIncome / cost);

            // Nợ lương
            if (r.wageDebt > 0)
            {
                report.debtors++;
                report.totalDebt += r.wageDebt;
                if (r.debtMonths < byte.MaxValue) r.debtMonths++;
                if (r.debtMonths >= EconomyRules.StrikeAfterDebtMonths && r.assignedWorkID >= 0)
                {
                    if (r.strikeMonths == 0)
                    {
                        newStrikers++;
                        if (strikeName == null)
                        {
                            strikeName = NameDatabase.FirstNames[r.firstNameID % NameDatabase.FirstNames.Length];
                            strikeProfession = r.professionType;
                        }
                    }
                    if (r.strikeMonths < byte.MaxValue) r.strikeMonths++;
                    if (r.strikeMonths == EconomyRules.MarchAfterStrikeMonths) marchers++;
                }
            }
            else
            {
                r.debtMonths = 0;
                r.strikeMonths = 0;   // Trả hết nợ -> đi làm lại
            }
            if (r.IsStriking) report.strikers++;
        }
        report.totalDebt = Mathf.RoundToInt(report.totalDebt * scale);
        report.strikeRate = employed > 0 ? (float)report.strikers / employed : 0f;

        // Cắt khẩu phần: phần lương thực của hộ đó ở lại kho
        if (cutPeople > 0)
            g.stockFood += Mathf.RoundToInt(cutPeople * scale * GlobalSystemManager.GetFoodPerCapita(g.CurrentSeason));

        int goldAfter = Mathf.Max(0, Mathf.RoundToInt(gold - subsidySample * scale));
        report.netGold = goldAfter - g.treasuryGold;
        g.treasuryGold = goldAfter;

        // Đình công 2 tháng -> kéo lên Tòa thị chính: Riot +10 ngay
        if (marchers > 0)
            g.riotRiskMeter = Mathf.Min(100f, g.riotRiskMeter + EconomyRules.MarchRiot);

        LastReport = report;
        Notify(in report, newStrikers, strikeName, strikeProfession, marchers);
        return report;
    }

    private static bool IsHeatedHome(in ResidentData r)
    {
        if (r.assignedHouseID < 0)
            return false;
        BuildingType t = TypeById[(ushort)r.assignedHouseID];
        return t != BuildingType.QuarantineWard && t != BuildingType.None;
    }

    private static void FillBuildingTypes(BuildingData[] buildings, int buildingCount)
    {
        System.Array.Clear(TypeById, 0, TypeById.Length);
        if (buildings == null)
            return;
        for (int i = 0; i < buildingCount; i++)
            TypeById[buildings[i].buildingID] = buildings[i].buildingType;
    }

    private static void Notify(in EconomyReport r, int newStrikers, string name, ProfessionType profession, int marchers)
    {
        if (newStrikers > 0)
        {
            string who = $"{ResidentInfoPopup.GetProfessionName(profession)} {name}";
            NotificationFeed.Show(newStrikers > 1
                    ? $"{who} và {newStrikers - 1} người khác đình công vì nợ lương"
                    : $"{who} đình công vì nợ lương",
                NoticeLevel.Critical, "strike", TreasuryWindow.Open);
        }
        if (marchers > 0)
            NotificationFeed.Show($"Thợ đình công kéo lên Tòa thị chính: bạo loạn +{EconomyRules.MarchRiot:0}", NoticeLevel.Critical, "march", TreasuryWindow.Open);
        if (r.newDebt > 0)
            NotificationFeed.Show($"Nợ lương: kho vàng không đủ trả, còn nợ {r.totalDebt} vàng", NoticeLevel.Warning, "wage-debt", TreasuryWindow.Open);
        if (r.cutHouseholds > 0)
            NotificationFeed.Show($"{r.cutHouseholds} hộ không đủ tiền ăn bị cắt khẩu phần", NoticeLevel.Warning, "cut-rations", TreasuryWindow.Open);
    }
}
