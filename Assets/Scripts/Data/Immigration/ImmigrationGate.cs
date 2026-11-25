using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ImmigrationGate : MonoBehaviour
{
    [SerializeField] private ImmigrationManager immigrationManager;
    [SerializeField] private TextMesh waitingBadge;
    [SerializeField] private Vector3 badgeOffset = new Vector3(0f, 1.65f, 0f);

    [Header("Rê chuột / click")]
    [SerializeField] private Color hoverTint = new Color(1f, 0.82f, 0.45f);
    [SerializeField] private Vector3 hoverLabelOffset = new Vector3(0f, 2.1f, 0f);

    private static ImmigrationGate _active;

    private Collider2D _collider;
    private SpriteRenderer[] _parts;
    private Color[] _partColors;
    private TextMeshPro _hoverLabel;
    private PlacementManager _placement;
    private Camera _camera;
    private bool _hovered;
    private int _waiting;

    public void Bind(ImmigrationManager manager)
    {
        immigrationManager = manager;
        EnsureBadge();
        Subscribe();
        SetWaitingCount(manager != null ? manager.waitingCount : 0);
    }

    private int[] _baseOrders;

    private void OnEnable()
    {
        _active = this;
        Subscribe();
        GridController.MapGenerated -= SnapToGrid;
        GridController.MapGenerated += SnapToGrid;
    }

    private void OnDisable()
    {
        SetHovered(false);
        if (_active == this)
            _active = null;
        Unsubscribe();
        GridController.MapGenerated -= SnapToGrid;
    }

    private void Start()
    {
        GridController grid = FindFirstObjectByType<GridController>();
        if (grid != null && grid.IsGenerated)
            SnapToGrid(grid);
    }

    /// <summary>Đặt Cổng tại GateCell (ngay ngoài mép bản đồ, đầu lối mòn) và xếp lớp theo độ sâu ô.</summary>
    private void SnapToGrid(GridController grid)
    {
        if (grid == null)
            return;

        Vector2Int cell = grid.GateCell;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        SpriteRenderer[] parts = GetComponentsInChildren<SpriteRenderer>(true);
        if (_baseOrders == null || _baseOrders.Length != parts.Length)
        {
            _baseOrders = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                _baseOrders[i] = parts[i].sortingOrder;
        }

        int order = grid.GetObjectSortingOrder(cell.x, cell.y);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].sortingLayerName = grid.SortingLayerName;
            parts[i].sortingOrder = order + _baseOrders[i];
        }

        if (waitingBadge != null)
        {
            MeshRenderer mesh = waitingBadge.GetComponent<MeshRenderer>();
            if (mesh != null)
            {
                mesh.sortingLayerName = grid.SortingLayerName;
                mesh.sortingOrder = order + 10;
            }
        }
    }

    private void Subscribe()
    {
        GameEvents.Unlisten(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Listen(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void Unsubscribe()
    {
        GameEvents.Unlisten(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnQueueChanged(object param)
    {
        int count = param is int waiting ? waiting : (immigrationManager != null ? immigrationManager.waitingCount : 0);
        SetWaitingCount(count);
    }

    private void OnLoadCompleted(object param)
    {
        if (immigrationManager != null)
            SetWaitingCount(immigrationManager.waitingCount);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _active = null;

    /// <summary>
    /// Chuột đang chỉ vào Cổng và click sẽ mở kiểm duyệt. Cổng vẽ trước mọi thứ trong thành
    /// nên công trình / cư dân phía sau phải nhường click.
    /// </summary>
    public static bool IsPointerOver => _active != null && _active.CheckPointer();

    // Project chỉ bật Input System mới nên OnMouseDown / OnMouseEnter không chạy -> tự kiểm tra
    private void Update()
    {
        bool over = CheckPointer();
        SetHovered(over);
        if (over && Mouse.current.leftButton.wasPressedThisFrame)
            OpenInspection();
    }

    private bool CheckPointer()
    {
        if (Mouse.current == null || RuntimeUIKit.IsPointerOverUI())
            return false;
        if (_placement == null)
            _placement = FindFirstObjectByType<PlacementManager>();
        if (_placement != null && (_placement.isPlacing || _placement.IsDemolishing))
            return false;
        if (_collider == null)
            _collider = GetComponent<Collider2D>();
        if (_camera == null)
            _camera = Camera.main;
        if (_collider == null || _camera == null)
            return false;

        return _collider.OverlapPoint(_camera.ScreenToWorldPoint(Mouse.current.position.ReadValue()));
    }

    private void OpenInspection()
    {
        SetHovered(false);
        if (immigrationManager == null)
            immigrationManager = FindFirstObjectByType<ImmigrationManager>();
        if (immigrationManager != null)
            immigrationManager.OpenInspection();
    }

    /// <summary>Rê chuột: Cổng sáng lên + nhãn cho biết bấm được.</summary>
    private void SetHovered(bool on)
    {
        if (_hovered == on)
            return;
        _hovered = on;

        if (_parts == null)
        {
            _parts = GetComponentsInChildren<SpriteRenderer>(true);
            _partColors = new Color[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
                _partColors[i] = _parts[i].color;
        }
        for (int i = 0; i < _parts.Length; i++)
            if (_parts[i] != null)
                _parts[i].color = on ? _partColors[i] * hoverTint : _partColors[i];

        if (on)
            EnsureHoverLabel();
        if (_hoverLabel != null)
        {
            RefreshHoverLabel();
            _hoverLabel.gameObject.SetActive(on);
        }
    }

    private void EnsureHoverLabel()
    {
        if (_hoverLabel != null)
            return;

        GameObject go = new GameObject("HoverLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = hoverLabelOffset;
        _hoverLabel = go.AddComponent<TextMeshPro>();
        UISkin skin = UISkin.Instance;
        if (skin != null && skin.bodyFont != null)
            _hoverLabel.font = skin.bodyFont;
        _hoverLabel.fontSize = 2.6f;
        _hoverLabel.alignment = TextAlignmentOptions.Center;
        _hoverLabel.textWrappingMode = TextWrappingModes.NoWrap;
        _hoverLabel.color = UISkin.Cream;
        _hoverLabel.rectTransform.sizeDelta = new Vector2(6f, 1f);

        // Luôn nổi trên cảnh quan phía trước thành
        MeshRenderer mesh = go.GetComponent<MeshRenderer>();
        if (_parts.Length > 0)
            mesh.sortingLayerID = _parts[0].sortingLayerID;
        mesh.sortingOrder = 1000;
    }

    private void RefreshHoverLabel()
    {
        if (_hoverLabel != null)
            _hoverLabel.text = _waiting > 0
                ? $"Cổng thành\n<size=75%>{_waiting} người đang chờ — bấm để kiểm duyệt</size>"
                : "Cổng thành\n<size=75%>Không có ai đang chờ</size>";
    }

    public void SetWaitingCount(int count)
    {
        EnsureBadge();
        if (waitingBadge == null)
            return;

        _waiting = count;
        RefreshHoverLabel();
        waitingBadge.text = count > 0 ? count.ToString() : "";
        waitingBadge.gameObject.SetActive(count > 0);
    }

    private void EnsureBadge()
    {
        if (waitingBadge != null)
            return;

        Transform existing = transform.Find("WaitingBadge");
        GameObject badgeGo = existing != null ? existing.gameObject : new GameObject("WaitingBadge");
        badgeGo.transform.SetParent(transform, false);
        badgeGo.transform.localPosition = badgeOffset;
        waitingBadge = badgeGo.GetComponent<TextMesh>();
        if (waitingBadge == null)
            waitingBadge = badgeGo.AddComponent<TextMesh>();
        waitingBadge.anchor = TextAnchor.MiddleCenter;
        waitingBadge.alignment = TextAlignment.Center;
        waitingBadge.fontSize = 32;
        waitingBadge.characterSize = 0.08f;
        waitingBadge.color = Color.yellow;
        MeshRenderer mesh = badgeGo.GetComponent<MeshRenderer>();
        if (mesh != null)
            mesh.sortingOrder = 25;
    }
}
