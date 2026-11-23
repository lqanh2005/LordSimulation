using UnityEngine;

public static class EdictRules
{
    /// <summary>Do ResearchManager gán: biến điều kiện đã được mở khoá chưa (null = mở hết).</summary>
    public static System.Func<RuleVariable, bool> IsVariableUnlocked;

    public static bool IsUnlocked(RuleVariable variable) => IsVariableUnlocked == null || IsVariableUnlocked(variable);

    /// <summary>Do ResearchManager gán: đã nghiên cứu Bộ luật chưa (null = mở sẵn, vd. khi test).</summary>
    public static System.Func<bool> AreEdictsUnlocked;

    public static bool EdictsUnlocked => AreEdictsUnlocked == null || AreEdictsUnlocked();

    // Project tắt Domain Reload: xoá hook còn sót từ lần Play trước
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsVariableUnlocked = null;
        AreEdictsUnlocked = null;
    }

    public const string EdictsLockedMessage = "Cần nghiên cứu <b>Bộ luật</b> (phím T) để ban hành Sắc lệnh.";

    private const float Epsilon = 0.0001f;

    // ===== Cái giá của sắc lệnh thành phố (Pillar 1) =====
    public const float TaxRiotPerGold = 0.05f;          // Thu thêm 50 vàng -> +2.5 bạo loạn / tháng
    public const float SubsidyRiotPerGold = 0.04f;      // Trợ cấp 50 vàng -> -2 bạo loạn / tháng
    public const int RiotReductionGoldPerPoint = 3;     // Xoa dịu 10 điểm -> 30 vàng / tháng
    public const int ReputationGoldPerPoint = 6;        // Uy tín +5 -> 30 vàng / tháng
    public const int ResourceGoldPerUnit = 2;           // Thêm tài nguyên mỗi tháng = mua bằng vàng
    public const float BoostRiotPerPercent = 0.05f;     // Tăng 20% sản xuất -> +1 bạo loạn / tháng

    /// <summary>Số vàng sắc lệnh phải trả mỗi tháng để có hiệu lực (0 nếu không tốn vàng).</summary>
    public static int GetMonthlyGoldCost(in RuleActionData action)
    {
        return action.type switch
        {
            RuleActionType.ReduceRiotRisk => Mathf.CeilToInt(Mathf.Max(0f, action.param0) * RiotReductionGoldPerPoint),
            RuleActionType.ModifyReputation => Mathf.CeilToInt(Mathf.Max(0f, action.param0) * ReputationGoldPerPoint),
            RuleActionType.AddResourceMonthly => Mathf.CeilToInt(Mathf.Max(0f, action.param1) * ResourceGoldPerUnit),
            RuleActionType.ModifyGoldMonthly => action.param0 < 0f ? Mathf.RoundToInt(-action.param0) : 0,
            _ => 0
        };
    }

    /// <summary>Mô tả ngắn cái giá của hành động, để hiện trên thẻ sắc lệnh.</summary>
    public static string DescribeCost(in RuleActionData action)
    {
        switch (action.type)
        {
            case RuleActionType.ModifyGoldMonthly:
                return action.param0 >= 0f
                    ? $"bạo loạn +{action.param0 * TaxRiotPerGold:0.#}/tháng"
                    : $"bạo loạn -{-action.param0 * SubsidyRiotPerGold:0.#}/tháng";
            case RuleActionType.BoostProduction:
                return action.param1 > 0f ? $"bạo loạn +{action.param1 * BoostRiotPerPercent:0.#}/tháng" : string.Empty;
            default:
                int gold = GetMonthlyGoldCost(in action);
                return gold > 0 ? $"-{gold} vàng/tháng" : string.Empty;
        }
    }

    public static bool IsApplicantVariable(RuleVariable variable)
    {
        return (byte)variable >= (byte)RuleVariable.ApplicantBodyTemp;
    }

    public static bool IsImmigrationAction(RuleActionType type)
    {
        return type >= RuleActionType.ImmigrationAdmit && type <= RuleActionType.ImmigrationEscort;
    }

    public static bool UsesApplicant(in EdictRuleData rule)
    {
        int count = rule.ConditionCount;
        for (int i = 0; i < count; i++)
        {
            if (IsApplicantVariable(rule.conditions[i].variable))
                return true;
        }

        return false;
    }

    public static bool HasImmigrationAction(in EdictRuleData rule)
    {
        return IsImmigrationAction(rule.thenAction.type) || IsImmigrationAction(rule.elseAction.type);
    }

    public static bool HasCityAction(in EdictRuleData rule)
    {
        return IsCityAction(rule.thenAction.type) || IsCityAction(rule.elseAction.type);
    }

    public static bool IsCityAction(RuleActionType type)
    {
        return type != RuleActionType.None && !IsImmigrationAction(type);
    }

    public static bool EvaluateConditions(
        in EdictRuleData rule,
        in GlobalSystemData global,
        in ResidentData applicant,
        bool hasApplicant)
    {
        int count = rule.ConditionCount;
        if (count == 0)
            return false;

        bool result = false;
        bool group = true;

        for (int i = 0; i < count; i++)
        {
            ref readonly RuleCondition condition = ref rule.conditions[i];
            bool matched = EvaluateCondition(in condition, in global, in applicant, hasApplicant);

            if (i > 0 && condition.link == LogicalLink.OR)
            {
                result |= group;
                group = matched;
            }
            else
            {
                group &= matched;
            }
        }

        return result | group;
    }

    public static bool EvaluateCondition(
        in RuleCondition condition,
        in GlobalSystemData global,
        in ResidentData applicant,
        bool hasApplicant)
    {
        if (IsApplicantVariable(condition.variable) && !hasApplicant)
            return false;
        if (!IsUnlocked(condition.variable))
            return false;

        float value = GetValue(condition.variable, in global, in applicant);
        return Compare(value, condition.op, condition.value);
    }

    public static RuleActionData Resolve(
        in EdictRuleData rule,
        in GlobalSystemData global,
        in ResidentData applicant,
        bool hasApplicant,
        out bool matched)
    {
        matched = EvaluateConditions(in rule, in global, in applicant, hasApplicant);
        return matched ? rule.thenAction : rule.elseAction;
    }

    public static float GetValue(RuleVariable variable, in GlobalSystemData global, in ResidentData applicant)
    {
        return variable switch
        {
            RuleVariable.Population => global.totalPopulation,
            RuleVariable.Food => global.stockFood,
            RuleVariable.FoodPerCapita => global.totalPopulation > 0
                ? (float)global.stockFood / global.totalPopulation
                : global.stockFood,
            RuleVariable.Wood => global.stockWood,
            RuleVariable.Coal => global.stockCoal,
            RuleVariable.Iron => global.stockIron,
            RuleVariable.Medicine => global.stockMedicine,
            RuleVariable.Gold => global.treasuryGold,
            RuleVariable.Authority => global.authorityPoints,
            RuleVariable.Happiness => global.overallHappiness,
            RuleVariable.Reputation => global.reputation,
            RuleVariable.RiotRisk => global.riotRiskMeter,
            RuleVariable.Infected => global.totalInfected,
            RuleVariable.InfectedPercent => global.totalPopulation > 0
                ? global.totalInfected * 100f / global.totalPopulation
                : 0f,
            RuleVariable.Temperature => global.environmentTemperature,
            RuleVariable.Month => global.currentMonth,
            RuleVariable.Season => (byte)global.CurrentSeason,
            RuleVariable.Weather => (byte)global.currentWeather,
            RuleVariable.ApplicantBodyTemp => applicant.bodyTemperature,
            RuleVariable.ApplicantWealth => applicant.wealth,
            RuleVariable.ApplicantOrigin => (byte)applicant.originRegion,
            RuleVariable.ApplicantProfession => (byte)applicant.professionType,
            _ => 0f
        };
    }

    public static bool Compare(float value, ComparisonOp op, float target)
    {
        return op switch
        {
            ComparisonOp.GreaterThan => value > target,
            ComparisonOp.LessThan => value < target,
            ComparisonOp.Equal => Mathf.Abs(value - target) < Epsilon,
            ComparisonOp.NotEqual => Mathf.Abs(value - target) >= Epsilon,
            ComparisonOp.GreaterOrEqual => value >= target - Epsilon,
            ComparisonOp.LessOrEqual => value <= target + Epsilon,
            _ => false
        };
    }

    /// <summary>
    /// GDD 3.5 Conflict Resolution: xét từ trên xuống, luật đầu tiên có điều kiện IF khớp hoàn chỉnh được chạy.
    /// Nhánh ELSE chỉ là phương án dự phòng: chỉ dùng khi KHÔNG luật nào khớp IF — khi đó lấy ELSE của luật
    /// ưu tiên cao nhất có ELSE (để ELSE không chặn các luật bên dưới).
    /// </summary>
    public static bool TryResolveImmigration(
        EdictRuleData[] edicts,
        int edictCount,
        in ResidentData applicant,
        in GlobalSystemData global,
        out RuleActionData action,
        out int edictIndex,
        out bool usedElse)
    {
        action = default;
        edictIndex = -1;
        usedElse = false;
        if (edicts == null || edictCount <= 0)
            return false;

        int bestThenOrder = int.MaxValue;
        int bestElseOrder = int.MaxValue;
        int elseIndex = -1;
        RuleActionData elseAction = default;

        for (int i = 0; i < edictCount; i++)
        {
            ref EdictRuleData edict = ref edicts[i];
            if (!edict.isActive || !HasImmigrationAction(in edict))
                continue;

            bool matched = EvaluateConditions(in edict, in global, in applicant, true);
            if (matched)
            {
                if (IsImmigrationAction(edict.thenAction.type) && edict.executionOrder < bestThenOrder)
                {
                    bestThenOrder = edict.executionOrder;
                    action = edict.thenAction;
                    edictIndex = i;
                }
            }
            else if (IsImmigrationAction(edict.elseAction.type) && edict.executionOrder < bestElseOrder)
            {
                bestElseOrder = edict.executionOrder;
                elseAction = edict.elseAction;
                elseIndex = i;
            }
        }

        if (edictIndex >= 0)
            return true;

        if (elseIndex >= 0)
        {
            action = elseAction;
            edictIndex = elseIndex;
            usedElse = true;
            return true;
        }

        return false;
    }

    public static bool TryGetSuggestedAction(
        EdictRuleData[] edicts,
        int edictCount,
        in ResidentData applicant,
        in GlobalSystemData global,
        out RuleAction action,
        out int edictIndex)
    {
        bool found = TryResolveImmigration(edicts, edictCount, in applicant, in global, out RuleActionData resolved, out edictIndex, out _);
        action = found ? ToRuleAction(resolved.type) : RuleAction.Admit;
        return found;
    }

    public static RuleAction ToRuleAction(RuleActionType type)
    {
        return type switch
        {
            RuleActionType.ImmigrationDeny => RuleAction.Deny,
            RuleActionType.ImmigrationQuarantine => RuleAction.Quarantine,
            RuleActionType.ImmigrationEscort => RuleAction.EscortToDesk,
            _ => RuleAction.Admit
        };
    }

    public static float GetTariffPercent(in RuleActionData action)
    {
        return action.type == RuleActionType.ImmigrationAdmit ? action.param0 : 0f;
    }
}
