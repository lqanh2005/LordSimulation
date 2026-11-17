using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Click trái vào công trình (trừ Nông trại — đã có FarmCropPopup) -> popup hiện trạng thái,
/// số cư dân / công nhân, và nút phá dỡ (bấm 2 lần để xác nhận).
/// </summary>
public class BuildingInfoPopup : MonoBehaviour
{
    private const float PanelWidth = 280f;
    private const float RefreshInterval = 0.25f;

    private PlacementManager _placement;
    private Canvas _canvas;
    private RectTransform _panel;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _body;
    private Image _demolishButton;
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

        // Chờ tới LateUpdate: nếu click cùng frame trúng cư dân thì ưu tiên popup cư dân
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
        Follow();
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
        Follow();
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
        if (BuildingRules.IsUnderConstruction(in b))
        {
            int pct = Mathf.RoundToInt(Mathf.Clamp01(b.constructionProgress) * 100f);
            _sb.Append("Trạng thái: <color=#B7950B><b>Đang xây</b></color> (").Append(pct).Append("%)\n");
        }
        else if (b.isOperational)
        {
            _sb.Append("Trạng thái: <color=#1E8449><b>Hoạt động</b></color>\n");
        }
        else
        {
            _sb.Append("Trạng thái: <color=#C0392B><b>Ngừng hoạt động</b></color>\n");
        }

        if (b.maxOccupancy > 0)
            _sb.Append("Cư dân: ").Append(FormatRatio(b.currentOccupancy, b.maxOccupancy)).Append('\n');
        if (b.maxWorkers > 0)
            _sb.Append("Công nhân: ").Append(FormatRatio(b.currentWorkers, b.maxWorkers)).Append('\n');
        if (b.maxOccupancy == 0 && b.maxWorkers == 0)
            _sb.Append("<color=#7F8C8D>Công trình hỗ trợ, không cần người</color>\n");

        bool canDemolish = _placement.CanDemolish(b.buildingID);
        if (canDemolish)
        {
            _placement.GetDemolishRefund(in b, out int wood, out int gold);
            _sb.Append("<size=14>Phá dỡ hoàn lại: ").Append(wood).Append(" gỗ, ").Append(gold).Append(" vàng</size>");
        }
        else
        {
            _sb.Append("<size=14><color=#7F8C8D>Không thể phá công trình này</color></size>");
        }

        _body.text = _sb.ToString();

        _demolishButton.gameObject.SetActive(canDemolish);
        _demolishButton.color = _confirmDemolish ? RuntimeUIKit.DangerButtonColor : RuntimeUIKit.ButtonColor;
        _demolishLabel.text = _confirmDemolish ? "Xác nhận phá?" : "Phá dỡ";
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
        string color = current >= max ? "#1E8449" : current == 0 ? "#C0392B" : "#B7950B";
        return $"<color={color}><b>{current}/{max}</b></color>";
    }

    private void Follow()
    {
        BuildingView view = _placement != null ? _placement.GetBuildingView(_buildingId) : null;
        if (view == null)
            return;

        // Neo tại tâm footprint (pivot của view là góc dưới footprint)
        Vector3 world = view.transform.position;
        Vector2Int size = view.Footprint;
        world.y += (size.x + size.y) * 0.125f;
        RuntimeUIKit.PlaceAtWorld(_panel, _canvas, world);
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

        _panel = RuntimeUIKit.CreatePanel(_canvas.transform, "BuildingInfoPanel", PanelWidth);
        _title = RuntimeUIKit.CreateHeader(_panel, Hide);
        _body = RuntimeUIKit.CreateText(_panel, "Body", 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _body.lineSpacing = 4f;

        RectTransform buttons = RuntimeUIKit.CreateRow(_panel, "Buttons", 38f);
        _demolishButton = RuntimeUIKit.CreateButton(buttons, "Demolish", "Phá dỡ", OnDemolishClicked);
        _demolishLabel = _demolishButton.GetComponentInChildren<TextMeshProUGUI>();

        _panel.gameObject.SetActive(false);
    }
}
