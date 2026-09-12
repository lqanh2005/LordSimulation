public static class EdictRules
{
    public static bool TryGetSuggestedAction(
        EdictRuleData[] edicts,
        int edictCount,
        in ResidentData applicant,
        in GlobalSystemData global,
        out RuleAction action,
        out int edictIndex)
    {
        action = RuleAction.Admit;
        edictIndex = -1;
        if (edicts == null || edictCount <= 0)
            return false;

        int bestOrder = int.MaxValue;
        bool found = false;

        for (int i = 0; i < edictCount; i++)
        {
            ref EdictRuleData edict = ref edicts[i];
            if (!edict.isActive)
                continue;

            if (!Matches(in edict, in applicant, in global))
                continue;

            int order = edict.executionOrder;
            if (!found || order < bestOrder)
            {
                found = true;
                bestOrder = order;
                action = edict.ruleAction;
                edictIndex = i;
            }
        }

        return found;
    }

    public static bool Matches(in EdictRuleData edict, in ResidentData applicant, in GlobalSystemData global)
    {
        float value = edict.targetAttribute switch
        {
            TargetAttribute.BodyTemp => applicant.bodyTemperature,
            TargetAttribute.Wealth => applicant.wealth,
            TargetAttribute.OriginRegion => (byte)applicant.originRegion,
            TargetAttribute.Profession => (byte)applicant.professionType,
            TargetAttribute.CityFood => global.stockFood,
            _ => 0f
        };

        return edict.comparisonOp switch
        {
            ComparisonOp.GreaterThan => value > edict.targetValue,
            ComparisonOp.LessThan => value < edict.targetValue,
            ComparisonOp.Equal => UnityEngine.Mathf.Abs(value - edict.targetValue) < 0.0001f,
            ComparisonOp.NotEqual => UnityEngine.Mathf.Abs(value - edict.targetValue) >= 0.0001f,
            _ => false
        };
    }
}
