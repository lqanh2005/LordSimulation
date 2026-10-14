using UnityEngine;

public static class EdictRules
{
    private const float Epsilon = 0.0001f;

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

    public static bool TryResolveImmigration(
        EdictRuleData[] edicts,
        int edictCount,
        in ResidentData applicant,
        in GlobalSystemData global,
        out RuleActionData action,
        out int edictIndex)
    {
        action = default;
        edictIndex = -1;
        if (edicts == null || edictCount <= 0)
            return false;

        int bestOrder = int.MaxValue;

        for (int i = 0; i < edictCount; i++)
        {
            ref EdictRuleData edict = ref edicts[i];
            if (!edict.isActive || !HasImmigrationAction(in edict))
                continue;

            RuleActionData resolved = Resolve(in edict, in global, in applicant, true, out _);
            if (!IsImmigrationAction(resolved.type))
                continue;

            if (edict.executionOrder < bestOrder)
            {
                bestOrder = edict.executionOrder;
                action = resolved;
                edictIndex = i;
            }
        }

        return edictIndex >= 0;
    }

    public static bool TryGetSuggestedAction(
        EdictRuleData[] edicts,
        int edictCount,
        in ResidentData applicant,
        in GlobalSystemData global,
        out RuleAction action,
        out int edictIndex)
    {
        bool found = TryResolveImmigration(edicts, edictCount, in applicant, in global, out RuleActionData resolved, out edictIndex);
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
