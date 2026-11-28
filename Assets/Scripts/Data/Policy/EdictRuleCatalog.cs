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

    /// <summary>GDD v0.3 mục 8.2: các trường xét gia đình (theo thứ tự trong bảng của GDD).</summary>
    public static readonly RuleVariable[] ApplicantVariables =
    {
        RuleVariable.ApplicantProfession, RuleVariable.ApplicantOrigin, RuleVariable.FamilyHasNote,
        RuleVariable.FamilyNoHealthNote, RuleVariable.FamilySize, RuleVariable.FamilyWorkers,
        RuleVariable.FamilyHasChildOrElderly, RuleVariable.FamilyWealthClass
    };

    private static readonly ComparisonOp[] EqualityOps = { ComparisonOp.Equal, ComparisonOp.NotEqual };
    private static readonly ComparisonOp[] EqualOnly = { ComparisonOp.Equal };
    private static readonly ComparisonOp[] RangeOps = { ComparisonOp.GreaterOrEqual, ComparisonOp.LessOrEqual };

    /// <summary>Phép so sánh chọn được cho từng trường (GDD 8.2). Trường thành phố dùng đủ 6 phép.</summary>
    public static ComparisonOp[] GetAllowedOperators(RuleVariable variable) => variable switch
    {
        RuleVariable.ApplicantProfession or RuleVariable.ApplicantOrigin or RuleVariable.FamilyHasNote => EqualityOps,
        RuleVariable.FamilyNoHealthNote or RuleVariable.FamilyHasChildOrElderly or RuleVariable.FamilyWealthClass => EqualOnly,
        RuleVariable.FamilySize or RuleVariable.FamilyWorkers => RangeOps,
        _ => Operators
    };

    private static readonly ProfessionType[] ProfessionChoices =
    {
        ProfessionType.Farmer, ProfessionType.Miner, ProfessionType.Lumberjack, ProfessionType.Craftsman,
        ProfessionType.Doctor, ProfessionType.Teacher, ProfessionType.Guard, ProfessionType.None
    };

    /// <summary>
    /// Danh sách giá trị chọn được (nhãn, giá trị lưu) — mọi phần của rule chọn từ danh sách, không gõ tay.
    /// Trả null với trường thành phố (nhập số). Note chỉ liệt kê những note hồ sơ hiện tại đã mở.
    /// </summary>
    public static System.Collections.Generic.List<(string label, float value)> GetValueOptions(RuleVariable variable)
    {
        var list = new System.Collections.Generic.List<(string, float)>();
        switch (variable)
        {
            case RuleVariable.ApplicantProfession:
                foreach (ProfessionType p in ProfessionChoices)
                    list.Add((ResidentInfoPopup.GetProfessionName(p), (byte)p));
                return list;
            case RuleVariable.ApplicantOrigin:
                list.Add((ImmigrationRules.OriginLabel(OriginRegion.GreenZone), (byte)OriginRegion.GreenZone));
                list.Add((ImmigrationRules.OriginLabel(OriginRegion.YellowZone), (byte)OriginRegion.YellowZone));
                list.Add((ImmigrationRules.OriginLabel(OriginRegion.RedZone), (byte)OriginRegion.RedZone));
                return list;
            case RuleVariable.FamilyHasNote:
            {
                byte level = FamilyRules.CurrentProfileLevel;
                for (int i = 0; i < FamilyRules.AllNotes.Length; i++)
                    if (FamilyRules.IsNoteVisible(FamilyRules.AllNotes[i], level))
                        list.Add((FamilyRules.NoteLabel(FamilyRules.AllNotes[i]), i));
                return list;
            }
            case RuleVariable.FamilyNoHealthNote:
            case RuleVariable.FamilyHasChildOrElderly:
                list.Add(("Có", 1f));
                list.Add(("Không", 0f));
                return list;
            case RuleVariable.FamilySize:
                for (int i = 1; i <= 5; i++) list.Add((i.ToString(), i));
                return list;
            case RuleVariable.FamilyWorkers:
                for (int i = 0; i <= 5; i++) list.Add((i.ToString(), i));
                return list;
            case RuleVariable.FamilyWealthClass:
                for (int i = FamilyRules.WealthPoor; i <= FamilyRules.WealthRich; i++)
                    list.Add((FamilyRules.WealthClassLabel(i), i));
                return list;
            default:
                return null;
        }
    }

    /// <summary>Nhãn của giá trị điều kiện (vd. "Sốt", "Vùng Đỏ", "Có").</summary>
    public static string FormatConditionValue(RuleVariable variable, float value)
    {
        switch (variable)
        {
            case RuleVariable.ApplicantProfession:
                return ResidentInfoPopup.GetProfessionName((ProfessionType)(byte)UnityEngine.Mathf.RoundToInt(value));
            case RuleVariable.ApplicantOrigin:
                return ImmigrationRules.OriginLabel((OriginRegion)(byte)UnityEngine.Mathf.RoundToInt(value));
            case RuleVariable.FamilyHasNote:
                int idx = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(value), 0, FamilyRules.AllNotes.Length - 1);
                return FamilyRules.NoteLabel(FamilyRules.AllNotes[idx]);
            case RuleVariable.FamilyNoHealthNote:
            case RuleVariable.FamilyHasChildOrElderly:
                return value >= 0.5f ? "Có" : "Không";
            case RuleVariable.FamilyWealthClass:
                return FamilyRules.WealthClassLabel(UnityEngine.Mathf.RoundToInt(value));
            default:
                return FormatNumber(value);
        }
    }

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
        RuleActionType.ImmigrationQuarantine, RuleActionType.ImmigrationManualReview
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
            RuleVariable.ApplicantBodyTemp => "Thân nhiệt cao nhất trong nhà",
            RuleVariable.ApplicantWealth => "Tài sản trung bình mỗi người",
            RuleVariable.ApplicantOrigin => "Nguồn gốc",
            RuleVariable.ApplicantProfession => "Có thành viên làm nghề",
            RuleVariable.FamilyHasNote => "Có thành viên có note",
            RuleVariable.FamilyNoHealthNote => "Không có note sức khoẻ",
            RuleVariable.FamilySize => "Số người trong gia đình",
            RuleVariable.FamilyWorkers => "Số người làm được việc",
            RuleVariable.FamilyHasChildOrElderly => "Có trẻ em / người già",
            RuleVariable.FamilyWealthClass => EdictRules.IsUnlocked(RuleVariable.FamilyWealthClass)
                ? "Wealth"
                : "Wealth (cần nghiên cứu Khám sơ bộ)",
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

    /// <summary>Nhãn tiếng Việt cho chip trong bảng tạo luật: Nếu / Và / Hoặc.</summary>
    public static string GetLinkLabel(LogicalLink link)
    {
        return link switch
        {
            LogicalLink.AND => "Và",
            LogicalLink.OR => "Hoặc",
            _ => "Nếu"
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
            RuleActionType.ImmigrationAdmit => "ADMIT — Cho vào",
            RuleActionType.ImmigrationDeny => "DENY — Từ chối",
            RuleActionType.ImmigrationQuarantine => "QUARANTINE — Cách ly",
            RuleActionType.ImmigrationManualReview => "MANUAL REVIEW — Duyệt tay",
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
            BuildingType.LargeHouse => "nhà ở lớn",
            BuildingType.Forge => "lò rèn",
            BuildingType.QuarantineWard => "khu cách ly",
            BuildingType.Farm => "nông nghiệp",
            BuildingType.Mine => "khai mỏ",
            BuildingType.Clinic => "y tế",
            BuildingType.Furnace => "lò bào chế",
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
                return 0;   // GDD 6.4: thuế nhập thành là mức chung chỉnh ở màn Cổng, sắc lệnh không đặt thuế riêng
            case RuleActionType.ImmigrationQuarantine:
                param0Label = "Số tháng cách ly";
                return 1;
            case RuleActionType.ImmigrationManualReview:
                return 0;
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
        if (condition.variable == RuleVariable.FamilyNoHealthNote)
            return $"{link}\t{GetVariableName(condition.variable)}{(condition.value >= 0.5f ? "" : " = Không")}";
        return $"{link}\t{GetVariableName(condition.variable)} {GetOperatorSymbol(condition.op)} {FormatConditionValue(condition.variable, condition.value)}";
    }

    public static string DescribeAction(in RuleActionData action)
    {
        string text = DescribeActionCore(in action);
        string cost = EdictRules.DescribeCost(in action);
        return string.IsNullOrEmpty(cost) ? text : $"{text} ({cost})";
    }

    private static string DescribeActionCore(in RuleActionData action)
    {
        return action.type switch
        {
            RuleActionType.ImmigrationQuarantine => $"Cách ly {FormatNumber(action.param0)} tháng",
            RuleActionType.BoostProduction => $"{(action.param1 >= 0f ? "Tăng" : "Giảm")} sản xuất {GetBuildingName((BuildingType)(byte)action.param0)} {FormatNumber(System.Math.Abs(action.param1))}%",
            RuleActionType.AddResourceMonthly => $"{(action.param1 >= 0f ? "Thêm" : "Tiêu thụ")} {FormatNumber(System.Math.Abs(action.param1))} {GetResourceName((ResourceType)(byte)action.param0)} mỗi tháng",
            RuleActionType.ModifyGoldMonthly => $"{(action.param0 >= 0f ? "Thu" : "Chi")} {FormatNumber(System.Math.Abs(action.param0))} vàng mỗi tháng",
            RuleActionType.ModifyReputation => $"{(action.param0 >= 0f ? "Tăng" : "Giảm")} uy tín {FormatNumber(System.Math.Abs(action.param0))}",
            RuleActionType.ReduceRiotRisk => $"Giảm nguy cơ bạo loạn {FormatNumber(action.param0)}",
            _ => GetActionName(action.type)
        };
    }

    /// <summary>
    /// GDD 8.4: hiện ngoặc tự động để người chơi thấy rule được hiểu thế nào — A HOẶC B VÀ C = A HOẶC (B VÀ C).
    /// </summary>
    public static string DescribeLogic(in EdictRuleData rule)
    {
        int count = rule.ConditionCount;
        if (count == 0)
            return string.Empty;

        var groups = new System.Collections.Generic.List<System.Collections.Generic.List<string>>();
        groups.Add(new System.Collections.Generic.List<string>());
        for (int i = 0; i < count; i++)
        {
            RuleCondition c = rule.conditions[i];
            if (i > 0 && c.link == LogicalLink.OR)
                groups.Add(new System.Collections.Generic.List<string>());
            string text = DescribeCondition(in c, true);
            int tab = text.IndexOf('\t');
            groups[groups.Count - 1].Add(tab >= 0 ? text.Substring(tab + 1) : text);
        }

        var sb = new StringBuilder();
        for (int g = 0; g < groups.Count; g++)
        {
            if (g > 0) sb.Append(" HOẶC ");
            bool paren = groups.Count > 1 && groups[g].Count > 1;
            if (paren) sb.Append('(');
            sb.Append(string.Join(" VÀ ", groups[g]));
            if (paren) sb.Append(')');
        }
        return sb.ToString();
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
