using System;
using System.IO;

public static class EdictDataSerializer
{
    public const int RuleFormatVersion = 4;

    public static void WriteEdict(BinaryWriter writer, in EdictRuleData data)
    {
        writer.Write(data.ruleID);
        writer.Write(data.ruleName ?? string.Empty);
        writer.Write(data.executionOrder);
        writer.Write(data.isActive);

        int count = Math.Min(data.ConditionCount, EdictRuleData.MaxConditions);
        writer.Write((byte)count);
        for (int i = 0; i < count; i++)
        {
            ref RuleCondition condition = ref data.conditions[i];
            writer.Write((byte)condition.link);
            writer.Write((byte)condition.variable);
            writer.Write((byte)condition.op);
            writer.Write(condition.value);
        }

        WriteAction(writer, in data.thenAction);
        WriteAction(writer, in data.elseAction);
    }

    public static void ReadEdict(BinaryReader reader, out EdictRuleData data)
    {
        ReadEdict(reader, out data, RuleFormatVersion);
    }

    public static void ReadEdict(BinaryReader reader, out EdictRuleData data, int saveVersion)
    {
        if (saveVersion < RuleFormatVersion)
        {
            ReadLegacyEdict(reader, out data);
            return;
        }

        data = new EdictRuleData
        {
            ruleID = reader.ReadUInt16(),
            ruleName = reader.ReadString(),
            executionOrder = reader.ReadByte(),
            isActive = reader.ReadBoolean(),
            conditions = new RuleCondition[EdictRuleData.MaxConditions]
        };

        int count = reader.ReadByte();
        if (count > EdictRuleData.MaxConditions)
            throw new InvalidDataException($"[EdictSerializer] conditionCount ({count}) vượt giới hạn ({EdictRuleData.MaxConditions}).");

        data.conditionCount = (byte)count;
        for (int i = 0; i < count; i++)
        {
            data.conditions[i] = new RuleCondition
            {
                link = (LogicalLink)reader.ReadByte(),
                variable = (RuleVariable)reader.ReadByte(),
                op = (ComparisonOp)reader.ReadByte(),
                value = reader.ReadSingle()
            };
        }

        data.thenAction = ReadAction(reader);
        data.elseAction = ReadAction(reader);
    }

    private static void WriteAction(BinaryWriter writer, in RuleActionData action)
    {
        writer.Write((byte)action.type);
        writer.Write(action.param0);
        writer.Write(action.param1);
    }

    private static RuleActionData ReadAction(BinaryReader reader)
    {
        return new RuleActionData
        {
            type = (RuleActionType)reader.ReadByte(),
            param0 = reader.ReadSingle(),
            param1 = reader.ReadSingle()
        };
    }

    private static void ReadLegacyEdict(BinaryReader reader, out EdictRuleData data)
    {
        byte ruleId = reader.ReadByte();
        byte executionOrder = reader.ReadByte();
        bool isActive = reader.ReadBoolean();
        TargetAttribute attribute = (TargetAttribute)reader.ReadByte();
        ComparisonOp op = (ComparisonOp)reader.ReadByte();
        float targetValue = reader.ReadSingle();
        reader.ReadByte();
        RuleAction ruleAction = (RuleAction)reader.ReadByte();
        float tariffPercent = reader.ReadSingle();
        byte quarantineDuration = reader.ReadByte();
        byte authorityCost = reader.ReadByte();
        reader.ReadSByte();

        RuleCondition[] conditions = new RuleCondition[EdictRuleData.MaxConditions];
        conditions[0] = new RuleCondition
        {
            link = LogicalLink.None,
            variable = attribute switch
            {
                TargetAttribute.BodyTemp => RuleVariable.ApplicantBodyTemp,
                TargetAttribute.Wealth => RuleVariable.ApplicantWealth,
                TargetAttribute.OriginRegion => RuleVariable.ApplicantOrigin,
                TargetAttribute.Profession => RuleVariable.ApplicantProfession,
                _ => RuleVariable.Food
            },
            op = op,
            value = targetValue
        };

        RuleActionData thenAction = ruleAction switch
        {
            RuleAction.Deny => new RuleActionData(RuleActionType.ImmigrationDeny),
            RuleAction.Quarantine => new RuleActionData(RuleActionType.ImmigrationQuarantine, quarantineDuration),
            RuleAction.EscortToDesk => new RuleActionData(RuleActionType.ImmigrationEscort, authorityCost),
            _ => new RuleActionData(RuleActionType.ImmigrationAdmit, tariffPercent)
        };

        data = new EdictRuleData
        {
            ruleID = ruleId,
            ruleName = $"Sắc lệnh {ruleId}",
            executionOrder = executionOrder,
            isActive = isActive,
            conditionCount = 1,
            conditions = conditions,
            thenAction = thenAction
        };
    }
}
