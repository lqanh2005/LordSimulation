using System;

public enum TargetAttribute : byte
{
    BodyTemp = 0, Wealth = 1, OriginRegion = 2, Profession = 3, CityFood = 4
}

public enum ComparisonOp : byte
{
    GreaterThan = 0, LessThan = 1, Equal = 2, NotEqual = 3, GreaterOrEqual = 4, LessOrEqual = 5
}

public enum LogicalLink : byte { None = 0, AND = 1, OR = 2 }

public enum RuleAction : byte
{
    Admit = 0, Deny = 1, Quarantine = 2, EscortToDesk = 3
}

public enum RuleVariable : byte
{
    Population = 0,
    Food = 1,
    FoodPerCapita = 2,
    Wood = 3,
    Coal = 4,
    Iron = 5,
    Medicine = 6,
    Gold = 7,
    Authority = 8,
    Happiness = 9,
    Reputation = 10,
    RiotRisk = 11,
    Infected = 12,
    InfectedPercent = 13,
    Temperature = 14,
    Month = 15,
    Season = 16,
    Weather = 17,

    // Chỉ có giá trị khi xét người nhập cư
    ApplicantBodyTemp = 100,
    ApplicantWealth = 101,
    ApplicantOrigin = 102,
    ApplicantProfession = 103
}

public enum RuleActionType : byte
{
    None = 0,

    // Nhập cư, xét theo từng người
    ImmigrationAdmit = 1,       // p0 = thuế %
    ImmigrationDeny = 2,
    ImmigrationQuarantine = 3,  // p0 = số tháng cách ly
    ImmigrationEscort = 4,      // p0 = điểm quyền lực tiêu tốn

    // Thành phố, xét mỗi tháng
    BoostProduction = 10,     // p0 = BuildingType, p1 = % thay đổi (âm để giảm)
    AddResourceMonthly = 11,  // p0 = ResourceType, p1 = số lượng (âm để tiêu thụ)
    ModifyGoldMonthly = 12,   // p0 = số vàng
    ModifyReputation = 13,    // p0 = lượng uy tín
    ReduceRiotRisk = 14       // p0 = lượng giảm
}

[Serializable]
public struct RuleCondition
{
    public LogicalLink link;
    public RuleVariable variable;
    public ComparisonOp op;
    public float value;
}

[Serializable]
public struct RuleActionData
{
    public RuleActionType type;
    public float param0;
    public float param1;

    public bool IsNone => type == RuleActionType.None;

    public RuleActionData(RuleActionType type, float param0 = 0f, float param1 = 0f)
    {
        this.type = type;
        this.param0 = param0;
        this.param1 = param1;
    }
}

[Serializable]
public struct EdictRuleData
{
    public const int MaxConditions = 6;

    public ushort ruleID;
    public string ruleName;
    public byte executionOrder;
    public bool isActive;
    public byte conditionCount;
    public RuleCondition[] conditions;
    public RuleActionData thenAction;
    public RuleActionData elseAction;
    public bool lastResult;

    public int ConditionCount => conditions == null ? 0 : Math.Min(conditionCount, conditions.Length);
}
