using System.IO;

public static class GlobalDataSerializer
{
    public static void WriteGlobal(BinaryWriter writer, in GlobalSystemData data)
    {
        writer.Write(data.currentYear);
        writer.Write(data.currentMonth);
        writer.Write(data.timeTickProgress);
        writer.Write((byte)data.currentGameSpeed);
        writer.Write(data.environmentTemperature);
        writer.Write((byte)data.currentWeather);
        writer.Write(data.weatherDuration);
        writer.Write(data.treasuryGold);
        writer.Write(data.stockFood);
        writer.Write(data.stockCoal);
        writer.Write(data.stockWood);
        writer.Write(data.stockIron);
        writer.Write(data.stockMedicine);
        writer.Write(data.authorityPoints);
        writer.Write(data.reputation);
        writer.Write(data.riotRiskMeter);
        writer.Write(data.totalPopulation);
        writer.Write(data.totalInfected);
        writer.Write(data.overallHappiness);
    }

    public static void ReadGlobal(BinaryReader reader, out GlobalSystemData data)
    {
        data = new GlobalSystemData
        {
            currentYear = reader.ReadUInt16(),
            currentMonth = reader.ReadByte(),
            timeTickProgress = reader.ReadSingle(),
            currentGameSpeed = (GameSpeed)reader.ReadByte(),
            environmentTemperature = reader.ReadSingle(),
            currentWeather = (WeatherEvent)reader.ReadByte(),
            weatherDuration = reader.ReadByte(),
            treasuryGold = reader.ReadInt32(),
            stockFood = reader.ReadInt32(),
            stockCoal = reader.ReadInt32(),
            stockWood = reader.ReadInt32(),
            stockIron = reader.ReadInt32(),
            stockMedicine = reader.ReadInt32(),
            authorityPoints = reader.ReadUInt16(),
            reputation = reader.ReadSingle(),
            riotRiskMeter = reader.ReadSingle(),
            totalPopulation = reader.ReadInt32(),
            totalInfected = reader.ReadInt32(),
            overallHappiness = reader.ReadSingle()
        };
    }
}