using UnityEngine;

public class ImmigrationGate : MonoBehaviour
{
    [SerializeField] private ImmigrationManager immigrationManager;
    [SerializeField] private TextMesh waitingBadge;
    [SerializeField] private Vector3 badgeOffset = new Vector3(0f, 1.65f, 0f);

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
        Subscribe();
        GridController.MapGenerated -= SnapToGrid;
        GridController.MapGenerated += SnapToGrid;
    }

    private void OnDisable()
    {
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

    private void OnMouseDown()
    {
        if (immigrationManager == null)
            immigrationManager = FindObjectOfType<ImmigrationManager>();
        if (immigrationManager != null)
            immigrationManager.OpenInspection();
    }

    public void SetWaitingCount(int count)
    {
        EnsureBadge();
        if (waitingBadge == null)
            return;

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
