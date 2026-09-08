using UnityEngine;

public static class ResidentDiseaseRules
{
    public const float NormalBodyTemperature = 36.6f;

    public static byte GetIncubationLength(DiseaseType type) => type switch
    {
        DiseaseType.RedFever => 1,
        DiseaseType.LungParasite => 2,
        DiseaseType.BloodPoison => 1,
        _ => 1
    };

    public static byte GetRecoveryLength(DiseaseType type) => type switch
    {
        DiseaseType.RedFever => 1,
        DiseaseType.LungParasite => 2,
        DiseaseType.BloodPoison => 1,
        _ => 1
    };

    public static SymptomFlags GetSymptoms(DiseaseType type) => type switch
    {
        DiseaseType.RedFever => SymptomFlags.Fever | SymptomFlags.Fatigue | SymptomFlags.Headache,
        DiseaseType.LungParasite => SymptomFlags.Cough | SymptomFlags.Fatigue | SymptomFlags.ShortnessOfBreath,
        DiseaseType.BloodPoison => SymptomFlags.Fever | SymptomFlags.Nausea | SymptomFlags.Rash | SymptomFlags.Dizziness,
        _ => SymptomFlags.None
    };

    public static float GetActiveBodyTemperature(DiseaseType type) => type switch
    {
        DiseaseType.RedFever => 39.5f,
        DiseaseType.LungParasite => 38.2f,
        DiseaseType.BloodPoison => 40.0f,
        _ => NormalBodyTemperature
    };

    public static float GetDeathChance(DiseaseType type) => type switch
    {
        DiseaseType.RedFever => 0.04f,
        DiseaseType.LungParasite => 0.06f,
        DiseaseType.BloodPoison => 0.08f,
        _ => 0.03f
    };

    public static float GetHappinessPenalty(DiseaseType type) => type switch
    {
        DiseaseType.RedFever => 0.08f,
        DiseaseType.LungParasite => 0.10f,
        DiseaseType.BloodPoison => 0.12f,
        _ => 0.05f
    };

    public static DiseaseType RollRandomDisease()
    {
        float roll = Random.value;
        if (roll < 0.45f) return DiseaseType.RedFever;
        if (roll < 0.80f) return DiseaseType.LungParasite;
        return DiseaseType.BloodPoison;
    }

    public static void Infect(ref ResidentData r, DiseaseType type)
    {
        r.diseaseType = type;
        r.healthStatus = HealthStatus.Incubating;
        r.IncubationMonths = 0;
        r.recoveryMonths = 0;
        r.symptoms = SymptomFlags.None;
        r.bodyTemperature = NormalBodyTemperature;
    }

    public static void ActivateDisease(ref ResidentData r)
    {
        r.healthStatus = HealthStatus.ActiveInfected;
        r.symptoms = GetSymptoms(r.diseaseType);
        r.bodyTemperature = GetActiveBodyTemperature(r.diseaseType);
        r.IncubationMonths = 0;
        r.recoveryMonths = 0;
    }

    public static void BeginTreatment(ref ResidentData r)
    {
        r.healthStatus = HealthStatus.Treated;
        r.IncubationMonths = 0;
        r.recoveryMonths = 0;
        r.symptoms = SymptomFlags.Fatigue;
        r.bodyTemperature = NormalBodyTemperature + 0.5f;
    }

    public static void ClearDisease(ref ResidentData r)
    {
        r.diseaseType = DiseaseType.None;
        r.healthStatus = HealthStatus.Healthy;
        r.IncubationMonths = 0;
        r.recoveryMonths = 0;
        r.symptoms = SymptomFlags.None;
        r.bodyTemperature = NormalBodyTemperature;
    }
}
