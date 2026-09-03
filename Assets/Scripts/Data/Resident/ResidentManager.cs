using UnityEngine;

public class ResidentManager : MonoBehaviour
{
    public const int MAX_RESIDENTS = 65535;

    [Header("Dữ Liệu Mảng Tĩnh Lõi")]
    public ResidentData[] allResidents = new ResidentData[MAX_RESIDENTS];
    public int activeCount = 0;

    // Thêm 1 cư dân mới vào mảng tĩnh
    public int AddResident(in ResidentData newResident)
    {
        if (activeCount >= MAX_RESIDENTS)
        {
            Debug.LogError("[ResidentManager] Đã đạt giới hạn dân số tối đa!");
            return -1;
        }

        int index = activeCount;
        allResidents[index] = newResident;
        allResidents[index].isAlive = true;
        activeCount++;
        return index;
    }

    // Trả về tham chiếu trực tiếp ref để sửa dữ liệu 0 byte GC Alloc
    public ref ResidentData GetResidentRef(int index)
    {
        return ref allResidents[index];
    }

    // Hook tái tạo View sau khi nạp file save
    public void RebindAllVisualAgents()
    {
        Debug.Log($"[ResidentManager] Đã đồng bộ lại {activeCount} cư dân lên bản đồ.");
    }
}