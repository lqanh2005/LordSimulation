using System;
using System.IO;
using UnityEngine;

public enum OriginRegion : byte { GreenZone = 0, YellowZone = 1, RedZone = 2 }
public enum FactionType : byte { None = 0, Commoner = 1, Aristocrat = 2, Scholar = 3, Zealot = 4 }
public enum ProfessionType : byte { None = 0, Farmer = 1, Miner = 2, Lumberjack = 3, Craftsman = 4, Doctor = 5 }
public enum HealthStatus : byte { Healthy = 0, Incubating = 1, ActiveInfected = 2, Treated = 3 }
public enum DiseaseType : byte { None = 0, RedFever = 1, LungParasite = 2, BloodPoison = 3 }

[Flags]
public enum SymptomFlags : byte
{
    None = 0,
    Fever = 1 << 0,
    Cough = 1 << 1,
    Fatigue = 1 << 2,
    Rash = 1 << 3,
    Nausea = 1 << 4,
    Headache = 1 << 5,
    ShortnessOfBreath = 1 << 6,
    Dizziness = 1 << 7
}

[Serializable]
public struct ResidentData
{
    public int residentID;   // ID của cư dân
    public ushort firstNameID;  // ID của tên đầu tiên
    public ushort lastNameID;
    public byte age;
    public OriginRegion originRegion;
    public FactionType factionType;
    public byte wealth;   // Tài sản: 0 - 100
    public ProfessionType professionType;
    public HealthStatus healthStatus;
    public DiseaseType diseaseType;
    public byte IncubationMonths;  // Số tháng ủ bệnh
    public float happiness;  // Mức độ hạnh phúc: 0.0 - 1.0
    public float bodyTemperature;  // Nhiệt độ cơ thể: 35.0 - 42.0
    public SymptomFlags symptoms;  // Các triệu chứng hiện tại
    public short assignedHouseID;  // ID của ngôi nhà được chỉ định
    public short assignedWorkID;  // ID của nơi làm việc được chỉ định
    public bool isAlive;  // Trạng thái sống/chết
    public byte strength;   // Sức mạnh: 0 - 100
    public byte endurance;  // Thể lực / Bền bỉ: 0 - 100
    public byte intellect;  // Trí lực: 0 - 100

    public string AgeGroup => age < 18 ? "Child" : (age < 65 ? "Adult" : "Elderly");
    public bool HasSymptom(SymptomFlags symptom) => (symptoms & symptom) != 0;
    public byte GetRelevantSkill()=> professionType switch
    {
        ProfessionType.Farmer => strength,
        ProfessionType.Miner => endurance,
        ProfessionType.Lumberjack => endurance,
        ProfessionType.Craftsman => intellect,
        ProfessionType.Doctor => intellect,
        _ => (byte)((strength+intellect+endurance)/3)
    };
    public float WorkMultiplier => 0.5f + (GetRelevantSkill() / 100f) * 1.5f;
}
