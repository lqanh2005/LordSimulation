using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Note trên hồ sơ (GDD v0.3 mục 4.1–4.2). Chỉ hiện khi người đó có vấn đề; không note ≠ chắc chắn khoẻ.
/// Lưu theo byte: các bit ngẫu nhiên lúc tạo người (Bị thương, Sốt khi ủ bệnh, Ho) nằm trong ResidentData.rolledNotes,
/// các note còn lại suy ra từ thuộc tính ẩn mỗi lần xem.
/// </summary>
[System.Flags]
public enum NoteFlags : byte
{
    None = 0,
    Rash = 1 << 0,          // Phát ban đỏ — cấp 0 (người Infected luôn có)
    Exhausted = 1 << 1,     // Suy kiệt — cấp 0 (đói lâu ngoài tường / đang hồi phục)
    Injured = 1 << 2,       // Bị thương — cấp 0
    Fever = 1 << 3,         // Sốt — cấp 1 (Infected, và 50% người ủ bệnh)
    Cough = 1 << 4,         // Ho — cấp 1 (cảm thường, note gây nhiễu)
    PlagueRoute = 1 << 5,   // Đi qua vùng có dịch — cấp 1
    Destitute = 1 << 6,     // Nghèo kiệt — cấp 0 (không trả nổi thuế nhập thành)
    MarketRumor = 1 << 7    // Tin đồn thị trường — cấp 0 (chưa dùng, Đợt 4)
}

/// <summary>Một gia đình trong hàng chờ: đoạn liên tiếp [start, start+count) của mảng người chờ.</summary>
public readonly struct ApplicantFamily
{
    public readonly ResidentData[] members;
    public readonly int start;
    public readonly int count;
    public readonly byte profileLevel;

    public ApplicantFamily(ResidentData[] members, int start, int count, byte profileLevel)
    {
        this.members = members;
        this.start = start;
        this.count = count;
        this.profileLevel = profileLevel;
    }

    public bool IsValid => members != null && count > 0;
    public ref ResidentData this[int i] => ref members[start + i];
    public ref ResidentData Head => ref members[start];

    /// <summary>Gia đình 1 người dựng từ một cư dân (test, gọi lẻ).</summary>
    public static ApplicantFamily Single(in ResidentData resident, byte profileLevel) =>
        new ApplicantFamily(new[] { resident }, 0, 1, profileLevel);
}

public static class FamilyRules
{
    // ===== Cấp hồ sơ (GDD 4.2) =====
    public const byte ProfileLevelBasic = 0;      // Có sẵn
    public const byte ProfileLevelExam = 1;       // Tier 1: Khám sơ bộ

    /// <summary>Do ResearchManager gán: cấp hồ sơ hiện tại (null = cấp 0).</summary>
    public static System.Func<byte> ProfileLevelProvider;

    public static byte CurrentProfileLevel => ProfileLevelProvider != null ? ProfileLevelProvider() : ProfileLevelBasic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ProfileLevelProvider = null;
        _nextFamilyId = 1;
    }

    // ===== Note =====
    public const NoteFlags HealthNotes = NoteFlags.Rash | NoteFlags.Exhausted | NoteFlags.Injured | NoteFlags.Fever | NoteFlags.Cough;
    public const NoteFlags Level1Notes = NoteFlags.Fever | NoteFlags.Cough | NoteFlags.PlagueRoute;

    public const float InjuredChance = 0.05f;
    public const float IncubatingFeverChance = 0.5f;   // GDD 7.4: một nửa người ủ bệnh có note Sốt (từ cấp 1)
    public const float HealthyCoughChance = 0.10f;     // GDD 7.4: 10% người khoẻ có note Ho (nhiễu)
    public const int ExhaustedAfterWaitMonths = 2;

    /// <summary>Danh sách note theo thứ tự hiển thị / chọn trong sắc lệnh (bit index = vị trí).</summary>
    public static readonly NoteFlags[] AllNotes =
    {
        NoteFlags.Rash, NoteFlags.Exhausted, NoteFlags.Injured, NoteFlags.Fever,
        NoteFlags.Cough, NoteFlags.PlagueRoute, NoteFlags.Destitute, NoteFlags.MarketRumor   // thêm vào cuối: sắc lệnh lưu theo vị trí
    };

    public static byte GetNoteLevel(NoteFlags note) => (Level1Notes & note) != 0 ? ProfileLevelExam : ProfileLevelBasic;

    public static bool IsNoteVisible(NoteFlags note, byte level) => GetNoteLevel(note) <= level;

    /// <summary>Note người chơi nhìn thấy ở cấp hồ sơ hiện tại.</summary>
    public static NoteFlags GetVisibleNotes(in ResidentData r, byte level)
    {
        NoteFlags notes = NoteFlags.None;
        NoteFlags rolled = (NoteFlags)r.rolledNotes;

        if (r.healthStatus == HealthStatus.ActiveInfected)
            notes |= NoteFlags.Rash;
        if (r.hungerMonths >= ExhaustedAfterWaitMonths || r.healthStatus == HealthStatus.Treated)
            notes |= NoteFlags.Exhausted;
        if ((rolled & NoteFlags.Injured) != 0)
            notes |= NoteFlags.Injured;
        if (ImmigrationRules.IsDestitute(in r))
            notes |= NoteFlags.Destitute;
        if ((rolled & NoteFlags.MarketRumor) != 0)
            notes |= NoteFlags.MarketRumor;   // GDD 6.6: tin đồn thị trường qua lời khai người di cư (cấp 0)

        if (level >= ProfileLevelExam)
        {
            if (r.healthStatus == HealthStatus.ActiveInfected
                || (r.healthStatus == HealthStatus.Incubating && (rolled & NoteFlags.Fever) != 0))
                notes |= NoteFlags.Fever;
            if ((rolled & NoteFlags.Cough) != 0)
                notes |= NoteFlags.Cough;
            if (r.originRegion == OriginRegion.RedZone)
                notes |= NoteFlags.PlagueRoute;
        }

        return notes;
    }

    public static string NoteLabel(NoteFlags note) => note switch
    {
        NoteFlags.Rash => "Phát ban đỏ",
        NoteFlags.Exhausted => "Suy kiệt",
        NoteFlags.Injured => "Bị thương",
        NoteFlags.Fever => "Sốt",
        NoteFlags.Cough => "Ho",
        NoteFlags.PlagueRoute => "Đi qua vùng có dịch",
        NoteFlags.Destitute => "Nghèo kiệt",
        NoteFlags.MarketRumor => "Tin đồn thị trường",
        _ => "?"
    };

    public static string DescribeNotes(NoteFlags notes)
    {
        if (notes == NoteFlags.None)
            return string.Empty;
        var sb = new System.Text.StringBuilder(48);
        foreach (NoteFlags n in AllNotes)
        {
            if ((notes & n) == 0)
                continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(NoteLabel(n));
        }
        return sb.ToString();
    }

    // ===== Gia đình trong mảng hàng chờ =====

    /// <summary>Đoạn gia đình chứa vị trí index. familyID 0 = người đi một mình.</summary>
    public static void GetFamilyRange(ResidentData[] arr, int count, int index, out int start, out int length)
    {
        start = index;
        length = 0;
        if (arr == null || (uint)index >= (uint)count)
            return;

        int id = arr[index].familyID;
        if (id == 0)
        {
            length = 1;
            return;
        }

        while (start > 0 && arr[start - 1].familyID == id)
            start--;
        int end = index + 1;
        while (end < count && arr[end].familyID == id)
            end++;
        length = end - start;
    }

    public static bool AnyMember(in ApplicantFamily f, System.Func<ResidentData, bool> predicate)
    {
        for (int i = 0; i < f.count; i++)
            if (predicate(f[i]))
                return true;
        return false;
    }

    public static NoteFlags GetFamilyNotes(in ApplicantFamily f)
    {
        NoteFlags all = NoteFlags.None;
        for (int i = 0; i < f.count; i++)
            all |= GetVisibleNotes(in f[i], f.profileLevel);
        return all;
    }

    public static bool HasChild(in ApplicantFamily f)
    {
        for (int i = 0; i < f.count; i++)
            if (f[i].GetAgeGroup() == AgeGroupType.Child)
                return true;
        return false;
    }

    public static bool HasChildOrElderly(in ApplicantFamily f)
    {
        for (int i = 0; i < f.count; i++)
            if (f[i].GetAgeGroup() != AgeGroupType.Adult)
                return true;
        return false;
    }

    public static int CountWorkers(in ApplicantFamily f)
    {
        int n = 0;
        for (int i = 0; i < f.count; i++)
            if (f[i].GetAgeGroup() == AgeGroupType.Adult)
                n++;
        return n;
    }

    public static int TotalWealth(in ApplicantFamily f)
    {
        int sum = 0;
        for (int i = 0; i < f.count; i++)
            sum += f[i].wealth;
        return sum;
    }

    // ===== Mức giàu nghèo (điều kiện sắc lệnh từ hồ sơ cấp 1) =====
    public const int WealthPoor = 0, WealthMiddle = 1, WealthRich = 2;
    public const float RichAverageWealth = 40f;

    public static int GetWealthClass(in ApplicantFamily f)
    {
        float avg = f.count > 0 ? (float)TotalWealth(in f) / f.count : 0f;
        if (avg < StabilityRules.PoorWealthThreshold) return WealthPoor;
        if (avg >= RichAverageWealth) return WealthRich;
        return WealthMiddle;
    }

    public static string WealthClassLabel(int c) => c switch
    {
        WealthPoor => "Nghèo",
        WealthRich => "Giàu",
        _ => "Trung bình"
    };

    public static string AgeStageLabel(AgeGroupType g) => g switch
    {
        AgeGroupType.Child => "Trẻ em",
        AgeGroupType.Elderly => "Người già",
        _ => "Người lớn"
    };

    // ===== Tạo gia đình di cư (GDD 4.1: 1–5 người, trung bình ~2,5) =====
    private static int _nextFamilyId = 1;

    /// <summary>Gia đình mới cần id khác mọi gia đình đang có (gọi sau khi nạp save để không trùng).</summary>
    public static void EnsureFamilyIdAbove(int maxExisting)
    {
        if (_nextFamilyId <= maxExisting)
            _nextFamilyId = maxExisting + 1;
    }

    public static int RollFamilySize()
    {
        float roll = Random.value;
        if (roll < 0.25f) return 1;
        if (roll < 0.50f) return 2;
        if (roll < 0.75f) return 3;
        if (roll < 0.90f) return 4;
        return 5;
    }

    /// <summary>
    /// Tạo một gia đình <paramref name="size"/> người vào <paramref name="output"/>. Cả nhà chung họ, nguồn gốc;
    /// chủ hộ là người lớn; 10% gia đình có ít nhất 1 người bệnh / ủ bệnh (×3 nếu đi qua vùng dịch hoặc tị nạn).
    /// </summary>
    public static void CreateFamily(List<ResidentData> output, ref int nextResidentId, int size, EntryTaxLevel tax, bool refugee)
    {
        int familyId = _nextFamilyId++;
        int first = output.Count;

        ResidentData head = ImmigrationRules.CreateApplicant(nextResidentId++, tax, refugee, false);
        head.age = (byte)Random.Range(ResidentData.AdultAge, ResidentData.ElderlyAge);
        head.professionType = ResidentProfessionRules.GetInitialProfession(in head);
        head.familyID = familyId;
        output.Add(head);

        for (int i = 1; i < size; i++)
        {
            ResidentData m = ImmigrationRules.CreateApplicant(nextResidentId++, tax, refugee, false);
            m.familyID = familyId;
            m.lastNameID = head.lastNameID;
            m.originRegion = head.originRegion;
            m.factionType = head.factionType;
            m.age = RollMemberAge(i);
            if (i == 1 && m.GetAgeGroup() == AgeGroupType.Adult)
                m.gender = head.gender == GenderType.Male ? GenderType.Female : GenderType.Male;
            if (m.GetAgeGroup() == AgeGroupType.Child)
                m.wealth = 0;
            m.professionType = ResidentProfessionRules.GetInitialProfession(in m);
            output.Add(m);
        }

        // Bệnh: tính theo gia đình
        if (Random.value < ImmigrationRules.GetLatentChance(head.originRegion, refugee))
        {
            int sickIndex = first + Random.Range(0, size);
            ResidentData sick = output[sickIndex];
            ImmigrationRules.ApplyLatentDisease(ref sick);
            output[sickIndex] = sick;
        }

        for (int i = first; i < output.Count; i++)
        {
            ResidentData m = output[i];
            if (Random.value < InjuredChance)
                m.rolledNotes |= (byte)NoteFlags.Injured;
            if (m.healthStatus == HealthStatus.Incubating && Random.value < IncubatingFeverChance)
                m.rolledNotes |= (byte)NoteFlags.Fever;
            else if (m.healthStatus == HealthStatus.Healthy && Random.value < HealthyCoughChance)
                m.rolledNotes |= (byte)NoteFlags.Cough;
            output[i] = m;
        }
    }

    private static byte RollMemberAge(int memberIndex)
    {
        float roll = Random.value;
        if (memberIndex == 1)   // Vợ / chồng hoặc cha mẹ già
            return roll < 0.7f
                ? (byte)Random.Range(ResidentData.AdultAge, ResidentData.ElderlyAge)
                : (byte)Random.Range(ResidentData.ElderlyAge, ResidentData.ElderlyAge + 4);
        if (roll < 0.7f) return (byte)Random.Range(0, ResidentData.AdultAge);
        if (roll < 0.85f) return (byte)Random.Range(ResidentData.ElderlyAge, ResidentData.ElderlyAge + 4);
        return (byte)Random.Range(ResidentData.AdultAge, ResidentData.ElderlyAge);
    }
}
