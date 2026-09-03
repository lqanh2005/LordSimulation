using UnityEngine;

public class EdictManager : MonoBehaviour
{
    public const int MAX_EDICTS = 64;

    [Header("Danh Sách Sắc Lệnh")]
    public EdictRuleData[] allEdicts = new EdictRuleData[MAX_EDICTS];
    public int activeCount = 0;

    public int AddEdict(in EdictRuleData newEdict)
    {
        if (activeCount >= MAX_EDICTS)
        {
            Debug.LogError("[EdictManager] Đã đạt giới hạn số lượng sắc lệnh!");
            return -1;
        }

        int index = activeCount;
        allEdicts[index] = newEdict;
        activeCount++;
        return index;
    }

    public ref EdictRuleData GetEdictRef(int index)
    {
        return ref allEdicts[index];
    }
}