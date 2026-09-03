using System.IO;

public static class EdictDataSerializer
{
    public static void WriteEdict(BinaryWriter writer, in EdictRuleData data)
    {
        writer.Write(data.ruleID);
        writer.Write(data.executionOrder);
        writer.Write(data.isActive);
        writer.Write((byte)data.targetAttribute);
        writer.Write((byte)data.comparisonOp);
        writer.Write(data.targetValue);
        writer.Write((byte)data.logicalLink);
        writer.Write((byte)data.ruleAction);
        writer.Write(data.tariffPercent);
        writer.Write(data.quarantineDuration);
        writer.Write(data.authorityCost);
        writer.Write(data.discontentModifier);
    }

    public static void ReadEdict(BinaryReader reader, out EdictRuleData data)
    {
        data = new EdictRuleData
        {
            ruleID = reader.ReadByte(),
            executionOrder = reader.ReadByte(),
            isActive = reader.ReadBoolean(),
            targetAttribute = (TargetAttribute)reader.ReadByte(),
            comparisonOp = (ComparisonOp)reader.ReadByte(),
            targetValue = reader.ReadSingle(),
            logicalLink = (LogicalLink)reader.ReadByte(),
            ruleAction = (RuleAction)reader.ReadByte(),
            tariffPercent = reader.ReadSingle(),
            quarantineDuration = reader.ReadByte(),
            authorityCost = reader.ReadByte(),
            discontentModifier = reader.ReadSByte()
        };
    }
}