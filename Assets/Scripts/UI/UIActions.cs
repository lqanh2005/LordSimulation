using UnityEngine;

/// <summary>
/// Các hành động UI dùng chung (mở cửa sổ, đưa camera tới công trình) — để thông báo "click để tới nơi"
/// (GDD 7.3) và phím tắt gọi cùng một chỗ.
/// </summary>
public static class UIActions
{
    public static void OpenEdicts()
    {
        var edicts = Object.FindFirstObjectByType<EdictPopupController>(FindObjectsInactive.Include);
        if (edicts != null && !edicts.gameObject.activeSelf)
            edicts.Show();
    }

    public static void OpenResearch()
    {
        var research = Object.FindFirstObjectByType<ResearchTreePopupController>(FindObjectsInactive.Include);
        if (research != null && !research.gameObject.activeSelf)
            research.Show();
    }

    public static void OpenGate()
    {
        PlayerContain contain = Object.FindFirstObjectByType<PlayerContain>();
        if (contain != null && contain.immigrationManager != null)
            contain.immigrationManager.OpenInspection();
    }

    public static void OpenMerchant()
    {
        var merchant = Object.FindFirstObjectByType<MerchantPopup>();
        if (merchant != null && !merchant.IsOpen)
            merchant.Show();
    }

    /// <summary>Mở nhóm xây dựng theo tên hiển thị trên thanh dưới (vd. "Nhà ở", "Nông nghiệp").</summary>
    public static void OpenBuildCategory(string categoryName)
    {
        var menu = Object.FindFirstObjectByType<BuildMenuController>(FindObjectsInactive.Include);
        if (menu == null)
            return;
        int index = menu.FindCategory(categoryName);
        if (index >= 0)
            menu.Open(index);
    }

    /// <summary>Đưa camera tới tâm công trình.</summary>
    public static void FocusBuilding(ushort buildingId)
    {
        Camera cam = Camera.main;
        PlacementManager placement = Object.FindFirstObjectByType<PlacementManager>();
        BuildingView view = placement != null ? placement.GetBuildingView(buildingId) : null;
        if (cam == null || view == null)
            return;

        Vector3 target = view.transform.position;
        Vector2Int size = view.Footprint;
        target.y += (size.x + size.y) * 0.125f;
        target.z = cam.transform.position.z;
        cam.transform.position = target;
    }
}
