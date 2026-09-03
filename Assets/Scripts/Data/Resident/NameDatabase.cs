public static class NameDatabase
{
    public static readonly string[] FirstNames = { "Arthur", "John", "David", "Elena", "Clara", "Marcus", "Sarah", "Victor", "Anna", "Leo" };
    public static readonly string[] LastNames = { "Smith", "Miller", "Taylor", "Brown", "Wilson", "Davies", "Evans", "Thomas", "Johnson", "Roberts" };

    public static string GetFullName(ushort firstIdx, ushort lastIdx)
    {
        string first = firstIdx < FirstNames.Length ? FirstNames[firstIdx] : "Unknown";
        string last = lastIdx < LastNames.Length ? LastNames[lastIdx] : "Citizen";
        return $"{first} {last}";
    }
}