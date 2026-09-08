public static class ResidentProfessionSystem
{
    public static int ProcessMonthly(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        if (residents == null || buildings == null)
            return 0;

        int openSlots = CountOpenFarmerSlots(residents, residentCount, buildings, buildingCount);
        if (openSlots <= 0)
            return 0;

        int assigned = 0;
        for (int i = 0; i < residentCount && openSlots > 0; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!ResidentProfessionRules.TryAssignFarmer(ref r))
                continue;

            openSlots--;
            assigned++;
        }

        return assigned;
    }

    private static int CountOpenFarmerSlots(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        int capacity = 0;
        for (int i = 0; i < buildingCount; i++)
            capacity += ResidentProfessionRules.GetFarmerCapacity(in buildings[i]);

        int workingFarmers = 0;
        for (int i = 0; i < residentCount; i++)
        {
            if (ResidentProfessionRules.CountsTowardFarmerLabor(in residents[i]))
                workingFarmers++;
        }

        int open = capacity - workingFarmers;
        return open > 0 ? open : 0;
    }
}
