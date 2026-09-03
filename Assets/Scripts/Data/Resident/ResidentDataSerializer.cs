using System.IO;

public static class ResidentDataSerializer
{
    public static void WriteResident(BinaryWriter writer, in ResidentData residentData)
    {
        writer.Write(residentData.residentID);
        writer.Write(residentData.firstNameID);
            writer.Write(residentData.lastNameID);
            writer.Write(residentData.age);
            writer.Write((byte)residentData.originRegion);
            writer.Write((byte)residentData.factionType);
            writer.Write(residentData.wealth);
            writer.Write((byte)residentData.professionType);
            writer.Write((byte)residentData.healthStatus);
            writer.Write((byte)residentData.diseaseType);
            writer.Write(residentData.IncubationMonths);
            writer.Write(residentData.happiness);
            writer.Write(residentData.bodyTemperature);
            writer.Write((byte)residentData.symptoms);
            writer.Write(residentData.assignedHouseID);
            writer.Write(residentData.assignedWorkID);
            writer.Write(residentData.isAlive);
            writer.Write(residentData.strength);
            writer.Write(residentData.endurance);
            writer.Write(residentData.intellect);
        
    }
    public static void ReadResident(BinaryReader reader, out ResidentData residentData)
    {
        residentData = new ResidentData();
        residentData.residentID = reader.ReadInt32();
            residentData.firstNameID = reader.ReadUInt16();
            residentData.lastNameID = reader.ReadUInt16();
            residentData.age = reader.ReadByte();
            residentData.originRegion = (OriginRegion)reader.ReadByte();
            residentData.factionType = (FactionType)reader.ReadByte();
            residentData.wealth = reader.ReadByte();
            residentData.professionType = (ProfessionType)reader.ReadByte();
            residentData.healthStatus = (HealthStatus)reader.ReadByte();
            residentData.diseaseType = (DiseaseType)reader.ReadByte();
            residentData.IncubationMonths = reader.ReadByte();
            residentData.happiness = reader.ReadSingle();
            residentData.bodyTemperature = reader.ReadSingle();
            residentData.symptoms = (SymptomFlags)reader.ReadByte();
            residentData.assignedHouseID = reader.ReadInt16();
            residentData.assignedWorkID = reader.ReadInt16();
            residentData.isAlive = reader.ReadBoolean();
            residentData.strength = reader.ReadByte();
            residentData.endurance = reader.ReadByte();
    residentData.intellect = reader.ReadByte();
    }
}
