using System.IO;

public static class BuildingDataSerializer
{
    // Ghi 1 Struct BuildingData vào luồng nhị phân
    public static void WriteBuilding(BinaryWriter writer, in BuildingData data)
    {
        writer.Write(data.buildingID);
        writer.Write((byte)data.buildingType);
        writer.Write(data.coordX);
        writer.Write(data.coordY);
        writer.Write(data.sizeFootprint);
        writer.Write(data.tierLevel);
        writer.Write((byte)data.buildingState);
        writer.Write(data.constructionProgress);
        writer.Write(data.priorityLevel);
        writer.Write(data.isOperational);
        writer.Write(data.isAllocated);
        writer.Write(data.currentOccupancy);
        writer.Write(data.maxOccupancy);
        writer.Write(data.currentWorkers);
        writer.Write(data.maxWorkers);
        writer.Write(data.efficiency);
        writer.Write(data.productionProgress);
        writer.Write(data.fuelStored);
        writer.Write(data.outputStored);
        writer.Write(data.durability);
        writer.Write(data.insulationValue);
        writer.Write(data.contamination);
        writer.Write(data.isHeated);
    }

    // Đọc 1 Struct BuildingData từ luồng nhị phân
    public static void ReadBuilding(BinaryReader reader, out BuildingData data)
    {
        data = new BuildingData
        {
            buildingID = reader.ReadUInt16(),
            buildingType = (BuildingType)reader.ReadByte(),
            coordX = reader.ReadUInt16(),
            coordY = reader.ReadUInt16(),
            sizeFootprint = reader.ReadByte(),
            tierLevel = reader.ReadByte(),
            buildingState = (BuildingState)reader.ReadByte(),
            constructionProgress = reader.ReadSingle(),
            priorityLevel = reader.ReadByte(),
            isOperational = reader.ReadBoolean(),
            isAllocated = reader.ReadBoolean(),
            currentOccupancy = reader.ReadByte(),
            maxOccupancy = reader.ReadByte(),
            currentWorkers = reader.ReadByte(),
            maxWorkers = reader.ReadByte(),
            efficiency = reader.ReadSingle(),
            productionProgress = reader.ReadSingle(),
            fuelStored = reader.ReadUInt16(),
            outputStored = reader.ReadUInt16(),
            durability = reader.ReadSingle(),
            insulationValue = reader.ReadSingle(),
            contamination = reader.ReadSingle(),
            isHeated = reader.ReadBoolean()
        };
    }
}