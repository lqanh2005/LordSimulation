#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Công cụ test di chuyển cư dân (chỉ có trong Editor / Development build, tự tạo khi vào Play).
///   F2            Bật/tắt chế độ test
///   Click trái    Chọn cư dân gần con trỏ nhất
///   G             Cư dân đang chọn đi tới ô dưới con trỏ (A*)
///   Shift + G     Tất cả cư dân đang hiển thị đi tới ô dưới con trỏ
///   C             Huỷ lệnh test, cư dân quay lại lịch đi làm / về nhà
///   [  /  ]       Giảm / tăng tốc độ đi bộ
///   O             Tô đỏ các ô IsOccupied (công trình chiếm)
/// </summary>
public class ResidentMovementDebug : MonoBehaviour
{
    private const float SelectRadius = 0.6f;
    private const float SpeedStep = 0.1f;

    private bool _enabled;
    private ResidentManager _manager;
    private ResidentBase _selected;
    private int _selectedResidentId = -1;
    private Vector2Int _lastTargetCell = new Vector2Int(-1, -1);
    private string _lastMessage = "";

    private LineRenderer _pathLine;
    private LineRenderer _selectRing;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Công cụ test (F2) chỉ có trong Editor / bản Development, không lọt vào bản phát hành
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#endif
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<ResidentMovementDebug>() != null)
            return;

        GameObject go = new GameObject("[Debug] ResidentMovement");
        DontDestroyOnLoad(go);
        go.AddComponent<ResidentMovementDebug>();
    }

    private void Awake()
    {
        _pathLine = CreateLine("PathLine", new Color(0.2f, 0.9f, 1f, 0.9f), 0.04f);
        _selectRing = CreateLine("SelectRing", new Color(1f, 0.85f, 0.1f, 1f), 0.03f);
        _selectRing.loop = true;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null)
            return;

        if (kb.f2Key.wasPressedThisFrame)
        {
            _enabled = !_enabled;
            _lastMessage = _enabled ? "Bật chế độ test" : "";
        }

        if (!_enabled)
        {
            HideVisuals();
            if (_showOccupied)
            {
                _showOccupied = false;
                RestoreTileTints();
            }
            return;
        }

        if (_manager == null)
            _manager = FindFirstObjectByType<ResidentManager>();
        if (_manager == null)
        {
            _lastMessage = "Không tìm thấy ResidentManager";
            HideVisuals();
            return;
        }

        ValidateSelection();

        Vector3 mouseWorld = Vector3.zero;
        bool hasMouse = mouse != null && TryGetMouseWorld(mouse, out mouseWorld);

        if (hasMouse && mouse.leftButton.wasPressedThisFrame)
            SelectNearest(mouseWorld);

        if (hasMouse && kb.gKey.wasPressedThisFrame)
        {
            GridController grid = _manager.GridController;
            if (grid == null)
            {
                _lastMessage = "Scene không dùng GridController — không có A*";
            }
            else
            {
                Vector2Int cell = grid.WorldToGrid(mouseWorld);
                _lastTargetCell = cell;
                if (kb.shiftKey.isPressed)
                    MoveAll(cell);
                else
                    MoveSelected(cell);
            }
        }

        if (kb.cKey.wasPressedThisFrame)
        {
            _manager.ClearDebugMoves();
            _lastTargetCell = new Vector2Int(-1, -1);
            _lastMessage = "Đã huỷ lệnh test — quay lại lịch thường";
        }

        if (kb.oKey.wasPressedThisFrame)
        {
            _showOccupied = !_showOccupied;
            if (!_showOccupied)
                RestoreTileTints();
        }
        if (_showOccupied)
            PaintOccupied();

        if (kb.leftBracketKey.wasPressedThisFrame)
            _manager.CommuteSpeed = Mathf.Max(0.1f, _manager.CommuteSpeed - SpeedStep);
        if (kb.rightBracketKey.wasPressedThisFrame)
            _manager.CommuteSpeed += SpeedStep;

        DrawSelection();
    }

    // ===== Overlay ô IsOccupied =====
    private static readonly Color OccupiedTint = new Color(1f, 0.45f, 0.45f, 1f);
    private bool _showOccupied;
    private int _paintedVersion = -1;
    private readonly System.Collections.Generic.List<Vector2Int> _tinted = new System.Collections.Generic.List<Vector2Int>();
    private GridController _tintedGrid;

    private void PaintOccupied()
    {
        GridController grid = _manager.GridController;
        if (grid == null || grid.OccupancyVersion == _paintedVersion)
            return;

        RestoreTileTints();
        _paintedVersion = grid.OccupancyVersion;
        _tintedGrid = grid;
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                if (!grid.IsOccupied(x, y))
                    continue;
                grid.SetBaseTileColor(x, y, OccupiedTint);
                _tinted.Add(new Vector2Int(x, y));
            }
        }
    }

    private void RestoreTileTints()
    {
        if (_tintedGrid != null)
        {
            for (int i = 0; i < _tinted.Count; i++)
                _tintedGrid.SetBaseTileColor(_tinted[i].x, _tinted[i].y, Color.white);
        }
        _tinted.Clear();
        _paintedVersion = -1;
    }

    private void SelectNearest(Vector3 world)
    {
        ResidentBase best = null;
        float bestSqr = SelectRadius * SelectRadius;
        var visuals = _manager.ActiveVisuals;
        for (int i = 0; i < visuals.Count; i++)
        {
            ResidentBase v = visuals[i];
            if (v == null || !v.IsBound || !v.gameObject.activeInHierarchy)
                continue;

            Vector3 d = v.transform.position - world;
            d.z = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = v;
            }
        }

        if (best == null)
            return; // click trúng chỗ trống → giữ lựa chọn cũ

        _selected = best;
        _selectedResidentId = _manager.GetResidentId(best);
        _lastMessage = "Đã chọn " + best.name;
    }

    private void MoveSelected(Vector2Int cell)
    {
        if (_selected == null)
        {
            _lastMessage = "Chưa chọn cư dân (click trái vào một cư dân)";
            return;
        }

        _lastMessage = _manager.DebugMoveTo(_selected, cell)
            ? $"Đi tới ô ({cell.x}, {cell.y})"
            : $"Ô ({cell.x}, {cell.y}) ngoài bản đồ";
    }

    private void MoveAll(Vector2Int cell)
    {
        int moved = 0;
        var visuals = _manager.ActiveVisuals;
        for (int i = 0; i < visuals.Count; i++)
        {
            if (_manager.DebugMoveTo(visuals[i], cell))
                moved++;
        }

        _lastMessage = $"{moved} cư dân đi tới ô ({cell.x}, {cell.y})";
    }

    // View lấy từ pool có thể đã bị bind sang cư dân khác
    private void ValidateSelection()
    {
        if (_selected == null)
            return;

        if (!_selected.IsBound || _manager.GetResidentId(_selected) != _selectedResidentId)
        {
            _selected = null;
            _selectedResidentId = -1;
        }
    }

    private bool TryGetMouseWorld(Mouse mouse, out Vector3 world)
    {
        world = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.forward, Vector3.zero);
        if (!ground.Raycast(ray, out float enter))
            return false;

        world = ray.GetPoint(enter);
        world.z = 0f;
        return true;
    }

    private void DrawSelection()
    {
        if (_selected == null)
        {
            HideVisuals();
            return;
        }

        Vector3 pos = _selected.transform.position;
        pos.z = 0f;

        const int ringSegments = 20;
        _selectRing.enabled = true;
        _selectRing.positionCount = ringSegments;
        for (int i = 0; i < ringSegments; i++)
        {
            float a = i * Mathf.PI * 2f / ringSegments;
            _selectRing.SetPosition(i, pos + new Vector3(Mathf.Cos(a) * 0.25f, Mathf.Sin(a) * 0.125f, 0f));
        }

        var path = _selected.PathPoints;
        int start = _selected.PathIndex;
        int remaining = path.Count - start;
        if (remaining <= 0)
        {
            _pathLine.enabled = false;
            return;
        }

        _pathLine.enabled = true;
        _pathLine.positionCount = remaining + 1;
        _pathLine.SetPosition(0, pos);
        for (int i = 0; i < remaining; i++)
        {
            Vector3 p = path[start + i];
            p.z = 0f;
            _pathLine.SetPosition(i + 1, p);
        }
    }

    private void HideVisuals()
    {
        if (_pathLine != null)
            _pathLine.enabled = false;
        if (_selectRing != null)
            _selectRing.enabled = false;
    }

    private LineRenderer CreateLine(string lineName, Color color, float width)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = 32000;
        lr.numCapVertices = 2;
        lr.enabled = false;
        return lr;
    }

    private void OnGUI()
    {
        if (!_enabled)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 360, 240), GUI.skin.box);
        GUILayout.Label("<b>TEST DI CHUYỂN CƯ DÂN</b>  (F2 tắt)", RichLabel());
        GUILayout.Label("Click trái: chọn cư dân");
        GUILayout.Label("G: đi tới ô dưới chuột   |   Shift+G: tất cả");
        GUILayout.Label("C: huỷ lệnh test   |   [ / ]: giảm / tăng tốc");
        GUILayout.Label("O: hiện ô IsOccupied (tô đỏ)" + (_showOccupied ? "  — ĐANG BẬT" : ""));
        if (_manager != null)
        {
            GUILayout.Label($"Tốc độ: {_manager.CommuteSpeed:0.0}   Đang ép: {_manager.DebugOverrideCount}");
            GUILayout.Label("Đang chọn: " + (_selected != null ? _selected.name : "—"));
        }
        if (_lastTargetCell.x >= 0)
            GUILayout.Label($"Ô đích gần nhất: ({_lastTargetCell.x}, {_lastTargetCell.y})");
        if (!string.IsNullOrEmpty(_lastMessage))
            GUILayout.Label(_lastMessage);
        GUILayout.EndArea();
    }

    private static GUIStyle _richLabel;
    private static GUIStyle RichLabel()
    {
        if (_richLabel == null)
            _richLabel = new GUIStyle(GUI.skin.label) { richText = true };
        return _richLabel;
    }
}
#endif
