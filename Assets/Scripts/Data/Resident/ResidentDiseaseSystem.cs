using UnityEngine;

public static class ResidentDiseaseSystem
{
    private const float MaxMonthlyInfectChance = 0.08f;
    private const float HungerDeathBonus = 0.02f;
    private const float ColdDeathBonus = 0.02f;

    public static int ProcessMonthly(
        ResidentData[] residents,
        int activeCount,
        float diseasePressure,
        float spreadFactor,
        float naturalRecoveryChance,
        GlobalSystemManager global)
    {
        diseasePressure = Mathf.Clamp01(diseasePressure);

        int alive = 0;
        int carrierCount = 0;
        int activeInfectedCount = 0;
        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            alive++;
            if (r.healthStatus == HealthStatus.ActiveInfected)
                activeInfectedCount++;
            if (r.healthStatus == HealthStatus.ActiveInfected
                || r.healthStatus == HealthStatus.Incubating)
                carrierCount++;
        }

        float infectedRate = alive > 0 ? (float)carrierCount / alive : 0f;
        float infectChance = Mathf.Min(
            MaxMonthlyInfectChance,
            diseasePressure * 0.5f + infectedRate * spreadFactor * 0.5f);

        int medicineLeft = 0;
        if (global != null)
            medicineLeft = Mathf.Max(0, global.GetGlobalDataRef().stockMedicine);

        int remainingPatients = activeInfectedCount;
        int deaths = 0;

        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            switch (r.healthStatus)
            {
                case HealthStatus.Healthy:
                    if (infectChance > 0f && Random.value < infectChance)
                        ResidentDiseaseRules.Infect(ref r, ResidentDiseaseRules.RollRandomDisease());
                    break;

                case HealthStatus.Incubating:
                    TickIncubation(ref r);
                    break;

                case HealthStatus.ActiveInfected:
                {
                    bool cured = false;
                    if (remainingPatients > 0 && medicineLeft > 0)
                    {
                        float cureChance = (float)medicineLeft / remainingPatients;
                        remainingPatients--;
                        if (Random.value < cureChance && global != null && global.TryConsumeMedicine(1))
                        {
                            medicineLeft--;
                            ResidentDiseaseRules.BeginTreatment(ref r);
                            cured = true;
                        }
                    }
                    else if (remainingPatients > 0)
                    {
                        remainingPatients--;
                    }

                    if (!cured && TickActiveInfection(ref r, naturalRecoveryChance))
                        deaths++;
                    break;
                }

                case HealthStatus.Treated:
                    TickTreatment(ref r);
                    break;
            }
        }

        if (global != null && alive > 0 && deaths > 0)
        {
            ref GlobalSystemData globalData = ref global.GetGlobalDataRef();
            float sampleDeathRate = (float)deaths / alive;
            int macroDeaths = Mathf.RoundToInt(globalData.totalPopulation * sampleDeathRate);
            globalData.totalPopulation = Mathf.Max(0, globalData.totalPopulation - macroDeaths);
        }

        return deaths;
    }

    private static void TickIncubation(ref ResidentData r)
    {
        if (r.IncubationMonths < byte.MaxValue)
            r.IncubationMonths++;

        if (r.IncubationMonths >= ResidentDiseaseRules.GetIncubationLength(r.diseaseType))
            ResidentDiseaseRules.ActivateDisease(ref r);
    }

    private static bool TickActiveInfection(ref ResidentData r, float naturalRecoveryChance)
    {
        DiseaseType type = r.diseaseType;
        r.happiness = Mathf.Max(0f, r.happiness - ResidentDiseaseRules.GetHappinessPenalty(type));

        float deathChance = ResidentDiseaseRules.GetDeathChance(type);
        if (r.hungerMonths > 0)
            deathChance += HungerDeathBonus;
        if (r.coldMonths > 0)
            deathChance += ColdDeathBonus;

        if (Random.value < deathChance)
        {
            r.isAlive = false;
            ResidentDiseaseRules.ClearDisease(ref r);
            return true;
        }

        if (Random.value < naturalRecoveryChance)
            ResidentDiseaseRules.BeginTreatment(ref r);

        return false;
    }

    private static void TickTreatment(ref ResidentData r)
    {
        if (r.recoveryMonths < byte.MaxValue)
            r.recoveryMonths++;

        if (r.recoveryMonths >= ResidentDiseaseRules.GetRecoveryLength(r.diseaseType))
            ResidentDiseaseRules.ClearDisease(ref r);
    }
}
