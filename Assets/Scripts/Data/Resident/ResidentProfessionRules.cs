public static class ResidentProfessionRules
{
    private const byte DominantStatGap = 10;

    public static ProfessionType GetInitialProfession(in ResidentData resident)
    {
        AgeGroupType ageGroup = resident.GetAgeGroup();
        if (ageGroup == AgeGroupType.Child)
            return ProfessionType.Student;

        if (ageGroup == AgeGroupType.Elderly)
            return ProfessionType.None;

        ProfessionType fromStat = ResolveFromDominantStat(
            resident.strength, resident.endurance, resident.intellect);
        return ResidentSocialRules.ApplyFactionProfessionBias(in resident, fromStat);
    }

    public static ProfessionType ResolveFromDominantStat(byte strength, byte endurance, byte intellect)
    {
        bool strengthLeads = strength >= endurance + DominantStatGap
            && strength >= intellect + DominantStatGap;
        bool enduranceLeads = endurance >= strength + DominantStatGap
            && endurance >= intellect + DominantStatGap;
        bool intellectLeads = intellect >= strength + DominantStatGap
            && intellect >= endurance + DominantStatGap;

        int leadCount = 0;
        if (strengthLeads) leadCount++;
        if (enduranceLeads) leadCount++;
        if (intellectLeads) leadCount++;
        if (leadCount != 1)
            return ProfessionType.None;

        if (strengthLeads)
            return ProfessionType.Farmer;

        if (enduranceLeads)
            return ProfessionType.Miner;

        return ProfessionType.Craftsman;
    }

    public static bool CanReceiveProfession(in ResidentData resident)
    {
        if (!resident.isAlive)
            return false;

        if (resident.professionType != ProfessionType.None)
            return false;

        if (resident.GetAgeGroup() != AgeGroupType.Adult)
            return false;

        if (resident.healthStatus == HealthStatus.ActiveInfected)
            return false;

        return true;
    }

    public static bool TryAssignStudent(ref ResidentData resident)
    {
        if (!resident.isAlive)
            return false;

        if (resident.GetAgeGroup() != AgeGroupType.Child)
            return false;

        if (resident.professionType != ProfessionType.None)
            return false;

        resident.professionType = ProfessionType.Student;
        return true;
    }

    public static bool TryGraduateStudent(ref ResidentData resident)
    {
        if (resident.professionType != ProfessionType.Student)
            return false;

        if (resident.GetAgeGroup() == AgeGroupType.Child)
            return false;

        resident.professionType = GetInitialProfession(in resident);
        ResidentAssignmentRules.ClearWorkAssignment(ref resident);
        return true;
    }

    public static bool TryAssignProfession(ref ResidentData resident, ProfessionType profession)
    {
        if (profession == ProfessionType.None || profession == ProfessionType.Student)
            return false;

        if (!CanReceiveProfession(in resident))
            return false;

        resident.professionType = profession;
        return true;
    }

    public static bool CountsTowardLabor(in ResidentData resident, ProfessionType profession) =>
        resident.professionType == profession
        && ResidentAssignmentRules.CanWork(in resident);

    public static int GetLaborCapacity(in BuildingData building, ProfessionType profession)
    {
        BuildingType preferred = ResidentAssignmentRules.GetPreferredWorkplace(profession);
        if (preferred == BuildingType.None || building.buildingType != preferred)
            return 0;

        if (!ResidentAssignmentRules.IsBuildingAcceptingResidents(in building))
            return 0;

        return building.maxWorkers;
    }
}
