using System;

public enum OriginRegion : byte { GreenZone = 0, YellowZone = 1, RedZone = 2 }
public enum FactionType : byte { None = 0, Commoner = 1, Aristocrat = 2, Scholar = 3, Zealot = 4 }
public enum ProfessionType : byte { None = 0, Farmer = 1, Miner = 2, Lumberjack = 3, Craftsman = 4, Doctor = 5, Student = 6, Teacher = 7, Guard = 8 }
public enum HealthStatus : byte { Healthy = 0, Incubating = 1, ActiveInfected = 2, Treated = 3 }
public enum DiseaseType : byte { None = 0, RedFever = 1, LungParasite = 2, BloodPoison = 3 }
public enum AgeGroupType : byte { Child = 0, Adult = 1, Elderly = 2 }
public enum GenderType : byte { Male = 0, Female = 1 }

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
    public GenderType gender;
    public OriginRegion originRegion;
    public FactionType factionType;
    public byte wealth;   // Tài sản: 0 - 100
    public ProfessionType professionType;
    public HealthStatus healthStatus;
    public DiseaseType diseaseType;
    public byte IncubationMonths;  // Số tháng ủ bệnh
    public byte recoveryMonths;  // Số tháng đang điều trị / hồi phục
    public float happiness;  // Mức độ hạnh phúc: 0.0 - 1.0
    public float bodyTemperature;  // Nhiệt độ cơ thể: 35.0 - 42.0
    public SymptomFlags symptoms;  // Các triệu chứng hiện tại
    public short assignedHouseID;  // ID của ngôi nhà được chỉ định
    public short assignedWorkID;  // ID của nơi làm việc được chỉ định
    public bool isAlive;  // Trạng thái sống/chết
    public byte hungerMonths;  // Số tháng đói liên tục (0 = no)
    public byte coldMonths;  // Số tháng lạnh liên tục (0 = no)
    public byte strength;   // Sức mạnh: 0 - 100
    public byte endurance;  // Thể lực / Bền bỉ: 0 - 100
    public byte intellect;  // Trí lực: 0 - 100
    public byte quarantineMonths;  // Số tháng cách ly còn lại (0 = không bị cách ly) — lưu từ save v10
    public byte thirstMonths;  // Số tháng khát liên tục (0 = no) — lưu từ save v11
    public byte immunityMonths;  // Miễn dịch Sốt Đỏ còn lại sau khi khỏi (GDD 7.4: 12 tháng) — save v12
    public byte griefMonths;     // Người thân cùng nhà chết trong N tháng qua (GDD 9.6: −15 Happiness) — save v12
    public int familyID;         // Gia đình di cư (GDD 4.1): cùng id = cùng hộ, ở chung nhà; 0 = đi một mình — save v13
    public byte rolledNotes;     // NoteFlags ngẫu nhiên lúc tạo (Bị thương / Sốt khi ủ bệnh / Ho) — save v13
    public byte processedByRule; // 0 = duyệt tay; n = sắc lệnh ưu tiên n đã xử lý ("Why was this NPC processed?") — save v13
    public ushort wageDebt;      // Nợ lương thành còn thiếu người này (vàng, tính theo mẫu) — save v14
    public byte debtMonths;      // Số tháng liên tiếp bị nợ lương — save v14
    public byte strikeMonths;    // Số tháng đang đình công (0 = đi làm) — save v14

    public readonly bool IsStriking => strikeMonths > 0;

    // GDD v0.3 mục 5.4: tuổi nén (năm trong game) — Child 0–3, Adult 3–15, Elderly 15+
    public const byte AdultAge = 3;
    public const byte ElderlyAge = 15;

    public readonly AgeGroupType GetAgeGroup()
    {
        if (age < AdultAge) return AgeGroupType.Child;
        if (age < ElderlyAge) return AgeGroupType.Adult;
        return AgeGroupType.Elderly;
    }

    public readonly bool HasSymptom(SymptomFlags symptom) => (symptoms & symptom) != 0;

    public readonly byte GetRelevantSkill() => professionType switch
    {
        ProfessionType.Farmer => strength,
        ProfessionType.Miner => endurance,
        ProfessionType.Lumberjack => endurance,
        ProfessionType.Craftsman => intellect,
        ProfessionType.Doctor => intellect,
        ProfessionType.Student => intellect,
        ProfessionType.Teacher => intellect,
        ProfessionType.Guard => strength,
        _ => (byte)((strength + intellect + endurance) / 3)
    };

    public readonly float WorkMultiplier => 0.5f + (GetRelevantSkill() / 100f) * 1.5f;
}
