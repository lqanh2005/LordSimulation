using System;

public enum TargetAttribute : byte
{
    BodyTemp = 0, Wealth = 1, OriginRegion = 2, Profession = 3, CityFood = 4
}

public enum ComparisonOp : byte
{
    GreaterThan = 0, LessThan = 1, Equal = 2, NotEqual = 3
}

public enum LogicalLink : byte { None = 0, AND = 1, OR = 2 }

public enum RuleAction : byte
{
    Admit = 0, Deny = 1, Quarantine = 2, EscortToDesk = 3
}

[Serializable]
public struct EdictRuleData
{
    public byte ruleID;
    public byte executionOrder;
    public bool isActive;
    public TargetAttribute targetAttribute;
    public ComparisonOp comparisonOp;
    public float targetValue;
    public LogicalLink logicalLink;
    public RuleAction ruleAction;
    public float tariffPercent;  // Phần trăm thuế áp dụng nếu ruleAction là Admit
    public byte quarantineDuration;  // Thời gian cách ly áp dụng nếu ruleAction là Quarantine
    public byte authorityCost;  // Chi phí hành chính áp dụng nếu ruleAction là EscortToDesk
    public sbyte discontentModifier;
}