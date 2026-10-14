using System;
using System.Collections.Generic;

public class EdictRuleBuilder
{
    private readonly List<RuleCondition> conditions = new List<RuleCondition>(EdictRuleData.MaxConditions);

    public string Name { get; set; }
    public RuleActionData Then { get; set; }
    public RuleActionData Else { get; set; }
    public bool StartActive { get; set; } = true;

    public int ConditionCount => conditions.Count;
    public IReadOnlyList<RuleCondition> Conditions => conditions;

    public EdictRuleBuilder(string name = null)
    {
        Name = name;
    }

    public static EdictRuleBuilder From(in EdictRuleData rule)
    {
        EdictRuleBuilder builder = new EdictRuleBuilder(rule.ruleName)
        {
            Then = rule.thenAction,
            Else = rule.elseAction,
            StartActive = rule.isActive
        };

        int count = rule.ConditionCount;
        for (int i = 0; i < count; i++)
        {
            builder.conditions.Add(rule.conditions[i]);
        }

        return builder;
    }

    public EdictRuleBuilder If(RuleVariable variable, ComparisonOp op, float value)
    {
        conditions.Clear();
        return AddCondition(LogicalLink.None, variable, op, value);
    }

    public EdictRuleBuilder And(RuleVariable variable, ComparisonOp op, float value)
    {
        return AddCondition(LogicalLink.AND, variable, op, value);
    }

    public EdictRuleBuilder Or(RuleVariable variable, ComparisonOp op, float value)
    {
        return AddCondition(LogicalLink.OR, variable, op, value);
    }

    public EdictRuleBuilder AddCondition(LogicalLink link, RuleVariable variable, ComparisonOp op, float value)
    {
        if (conditions.Count >= EdictRuleData.MaxConditions)
        {
            return this;
        }

        conditions.Add(new RuleCondition
        {
            link = conditions.Count == 0 ? LogicalLink.None : (link == LogicalLink.None ? LogicalLink.AND : link),
            variable = variable,
            op = op,
            value = value
        });
        return this;
    }

    public void SetCondition(int index, LogicalLink link, RuleVariable variable, ComparisonOp op, float value)
    {
        if ((uint)index >= (uint)conditions.Count)
        {
            return;
        }

        conditions[index] = new RuleCondition
        {
            link = index == 0 ? LogicalLink.None : (link == LogicalLink.None ? LogicalLink.AND : link),
            variable = variable,
            op = op,
            value = value
        };
    }

    public void RemoveCondition(int index)
    {
        if ((uint)index >= (uint)conditions.Count)
        {
            return;
        }

        conditions.RemoveAt(index);

        if (conditions.Count > 0 && conditions[0].link != LogicalLink.None)
        {
            RuleCondition first = conditions[0];
            first.link = LogicalLink.None;
            conditions[0] = first;
        }
    }

    public EdictRuleBuilder ThenDo(RuleActionType type, float param0 = 0f, float param1 = 0f)
    {
        Then = new RuleActionData(type, param0, param1);
        return this;
    }

    public EdictRuleBuilder ElseDo(RuleActionType type, float param0 = 0f, float param1 = 0f)
    {
        Else = new RuleActionData(type, param0, param1);
        return this;
    }

    public bool IsValid(out string error)
    {
        if (conditions.Count == 0)
        {
            error = "Luật cần ít nhất 1 điều kiện.";
            return false;
        }

        if (Then.IsNone && Else.IsNone)
        {
            error = "Luật cần có hành động Then hoặc Else.";
            return false;
        }

        bool usesApplicant = false;
        foreach (RuleCondition condition in conditions)
        {
            usesApplicant |= EdictRules.IsApplicantVariable(condition.variable);
        }

        if (usesApplicant && (EdictRules.IsCityAction(Then.type) || EdictRules.IsCityAction(Else.type)))
        {
            error = "Điều kiện về người nhập cư chỉ dùng được với hành động nhập cư.";
            return false;
        }

        error = null;
        return true;
    }

    public EdictRuleData Build()
    {
        RuleCondition[] array = new RuleCondition[EdictRuleData.MaxConditions];
        int count = Math.Min(conditions.Count, EdictRuleData.MaxConditions);
        for (int i = 0; i < count; i++)
        {
            array[i] = conditions[i];
        }

        return new EdictRuleData
        {
            ruleName = string.IsNullOrWhiteSpace(Name) ? EdictRuleCatalog.GetActionName(Then.type) : Name,
            isActive = StartActive,
            conditionCount = (byte)count,
            conditions = array,
            thenAction = Then,
            elseAction = Else
        };
    }
}
