using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class SeasonRingGraphic : MaskableGraphic
{
    [Header("Màu 4 Mùa (Xuân - Hạ - Thu - Đông, theo chiều kim đồng hồ từ đỉnh)")]
    [SerializeField] private Color[] seasonColors =
    {
        new Color32(0x7B, 0xC8, 0x6C, 0xFF),
        new Color32(0xF5, 0xC2, 0x42, 0xFF),
        new Color32(0xE0, 0x7B, 0x39, 0xFF),
        new Color32(0x7E, 0xC8, 0xE3, 0xFF)
    };

    [SerializeField, Min(0f)] private float thickness = 10f;
    [SerializeField, Range(0f, 30f)] private float gapDegrees = 4f;
    [SerializeField, Range(4, 64)] private int stepsPerSeason = 24;

    public Color GetSeasonColor(SeasonType season)
    {
        int index = (int)season;
        if (seasonColors == null || index < 0 || index >= seasonColors.Length)
            return Color.white;
        return seasonColors[index];
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (seasonColors == null || seasonColors.Length < 4)
            return;

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
        float inner = Mathf.Max(0f, outer - thickness);
        float span = 90f - gapDegrees;

        for (int season = 0; season < 4; season++)
        {
            Color32 segmentColor = seasonColors[season] * color;
            float start = season * 90f + gapDegrees * 0.5f;
            int baseIndex = vh.currentVertCount;

            for (int i = 0; i <= stepsPerSeason; i++)
            {
                float angle = (start + span * i / stepsPerSeason) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                vh.AddVert(center + dir * outer, segmentColor, Vector2.zero);
                vh.AddVert(center + dir * inner, segmentColor, Vector2.zero);
            }

            for (int i = 0; i < stepsPerSeason; i++)
            {
                int outer0 = baseIndex + i * 2;
                int inner0 = outer0 + 1;
                int outer1 = outer0 + 2;
                int inner1 = outer0 + 3;
                vh.AddTriangle(outer0, outer1, inner1);
                vh.AddTriangle(outer0, inner1, inner0);
            }
        }
    }
}
