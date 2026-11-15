using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup cây nghiên cứu. Node và đường nối được sinh lúc chạy từ ResearchCatalog
/// (dựa trên template do GameplayHUDBuilder tạo), nên thêm công nghệ chỉ cần sửa catalog.
/// </summary>
public class ResearchTreePopupController : MonoBehaviour
{
    [Header("Tham Chiếu")]
    [SerializeField] private Button btnClose;
    [SerializeField] private TMP_Text txtPoints;
    [SerializeField] private TMP_Text txtCurrent;
    [SerializeField] private Image currentProgressFill;

    [Header("Cây")]
    [SerializeField] private RectTransform treeContent;
    [SerializeField] private ResearchNodeView nodeTemplate;
    [SerializeField] private Image lineTemplate;
    [SerializeField] private TMP_Text branchLabelTemplate;
    [SerializeField] private Image branchBandTemplate;

    [Header("Chi Tiết")]
    [SerializeField] private TMP_Text txtDetailName;
    [SerializeField] private TMP_Text txtDetailBranch;
    [SerializeField] private TMP_Text txtDetailDesc;
    [SerializeField] private TMP_Text txtDetailEffects;
    [SerializeField] private TMP_Text txtDetailStatus;
    [SerializeField] private Button btnResearch;
    [SerializeField] private TMP_Text txtResearchButton;

    [Header("Bố Cục")]
    [SerializeField] private Vector2 nodeSize = new Vector2(210f, 78f);
    [SerializeField] private float columnSpacing = 270f;
    [SerializeField] private float rowSpacing = 96f;
    [SerializeField] private Vector2 padding = new Vector2(170f, 28f);
    [SerializeField] private float lineThickness = 4f;

    [Header("Màu")]
    [SerializeField] private Color lockedColor = new Color32(0xC9, 0xCC, 0xCF, 0xFF);
    [SerializeField] private Color availableColor = new Color32(0xF4, 0xF1, 0xE6, 0xFF);
    [SerializeField] private Color researchingColor = new Color32(0xFF, 0xE0, 0x9A, 0xFF);
    [SerializeField] private Color completedColor = new Color32(0xA8, 0xD5, 0xAA, 0xFF);
    [SerializeField] private Color lockedTextColor = new Color32(0x80, 0x84, 0x88, 0xFF);
    [SerializeField] private Color textColor = new Color32(0x33, 0x33, 0x33, 0xFF);
    [SerializeField] private Color selectedOutlineColor = new Color32(0x3B, 0x7D, 0xD8, 0xFF);
    [SerializeField] private Color lineColor = new Color32(0xA0, 0xA5, 0xAA, 0xFF);
    [SerializeField] private Color lineDoneColor = new Color32(0x4C, 0xAF, 0x50, 0xFF);
    [SerializeField] private Color[] branchBandColors =
    {
        new Color32(0xE8, 0xF2, 0xE0, 0xFF),
        new Color32(0xEE, 0xE8, 0xE0, 0xFF),
        new Color32(0xE2, 0xEA, 0xF2, 0xFF)
    };

    private struct LineSegment
    {
        public Image image;
        public ResearchID from;
    }

    private readonly Dictionary<ResearchID, ResearchNodeView> nodeViews = new Dictionary<ResearchID, ResearchNodeView>();
    private readonly List<LineSegment> lines = new List<LineSegment>();
    private ResearchManager research;
    private ResearchID selected = ResearchID.None;
    private bool built;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(Hide);
        if (btnResearch != null) btnResearch.onClick.AddListener(OnResearchClicked);
        if (nodeTemplate != null) nodeTemplate.gameObject.SetActive(false);
        if (lineTemplate != null) lineTemplate.gameObject.SetActive(false);
        if (branchLabelTemplate != null) branchLabelTemplate.gameObject.SetActive(false);
        if (branchBandTemplate != null) branchBandTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.Unlisten(EventID.ResearchChanged, OnResearchChanged);
        GameEvents.Listen(EventID.ResearchChanged, OnResearchChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.ResearchChanged, OnResearchChanged);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ResolveManager();
        BuildTree();

        if (selected == ResearchID.None)
            selected = research != null && research.CurrentResearch != ResearchID.None
                ? research.CurrentResearch
                : ResearchCatalog.All[0].id;

        RefreshAll();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (gameObject.activeSelf)
            Hide();
        else
            Show();
    }

    private void ResolveManager()
    {
        if (research != null)
            return;

        PlayerContain contain = GamePlayController.Instance != null ? GamePlayController.Instance.playerContain : null;
        if (contain == null)
            contain = FindFirstObjectByType<PlayerContain>();
        research = contain != null ? contain.researchManager : FindFirstObjectByType<ResearchManager>();
    }

    // ==========================================
    // DỰNG CÂY
    // ==========================================

    private Vector2 NodePosition(ResearchNode node) =>
        new Vector2(padding.x + node.tier * columnSpacing, -(padding.y + node.row * rowSpacing));

    private void BuildTree()
    {
        if (built || treeContent == null || nodeTemplate == null)
            return;
        built = true;

        float width = padding.x + padding.y + ResearchCatalog.MaxTier * columnSpacing + nodeSize.x;
        float height = padding.y * 2f + ResearchCatalog.MaxRow * rowSpacing + nodeSize.y;
        treeContent.sizeDelta = new Vector2(Mathf.Max(width, treeContent.sizeDelta.x), height);

        BuildBranchBands(treeContent.sizeDelta.x);

        // Đường nối vẽ trước để nằm dưới node
        for (int i = 0; i < ResearchCatalog.All.Length; i++)
        {
            ResearchNode node = ResearchCatalog.All[i];
            for (int p = 0; p < node.prerequisites.Length; p++)
            {
                ResearchNode parent = ResearchCatalog.Get(node.prerequisites[p]);
                if (parent != null)
                    BuildConnector(parent, node);
            }
        }

        for (int i = 0; i < ResearchCatalog.All.Length; i++)
        {
            ResearchNode node = ResearchCatalog.All[i];
            ResearchNodeView view = Instantiate(nodeTemplate, treeContent);
            view.name = $"Node_{node.id}";
            view.gameObject.SetActive(true);

            RectTransform rt = view.Rect;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = nodeSize;
            rt.anchoredPosition = NodePosition(node);

            view.Bind(node, OnNodeClicked);
            nodeViews[node.id] = view;
        }
    }

    private void BuildBranchBands(float contentWidth)
    {
        for (int b = 0; b <= (int)ResearchBranch.Society; b++)
        {
            ResearchBranch branch = (ResearchBranch)b;
            float minRow = float.MaxValue, maxRow = float.MinValue;
            for (int i = 0; i < ResearchCatalog.All.Length; i++)
            {
                ResearchNode n = ResearchCatalog.All[i];
                if (n.branch != branch) continue;
                minRow = Mathf.Min(minRow, n.row);
                maxRow = Mathf.Max(maxRow, n.row);
            }
            if (minRow > maxRow)
                continue;

            float top = padding.y + minRow * rowSpacing - (rowSpacing - nodeSize.y) * 0.5f;
            float bottom = padding.y + maxRow * rowSpacing + nodeSize.y + (rowSpacing - nodeSize.y) * 0.5f;

            if (branchBandTemplate != null)
            {
                Image band = Instantiate(branchBandTemplate, treeContent);
                band.name = $"Band_{branch}";
                band.gameObject.SetActive(true);
                band.color = branchBandColors.Length > b ? branchBandColors[b] : Color.white;
                RectTransform rt = band.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(0f, -top);
                rt.sizeDelta = new Vector2(contentWidth, bottom - top);
            }

            if (branchLabelTemplate != null)
            {
                TMP_Text label = Instantiate(branchLabelTemplate, treeContent);
                label.name = $"Label_{branch}";
                label.gameObject.SetActive(true);
                label.text = ResearchCatalog.GetBranchName(branch);
                RectTransform rt = label.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(14f, -top);
                rt.sizeDelta = new Vector2(padding.x - 28f, bottom - top);
            }
        }
    }

    // Đường gấp khúc: ngang ra giữa 2 cột → dọc → ngang vào node con
    private void BuildConnector(ResearchNode from, ResearchNode to)
    {
        Vector2 a = NodePosition(from) + new Vector2(nodeSize.x, -nodeSize.y * 0.5f);
        Vector2 b = NodePosition(to) + new Vector2(0f, -nodeSize.y * 0.5f);
        float midX = b.x - (columnSpacing - nodeSize.x) * 0.5f;

        AddSegment(from.id, new Vector2(a.x, a.y), new Vector2(midX, a.y));
        AddSegment(from.id, new Vector2(midX, a.y), new Vector2(midX, b.y));
        AddSegment(from.id, new Vector2(midX, b.y), new Vector2(b.x, b.y));
    }

    private void AddSegment(ResearchID from, Vector2 p0, Vector2 p1)
    {
        if (lineTemplate == null)
            return;

        float half = lineThickness * 0.5f;
        float xMin = Mathf.Min(p0.x, p1.x) - half;
        float xMax = Mathf.Max(p0.x, p1.x) + half;
        float yMax = Mathf.Max(p0.y, p1.y) + half;
        float yMin = Mathf.Min(p0.y, p1.y) - half;
        if (xMax - xMin <= lineThickness && yMax - yMin <= lineThickness)
            return;

        Image line = Instantiate(lineTemplate, treeContent);
        line.gameObject.SetActive(true);
        RectTransform rt = line.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(xMin, yMax);
        rt.sizeDelta = new Vector2(xMax - xMin, yMax - yMin);
        lines.Add(new LineSegment { image = line, from = from });
    }

    // ==========================================
    // CẬP NHẬT HIỂN THỊ
    // ==========================================

    private void OnResearchChanged(object param)
    {
        if (gameObject.activeInHierarchy)
            RefreshAll();
    }

    private void OnNodeClicked(ResearchID id)
    {
        selected = id;
        RefreshAll();
    }

    private void OnResearchClicked()
    {
        if (research == null || selected == ResearchID.None)
            return;

        if (research.CurrentResearch == selected)
            research.CancelResearch();
        else
            research.StartResearch(selected);
        // ResearchChanged sẽ gọi RefreshAll
    }

    private void RefreshAll()
    {
        RefreshHeader();
        RefreshNodes();
        RefreshDetails();
    }

    private void RefreshHeader()
    {
        if (research == null)
        {
            if (txtPoints != null) txtPoints.text = string.Empty;
            if (txtCurrent != null) txtCurrent.text = "Chưa có hệ thống nghiên cứu";
            if (currentProgressFill != null) SetBarFill(currentProgressFill, 0f);
            return;
        }

        if (txtPoints != null)
            txtPoints.text = $"Điểm NC: +{research.LastPointsPerMonth:0.#}/tháng   ·   Đã xong {research.CompletedCount}/{ResearchCatalog.All.Length}";

        ResearchNode current = ResearchCatalog.Get(research.CurrentResearch);
        if (current == null)
        {
            if (txtCurrent != null) txtCurrent.text = "Đang nghiên cứu: (chưa chọn)";
            if (currentProgressFill != null) SetBarFill(currentProgressFill, 0f);
            return;
        }

        if (txtCurrent != null)
            txtCurrent.text = $"Đang nghiên cứu: {current.displayName}  {research.CurrentProgress:0.#}/{current.cost}  ({FormatEta(current)})";
        if (currentProgressFill != null)
            SetBarFill(currentProgressFill, research.CurrentProgress / current.cost);
    }

    private string FormatEta(ResearchNode node)
    {
        if (research == null || research.LastPointsPerMonth <= 0.01f)
            return "không có tiến triển";

        float remain = Mathf.Max(0f, node.cost - (research.CurrentResearch == node.id ? research.CurrentProgress : 0f));
        int months = Mathf.CeilToInt(remain / research.LastPointsPerMonth);
        return $"~{months} tháng";
    }

    private void RefreshNodes()
    {
        foreach (KeyValuePair<ResearchID, ResearchNodeView> pair in nodeViews)
        {
            ResearchNode node = ResearchCatalog.Get(pair.Key);
            ResearchNodeState state = research != null ? research.GetState(pair.Key) : ResearchNodeState.Locked;

            Color fill = state switch
            {
                ResearchNodeState.Completed => completedColor,
                ResearchNodeState.Researching => researchingColor,
                ResearchNodeState.Available => availableColor,
                _ => lockedColor
            };

            string info = state switch
            {
                ResearchNodeState.Completed => "Đã hoàn thành",
                ResearchNodeState.Researching => $"{research.CurrentProgress:0}/{node.cost} điểm",
                _ => $"{node.cost} điểm"
            };

            float progress = state == ResearchNodeState.Researching ? research.CurrentProgress / node.cost : 0f;
            pair.Value.Refresh(state, info, progress, fill,
                state == ResearchNodeState.Locked ? lockedTextColor : textColor,
                pair.Key == selected, selectedOutlineColor);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            bool done = research != null && research.IsCompleted(lines[i].from);
            lines[i].image.color = done ? lineDoneColor : lineColor;
        }
    }

    private void RefreshDetails()
    {
        ResearchNode node = ResearchCatalog.Get(selected);
        if (node == null)
        {
            SetText(txtDetailName, "Chọn một công nghệ");
            SetText(txtDetailBranch, string.Empty);
            SetText(txtDetailDesc, string.Empty);
            SetText(txtDetailEffects, string.Empty);
            SetText(txtDetailStatus, string.Empty);
            if (btnResearch != null) btnResearch.interactable = false;
            return;
        }

        ResearchNodeState state = research != null ? research.GetState(node.id) : ResearchNodeState.Locked;

        SetText(txtDetailName, node.displayName);
        SetText(txtDetailBranch, $"{ResearchCatalog.GetBranchName(node.branch)}  ·  {node.cost} điểm");
        SetText(txtDetailDesc, node.description);
        SetText(txtDetailEffects, "Hiệu quả:\n" + ResearchCatalog.DescribeEffects(node));

        string status;
        string buttonLabel;
        bool interactable;
        switch (state)
        {
            case ResearchNodeState.Completed:
                status = "Đã nghiên cứu xong.";
                buttonLabel = "Đã hoàn thành";
                interactable = false;
                break;
            case ResearchNodeState.Researching:
                status = $"Tiến độ {research.CurrentProgress:0.#}/{node.cost} ({FormatEta(node)}).";
                buttonLabel = "Hủy nghiên cứu";
                interactable = true;
                break;
            case ResearchNodeState.Available:
                status = research != null && research.CurrentResearch != ResearchID.None
                    ? $"Chuyển sang sẽ mất tiến độ hiện tại. Dự kiến {FormatEta(node)}."
                    : $"Dự kiến {FormatEta(node)}.";
                buttonLabel = "Nghiên cứu";
                interactable = research != null;
                break;
            default:
                status = "Cần nghiên cứu trước: " + DescribeMissingPrereqs(node);
                buttonLabel = "Chưa mở khóa";
                interactable = false;
                break;
        }

        if (research != null && research.LastPointsPerMonth <= basePointsHint)
            status += "\nMẹo: xây Trường học và giao Giáo viên để tăng điểm nghiên cứu.";

        SetText(txtDetailStatus, status);
        SetText(txtResearchButton, buttonLabel);
        if (btnResearch != null)
            btnResearch.interactable = interactable;
    }

    private const float basePointsHint = 3.01f;

    private string DescribeMissingPrereqs(ResearchNode node)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < node.prerequisites.Length; i++)
        {
            if (research != null && research.IsCompleted(node.prerequisites[i]))
                continue;
            ResearchNode pre = ResearchCatalog.Get(node.prerequisites[i]);
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(pre != null ? pre.displayName : node.prerequisites[i].ToString());
        }
        return sb.ToString();
    }

    /// <summary>Thanh tiến độ: co giãn theo anchorMax.x (rect phải căn kiểu stretch ngang).</summary>
    public static void SetBarFill(Image bar, float progress01)
    {
        if (bar == null)
            return;
        RectTransform rt = bar.rectTransform;
        rt.anchorMax = new Vector2(Mathf.Clamp01(progress01), rt.anchorMax.y);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
