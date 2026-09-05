using System;
using System.IO;

public static class CitySaveSerializer
{
    private const string FILE_MAGIC = "CITYSAVE";
    private const int SAVE_VERSION = 1;

    // --- GHI TOÀN BỘ GAME (SAVE) ---
    public static void SerializeFullGame(
        BinaryWriter writer,
        in GlobalSystemData globalData,
        ResidentData[] residents, int residentCount,
        BuildingData[] buildings, int buildingCount,
        EdictRuleData[] edicts, int edictCount,
        TradeRouteData[] tradeRoutes, int tradeCount)
    {
        // 1. Header & Version
        writer.Write(FILE_MAGIC);
        writer.Write(SAVE_VERSION);

        // 2. Global State (65 bytes)
        GlobalDataSerializer.WriteGlobal(writer, in globalData);

        // 3. Buildings
        writer.Write(buildingCount);
        for (int i = 0; i < buildingCount; i++)
        {
            BuildingDataSerializer.WriteBuilding(writer, in buildings[i]);
        }

        // 4. Residents
        writer.Write(residentCount);
        for (int i = 0; i < residentCount; i++)
        {
            ResidentDataSerializer.WriteResident(writer, in residents[i]);
        }

        // 5. Edicts
        writer.Write(edictCount);
        for (int i = 0; i < edictCount; i++)
        {
            EdictDataSerializer.WriteEdict(writer, in edicts[i]);
        }

        // 6. Trade Routes
        writer.Write(tradeCount);
        for (int i = 0; i < tradeCount; i++)
        {
            TradeDataSerializer.WriteTradeRoute(writer, in tradeRoutes[i]);
        }
    }

    // --- ĐỌC TOÀN BỘ GAME (LOAD) ---
    public static void DeserializeFullGame(
        BinaryReader reader,
        out GlobalSystemData globalData,
        ResidentData[] residents, out int residentCount,
        BuildingData[] buildings, out int buildingCount,
        EdictRuleData[] edicts, out int edictCount,
        TradeRouteData[] tradeRoutes, out int tradeCount)
    {
        // 1. Kiểm tra tính toàn vẹn Header
        string magic = reader.ReadString();
        int version = reader.ReadInt32();

        if (magic != FILE_MAGIC)
        {
            throw new InvalidDataException("[SaveSerializer] Magic header không khớp hoặc file save bị lỗi!");
        }

        // 2. Global State
        GlobalDataSerializer.ReadGlobal(reader, out globalData);

        // 3. Buildings
        buildingCount = reader.ReadInt32();
        if (buildingCount < 0 || buildingCount > buildings.Length)
            throw new InvalidDataException($"[SaveSerializer] buildingCount ({buildingCount}) vượt giới hạn mảng ({buildings.Length}).");
        for (int i = 0; i < buildingCount; i++)
        {
            BuildingDataSerializer.ReadBuilding(reader, out buildings[i]);
        }

        // 4. Residents
        residentCount = reader.ReadInt32();
        if (residentCount < 0 || residentCount > residents.Length)
            throw new InvalidDataException($"[SaveSerializer] residentCount ({residentCount}) vượt giới hạn mảng ({residents.Length}).");
        for (int i = 0; i < residentCount; i++)
        {
            ResidentDataSerializer.ReadResident(reader, out residents[i]);
        }

        // 5. Edicts
        edictCount = reader.ReadInt32();
        if (edictCount < 0 || edictCount > edicts.Length)
            throw new InvalidDataException($"[SaveSerializer] edictCount ({edictCount}) vượt giới hạn mảng ({edicts.Length}).");
        for (int i = 0; i < edictCount; i++)
        {
            EdictDataSerializer.ReadEdict(reader, out edicts[i]);
        }

        // 6. Trade Routes
        tradeCount = reader.ReadInt32();
        if (tradeCount < 0 || tradeCount > tradeRoutes.Length)
            throw new InvalidDataException($"[SaveSerializer] tradeCount ({tradeCount}) vượt giới hạn mảng ({tradeRoutes.Length}).");
        for (int i = 0; i < tradeCount; i++)
        {
            TradeDataSerializer.ReadTradeRoute(reader, out tradeRoutes[i]);
        }
    }
}