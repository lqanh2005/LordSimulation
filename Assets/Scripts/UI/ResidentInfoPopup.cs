using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Click trái vào cư dân -> popup cố định ở góc phải dưới màn hình hiện tên, tuổi, nghề, sức khỏe, hạnh phúc, nhà, nơi làm.
/// UI tự dựng lúc chạy dưới canvas HUD.
/// </summary>
public class ResidentInfoPopup : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    /// <summary>Frame gần nhất click trúng cư dân — popup công trình bỏ qua click này.</summary>
    public static int LastPickFrame { get; private set; } = -1;

    private PlacementManager _placement;
    private ResidentManager _residents;
    private Canvas _canvas;
    private RectTransform _panel;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _body;
    private readonly StringBuilder _sb = new StringBuilder(256);

    private ResidentBase _target;
    private int _targetResidentId = -1;
    private float _nextRefresh;

    public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

    public static void EnsureInstance(PlacementManager placement)
    {
        if (FindFirstObjectByType<ResidentInfoPopup>() != null)
            return;

        GameObject go = new GameObject("ResidentInfoPopup");
        go.AddComponent<ResidentInfoPopup>()._placement = placement;
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnLoadCompleted(object param) => Hide();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && IsOpen)
            Hide();

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || RuntimeUIKit.IsPointerOverUI())
            return;
        if (_placement != null && (_placement.isPlacing || _placement.IsDemolishing))
            return;

        // Ưu tiên nhà / công trình: click trúng ô có công trình (trừ đường) thì để popup công trình xử lý
        // (Cổng thành cũng vậy)
        if (ImmigrationGate.IsPointerOver || (_placement != null && _placement.GetSolidBuildingIdUnderMouse() != 0))
        {
            if (IsOpen)
                Hide();
            return;
        }

        ResidentBase picked = PickResident(mouse.position.ReadValue());
        if (picked != null)
        {
            LastPickFrame = Time.frameCount;
            Open(picked);
        }
        else if (IsOpen)
        {
            Hide();
        }
    }

    private void LateUpdate()
    {
        if (!IsOpen)
            return;

        if (!ValidateTarget() || (_placement != null && (_placement.isPlacing || _placement.IsDemolishing)))
        {
            Hide();
            return;
        }

        if (Time.unscaledTime >= _nextRefresh)
            Refresh();
    }

    // ==========================================
    // CHỌN CƯ DÂN
    // ==========================================

    private ResidentBase PickResident(Vector2 screen)
    {
        ResidentManager manager = ResolveResidents();
        Camera cam = Camera.main;
        if (manager == null || cam == null)
            return null;

        Ray ray = cam.ScreenPointToRay(screen);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        if (!plane.Raycast(ray, out float enter))
            return null;
        Vector3 world = ray.GetPoint(enter);

        ResidentBase best = null;
        int bestOrder = int.MinValue;
        var visuals = manager.ActiveVisuals;
        for (int i = 0; i < visuals.Count; i++)
        {
            ResidentBase v = visuals[i];
            if (v == null || !v.IsBound || !v.gameObject.activeInHierarchy || v.IsIndoors)
                continue;   // Đang ngủ trong nhà: không bấm chọn được (click sẽ rơi vào ngôi nhà)

            SpriteRenderer sr = v.BodyRenderer;
            if (sr == null || sr.sprite == null)
                continue;

            Bounds b = sr.bounds;
            if (world.x < b.min.x || world.x > b.max.x || world.y < b.min.y || world.y > b.max.y)
                continue;

            // Ưu tiên cư dân đứng trước (sorting order cao hơn)
            if (sr.sortingOrder > bestOrder)
            {
                bestOrder = sr.sortingOrder;
                best = v;
            }
        }

        return best;
    }

    private bool ValidateTarget()
    {
        ResidentManager manager = ResolveResidents();
        if (_target == null || manager == null || !_target.IsBound || !_target.gameObject.activeInHierarchy)
            return false;
        if ((uint)_target.DataIndex >= (uint)manager.activeCount)
            return false;

        ref ResidentData data = ref manager.GetResidentRef(_target.DataIndex);
        // View có thể bị pool tái dùng cho cư dân khác
        return data.isAlive && data.residentID == _targetResidentId;
    }

    // ==========================================
    // HIỂN THỊ
    // ==========================================

    private void Open(ResidentBase view)
    {
        ResidentManager manager = ResolveResidents();
        EnsureUI();
        if (_panel == null || manager == null)
            return;

        _target = view;
        _targetResidentId = manager.GetResidentRef(view.DataIndex).residentID;
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        Refresh();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
    }

    public void Hide()
    {
        _target = null;
        _targetResidentId = -1;
        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        _nextRefresh = Time.unscaledTime + RefreshInterval;
        if (!ValidateTarget())
            return;

        ref ResidentData r = ref ResolveResidents().GetResidentRef(_target.DataIndex);
        _title.text = NameDatabase.GetFullName(r.firstNameID, r.lastNameID);

        _sb.Clear();
        _sb.Append("Tuổi: <b>").Append(r.age).Append("</b> (").Append(GetAgeGroupName(r.GetAgeGroup())).Append(") · ")
           .Append(r.gender == GenderType.Male ? "Nam" : "Nữ").Append('\n');
        _sb.Append("Nghề: <b>").Append(GetProfessionName(r.professionType)).Append("</b>\n");
        _sb.Append("Sức khỏe: ").Append(DescribeHealth(in r)).Append('\n');
        _sb.Append("Hạnh phúc: ").Append(ColorPercent(r.happiness)).Append('\n');
        _sb.Append("Nhà ở: ").Append(DescribeBuilding(r.assignedHouseID, "<color=#E86A5A>Vô gia cư</color>")).Append('\n');
        _sb.Append("Nơi làm: ").Append(DescribeBuilding(r.assignedWorkID, "Không có")).Append('\n');

        if (r.hungerMonths > 0 || r.coldMonths > 0 || r.thirstMonths > 0)
        {
            _sb.Append("<color=#E86A5A>");
            bool first = true;
            AppendNeed(ref first, "Đói", r.hungerMonths);
            AppendNeed(ref first, "Khát", r.thirstMonths);
            AppendNeed(ref first, "Lạnh", r.coldMonths);
            _sb.Append("</color>\n");
        }

        _sb.Append("<size=88%>Sức mạnh ").Append(r.strength)
           .Append(" · Bền ").Append(r.endurance)
           .Append(" · Trí ").Append(r.intellect).Append("</size>");

        _body.text = _sb.ToString();
    }

    private void AppendNeed(ref bool first, string label, byte months)
    {
        if (months == 0)
            return;
        if (!first)
            _sb.Append(" · ");
        _sb.Append(label).Append(' ').Append(months).Append(" tháng");
        first = false;
    }

    private string DescribeHealth(in ResidentData r)
    {
        switch (r.healthStatus)
        {
            case HealthStatus.Incubating:
                return "<color=#F0C24B>Ủ bệnh</color>";
            case HealthStatus.ActiveInfected:
                return $"<color=#E86A5A>Đang bệnh — {GetDiseaseName(r.diseaseType)} ({r.bodyTemperature:0.0}°C)</color>";
            case HealthStatus.Treated:
                return "<color=#6FB3E8>Đang điều trị</color>";
            default:
                return "<color=#5CCB6A>Khỏe mạnh</color>";
        }
    }

    private string DescribeBuilding(short buildingId, string none)
    {
        if (buildingId < 0)
            return none;

        BuildingManager buildings = _placement != null ? _placement.buildingManager : null;
        int index = buildings != null ? buildings.FindBuildingIndexById((ushort)buildingId) : -1;
        if (index < 0)
            return none;

        BuildingType type = buildings.GetBuildingRef(index).buildingType;
        return $"{BuildingInfoPopup.GetBuildingName(_placement, type)} #{buildingId}";
    }

    private static string ColorPercent(float value01)
    {
        int pct = Mathf.RoundToInt(Mathf.Clamp01(value01) * 100f);
        string color = pct >= 60 ? "#5CCB6A" : pct >= 30 ? "#F0C24B" : "#E86A5A";
        return $"<color={color}><b>{pct}%</b></color>";
    }

    public static string GetProfessionName(ProfessionType type) => type switch
    {
        ProfessionType.Farmer => "Nông dân",
        ProfessionType.Miner => "Thợ mỏ",
        ProfessionType.Lumberjack => "Tiều phu",
        ProfessionType.Craftsman => "Thợ thủ công",
        ProfessionType.Doctor => "Thầy thuốc",
        ProfessionType.Student => "Học sinh",
        ProfessionType.Teacher => "Giáo viên",
        ProfessionType.Guard => "Lính gác",
        _ => "Không nghề"
    };

    public static string GetDiseaseName(DiseaseType type) => type switch
    {
        DiseaseType.RedFever => "Sốt đỏ",
        DiseaseType.LungParasite => "Ký sinh phổi",
        DiseaseType.BloodPoison => "Nhiễm độc máu",
        _ => "Không rõ"
    };

    private static string GetAgeGroupName(AgeGroupType group) => group switch
    {
        AgeGroupType.Child => "Trẻ em",
        AgeGroupType.Elderly => "Người già",
        _ => "Người lớn"
    };

    // ==========================================
    // DỰNG UI
    // ==========================================

    private void EnsureUI()
    {
        if (_panel != null)
            return;

        _canvas = RuntimeUIKit.FindHudCanvas();
        if (_canvas == null)
            return;

        _panel = RuntimeUIKit.CreatePanel(_canvas.transform, "ResidentInfoPanel", RuntimeUIKit.InfoPopupWidth);
        _title = RuntimeUIKit.CreateHeader(_panel, Hide);
        _body = RuntimeUIKit.CreateText(_panel, "Body", 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _body.lineSpacing = 4f;
        RuntimeUIKit.SetupInfoPopup(_panel);
        _panel.gameObject.SetActive(false);
    }

    private ResidentManager ResolveResidents()
    {
        if (_residents == null)
        {
            PlayerContain contain = FindFirstObjectByType<PlayerContain>();
            _residents = contain != null ? contain.residentManager : FindFirstObjectByType<ResidentManager>();
        }
        return _residents;
    }
}
