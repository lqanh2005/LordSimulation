using UnityEngine;

/// <summary>
/// Kích thước footprint (theo ô) của 1 prefab art.
/// size.x = số ô theo trục X (cạnh dưới-phải), size.y = số ô theo trục Y (cạnh dưới-trái).
/// Pivot của prefab nằm ở góc dưới cùng (góc trước) của footprint.
/// </summary>
public class IsoFootprint : MonoBehaviour
{
    public Vector2Int size = Vector2Int.one;
}
