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

        // Điều trị bệnh nhân Infirmary chuyển sang ResidentDiseaseSystem (GDD 7.4)
        ApplySchooling(residents, residentCount, buildings, buildingCount);
        // Tuần tra giảm Riot đã bỏ: Riot theo công thức GDD 9.6 (Thiết quân luật là [Later]); lính gác lo thu gom thi thể
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

    /// <summary>Số dân thật mà mỗi cư dân mẫu còn sống đại diện (>= 1).</summary>
    public static float GetSampleScale(ResidentData[] residents, int residentCount, GlobalSystemManager global)
    {
        if (global == null)
            return 1f;
        int alive = 0;
        for (int i = 0; i < residentCount; i++)
            if (residents[i].isAlive)
                alive++;
        int pop = global.GetGlobalDataRef().totalPopulation;
        return alive > 0 && pop > alive ? (float)pop / alive : 1f;
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
