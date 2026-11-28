using System;
using System.IO;

public static class CitySaveSerializer
{
    private const string FILE_MAGIC = "CITYSAVE";
    public const int SaveVersion = 16;   // v10: quarantineMonths · v11: thirstMonths · v12: GDD v0.3 (tuổi nén, miễn dịch, Reputation 0–100, thuế nhập thành) · v13: gia đình, note, Gate Clerk · v14: lương, thuế thu nhập, nợ lương · v15: giá thị trường, tin đồn, báo trước bão tuyết · v16: Lệnh khẩn cấp

    public static void ValidateSaveCounts(
        int residentCount, int residentCapacity,
        int buildingCount, int buildingCapacity,
        int edictCount, int edictCapacity,
        int tradeCount, int tradeCapacity,
        int immigrantCount, int immigrantCapacity)
    {
        if (residentCount < 0 || residentCount > residentCapacity)
            throw new InvalidDataException($"[SaveSerializer] residentCount ({residentCount}) vượt giới hạn mảng ({residentCapacity}).");

        if (buildingCount < 0 || buildingCount > buildingCapacity)
            throw new InvalidDataException($"[SaveSerializer] buildingCount ({buildingCount}) vượt giới hạn mảng ({buildingCapacity}).");

        if (edictCount < 0 || edictCount > edictCapacity)
            throw new InvalidDataException($"[SaveSerializer] edictCount ({edictCount}) vượt giới hạn mảng ({edictCapacity}).");

        if (tradeCount < 0 || tradeCount > tradeCapacity)
            throw new InvalidDataException($"[SaveSerializer] tradeCount ({tradeCount}) vượt giới hạn mảng ({tradeCapacity}).");

        if (immigrantCount < 0 || immigrantCount > immigrantCapacity)
            throw new InvalidDataException($"[SaveSerializer] immigrantCount ({immigrantCount}) vượt giới hạn mảng ({immigrantCapacity}).");
    }

    // --- GHI TOÀN BỘ GAME (SAVE) ---
    public static void SerializeFullGame(
        BinaryWriter writer,
        in GlobalSystemData globalData,
        ResidentData[] residents, int residentCount,
        BuildingData[] buildings, int buildingCount,
        EdictRuleData[] edicts, int edictCount,
        TradeRouteData[] tradeRoutes, int tradeCount,
        ResidentData[] immigrants, int immigrantCount,
        in ResearchSaveData research,
        in MerchantSaveData merchant)
    {
        ValidateSaveCounts(
            residentCount, residents.Length,
            buildingCount, buildings.Length,
            edictCount, edicts.Length,
            tradeCount, tradeRoutes.Length,
            immigrantCount, immigrants.Length);

        // 1. Header & Version
        writer.Write(FILE_MAGIC);
        writer.Write(SaveVersion);

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

        writer.Write(immigrantCount);
        for (int i = 0; i < immigrantCount; i++)
            ResidentDataSerializer.WriteResident(writer, in immigrants[i]);

        // 7. Research (v6+)
        ResearchDataSerializer.WriteResearch(writer, in research);

        // 8. Cờ chiến thắng (v7+) — ghi cuối để không đổi layout khối Global cũ
        writer.Write(globalData.victoryAchieved);

        // 9. Thương nhân (v8+)
        merchant.Write(writer);

        // 10. Chuỗi khủng hoảng (v9+)
        writer.Write(globalData.corpsesFresh);
        writer.Write(globalData.corpsesRotting);
        writer.Write(globalData.miasma);

        // 11. GDD v0.3 (v12+)
        writer.Write((byte)globalData.entryTaxLevel);
        writer.Write((byte)globalData.pendingEntryTaxLevel);
        writer.Write(globalData.miasmaZonesPrev);
        writer.Write(globalData.disasterWaveBoost);

        // 12. GDD v0.3 Đợt 2 (v13+)
        writer.Write(globalData.gateClerkResidentID);

        // 13. GDD v0.3 Đợt 3 (v14+)
        writer.Write((byte)globalData.wageLevel);
        writer.Write((byte)globalData.pendingWageLevel);
        writer.Write((byte)globalData.incomeTaxLevel);
        writer.Write((byte)globalData.pendingIncomeTaxLevel);
        writer.Write((byte)globalData.feedPolicy);

        // 14. GDD v0.3 Đợt 4 (v15+)
        writer.Write(globalData.foodPriceEvent);
        writer.Write(globalData.coalPriceEvent);
        writer.Write(globalData.woodPriceEvent);
        writer.Write((byte)globalData.marketActiveEvent);
        writer.Write(globalData.marketActiveMonths);
        writer.Write(globalData.marketActiveMagnitude);
        writer.Write((byte)globalData.marketPendingEvent);
        writer.Write(globalData.marketPendingInMonths);
        writer.Write(globalData.marketPendingMagnitude);
        writer.Write((byte)globalData.rumorEvent);
        writer.Write(globalData.rumorInMonths);
        writer.Write(globalData.rumorTrue);
        writer.Write(globalData.blizzardForecast);

        // 15. Lệnh khẩn cấp (v16+)
        EmergencyOrders.State.Write(writer);
    }

    // --- ĐỌC TOÀN BỘ GAME (LOAD) ---
    public static void DeserializeFullGame(
        BinaryReader reader,
        out GlobalSystemData globalData,
        ResidentData[] residents, out int residentCount,
        BuildingData[] buildings, out int buildingCount,
        EdictRuleData[] edicts, out int edictCount,
        TradeRouteData[] tradeRoutes, out int tradeCount,
        ResidentData[] immigrants, out int immigrantCount,
        out ResearchSaveData research,
        out MerchantSaveData merchant)
    {
        research = default;
        merchant = default;

        // 1. Kiểm tra tính toàn vẹn Header
        string magic = reader.ReadString();
        int version = reader.ReadInt32();

        if (magic != FILE_MAGIC)
        {
            throw new InvalidDataException("[SaveSerializer] Magic header không khớp hoặc file save bị lỗi!");
        }

        if (version < 1 || version > SaveVersion)
        {
            throw new InvalidDataException($"[SaveSerializer] Phiên bản save ({version}) không được hỗ trợ. Yêu cầu v1–v{SaveVersion}.");
        }

        // 2. Global State
        GlobalDataSerializer.ReadGlobal(reader, out globalData);

        // 3. Buildings
        buildingCount = reader.ReadInt32();
        if (buildingCount < 0 || buildingCount > buildings.Length)
            throw new InvalidDataException($"[SaveSerializer] buildingCount ({buildingCount}) vượt giới hạn mảng ({buildings.Length}).");
        for (int i = 0; i < buildingCount; i++)
        {
            BuildingDataSerializer.ReadBuilding(reader, out buildings[i], version);
        }

        // 4. Residents
        residentCount = reader.ReadInt32();
        if (residentCount < 0 || residentCount > residents.Length)
            throw new InvalidDataException($"[SaveSerializer] residentCount ({residentCount}) vượt giới hạn mảng ({residents.Length}).");
        for (int i = 0; i < residentCount; i++)
        {
            ResidentDataSerializer.ReadResident(reader, out residents[i], version);
        }

        // 5. Edicts
        edictCount = reader.ReadInt32();
        if (edictCount < 0 || edictCount > edicts.Length)
            throw new InvalidDataException($"[SaveSerializer] edictCount ({edictCount}) vượt giới hạn mảng ({edicts.Length}).");
        for (int i = 0; i < edictCount; i++)
        {
            EdictDataSerializer.ReadEdict(reader, out edicts[i], version);
        }

        // 6. Trade Routes
        tradeCount = reader.ReadInt32();
        if (tradeCount < 0 || tradeCount > tradeRoutes.Length)
            throw new InvalidDataException($"[SaveSerializer] tradeCount ({tradeCount}) vượt giới hạn mảng ({tradeRoutes.Length}).");
        for (int i = 0; i < tradeCount; i++)
        {
            TradeDataSerializer.ReadTradeRoute(reader, out tradeRoutes[i]);
        }

        immigrantCount = 0;
        if (version < 3)
        {
            MigrateGlobalToV12(ref globalData);
            EmergencyOrders.State = new EmergencyState();
            return;
        }

        immigrantCount = reader.ReadInt32();
        if (immigrantCount < 0 || immigrantCount > immigrants.Length)
            throw new InvalidDataException($"[SaveSerializer] immigrantCount ({immigrantCount}) vượt giới hạn mảng ({immigrants.Length}).");
        for (int i = 0; i < immigrantCount; i++)
            ResidentDataSerializer.ReadResident(reader, out immigrants[i], version);

        // 7. Research (v6+) — save cũ hơn bắt đầu với cây nghiên cứu trống
        if (version >= 6)
            ResearchDataSerializer.ReadResearch(reader, out research);

        // 8. Cờ chiến thắng (v7+)
        if (version >= 7)
            globalData.victoryAchieved = reader.ReadBoolean();

        // 9. Thương nhân (v8+) — save cũ: chưa có thương nhân
        if (version >= 8)
            merchant = MerchantSaveData.Read(reader);

        // 10. Chuỗi khủng hoảng (v9+)
        if (version >= 9)
        {
            globalData.corpsesFresh = reader.ReadInt32();
            globalData.corpsesRotting = reader.ReadInt32();
            globalData.miasma = reader.ReadSingle();
        }

        // 11. GDD v0.3 (v12+)
        if (version >= 12)
        {
            globalData.entryTaxLevel = (EntryTaxLevel)reader.ReadByte();
            globalData.pendingEntryTaxLevel = (EntryTaxLevel)reader.ReadByte();
            globalData.miasmaZonesPrev = reader.ReadInt32();
            globalData.disasterWaveBoost = reader.ReadBoolean();
        }
        else
        {
            MigrateGlobalToV12(ref globalData);
        }

        // 12. GDD v0.3 Đợt 2 (v13+)
        if (version >= 13)
            globalData.gateClerkResidentID = reader.ReadInt32();

        // 13. GDD v0.3 Đợt 3 (v14+) — save cũ: mức Vừa, cấp phát
        if (version >= 14)
        {
            globalData.wageLevel = (WageLevel)reader.ReadByte();
            globalData.pendingWageLevel = (WageLevel)reader.ReadByte();
            globalData.incomeTaxLevel = (EntryTaxLevel)reader.ReadByte();
            globalData.pendingIncomeTaxLevel = (EntryTaxLevel)reader.ReadByte();
            globalData.feedPolicy = (FeedPolicy)reader.ReadByte();
        }
        else
        {
            globalData.wageLevel = globalData.pendingWageLevel = WageLevel.Normal;
            globalData.incomeTaxLevel = globalData.pendingIncomeTaxLevel = EntryTaxLevel.Normal;
            globalData.feedPolicy = FeedPolicy.Subsidize;
        }

        // 14. GDD v0.3 Đợt 4 (v15+) — save cũ: giá gốc, chưa có sự kiện
        if (version >= 15)
        {
            globalData.foodPriceEvent = reader.ReadSingle();
            globalData.coalPriceEvent = reader.ReadSingle();
            globalData.woodPriceEvent = reader.ReadSingle();
            globalData.marketActiveEvent = (MarketEventType)reader.ReadByte();
            globalData.marketActiveMonths = reader.ReadByte();
            globalData.marketActiveMagnitude = reader.ReadSingle();
            globalData.marketPendingEvent = (MarketEventType)reader.ReadByte();
            globalData.marketPendingInMonths = reader.ReadByte();
            globalData.marketPendingMagnitude = reader.ReadSingle();
            globalData.rumorEvent = (MarketEventType)reader.ReadByte();
            globalData.rumorInMonths = reader.ReadByte();
            globalData.rumorTrue = reader.ReadBoolean();
            globalData.blizzardForecast = reader.ReadBoolean();
        }
        else
        {
            globalData.foodPriceEvent = globalData.coalPriceEvent = globalData.woodPriceEvent = 1f;
        }

        // 15. Lệnh khẩn cấp (v16+) — save cũ: chưa dùng lệnh nào
        EmergencyOrders.State = version >= 16 ? EmergencyState.Read(reader) : new EmergencyState();
    }

    /// <summary>Save trước v12: Reputation thang −50..100 (bắt đầu 0) -> 0..100 (bắt đầu 50); Miasma cũ là mức khí độc, không phải số vùng.</summary>
    private static void MigrateGlobalToV12(ref GlobalSystemData g)
    {
        g.reputation = UnityEngine.Mathf.Clamp(g.reputation + StabilityRules.ReputationStart,
            StabilityRules.ReputationMin, StabilityRules.ReputationMax);
        g.entryTaxLevel = StabilityRules.DefaultEntryTax;
        g.pendingEntryTaxLevel = StabilityRules.DefaultEntryTax;
        g.miasma = 0f;
        g.miasmaZonesPrev = 0;
        g.disasterWaveBoost = false;
    }
}