using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* trên lưới GridController (8 hướng, không cắt góc).
/// Ô bị chặn: GridCell.IsOccupied (trừ công trình xuất phát / đích) hoặc có object decor (cây...).
/// Buffer được tái sử dụng giữa các lần tìm đường để tránh GC.
/// </summary>
public sealed class GridPathfinder
{
    private const int StraightCost = 10;
    private const int DiagonalCost = 14;

    private static readonly int[] DirX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] DirY = { 0, 0, 1, -1, 1, -1, 1, -1 };

    private readonly GridController _grid;

    private int _width;
    private int _height;
    private int[] _gScore;
    private int[] _parent;
    private int[] _openStamp;
    private int[] _closedStamp;
    private int _stamp;

    // Binary min-heap (cho phép trùng node, bỏ qua node đã đóng khi pop)
    private int[] _heapNode = new int[256];
    private int[] _heapF = new int[256];
    private int _heapCount;

    private ushort _allowA;
    private ushort _allowB;

    public int MaxExpandedNodes { get; set; } = 20000;

    public GridPathfinder(GridController grid)
    {
        _grid = grid;
    }

    public bool IsValid => _grid != null;

    /// <summary>
    /// Tìm đường từ start → goal. Kết quả (gồm cả start và goal) ghi vào <paramref name="result"/>.
    /// allowA/allowB: id công trình được phép đi xuyên (nhà đang đứng, công trình đích).
    /// </summary>
    public bool FindPath(Vector2Int start, Vector2Int goal, ushort allowA, ushort allowB, List<Vector2Int> result)
    {
        result.Clear();
        if (_grid == null)
            return false;

        EnsureBuffers();

        start = Clamp(start);
        goal = Clamp(goal);

        if (start == goal)
        {
            result.Add(goal);
            return true;
        }

        _allowA = allowA;
        _allowB = allowB;

        unchecked { _stamp++; }
        if (_stamp == 0)
        {
            System.Array.Clear(_openStamp, 0, _openStamp.Length);
            System.Array.Clear(_closedStamp, 0, _closedStamp.Length);
            _stamp = 1;
        }

        _heapCount = 0;
        int startIdx = ToIndex(start.x, start.y);
        int goalIdx = ToIndex(goal.x, goal.y);

        _gScore[startIdx] = 0;
        _parent[startIdx] = -1;
        _openStamp[startIdx] = _stamp;
        HeapPush(startIdx, Heuristic(start.x, start.y, goal.x, goal.y));

        int expanded = 0;
        while (_heapCount > 0)
        {
            int current = HeapPop();
            if (_closedStamp[current] == _stamp)
                continue;

            _closedStamp[current] = _stamp;

            if (current == goalIdx)
            {
                BuildResult(goalIdx, result);
                return true;
            }

            if (++expanded > MaxExpandedNodes)
                break;

            int cx = current % _width;
            int cy = current / _width;
            int currentG = _gScore[current];

            for (int d = 0; d < 8; d++)
            {
                int nx = cx + DirX[d];
                int ny = cy + DirY[d];
                if (nx < 0 || ny < 0 || nx >= _width || ny >= _height)
                    continue;

                int nIdx = ToIndex(nx, ny);
                if (_closedStamp[nIdx] == _stamp)
                    continue;

                bool isGoal = nIdx == goalIdx;
                if (!isGoal && !IsWalkable(nx, ny))
                    continue;

                bool diagonal = d >= 4;
                if (diagonal)
                {
                    // Không cắt góc qua ô bị chặn
                    if (!IsWalkableOrGoal(cx + DirX[d], cy, goalIdx) || !IsWalkableOrGoal(cx, cy + DirY[d], goalIdx))
                        continue;
                }

                int tentative = currentG + (diagonal ? DiagonalCost : StraightCost);
                if (_openStamp[nIdx] == _stamp && tentative >= _gScore[nIdx])
                    continue;

                _openStamp[nIdx] = _stamp;
                _gScore[nIdx] = tentative;
                _parent[nIdx] = current;
                HeapPush(nIdx, tentative + Heuristic(nx, ny, goal.x, goal.y));
            }
        }

        return false;
    }

    public bool IsWalkable(int x, int y)
    {
        if (!_grid.IsInside(x, y))
            return false;

        // Ô có công trình (IsOccupied) → né, trừ công trình xuất phát / đích
        GridCell cell = _grid.GetCell(x, y);
        if (cell.IsOccupied)
            return cell.OccupantBuildingId == _allowA || cell.OccupantBuildingId == _allowB;

        return !_grid.HasObject(x, y);
    }

    private bool IsWalkableOrGoal(int x, int y, int goalIdx)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height)
            return false;
        return ToIndex(x, y) == goalIdx || IsWalkable(x, y);
    }

    private void BuildResult(int goalIdx, List<Vector2Int> result)
    {
        int node = goalIdx;
        while (node >= 0)
        {
            result.Add(new Vector2Int(node % _width, node / _width));
            node = _parent[node];
        }

        result.Reverse();
    }

    private void EnsureBuffers()
    {
        int w = Mathf.Max(1, _grid.Width);
        int h = Mathf.Max(1, _grid.Height);
        if (_gScore != null && w == _width && h == _height)
            return;

        _width = w;
        _height = h;
        int size = w * h;
        _gScore = new int[size];
        _parent = new int[size];
        _openStamp = new int[size];
        _closedStamp = new int[size];
        _stamp = 0;
    }

    private Vector2Int Clamp(Vector2Int c)
    {
        return new Vector2Int(Mathf.Clamp(c.x, 0, _width - 1), Mathf.Clamp(c.y, 0, _height - 1));
    }

    private int ToIndex(int x, int y) => y * _width + x;

    private static int Heuristic(int ax, int ay, int bx, int by)
    {
        int dx = Mathf.Abs(ax - bx);
        int dy = Mathf.Abs(ay - by);
        int min = dx < dy ? dx : dy;
        int max = dx < dy ? dy : dx;
        return DiagonalCost * min + StraightCost * (max - min);
    }

    private void HeapPush(int node, int f)
    {
        if (_heapCount == _heapNode.Length)
        {
            System.Array.Resize(ref _heapNode, _heapCount * 2);
            System.Array.Resize(ref _heapF, _heapCount * 2);
        }

        int i = _heapCount++;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (_heapF[p] <= f)
                break;
            _heapNode[i] = _heapNode[p];
            _heapF[i] = _heapF[p];
            i = p;
        }

        _heapNode[i] = node;
        _heapF[i] = f;
    }

    private int HeapPop()
    {
        int result = _heapNode[0];
        _heapCount--;
        if (_heapCount == 0)
            return result;

        int lastNode = _heapNode[_heapCount];
        int lastF = _heapF[_heapCount];
        int i = 0;
        while (true)
        {
            int l = i * 2 + 1;
            if (l >= _heapCount)
                break;
            int r = l + 1;
            int c = r < _heapCount && _heapF[r] < _heapF[l] ? r : l;
            if (_heapF[c] >= lastF)
                break;
            _heapNode[i] = _heapNode[c];
            _heapF[i] = _heapF[c];
            i = c;
        }

        _heapNode[i] = lastNode;
        _heapF[i] = lastF;
        return result;
    }
}
