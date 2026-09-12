using UnityEngine;

public static class NameDatabase
{
    public static readonly string[] FirstNames = { "Arthur", "John", "David", "Elena", "Clara", "Marcus", "Sarah", "Victor", "Anna", "Leo" };
    public static readonly string[] LastNames = { "Smith", "Miller", "Taylor", "Brown", "Wilson", "Davies", "Evans", "Thomas", "Johnson", "Roberts" };

    private static readonly ushort[] MaleNameIds = { 0, 1, 2, 5, 7, 9 };
    private static readonly ushort[] FemaleNameIds = { 3, 4, 6, 8 };

    public static string GetFullName(ushort firstIdx, ushort lastIdx)
    {
        string first = firstIdx < FirstNames.Length ? FirstNames[firstIdx] : "Unknown";
        string last = lastIdx < LastNames.Length ? LastNames[lastIdx] : "Citizen";
        return $"{first} {last}";
    }

    public static ushort RollFirstNameId(GenderType gender)
    {
        ushort[] pool = gender == GenderType.Female ? FemaleNameIds : MaleNameIds;
        return pool[Random.Range(0, pool.Length)];
    }
}
