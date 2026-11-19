using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Click trái vào công trình (trừ Nông trại — đã có FarmCropPopup) -> popup hiện trạng thái,
/// số cư dân / công nhân, và nút phá dỡ (bấm 2 lần để xác nhận). Popup cố định ở góc phải dưới màn hình.
/// </summary>
public class BuildingInfoPopup : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private PlacementManager _placement;
    private Canvas _canvas;
    private RectTransform _panel;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _body;
    private Image _demolishButton;
    private Image _toggleButton;
    private TextMeshProUGUI _toggleLabel;
    private TextMeshProUGUI _demolishLabel;
    private readonly StringBuilder _sb = new StringBuilder(256);

    private ushort _buildingId;
    private ushort _pendingClickId;
    private int _pendingClickFrame = -1;
    private int _openedFrame = -1;
    private bool _clickedOutside;
    private bool _confirmDemolish;
    private float _nextRefresh;

    public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

    public static void EnsureInstance(PlacementManager placement)
    {
        if (FindFirstObjectByType<BuildingInfoPopup>() != null)
            return;

        GameObject go = new GameObject("BuildingInfoPopup");
        go.AddComponent<BuildingInfoPopup>()._placement = placement;
    }

    public static string GetBuildingName(PlacementManager placement, BuildingType type)
    {
        BuildingConfig config = placement != null && placement.database != null ? placement.database.GetConfig(type) : null;
        return config != null && !string.IsNullOrEmpty(config.displayName)
            ? config.displayName
            : ResearchCatalog.GetBuildingName(type);
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.BuildingClicked, OnBuildingClicked);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.BuildingClicked, OnBuildingClicked);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnLoadCompleted(object param) => Hide();

    private void OnBuildingClicked(object param)
    {
        if (!(param is ushort id))
            return;

        // Chờ tới LateUpdate: công trình thường luôn được ưu tiên (popup cư dân tự nhường),
        // riêng ô đường mà click trúng cư dân đang đi trên đó thì nhường popup cư dân
        _pendingClickId = id;
        _pendingClickFrame = Time.frameCount;
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            && !RuntimeUIKit.IsPointerOverUI())
            _clickedOutside = true;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Hide();
    }

    private void LateUpdate()
    {
        if (_pendingClickFrame == Time.frameCount)
        {
            _pendingClickFrame = -1;
            if (ResidentInfoPopup.LastPickFrame != Time.frameCount)
                TryOpen(_pendingClickId);
        }

        if (!IsOpen)
            return;

        if ((_clickedOutside && _openedFrame != Time.frameCount)
            || (_placement != null && (_placement.isPlacing || _placement.IsDemolishing))
            || !TryGetBuilding(_buildingId, out _))
        {
            Hide();
            return;
        }

        _clickedOutside = false;
        if (Time.unscaledTime >= _nextRefresh)
            Refresh();
    }

    private void TryOpen(ushort id)
    {
        if (!TryGetBuilding(id, out int index))
            return;

        // Nông trại / vườn thuốc đã có popup chọn cây riêng
        if (FarmCropRules.HasCropSlot(_placement.buildingManager.GetBuildingRef(index).buildingType))
            return;

        EnsureUI();
        if (_panel == null)
            return;

        if (_buildingId != id)
            _confirmDemolish = false;

        _buildingId = id;
        _openedFrame = Time.frameCount;
        _clickedOutside = false;
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        Refresh();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
    }

    public void Hide()
    {
        _confirmDemolish = false;
        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        _nextRefresh = Time.unscaledTime + RefreshInterval;
        if (!TryGetBuilding(_buildingId, out int index))
            return;

        ref BuildingData b = ref _placement.buildingManager.GetBuildingRef(index);
        _title.text = $"{GetBuildingName(_placement, b.buildingType)} #{b.buildingID}";

        _sb.Clear();
        GlobalSystemManager global = HUDLocator.ResolveGlobalSystem(null);
        GlobalSystemData globalData = global != null ? global.GetGlobalDataRef() : default;
        bool connected = RoadNetwork.IsConnected(_placement.buildingManager, b.buildingID);
        BuildingStatusLevel level = BuildingStatusRules.Evaluate(in b, in globalData, connected, out string reason);
        string color = level switch
        {
            BuildingStatusLevel.Ok => "#5CCB6A",
            BuildingStatusLevel.Limited => "#F0C24B",
            _ => "#E86A5A"
        };
        _sb.Append("<color=").Append(color).Append("><b>").Append(reason).Append("</b></color>\n");

        string output = BuildingStatusRules.DescribeOutput(in b);
        if (!string.IsNullOrEmpty(output))
            _sb.Append(output).Append('\n');

        if (b.maxOccupancy > 0)
            _sb.Append("Cư dân: ").Append(FormatRatio(b.currentOccupancy, b.maxOccupancy)).Append('\n');
        if (b.maxWorkers > 0)
            _sb.Append("Công nhân: ").Append(FormatRatio(b.currentWorkers, b.maxWorkers)).Append('\n');
        // GDD 3.7: Trạm gác / Phòng khám thu gom thi thể -> cho người chơi thấy tác dụng
        if (CrisisManager.IsCollector(b.buildingType))
        {
            PlayerContain contain = FindFirstObjectByType<PlayerContain>();
            CrisisManager crisis = contain != null ? contain.crisisManager : null;
            if (crisis != null)
                _sb.Append(crisis.DescribeCollection(in b)).Append('\n');
        }

        // Giếng nước: cho thấy sức cấp nước của cả thành
        if (b.buildingType == BuildingType.WaterWell && global != null)
        {
            int capacity = global.CurrentWaterCapacity;
            int population = globalData.totalPopulation;
            string waterColor = population > capacity ? "#E86A5A" : "#5CCB6A";
            _sb.Append("Cấp nước cho ").Append(WaterRules.WellWaterCapacity).Append(" người\n");
            _sb.Append("Nước toàn thành: <color=").Append(waterColor).Append('>')
               .Append(population).Append('/').Append(capacity).Append(" người</color>\n");
        }

        if (b.maxOccupancy == 0 && b.maxWorkers == 0)
            _sb.Append("<color=#A59D8A>Công trình hỗ trợ, không cần người</color>\n");

        bool canDemolish = _placement.CanDemolish(b.buildingID);
        if (canDemolish)
        {
            _placement.GetDemolishRefund(in b, out int wood, out int gold);
            _sb.Append("<size=88%>Phá dỡ hoàn lại: ").Append(wood).Append(" gỗ, ").Append(gold).Append(" vàng</size>");
        }
        else
        {
            _sb.Append("<size=88%><color=#A59D8A>Không thể phá công trình này</color></size>");
        }

        _body.text = _sb.ToString();

        bool canToggle = BuildingStatusRules.CanToggle(b.buildingType) && !BuildingRules.IsUnderConstruction(in b);
        _toggleButton.gameObject.SetActive(canToggle);
        _toggleLabel.text = b.isOperational ? "Tạm dừng" : "Chạy lại";
        RuntimeUIKit.SetButtonStyle(_toggleButton, b.isOperational ? RuntimeUIKit.ButtonStyle.Normal : RuntimeUIKit.ButtonStyle.Active);

        _demolishButton.gameObject.SetActive(canDemolish);
        RuntimeUIKit.SetButtonStyle(_demolishButton, _confirmDemolish ? RuntimeUIKit.ButtonStyle.Danger : RuntimeUIKit.ButtonStyle.Normal);
        _demolishLabel.text = _confirmDemolish ? "Xác nhận phá?" : "Phá dỡ";
    }

    /// <summary>GDD 7.2 Enable / Disable: tạm dừng thì công nhân rời đi ở tháng sau, không sản xuất.</summary>
    private void OnToggleClicked()
    {
        if (!TryGetBuilding(_buildingId, out int index))
            return;

        ref BuildingData b = ref _placement.buildingManager.GetBuildingRef(index);
        if (!BuildingStatusRules.CanToggle(b.buildingType) || BuildingRules.IsUnderConstruction(in b))
            return;

        b.isOperational = !b.isOperational;
        RequestResidentReassign();   // Thợ rời đi / quay lại làm ngay, không đợi sang tháng
        NotificationFeed.Show(
            $"{GetBuildingName(_placement, b.buildingType)} #{b.buildingID} {(b.isOperational ? "hoạt động trở lại" : "đã tạm dừng")}",
            NoticeLevel.Info, "toggle:" + b.buildingID);
        Refresh();
    }

    private void OnDemolishClicked()
    {
        if (!_confirmDemolish)
        {
            _confirmDemolish = true;
            Refresh();
            return;
        }

        ushort id = _buildingId;
        Hide();
        if (_placement != null)
            _placement.DemolishBuilding(id);
    }

    private static string FormatRatio(int current, int max)
    {
        string color = current >= max ? "#5CCB6A" : current == 0 ? "#E86A5A" : "#F0C24B";
        return $"<color={color}><b>{current}/{max}</b></color>";
    }

    private bool TryGetBuilding(ushort id, out int index)
    {
        index = -1;
        if (_placement == null)
            _placement = FindFirstObjectByType<PlacementManager>();
        if (_placement == null || _placement.buildingManager == null || id == 0)
            return false;

        index = _placement.buildingManager.FindBuildingIndexById(id);
        return index >= 0;
    }

    private void EnsureUI()
    {
        if (_panel != null)
            return;

        _canvas = RuntimeUIKit.FindHudCanvas();
        if (_canvas == null)
            return;

        _panel = RuntimeUIKit.CreatePanel(_canvas.transform, "BuildingInfoPanel", RuntimeUIKit.InfoPopupWidth);
        _title = RuntimeUIKit.CreateHeader(_panel, Hide);
        _body = RuntimeUIKit.CreateText(_panel, "Body", 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _body.lineSpacing = 4f;

        RectTransform buttons = RuntimeUIKit.CreateRow(_panel, "Buttons", 38f);
        _toggleButton = RuntimeUIKit.CreateButton(buttons, "Toggle", "Tạm dừng", OnToggleClicked);
        _toggleLabel = _toggleButton.GetComponentInChildren<TextMeshProUGUI>();
        _demolishButton = RuntimeUIKit.CreateButton(buttons, "Demolish", "Phá dỡ", OnDemolishClicked);
        _demolishLabel = _demolishButton.GetComponentInChildren<TextMeshProUGUI>();

        RuntimeUIKit.SetupInfoPopup(_panel);
        _panel.gameObject.SetActive(false);
    }

    private static void RequestResidentReassign()
    {
        PlayerContain contain = GamePlayController.Instance != null ? GamePlayController.Instance.playerContain : null;
        if (contain != null && contain.residentManager != null)
            contain.residentManager.RequestReassign();
    }
}
