using System.Text;

public static class EdictRuleCatalog
{
    public static readonly RuleVariable[] CityVariables =
    {
        RuleVariable.Population, RuleVariable.Food, RuleVariable.FoodPerCapita, RuleVariable.Wood,
        RuleVariable.Coal, RuleVariable.Iron, RuleVariable.Medicine, RuleVariable.Gold,
        RuleVariable.Authority, RuleVariable.Happiness, RuleVariable.Reputation, RuleVariable.RiotRisk,
        RuleVariable.Infected, RuleVariable.InfectedPercent, RuleVariable.Temperature, RuleVariable.Month,
        RuleVariable.Season, RuleVariable.Weather
    };

    public static readonly RuleVariable[] ApplicantVariables =
    {
        RuleVariable.ApplicantBodyTemp, RuleVariable.ApplicantWealth,
        RuleVariable.ApplicantOrigin, RuleVariable.ApplicantProfession
    };

    public static readonly ComparisonOp[] Operators =
    {
        ComparisonOp.LessThan, ComparisonOp.LessOrEqual, ComparisonOp.Equal,
        ComparisonOp.NotEqual, ComparisonOp.GreaterOrEqual, ComparisonOp.GreaterThan
    };

    public static readonly RuleActionType[] CityActions =
    {
        RuleActionType.BoostProduction, RuleActionType.AddResourceMonthly, RuleActionType.ModifyGoldMonthly,
        RuleActionType.ModifyReputation, RuleActionType.ReduceRiotRisk
    };

    public static readonly RuleActionType[] ImmigrationActions =
    {
        RuleActionType.ImmigrationAdmit, RuleActionType.ImmigrationDeny,
        RuleActionType.ImmigrationQuarantine, RuleActionType.ImmigrationEscort
    };

    public static string GetVariableName(RuleVariable variable)
    {
        return variable switch
        {
            RuleVariable.Population => "Dân số",
            RuleVariable.Food => "Lương thực",
            RuleVariable.FoodPerCapita => "Lương thực / người",
            RuleVariable.Wood => "Gỗ",
            RuleVariable.Coal => "Than",
            RuleVariable.Iron => "Sắt",
            RuleVariable.Medicine => "Thuốc",
            RuleVariable.Gold => "Vàng",
            RuleVariable.Authority => "Quyền lực",
            RuleVariable.Happiness => "Hạnh phúc",
            RuleVariable.Reputation => "Uy tín",
            RuleVariable.RiotRisk => "Nguy cơ bạo loạn",
            RuleVariable.Infected => "Số người nhiễm bệnh",
            RuleVariable.InfectedPercent => "Tỉ lệ nhiễm bệnh (%)",
            RuleVariable.Temperature => "Nhiệt độ",
            RuleVariable.Month => "Tháng",
            RuleVariable.Season => "Mùa",
            RuleVariable.Weather => "Thời tiết",
            RuleVariable.ApplicantBodyTemp => "Thân nhiệt người nhập cư",
            RuleVariable.ApplicantWealth => "Tài sản người nhập cư",
            RuleVariable.ApplicantOrigin => "Vùng xuất thân",
            RuleVariable.ApplicantProfession => "Nghề nghiệp",
            _ => variable.ToString()
        };
    }

    public static string GetOperatorSymbol(ComparisonOp op)
    {
        return op switch
        {
            ComparisonOp.GreaterThan => ">",
            ComparisonOp.LessThan => "<",
            ComparisonOp.Equal => "=",
            ComparisonOp.NotEqual => "≠",
            ComparisonOp.GreaterOrEqual => "≥",
            ComparisonOp.LessOrEqual => "≤",
            _ => "?"
        };
    }

    public static string GetLinkName(LogicalLink link)
    {
        return link switch
        {
            LogicalLink.AND => "And",
            LogicalLink.OR => "Or",
            _ => "If"
        };
    }

    public static string GetActionName(RuleActionType type)
    {
        return type switch
        {
            RuleActionType.None => "Không làm gì",
            RuleActionType.ImmigrationAdmit => "Cho nhập cư",
            RuleActionType.ImmigrationDeny => "Từ chối nhập cư",
            RuleActionType.ImmigrationQuarantine => "Cách ly người nhập cư",
            RuleActionType.ImmigrationEscort => "Áp giải tới bàn thẩm vấn",
            RuleActionType.BoostProduction => "Thay đổi sản xuất",
            RuleActionType.AddResourceMonthly => "Thêm/bớt tài nguyên mỗi tháng",
            RuleActionType.ModifyGoldMonthly => "Thêm/bớt vàng mỗi tháng",
            RuleActionType.ModifyReputation => "Thay đổi uy tín",
            RuleActionType.ReduceRiotRisk => "Giảm nguy cơ bạo loạn",
            _ => type.ToString()
        };
    }

    public static string GetBuildingName(BuildingType type)
    {
        return type switch
        {
            BuildingType.House => "nhà ở",
            BuildingType.QuarantineWard => "khu cách ly",
            BuildingType.Farm => "nông nghiệp",
            BuildingType.Mine => "khai mỏ",
            BuildingType.Clinic => "y tế",
            BuildingType.Furnace => "lò sưởi",
            BuildingType.School => "giáo dục",
            BuildingType.GuardPost => "trạm gác",
            BuildingType.LumberCamp => "khai thác gỗ",
            BuildingType.Tent => "lều tạm",
            _ => type.ToString()
        };
    }

    public static string GetResourceName(ResourceType type)
    {
        return type switch
        {
            ResourceType.Food => "lương thực",
            ResourceType.Wood => "gỗ",
            ResourceType.Iron => "sắt",
            ResourceType.Coal => "than",
            ResourceType.Medicine => "thuốc men",
            ResourceType.Machinery => "máy móc",
            _ => type.ToString()
        };
    }

    public static int GetParamLabels(RuleActionType type, out string param0Label, out string param1Label)
    {
        param0Label = null;
        param1Label = null;

        switch (type)
        {
            case RuleActionType.ImmigrationAdmit:
                param0Label = "Thuế (%)";
                return 1;
            case RuleActionType.ImmigrationQuarantine:
                param0Label = "Số tháng cách ly";
                return 1;
            case RuleActionType.ImmigrationEscort:
                param0Label = "Điểm quyền lực";
                return 1;
            case RuleActionType.BoostProduction:
                param0Label = "Công trình";
                param1Label = "Thay đổi (%)";
                return 2;
            case RuleActionType.AddResourceMonthly:
                param0Label = "Tài nguyên";
                param1Label = "Số lượng";
                return 2;
            case RuleActionType.ModifyGoldMonthly:
                param0Label = "Số vàng";
                return 1;
            case RuleActionType.ModifyReputation:
                param0Label = "Uy tín";
                return 1;
            case RuleActionType.ReduceRiotRisk:
                param0Label = "Lượng giảm";
                return 1;
            default:
                return 0;
        }
    }

    public static string DescribeCondition(in RuleCondition condition, bool isFirst)
    {
        string link = isFirst ? "If" : GetLinkName(condition.link);
        return $"{link}\t{GetVariableName(condition.variable)} {GetOperatorSymbol(condition.op)} {FormatNumber(condition.value)}";
    }

    public static string DescribeAction(in RuleActionData action)
    {
        return action.type switch
        {
            RuleActionType.ImmigrationAdmit when action.param0 > 0f => $"Cho nhập cư (thuế {FormatNumber(action.param0)}%)",
            RuleActionType.ImmigrationQuarantine => $"Cách ly {FormatNumber(action.param0)} tháng",
            RuleActionType.ImmigrationEscort => $"Áp giải tới bàn thẩm vấn (-{FormatNumber(action.param0)} quyền lực)",
            RuleActionType.BoostProduction => $"{(action.param1 >= 0f ? "Tăng" : "Giảm")} sản xuất {GetBuildingName((BuildingType)(byte)action.param0)} {FormatNumber(System.Math.Abs(action.param1))}%",
            RuleActionType.AddResourceMonthly => $"{(action.param1 >= 0f ? "Thêm" : "Tiêu thụ")} {FormatNumber(System.Math.Abs(action.param1))} {GetResourceName((ResourceType)(byte)action.param0)} mỗi tháng",
            RuleActionType.ModifyGoldMonthly => $"{(action.param0 >= 0f ? "Thu" : "Chi")} {FormatNumber(System.Math.Abs(action.param0))} vàng mỗi tháng",
            RuleActionType.ModifyReputation => $"{(action.param0 >= 0f ? "Tăng" : "Giảm")} uy tín {FormatNumber(System.Math.Abs(action.param0))}",
            RuleActionType.ReduceRiotRisk => $"Giảm nguy cơ bạo loạn {FormatNumber(action.param0)}",
            _ => GetActionName(action.type)
        };
    }

    public static string Describe(in EdictRuleData rule)
    {
        StringBuilder sb = new StringBuilder();
        int count = rule.ConditionCount;

        for (int i = 0; i < count; i++)
        {
            sb.AppendLine(DescribeCondition(in rule.conditions[i], i == 0));
        }

        sb.Append("Then\t").Append(DescribeAction(in rule.thenAction));

        if (!rule.elseAction.IsNone)
        {
            sb.AppendLine().Append("Else\t").Append(DescribeAction(in rule.elseAction));
        }

        return sb.ToString();
    }

    private static string FormatNumber(float value)
    {
        return value.ToString(value % 1f == 0f ? "0" : "0.##");
    }
}
