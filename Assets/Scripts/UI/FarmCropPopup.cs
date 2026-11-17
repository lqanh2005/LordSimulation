using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Popup nhỏ hiện cạnh Farm khi click: chọn trồng Lúa hoặc Cây thuốc.
/// UI tự dựng lúc chạy dưới canvas HUD nên không cần chỉnh scene.
/// </summary>
public class FarmCropPopup : MonoBehaviour
{
    private static readonly Color PanelColor = new Color32(0xE2, 0xE5, 0xE7, 0xF2);
    private static readonly Color ButtonColor = new Color32(0xD3, 0xD7, 0xDA, 0xFF);
    private static readonly Color ActiveButtonColor = new Color32(0x9F, 0xC8, 0x8A, 0xFF);
    private static readonly Color TextColor = new Color32(0x33, 0x33, 0x33, 0xFF);
    private static readonly Vector2 PanelSize = new Vector2(260f, 150f);
    private const float HudTopMargin = 130f;
    private const float HudBottomMargin = 140f;

    private PlacementManager placement;
    private RectTransform canvasRect;
    private Canvas canvas;
    private RectTransform panel;
    private TextMeshProUGUI title;
    private TextMeshProUGUI status;
    private Image riceButton;
    private Image drugButton;

    private ushort farmId;
    private int openedFrame = -1;
    private bool clickedOutside;

    public static void EnsureInstance(PlacementManager placement)
    {
        if (FindFirstObjectByType<FarmCropPopup>() != null)
            return;

        GameObject go = new GameObject("FarmCropPopup");
        go.AddComponent<FarmCropPopup>().placement = placement;
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.BuildingClicked, OnBuildingClicked);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.BuildingClicked, OnBuildingClicked);
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        Mouse mouse = Mouse.current;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) && !overUI)
            clickedOutside = true;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Hide();
    }

    private void LateUpdate()
    {
        if (!IsOpen)
            return;

        // Click ra ngoài (không trúng Farm khác), chuyển sang chế độ xây / phá, hoặc Farm đã bị phá -> đóng
        if ((clickedOutside && openedFrame != Time.frameCount)
            || (placement != null && (placement.isPlacing || placement.IsDemolishing))
            || !TryGetFarm(farmId, out _))
        {
            Hide();
            return;
        }

        clickedOutside = false;
        FollowFarm();
    }

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    private void OnBuildingClicked(object param)
    {
        if (!(param is ushort id) || !TryGetFarm(id, out _))
            return;

        EnsureUI();
        if (panel == null)
            return;

        farmId = id;
        openedFrame = Time.frameCount;
        clickedOutside = false;
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
        Refresh();
        FollowFarm();
    }

    private void OnMonthChanged(object param)
    {
        if (IsOpen)
            Refresh();
    }

    private void Plant(CropType crop)
    {
        if (!TryGetFarm(farmId, out int index))
            return;

        ref BuildingData farm = ref placement.buildingManager.GetBuildingRef(index);
        if (farm.cropType == crop)
            return;

        FarmCropRules.Plant(ref farm, crop);
        placement.RefreshFarmVisual(farmId);
        GameEvents.Post(EventID.FarmCropChanged, farmId);
        Refresh();
    }

    public void Hide()
    {
        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    private bool TryGetFarm(ushort id, out int index)
    {
        index = -1;
        if (placement == null)
            placement = FindFirstObjectByType<PlacementManager>();
        if (placement == null || placement.buildingManager == null)
            return false;

        index = placement.buildingManager.FindBuildingIndexById(id);
        return index >= 0 && FarmCropRules.HasCropSlot(placement.buildingManager.GetBuildingRef(index).buildingType);
    }

    private void Refresh()
    {
        if (!TryGetFarm(farmId, out int index))
        {
            Hide();
            return;
        }

        ref BuildingData farm = ref placement.buildingManager.GetBuildingRef(index);
        title.text = $"Nông trại #{farm.buildingID}";

        if (BuildingRules.IsUnderConstruction(in farm))
            status.text = farm.cropType == CropType.None
                ? "Đang xây — chọn cây để trồng khi xong"
                : $"Đang xây — sẽ trồng {FarmCropRules.GetCropName(farm.cropType)}";
        else if (farm.cropType == CropType.None)
            status.text = "Đất trống — chọn cây để trồng";
        else
            status.text = $"{FarmCropRules.GetCropName(farm.cropType)} — giai đoạn {farm.cropStage + 1}/{FarmCropRules.StageCount}";

        riceButton.color = farm.cropType == CropType.Rice ? ActiveButtonColor : ButtonColor;
        drugButton.color = farm.cropType == CropType.Drug ? ActiveButtonColor : ButtonColor;
    }

    private void FollowFarm()
    {
        Camera cam = Camera.main;
        BuildingView view = placement != null ? placement.GetBuildingView(farmId) : null;
        if (cam == null || view == null || canvasRect == null)
            return;

        // Neo popup tại tâm footprint của Farm (pivot của view là góc dưới footprint)
        Vector3 world = view.transform.position;
        Vector2Int size = view.Footprint;
        world.y += (size.x + size.y) * 0.125f;
        Vector2 screen = cam.WorldToScreenPoint(world);

        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out Vector2 local))
            return;

        Rect r = canvasRect.rect;
        local.x = Mathf.Clamp(local.x, r.xMin + PanelSize.x * 0.5f, r.xMax - PanelSize.x * 0.5f);
        // Chừa thanh tài nguyên phía trên và thanh xây dựng phía dưới
        local.y = Mathf.Clamp(local.y, r.yMin + HudBottomMargin, r.yMax - PanelSize.y - HudTopMargin);
        panel.anchoredPosition = local;
    }

    // ==========================================
    // DỰNG UI
    // ==========================================

    private void EnsureUI()
    {
        if (panel != null)
            return;

        canvas = FindHudCanvas();
        if (canvas == null)
            return;

        canvasRect = (RectTransform)canvas.transform;

        GameObject panelGo = new GameObject("FarmCropPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel = (RectTransform)panelGo.transform;
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.sizeDelta = PanelSize;
        panelGo.GetComponent<Image>().color = PanelColor;

        VerticalLayoutGroup layout = panelGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 12);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        RectTransform header = CreateRow(panel, "Header", 26f);
        title = CreateText(header, "Title", 20f, FontStyles.Bold, TextAlignmentOptions.Left);
        Image close = CreateButton(header, "Close", "X", Hide);
        LayoutElement closeLayout = close.gameObject.AddComponent<LayoutElement>();
        closeLayout.preferredWidth = 26f;
        closeLayout.flexibleWidth = 0f;

        status = CreateText(panel, "Status", 16f, FontStyles.Normal, TextAlignmentOptions.Left);
        status.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

        RectTransform buttons = CreateRow(panel, "Buttons", 40f);
        riceButton = CreateButton(buttons, "Rice", "Trồng lúa", () => Plant(CropType.Rice));
        drugButton = CreateButton(buttons, "Drug", "Trồng thuốc", () => Plant(CropType.Drug));

        panelGo.SetActive(false);
    }

    private static Canvas FindHudCanvas()
    {
        GameObject hud = GameObject.Find("GameplayHUD");
        Canvas found = hud != null ? hud.GetComponentInParent<Canvas>() : null;
        if (found != null)
            return found.rootCanvas;

        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }

        return null;
    }

    private static RectTransform CreateRow(Transform parent, string name, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        HorizontalLayoutGroup row = go.GetComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = TextColor;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Image CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = ButtonColor;
        go.GetComponent<Button>().onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(go.transform, "Label", 16f, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        text.text = label;
        return image;
    }
}
