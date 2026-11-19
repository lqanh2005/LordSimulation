using UnityEngine;

public enum DayPhase : byte { Dawn = 0, Morning = 1, Noon = 2, Afternoon = 3, Evening = 4, Night = 5 }

/// <summary>
/// Vòng ngày–đêm CHỈ ĐỂ HIỂN THỊ (giống Cities: Skylines II): 1 tháng mô phỏng = 1 vòng ngày–đêm.
/// Không đếm "ngày thứ mấy" — lịch chỉ có tháng / mùa. Giờ trong vòng suy ra từ timeTickProgress:
/// đầu tháng (lúc tick mô phỏng) là 6h sáng, giữa tháng là 18h.
/// Mọi logic tiêu thụ / sản xuất / save vẫn chạy theo tháng như cũ.
/// </summary>
public static class DayCycle
{
    public const float StartHour = 6f;   // Giờ lúc bắt đầu tháng

    // Lịch sinh hoạt (giờ trong vòng)
    public const float WorkStart = 7f;
    public const float WorkEnd = 17f;
    public const float SchoolStart = 8f;
    public const float SchoolEnd = 15f;
    public const float NightShiftStart = 19f;
    public const float NightShiftEnd = 6f;
    public const float OutingStart = 9f;    // Người không đi làm ra quảng trường
    public const float OutingEnd = 16f;
    public const float BedTime = 21f;       // Sau giờ này người ở nhà vào trong (ẩn hình)
    public const float WakeTime = 5.5f;

    /// <summary>Giờ trong vòng (0–24) theo tiến độ tháng.</summary>
    public static float GetHour(float monthProgress) =>
        Mathf.Repeat(StartHour + Mathf.Clamp01(monthProgress) * 24f, 24f);

    public static float GetHour(in GlobalSystemData data) => GetHour(data.timeTickProgress);

    /// <summary>Giờ có nằm trong khoảng [from, to) không (hỗ trợ khoảng qua nửa đêm).</summary>
    public static bool IsBetween(float hour, float from, float to)
    {
        hour = Mathf.Repeat(hour, 24f);
        from = Mathf.Repeat(from, 24f);
        to = Mathf.Repeat(to, 24f);
        return from <= to ? hour >= from && hour < to : hour >= from || hour < to;
    }

    /// <summary>Giờ mặt trời mọc / lặn theo mùa — mùa đông ngày ngắn, đêm dài.</summary>
    public static void GetSunTimes(SeasonType season, out float sunrise, out float sunset)
    {
        switch (season)
        {
            case SeasonType.Summer: sunrise = 5f; sunset = 20f; break;
            case SeasonType.Autumn: sunrise = 6.5f; sunset = 17.5f; break;
            case SeasonType.Winter: sunrise = 7.5f; sunset = 16.5f; break;
            default: sunrise = 6f; sunset = 18.5f; break;
        }
    }

    public static DayPhase GetPhase(float hour, SeasonType season)
    {
        GetSunTimes(season, out float sunrise, out float sunset);
        if (IsBetween(hour, sunrise - 1f, sunrise + 1f)) return DayPhase.Dawn;
        if (IsBetween(hour, sunrise + 1f, 11f)) return DayPhase.Morning;
        if (IsBetween(hour, 11f, 14f)) return DayPhase.Noon;
        if (IsBetween(hour, 14f, sunset - 1f)) return DayPhase.Afternoon;
        if (IsBetween(hour, sunset - 1f, sunset + 1.5f)) return DayPhase.Evening;
        return DayPhase.Night;
    }

    public static string GetPhaseName(DayPhase phase) => phase switch
    {
        DayPhase.Dawn => "Bình minh",
        DayPhase.Morning => "Buổi sáng",
        DayPhase.Noon => "Buổi trưa",
        DayPhase.Afternoon => "Buổi chiều",
        DayPhase.Evening => "Chạng vạng",
        _ => "Ban đêm"
    };

    /// <summary>
    /// Độ tối 0 (trưa) → 1 (nửa đêm), chuyển mượt quanh giờ mọc / lặn. Dùng cho lớp phủ ánh sáng.
    /// </summary>
    public static float GetDarkness(float hour, SeasonType season)
    {
        GetSunTimes(season, out float sunrise, out float sunset);
        const float fade = 1.25f;   // Số giờ chạng vạng
        if (IsBetween(hour, sunrise + fade, sunset - fade))
            return 0f;
        if (IsBetween(hour, sunrise - fade, sunrise + fade))
            return 1f - Mathf.InverseLerp(sunrise - fade, sunrise + fade, Unwrap(hour, sunrise));
        if (IsBetween(hour, sunset - fade, sunset + fade))
            return Mathf.InverseLerp(sunset - fade, sunset + fade, Unwrap(hour, sunset));
        return 1f;
    }

    // Đưa giờ về cùng phía với mốc (tránh nhảy 0/24 khi so khoảng quanh mốc)
    private static float Unwrap(float hour, float pivot)
    {
        float diff = Mathf.DeltaAngle(pivot * 15f, hour * 15f) / 15f;
        return pivot + diff;
    }

    /// <summary>Lệch giờ ngẫu nhiên nhưng cố định theo từng người (giờ), trong [-range, range].</summary>
    public static float PersonalJitter(int residentId, float range, int salt = 0)
    {
        uint h = (uint)(residentId * 73856093) ^ (uint)(salt * 19349663);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return ((h & 0xFFFF) / 65535f * 2f - 1f) * range;
    }

    /// <summary>Số nguyên băm cố định theo người (chọn ca đêm, đi quảng trường…).</summary>
    public static int PersonalHash(int residentId, int salt) =>
        (int)((uint)(residentId * 2654435761u ^ (uint)salt * 40503u) >> 8);
}
