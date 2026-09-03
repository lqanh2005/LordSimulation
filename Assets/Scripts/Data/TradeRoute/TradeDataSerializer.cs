using System.IO;

public static class TradeDataSerializer
{
    public static void WriteTradeRoute(BinaryWriter writer, in TradeRouteData data)
    {
        writer.Write(data.routeID);
        writer.Write((byte)data.destinationNode);
        writer.Write((byte)data.transportType);
        writer.Write(data.travelMonths);
        writer.Write(data.currentProgress);
        writer.Write(data.safetyRating);
        writer.Write(data.guardCount);
        writer.Write((byte)data.exportItem);
        writer.Write(data.exportAmount);
        writer.Write((byte)data.importItem);
        writer.Write(data.importAmount);
        writer.Write((byte)data.routeStatus);
        writer.Write(data.outpostInfectionRisk);
        writer.Write(data.isRepeat);
        writer.Write(data.isAllocated);
    }

    public static void ReadTradeRoute(BinaryReader reader, out TradeRouteData data)
    {
        data = new TradeRouteData
        {
            routeID = reader.ReadByte(),
            destinationNode = (DestinationNode)reader.ReadByte(),
            transportType = (TransportType)reader.ReadByte(),
            travelMonths = reader.ReadByte(),
            currentProgress = reader.ReadByte(),
            safetyRating = reader.ReadByte(),
            guardCount = reader.ReadByte(),
            exportItem = (ResourceType)reader.ReadByte(),
            exportAmount = reader.ReadUInt16(),
            importItem = (ResourceType)reader.ReadByte(),
            importAmount = reader.ReadUInt16(),
            routeStatus = (RouteStatus)reader.ReadByte(),
            outpostInfectionRisk = reader.ReadByte(),
            isRepeat = reader.ReadBoolean(),
            isAllocated = reader.ReadBoolean()
        };
    }
}