using System.IO;

public static class ResearchDataSerializer
{
    public static void WriteResearch(BinaryWriter writer, in ResearchSaveData data)
    {
        writer.Write(data.completedMask);
        writer.Write((byte)data.currentResearch);
        writer.Write(data.currentProgress);
    }

    public static void ReadResearch(BinaryReader reader, out ResearchSaveData data)
    {
        data = new ResearchSaveData
        {
            completedMask = reader.ReadUInt64(),
            currentResearch = (ResearchID)reader.ReadByte(),
            currentProgress = reader.ReadSingle()
        };
    }
}
