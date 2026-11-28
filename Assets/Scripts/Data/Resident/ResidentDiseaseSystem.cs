using UnityEngine;

/// <summary>
/// Sốt Đỏ theo GDD v0.3 mục 7.4, tính mỗi tháng:
/// - Incubating đúng 1 tháng (mức lây 0,3, vẫn làm việc) → Infected (mức lây 1,0, không làm việc)
///   → Recovering 1 tháng (không lây, 50% hiệu suất) → Healthy, miễn dịch 12 tháng.
/// - P(bị lây) = 1 − Π(1 − β × mức lây của từng người bệnh tiếp xúc), nhân hệ số cá nhân.
///   β: cùng nhà 30%, cùng chỗ làm 10%, homeless với nhau 15%, trong Trạm cách ly 15%, vùng Miasma 20%.
/// - Infected mỗi tháng: không điều trị chết 15% / hồi phục 25%; được điều trị (giường + bác sĩ + thuốc) 5% / 60%.
/// - Người nằm Infirmary (Clinic) không lây cho nhà và chỗ làm.
/// </summary>
public static class ResidentDiseaseSystem
{
    // ===== Hằng số GDD 7.4 =====
    public const float IncubatingInfectiousness = 0.3f;
    public const float InfectedInfectiousness = 1f;
    public const float BetaHouse = 0.30f;
    public const float BetaWork = 0.10f;
    public const float BetaHomeless = 0.15f;
    public const float BetaQuarantine = 0.15f;
    public const float MiasmaInfectChance = 0.20f;

    public const float HungryMultiplier = 1.5f;
    public const float ColdMultiplier = 1.5f;
    public const float ChildOrElderlyMultiplier = 1.3f;

    public const float UntreatedDeath = 0.15f;
    public const float UntreatedRecover = 0.25f;
    public const float TreatedDeath = 0.05f;
    public const float TreatedRecover = 0.60f;
    public const float ElderlyDeathMultiplier = 2f;
    public const float ChildDeathMultiplier = 1.5f;
    public const float HungryDeathMultiplier = 1.5f;
    public const float ColdDeathMultiplier = 1.5f;

    public const byte ImmunityMonths = 12;
    public const int InfirmaryBeds = 8;           // Mỗi Infirmary (Clinic)
    public const int PatientsPerDoctor = 4;
    public const int OutbreakInfectedThreshold = 3;   // Từ 3 người Infected -> Critical "Dịch bùng phát"

    /// <summary>Tháng gần nhất: có người Infected không được nằm Infirmary (Riot +5).</summary>
    public static bool LastInfirmaryOverloaded { get; private set; }
    public static int LastTreatedCount { get; private set; }
    public static int LastUntreatedCount { get; private set; }

    // Tích Π(1 − β × mức lây) theo id công trình (nhà / chỗ làm)
    private static readonly float[] HouseSurvive = new float[ushort.MaxValue + 1];
    private static readonly float[] WorkSurvive = new float[ushort.MaxValue + 1];
    private static readonly bool[] Dirty = new bool[ushort.MaxValue + 1];
    private static readonly ushort[] DirtyIds = new ushort[ushort.MaxValue + 1];
    private static int dirtyCount;

    private static readonly BuildingType[] TypeById = new BuildingType[ushort.MaxValue + 1];
    private static readonly bool[] OvercrowdedById = new bool[ushort.MaxValue + 1];
    public const float OvercrowdedMultiplier = 2f;   // Nhà bị dồn quá sức chứa ×2
    private static readonly int[] DoctorSlots = new int[ushort.MaxValue + 1];   // Số bệnh nhân bác sĩ lo được / Infirmary

    public static void ResetStats()
    {
        LastInfirmaryOverloaded = false;
        LastTreatedCount = 0;
        LastUntreatedCount = 0;
    }

    /// <param name="miasmaCoverage">Xác suất một cư dân đang ở trong ít nhất một vùng Miasma (0–1).</param>
    public static int ProcessMonthly(
        ResidentData[] residents,
        int activeCount,
        BuildingData[] buildings,
        int buildingCount,
        float miasmaCoverage,
        GlobalSystemManager global)
    {
        miasmaCoverage = Mathf.Clamp01(miasmaCoverage);
        ClearMarks();
        FillBuildingTypes(buildings, buildingCount);

        // 1. Tiếp xúc: trạng thái đầu tháng (người lây trong tháng này chỉ bắt đầu lây từ tháng sau)
        float homelessSurvive = 1f;
        int alive = 0;
        int infectedTotal = 0;
        int admitted = 0;
        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;
            alive++;

            if (IsDoctorAtInfirmary(in r))
                DoctorSlots[(ushort)r.assignedWorkID] += PatientsPerDoctor;

            float level = GetInfectiousness(in r);
            if (r.healthStatus == HealthStatus.ActiveInfected)
            {
                infectedTotal++;
                if (IsInInfirmary(in r))
                {
                    admitted++;
                    continue;   // Nằm viện: không lây cho nhà và chỗ làm
                }
            }
            if (level <= 0f)
                continue;

            if (r.assignedHouseID >= 0)
            {
                ushort house = (ushort)r.assignedHouseID;
                float beta = TypeById[house] == BuildingType.QuarantineWard ? BetaQuarantine : BetaHouse;
                Mark(house);
                HouseSurvive[house] *= 1f - beta * level;
            }
            else
            {
                homelessSurvive *= 1f - BetaHomeless * level;
            }

            if (r.assignedWorkID >= 0 && TypeById[(ushort)r.assignedWorkID] != BuildingType.Clinic)
            {
                ushort work = (ushort)r.assignedWorkID;
                Mark(work);
                WorkSurvive[work] *= 1f - BetaWork * level;
            }
        }

        // GDD 9.6: Infirmary quá tải khi còn người Infected không có giường
        LastInfirmaryOverloaded = infectedTotal > admitted;

        // Thuốc: mỗi bệnh nhân mẫu đại diện cho (dân thật / mẫu) người
        int medicinePerPatient = 1;
        if (global != null && alive > 0)
        {
            int pop = global.GetGlobalDataRef().totalPopulation;
            medicinePerPatient = Mathf.Max(1, Mathf.RoundToInt(pop > alive ? (float)pop / alive : 1f));
        }

        // 2. Chuyển trạng thái
        int deaths = 0;
        LastTreatedCount = 0;
        LastUntreatedCount = 0;
        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            if (r.immunityMonths > 0 && r.healthStatus == HealthStatus.Healthy)
                r.immunityMonths--;

            switch (r.healthStatus)
            {
                case HealthStatus.Healthy:
                    TryInfect(ref r, homelessSurvive, miasmaCoverage);
                    break;

                case HealthStatus.Incubating:
                    if (r.IncubationMonths < byte.MaxValue)
                        r.IncubationMonths++;
                    if (r.IncubationMonths >= ResidentDiseaseRules.GetIncubationLength(r.diseaseType))
                        ResidentDiseaseRules.ActivateDisease(ref r);
                    break;

                case HealthStatus.ActiveInfected:
                    bool treated = TryTreat(in r, global, medicinePerPatient);
                    if (treated) LastTreatedCount++;
                    else LastUntreatedCount++;
                    if (TickInfected(ref r, treated))
                        deaths++;
                    break;

                case HealthStatus.Treated:   // Recovering
                    if (r.recoveryMonths < byte.MaxValue)
                        r.recoveryMonths++;
                    if (r.recoveryMonths >= ResidentDiseaseRules.GetRecoveryLength(r.diseaseType))
                    {
                        ResidentDiseaseRules.ClearDisease(ref r);
                        r.immunityMonths = ImmunityMonths;
                    }
                    break;
            }
        }

        if (global != null && alive > 0 && deaths > 0)
        {
            ref GlobalSystemData globalData = ref global.GetGlobalDataRef();
            float sampleDeathRate = (float)deaths / alive;
            int macroDeaths = Mathf.RoundToInt(globalData.totalPopulation * sampleDeathRate);
            globalData.totalPopulation = Mathf.Max(0, globalData.totalPopulation - macroDeaths);
            if (macroDeaths > 0)
                GameEvents.Post(EventID.ResidentsDied, new ResidentsDiedPayload { cause = DeathCause.Disease, count = macroDeaths });
        }

        return deaths;
    }

    public static float GetInfectiousness(in ResidentData r) => r.healthStatus switch
    {
        HealthStatus.Incubating => IncubatingInfectiousness,
        HealthStatus.ActiveInfected => InfectedInfectiousness,
        _ => 0f
    };

    /// <summary>Hệ số cá nhân: đói ×1,5; lạnh ×1,5; trẻ em / người già ×1,3.</summary>
    public static float GetPersonalMultiplier(in ResidentData r)
    {
        float mult = 1f;
        if (r.hungerMonths > 0 || r.thirstMonths > 0)
            mult *= HungryMultiplier;
        if (r.coldMonths > 0)
            mult *= ColdMultiplier;
        if (r.GetAgeGroup() != AgeGroupType.Adult)
            mult *= ChildOrElderlyMultiplier;
        return mult;
    }

    private static void TryInfect(ref ResidentData r, float homelessSurvive, float miasmaCoverage)
    {
        if (r.immunityMonths > 0)
            return;

        float survive = 1f;
        if (r.assignedHouseID >= 0)
        {
            ushort house = (ushort)r.assignedHouseID;
            if (Dirty[house])
                survive *= HouseSurvive[house];
        }
        else
        {
            survive *= homelessSurvive;
        }

        if (r.assignedWorkID >= 0)
        {
            ushort work = (ushort)r.assignedWorkID;
            if (Dirty[work])
                survive *= WorkSurvive[work];
        }

        // Vùng Miasma: 20% cố định, không cộng dồn giữa các vùng
        if (miasmaCoverage > 0f && Random.value < miasmaCoverage)
            survive *= 1f - MiasmaInfectChance;

        float personal = GetPersonalMultiplier(in r);
        if (r.assignedHouseID >= 0 && OvercrowdedById[(ushort)r.assignedHouseID])
            personal *= OvercrowdedMultiplier;
        float chance = Mathf.Clamp01((1f - survive) * personal);
        if (chance > 0f && Random.value < chance)
            ResidentDiseaseRules.Infect(ref r, DiseaseType.RedFever);
    }

    /// <summary>Được điều trị khi đủ cả ba: giường Infirmary, bác sĩ (1 lo 4 người) và 1 Medicine / tháng.</summary>
    private static bool TryTreat(in ResidentData r, GlobalSystemManager global, int medicinePerPatient)
    {
        if (!IsInInfirmary(in r))
            return false;
        ushort clinic = (ushort)r.assignedWorkID;
        if (DoctorSlots[clinic] <= 0)
            return false;
        if (global == null || !global.TryConsumeMedicine(medicinePerPatient))
            return false;
        DoctorSlots[clinic]--;
        return true;
    }

    /// <summary>Trả true nếu người bệnh chết.</summary>
    private static bool TickInfected(ref ResidentData r, bool treated)
    {
        float death = treated ? TreatedDeath : UntreatedDeath;
        float recover = treated ? TreatedRecover : UntreatedRecover;

        AgeGroupType age = r.GetAgeGroup();
        if (age == AgeGroupType.Elderly) death *= ElderlyDeathMultiplier;
        else if (age == AgeGroupType.Child) death *= ChildDeathMultiplier;
        if (r.hungerMonths > 0 || r.thirstMonths > 0) death *= HungryDeathMultiplier;
        if (r.coldMonths > 0) death *= ColdDeathMultiplier;
        death = Mathf.Min(death, 0.95f);

        float roll = Random.value;
        if (roll < death)
        {
            r.isAlive = false;
            ResidentDiseaseRules.ClearDisease(ref r);
            return true;
        }

        if (roll < death + recover)
            ResidentDiseaseRules.BeginTreatment(ref r);   // -> Recovering
        return false;
    }

    public static bool IsInInfirmary(in ResidentData r) =>
        r.assignedWorkID >= 0 && TypeById[(ushort)r.assignedWorkID] == BuildingType.Clinic
        && !IsDoctorAtInfirmary(in r);

    private static bool IsDoctorAtInfirmary(in ResidentData r) =>
        ResidentProfessionEffectRules.IsWorkingDoctor(in r)
        && TypeById[(ushort)r.assignedWorkID] == BuildingType.Clinic;

    private static void Mark(ushort id)
    {
        if (Dirty[id])
            return;
        Dirty[id] = true;
        HouseSurvive[id] = 1f;
        WorkSurvive[id] = 1f;
        DirtyIds[dirtyCount++] = id;
    }

    private static void ClearMarks()
    {
        for (int i = 0; i < dirtyCount; i++)
            Dirty[DirtyIds[i]] = false;
        dirtyCount = 0;
    }

    private static void FillBuildingTypes(BuildingData[] buildings, int buildingCount)
    {
        System.Array.Clear(TypeById, 0, TypeById.Length);
        System.Array.Clear(DoctorSlots, 0, DoctorSlots.Length);
        System.Array.Clear(OvercrowdedById, 0, OvercrowdedById.Length);
        if (buildings == null)
            return;
        for (int i = 0; i < buildingCount; i++)
        {
            ref BuildingData b = ref buildings[i];
            if (b.buildingState == BuildingState.Active && b.isOperational)
                TypeById[b.buildingID] = b.buildingType;
            OvercrowdedById[b.buildingID] = b.currentOccupancy > b.maxOccupancy;
        }
    }
}
