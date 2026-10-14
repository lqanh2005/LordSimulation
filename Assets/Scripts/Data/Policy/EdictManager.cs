using System;
using System.Collections.Generic;
using UnityEngine;

public class EdictManager : MonoBehaviour
{
    public const int MAX_EDICTS = 64;

    [Header("Danh Sách Sắc Lệnh")]
    public EdictRuleData[] allEdicts = new EdictRuleData[MAX_EDICTS];
    public int activeCount = 0;

    [Header("Tham Chiếu")]
    [SerializeField] private GlobalSystemManager globalSystemManager;

    private readonly Dictionary<BuildingType, float> productionBonus = new Dictionary<BuildingType, float>();
    private bool suppressEffects;

    private void Awake()
    {
        if (globalSystemManager == null)
            globalSystemManager = FindFirstObjectByType<GlobalSystemManager>();
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
    }

    public int CreateRule(EdictRuleBuilder builder)
    {
        if (builder == null || !builder.IsValid(out _))
            return -1;

        return CreateRule(builder.Build());
    }

    public int CreateRule(in EdictRuleData rule)
    {
        if (activeCount >= MAX_EDICTS)
            return -1;

        int index = activeCount;
        allEdicts[index] = rule;
        allEdicts[index].ruleID = GetNextRuleId();
        allEdicts[index].executionOrder = (byte)index;
        activeCount++;

        OnRulesModified();
        return index;
    }

    public int AddEdict(in EdictRuleData newEdict)
    {
        return CreateRule(in newEdict);
    }

    public bool UpdateRule(ushort ruleId, EdictRuleBuilder builder)
    {
        if (builder == null || !builder.IsValid(out _))
            return false;

        int index = FindIndexById(ruleId);
        if (index < 0)
            return false;

        EdictRuleData updated = builder.Build();
        updated.ruleID = ruleId;
        updated.executionOrder = allEdicts[index].executionOrder;
        allEdicts[index] = updated;

        OnRulesModified();
        return true;
    }

    public bool SetRuleActive(ushort ruleId, bool active)
    {
        int index = FindIndexById(ruleId);
        if (index < 0)
            return false;

        allEdicts[index].isActive = active;
        OnRulesModified();
        return true;
    }

    public bool ToggleRule(ushort ruleId)
    {
        int index = FindIndexById(ruleId);
        return index >= 0 && SetRuleActive(ruleId, !allEdicts[index].isActive);
    }

    public bool RemoveRule(ushort ruleId)
    {
        int index = FindIndexById(ruleId);
        if (index < 0)
            return false;

        int tail = activeCount - index - 1;
        if (tail > 0)
            Array.Copy(allEdicts, index + 1, allEdicts, index, tail);

        activeCount--;
        allEdicts[activeCount] = default;

        for (int i = 0; i < activeCount; i++)
            allEdicts[i].executionOrder = (byte)i;

        OnRulesModified();
        return true;
    }

    public bool MoveRule(ushort ruleId, int newIndex)
    {
        int index = FindIndexById(ruleId);
        if (index < 0)
            return false;

        newIndex = Mathf.Clamp(newIndex, 0, activeCount - 1);
        if (newIndex == index)
            return true;

        EdictRuleData moving = allEdicts[index];
        if (newIndex < index)
            Array.Copy(allEdicts, newIndex, allEdicts, newIndex + 1, index - newIndex);
        else
            Array.Copy(allEdicts, index + 1, allEdicts, index, newIndex - index);
        allEdicts[newIndex] = moving;

        for (int i = 0; i < activeCount; i++)
            allEdicts[i].executionOrder = (byte)i;

        OnRulesModified();
        return true;
    }

    public int FindIndexById(ushort ruleId)
    {
        for (int i = 0; i < activeCount; i++)
        {
            if (allEdicts[i].ruleID == ruleId)
                return i;
        }

        return -1;
    }

    public ref EdictRuleData GetEdictRef(int index)
    {
        if ((uint)index >= (uint)activeCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return ref allEdicts[index];
    }

    public float GetProductionMultiplier(BuildingType type)
    {
        return productionBonus.TryGetValue(type, out float bonus)
            ? Mathf.Max(0f, 1f + bonus / 100f)
            : 1f;
    }

    public void SetSuppressEffects(bool suppress)
    {
        suppressEffects = suppress;
    }

    public void RefreshAfterLoad()
    {
        EvaluateCityRules(false);
        GameEvents.Post(EventID.EdictsChanged);
    }

    public void EvaluateCityRules(bool applyMonthlyEffects)
    {
        productionBonus.Clear();

        if (globalSystemManager == null)
            return;

        ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
        ResidentData noApplicant = default;

        for (int i = 0; i < activeCount; i++)
        {
            ref EdictRuleData rule = ref allEdicts[i];
            if (!rule.isActive || EdictRules.UsesApplicant(in rule) || !EdictRules.HasCityAction(in rule))
                continue;

            RuleActionData action = EdictRules.Resolve(in rule, in global, in noApplicant, false, out bool matched);
            rule.lastResult = matched;

            if (!EdictRules.IsCityAction(action.type))
                continue;

            if (action.type == RuleActionType.BoostProduction)
            {
                BuildingType target = (BuildingType)(byte)action.param0;
                productionBonus.TryGetValue(target, out float current);
                productionBonus[target] = current + action.param1;
            }

            if (applyMonthlyEffects)
            {
                ApplyMonthlyEffect(in action, ref global);
                GameEvents.Post(EventID.EdictTriggered, rule.ruleID);
            }
        }
    }

    private void ApplyMonthlyEffect(in RuleActionData action, ref GlobalSystemData global)
    {
        switch (action.type)
        {
            case RuleActionType.AddResourceMonthly:
                ResourceType resource = (ResourceType)(byte)action.param0;
                int amount = Mathf.RoundToInt(action.param1);
                if (amount > 0)
                    globalSystemManager.AddResource(resource, amount);
                else if (amount < 0)
                    globalSystemManager.ConsumeAvailable(resource, -amount);
                break;
            case RuleActionType.ModifyGoldMonthly:
                globalSystemManager.ModifyGold(Mathf.RoundToInt(action.param0));
                break;
            case RuleActionType.ModifyReputation:
                globalSystemManager.ModifyReputation(action.param0);
                break;
            case RuleActionType.ReduceRiotRisk:
                global.riotRiskMeter = Mathf.Clamp(global.riotRiskMeter - action.param0, 0f, 100f);
                break;
        }
    }

    private void OnMonthChanged(object param)
    {
        EvaluateCityRules(!suppressEffects);
        GameEvents.Post(EventID.EdictsChanged);
    }

    private void OnRulesModified()
    {
        EvaluateCityRules(false);
        GameEvents.Post(EventID.EdictsChanged);
    }

    private ushort GetNextRuleId()
    {
        int maxId = 0;
        for (int i = 0; i < activeCount; i++)
            maxId = Mathf.Max(maxId, allEdicts[i].ruleID);

        return (ushort)(maxId + 1);
    }
}
