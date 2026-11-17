using System.Collections.Generic;
using UnityEngine;

public enum ResearchNodeState : byte { Locked = 0, Available = 1, Researching = 2, Completed = 3 }

[System.Serializable]
public struct ResearchSaveData
{
    public ulong completedMask;      // Bit i = ResearchID i đã hoàn thành
    public ResearchID currentResearch;
    public float currentProgress;    // Điểm đã tích lũy cho công nghệ đang nghiên cứu
}

/// <summary>
/// Cây nghiên cứu: Giáo viên làm việc ở Trường học sinh điểm nghiên cứu mỗi tháng,
/// điểm dồn vào công nghệ đang chọn. Công nghệ hoàn thành mở khóa công trình / tăng chỉ số.
/// </summary>
public class ResearchManager : MonoBehaviour
{
    [Header("Điểm Nghiên Cứu Mỗi Tháng")]
    [SerializeField] private float basePointsPerMonth = 1f;
    [SerializeField] private float cityHallPoints = 2f;
    [SerializeField] private float pointsPerTeacher = 2.5f;   // x WorkMultiplier của giáo viên
    [SerializeField] private float pointsPerStudent = 0.25f;

    [SerializeField] private ResearchSaveData state;

    private readonly Dictionary<BuildingType, float> productionBonus = new Dictionary<BuildingType, float>();
    private float diseaseReduction;
    private int warmPeople;
    private int goldPerMonth;
    private float researchSpeedBonus;

    public ResearchID CurrentResearch => state.currentResearch;
    public float CurrentProgress => state.currentProgress;
    public float LastPointsPerMonth { get; private set; }
    public float DiseaseReduction => diseaseReduction;
    public int WarmPeople => warmPeople;
    public int GoldPerMonth => goldPerMonth;

    private void OnEnable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
    }

    // ==========================================
    // KHỞI TẠO / SAVE-LOAD
    // ==========================================

    public void Init()
    {
        ApplySaveData(default);
        // Điều kiện "Thân nhiệt" của sắc lệnh chỉ dùng được khi đã có Nhiệt kế
        EdictRules.IsVariableUnlocked = v => v != RuleVariable.ApplicantBodyTemp || IsCompleted(ResearchID.Thermometer);
    }

    public bool HasThermometer => IsCompleted(ResearchID.Thermometer);

    public ResearchSaveData GetSaveData() => state;

    public void ApplySaveData(in ResearchSaveData data)
    {
        state = data;
        if (state.currentResearch != ResearchID.None
            && (ResearchCatalog.Get(state.currentResearch) == null || IsCompleted(state.currentResearch)))
        {
            state.currentResearch = ResearchID.None;
            state.currentProgress = 0f;
        }

        RecalculateEffects();
        LastPointsPerMonth = CalculateMonthlyPoints();
        GameEvents.Post(EventID.ResearchChanged);
    }

    // ==========================================
    // TRẠNG THÁI
    // ==========================================

    public bool IsCompleted(ResearchID id) =>
        id == ResearchID.None || (state.completedMask & Bit(id)) != 0;

    public bool ArePrerequisitesMet(ResearchNode node)
    {
        if (node == null)
            return false;

        for (int i = 0; i < node.prerequisites.Length; i++)
        {
            if (!IsCompleted(node.prerequisites[i]))
                return false;
        }
        return true;
    }

    public ResearchNodeState GetState(ResearchID id)
    {
        if (IsCompleted(id))
            return ResearchNodeState.Completed;
        if (state.currentResearch == id)
            return ResearchNodeState.Researching;
        return ArePrerequisitesMet(ResearchCatalog.Get(id))
            ? ResearchNodeState.Available
            : ResearchNodeState.Locked;
    }

    public bool IsBuildingUnlocked(BuildingType type) =>
        IsCompleted(ResearchCatalog.GetRequiredResearch(type));

    public float GetProductionMultiplier(BuildingType type) =>
        productionBonus.TryGetValue(type, out float bonus) ? 1f + bonus / 100f : 1f;

    public int CompletedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < ResearchCatalog.All.Length; i++)
                if (IsCompleted(ResearchCatalog.All[i].id)) count++;
            return count;
        }
    }

    // ==========================================
    // HÀNH ĐỘNG NGƯỜI CHƠI
    // ==========================================

    /// <summary>Chọn công nghệ để nghiên cứu. Đổi sang công nghệ khác sẽ mất tiến độ hiện tại.</summary>
    public bool StartResearch(ResearchID id)
    {
        ResearchNode node = ResearchCatalog.Get(id);
        if (node == null || IsCompleted(id) || !ArePrerequisitesMet(node))
            return false;

        if (state.currentResearch == id)
            return true;

        state.currentResearch = id;
        state.currentProgress = 0f;
        GameEvents.Post(EventID.ResearchChanged);
        return true;
    }

    public void CancelResearch()
    {
        if (state.currentResearch == ResearchID.None)
            return;

        state.currentResearch = ResearchID.None;
        state.currentProgress = 0f;
        GameEvents.Post(EventID.ResearchChanged);
    }

    // ==========================================
    // TICK HÀNG THÁNG
    // ==========================================

    private void OnMonthChanged(object param)
    {
        if (!(param is MonthChangedPayload payload))
            return;

        LastPointsPerMonth = CalculateMonthlyPoints();
        if (payload.isSimulationTick)
            AddPoints(LastPointsPerMonth);

        GameEvents.Post(EventID.ResearchChanged);
    }

    public void AddPoints(float points)
    {
        if (points <= 0f || state.currentResearch == ResearchID.None)
            return;

        ResearchNode node = ResearchCatalog.Get(state.currentResearch);
        if (node == null)
            return;

        state.currentProgress += points;
        if (state.currentProgress + 0.0001f < node.cost)
            return;

        Complete(node.id);
    }

    private void Complete(ResearchID id)
    {
        state.completedMask |= Bit(id);
        if (state.currentResearch == id)
        {
            state.currentResearch = ResearchID.None;
            state.currentProgress = 0f;
        }

        RecalculateEffects();
        GameEvents.Post(EventID.ResearchCompleted, id);
        GameEvents.Post(EventID.ResearchChanged);
    }

    public float CalculateMonthlyPoints()
    {
        PlayerContain contain = GamePlayController.Instance != null ? GamePlayController.Instance.playerContain : null;
        if (contain == null || contain.buildingManager == null || contain.residentManager == null)
            return 0f;

        BuildingManager buildings = contain.buildingManager;
        bool hasCityHall = false;
        for (int i = 0; i < buildings.activeCount; i++)
        {
            ref BuildingData b = ref buildings.allBuildings[i];
            if (b.buildingType == BuildingType.CityHall && BuildingEffectRules.IsEffectActive(in b))
            {
                hasCityHall = true;
                break;
            }
        }

        float points = basePointsPerMonth + (hasCityHall ? cityHallPoints : 0f);

        ResidentManager residents = contain.residentManager;
        for (int i = 0; i < residents.activeCount; i++)
        {
            ref ResidentData r = ref residents.allResidents[i];
            if (!r.isAlive)
                continue;

            if (ResidentProfessionEffectRules.IsWorkingTeacher(in r))
                points += pointsPerTeacher * r.WorkMultiplier;
            else if (ResidentProfessionEffectRules.IsAttendingStudent(in r))
                points += pointsPerStudent;
        }

        return points * (1f + researchSpeedBonus / 100f);
    }

    private void RecalculateEffects()
    {
        productionBonus.Clear();
        diseaseReduction = 0f;
        warmPeople = 0;
        goldPerMonth = 0;
        researchSpeedBonus = 0f;

        for (int i = 0; i < ResearchCatalog.All.Length; i++)
        {
            ResearchNode node = ResearchCatalog.All[i];
            if (!IsCompleted(node.id))
                continue;

            for (int e = 0; e < node.effects.Length; e++)
            {
                ResearchEffect fx = node.effects[e];
                switch (fx.type)
                {
                    case ResearchEffectType.ProductionBonus:
                        BuildingType target = (BuildingType)fx.param0;
                        productionBonus.TryGetValue(target, out float current);
                        productionBonus[target] = current + fx.param1;
                        break;
                    case ResearchEffectType.DiseaseReduction:
                        diseaseReduction += fx.param1;
                        break;
                    case ResearchEffectType.WarmPeople:
                        warmPeople += Mathf.RoundToInt(fx.param1);
                        break;
                    case ResearchEffectType.GoldPerMonth:
                        goldPerMonth += Mathf.RoundToInt(fx.param1);
                        break;
                    case ResearchEffectType.ResearchSpeed:
                        researchSpeedBonus += fx.param1;
                        break;
                }
            }
        }
    }

    private static ulong Bit(ResearchID id) => 1UL << ((int)id & 63);
}
