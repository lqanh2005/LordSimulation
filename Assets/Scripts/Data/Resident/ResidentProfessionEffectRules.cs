using UnityEngine;

public static class ResidentProfessionEffectRules
{
    public const byte MaxIntellect = 100;
    public const float StudentHappinessGain = 0.03f;
    public const float TeacherHappinessGain = 0.01f;
    public const float TreatedHappinessGain = 0.04f;

    public static bool IsWorkingDoctor(in ResidentData resident) =>
        resident.professionType == ProfessionType.Doctor
        && ResidentAssignmentRules.CanWork(in resident)
        && resident.assignedWorkID >= 0;

    public static bool IsWorkingTeacher(in ResidentData resident) =>
        resident.professionType == ProfessionType.Teacher
        && ResidentAssignmentRules.CanWork(in resident)
        && resident.assignedWorkID >= 0;

    public static bool IsAttendingStudent(in ResidentData resident) =>
        ResidentAssignmentRules.CanStudy(in resident)
        && resident.assignedWorkID >= 0;

    public static bool IsWorkingGuard(in ResidentData resident) =>
        resident.professionType == ProfessionType.Guard
        && ResidentAssignmentRules.CanWork(in resident)
        && resident.assignedWorkID >= 0;

    public static int GetDoctorCareTokens(in ResidentData doctor)
    {
        int tokens = 1 + doctor.GetRelevantSkill() / 40;
        return tokens > 4 ? 4 : tokens;
    }

    public static float GetUnmedicatedTreatChance(float averageWorkMultiplier)
    {
        float chance = 0.12f * averageWorkMultiplier;
        if (chance < 0f)
            return 0f;
        return chance > 0.35f ? 0.35f : chance;
    }

    public static float GetDoctorCoverage(float doctorPower, int patientCount)
    {
        if (doctorPower <= 0f || patientCount <= 0)
            return 0f;

        float coverage = doctorPower / patientCount;
        return coverage > 1f ? 1f : coverage;
    }

    public static float ScaleDeathChance(float deathChance, float doctorCoverage) =>
        deathChance * (1f - 0.4f * doctorCoverage);

    public static float ScaleMedicineCureChance(float baseChance, float doctorCoverage)
    {
        float chance = baseChance * (1f + 0.5f * doctorCoverage);
        return chance > 1f ? 1f : chance;
    }

    public static bool TryGainSchoolIntellect(ref ResidentData student, float teacherPower)
    {
        if (student.intellect >= MaxIntellect)
            return false;

        float chance = 0.08f + teacherPower * 0.06f;
        if (chance > 0.35f)
            chance = 0.35f;

        if (Random.value >= chance)
            return false;

        student.intellect++;
        return true;
    }

    public static void AddHappiness(ref ResidentData resident, float amount)
    {
        float next = resident.happiness + amount;
        if (next < 0f)
            next = 0f;
        else if (next > 1f)
            next = 1f;
        resident.happiness = next;
    }

    public static float GetRiotReduction(float guardPower, int aliveCount)
    {
        if (guardPower <= 0f || aliveCount <= 0)
            return 0f;

        float needed = aliveCount / 50f;
        if (needed < 1f)
            needed = 1f;

        float coverage = guardPower / needed;
        if (coverage > 2f)
            coverage = 2f;

        float reduction = coverage * 6f;
        return reduction > 15f ? 15f : reduction;
    }
}
