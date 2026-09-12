using UnityEngine;

public static class ResidentProfessionEffectSystem
{
    private static readonly float[] TeacherPowerByBuilding = new float[BuildingManager.MAX_BUILDINGS];

    public static void ProcessMonthly(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount,
        GlobalSystemManager global)
    {
        if (residents == null)
            return;

        ApplyDoctorCare(residents, residentCount, global);
        ApplySchooling(residents, residentCount, buildings, buildingCount);
        ApplyGuardPatrol(residents, residentCount, global);
    }

    private static void ApplyDoctorCare(
        ResidentData[] residents,
        int residentCount,
        GlobalSystemManager global)
    {
        int doctorCount = 0;
        int careTokens = 0;
        float powerSum = 0f;

        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData doctor = ref residents[i];
            if (!ResidentProfessionEffectRules.IsWorkingDoctor(in doctor))
                continue;

            doctorCount++;
            powerSum += doctor.WorkMultiplier;
            careTokens += ResidentProfessionEffectRules.GetDoctorCareTokens(in doctor);
        }

        if (careTokens <= 0)
            return;

        float treatChance = ResidentProfessionEffectRules.GetUnmedicatedTreatChance(
            doctorCount > 0 ? powerSum / doctorCount : 0f);

        for (int i = 0; i < residentCount && careTokens > 0; i++)
        {
            ref ResidentData patient = ref residents[i];
            if (!patient.isAlive || patient.healthStatus != HealthStatus.ActiveInfected)
                continue;

            careTokens--;

            bool treated = false;
            if (global != null && global.TryConsumeMedicine(1))
                treated = true;
            else if (treatChance > 0f && Random.value < treatChance)
                treated = true;

            if (!treated)
                continue;

            ResidentDiseaseRules.BeginTreatment(ref patient);
            ResidentProfessionEffectRules.AddHappiness(
                ref patient, ResidentProfessionEffectRules.TreatedHappinessGain);
        }
    }

    private static void ApplySchooling(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        if (buildings == null || buildingCount <= 0)
            return;

        for (int i = 0; i < buildingCount; i++)
            TeacherPowerByBuilding[i] = 0f;

        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData teacher = ref residents[i];
            if (!ResidentProfessionEffectRules.IsWorkingTeacher(in teacher))
                continue;

            int schoolIndex = FindBuildingIndexById(
                buildings, buildingCount, (ushort)teacher.assignedWorkID);
            if (schoolIndex < 0)
                continue;

            TeacherPowerByBuilding[schoolIndex] += teacher.WorkMultiplier;
            ResidentProfessionEffectRules.AddHappiness(
                ref teacher, ResidentProfessionEffectRules.TeacherHappinessGain);
        }

        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData student = ref residents[i];
            if (!ResidentProfessionEffectRules.IsAttendingStudent(in student))
                continue;

            int schoolIndex = FindBuildingIndexById(
                buildings, buildingCount, (ushort)student.assignedWorkID);
            if (schoolIndex < 0)
                continue;

            float teacherPower = TeacherPowerByBuilding[schoolIndex];
            if (teacherPower <= 0f)
                continue;

            ResidentProfessionEffectRules.TryGainSchoolIntellect(ref student, teacherPower);
            ResidentProfessionEffectRules.AddHappiness(
                ref student, ResidentProfessionEffectRules.StudentHappinessGain);
        }
    }

    private static void ApplyGuardPatrol(
        ResidentData[] residents,
        int residentCount,
        GlobalSystemManager global)
    {
        if (global == null)
            return;

        int alive = 0;
        float guardPower = 0f;

        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            alive++;
            if (ResidentProfessionEffectRules.IsWorkingGuard(in r))
                guardPower += r.WorkMultiplier;
        }

        float reduction = ResidentProfessionEffectRules.GetRiotReduction(guardPower, alive);
        if (reduction <= 0f)
            return;

        ref GlobalSystemData data = ref global.GetGlobalDataRef();
        data.riotRiskMeter = Mathf.Max(0f, data.riotRiskMeter - reduction);
    }

    private static int FindBuildingIndexById(
        BuildingData[] buildings,
        int buildingCount,
        ushort buildingId)
    {
        for (int i = 0; i < buildingCount; i++)
        {
            if (buildings[i].buildingID == buildingId)
                return i;
        }

        return -1;
    }
}
