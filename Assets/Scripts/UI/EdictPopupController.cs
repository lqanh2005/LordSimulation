using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EdictPopupController : MonoBehaviour
{
    [Serializable]
    public struct ActionPreset
    {
        public string label;
        public RuleActionType type;
        public float param0;
        public float param1;

        public ActionPreset(string label, RuleActionType type, float param0 = 0f, float param1 = 0f)
        {
            this.label = label;
            this.type = type;
            this.param0 = param0;
            this.param1 = param1;
        }
    }

    [Header("Tham Chiếu")]
    [SerializeField] private EdictManager edictManager;
    [SerializeField] private Button btnClose;

    [Header("Tạo Luật Mới")]
    [SerializeField] private RectTransform conditionContainer;
    [SerializeField] private EdictConditionRowView firstConditionRow;
    [SerializeField] private Button btnAnd;
    [SerializeField] private Button btnOr;
    [SerializeField] private Button btnAddCondition;
    [SerializeField] private TMP_Dropdown ddAction;
    [SerializeField] private Button btnActivate;
    [SerializeField] private TMP_Text txtStatus;
    [SerializeField] private string defaultConditionValue = "30";

    [Header("Luật Đang Hoạt Động")]
    [SerializeField] private RectTransform ruleListContent;
    [SerializeField] private EdictRuleCardView ruleCardTemplate;
    [SerializeField] private TMP_Text txtEmpty;

    [Header("Màu")]
    [SerializeField] private Color linkNormalColor = new Color32(0xC9, 0xC9, 0xC9, 0xFF);
    [SerializeField] private Color linkSelectedColor = new Color32(0xAF, 0xAF, 0xAF, 0xFF);
    [SerializeField] private Color successColor = new Color32(0x4C, 0xAF, 0x50, 0xFF);
    [SerializeField] private Color errorColor = new Color32(0xE5, 0x48, 0x48, 0xFF);

    [Header("Hành Động")]
    [SerializeField] private string actionPlaceholder = "Hành động";
    [SerializeField] private ActionPreset[] actionPresets =
    {
        new ActionPreset("Tăng sản xuất lương thực", RuleActionType.BoostProduction, (float)BuildingType.Farm, 20f),
        new ActionPreset("Tăng khai thác gỗ", RuleActionType.BoostProduction, (float)BuildingType.LumberCamp, 20f),
        new ActionPreset("Tăng khai thác mỏ", RuleActionType.BoostProduction, (float)BuildingType.Mine, 20f),
        new ActionPreset("Tăng cường lò sưởi", RuleActionType.BoostProduction, (float)BuildingType.Furnace, 20f),
        new ActionPreset("Thu thêm thuế", RuleActionType.ModifyGoldMonthly, 50f),
        new ActionPreset("Trợ cấp dân chúng", RuleActionType.ModifyGoldMonthly, -50f),
        new ActionPreset("Xoa dịu dân chúng", RuleActionType.ReduceRiotRisk, 10f),
        new ActionPreset("Củng cố uy tín", RuleActionType.ModifyReputation, 5f),
        new ActionPreset("Cho nhập cư", RuleActionType.ImmigrationAdmit),
        new ActionPreset("Từ chối nhập cư", RuleActionType.ImmigrationDeny),
        new ActionPreset("Cách ly người nhập cư", RuleActionType.ImmigrationQuarantine, 2f),
        new ActionPreset("Áp giải tới bàn thẩm vấn", RuleActionType.ImmigrationEscort, 5f)
    };

    private readonly List<EdictConditionRowView> rows = new List<EdictConditionRowView>(EdictRuleData.MaxConditions);
    private readonly List<EdictRuleCardView> cards = new List<EdictRuleCardView>();
    private readonly List<string> variableOptions = new List<string>();
    private readonly List<string> operatorOptions = new List<string>();
    private RuleVariable[] variables;
    private LogicalLink pendingLink = LogicalLink.AND;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        GameEvents.Unlisten(EventID.EdictsChanged, OnEdictsChanged);
        GameEvents.Listen(EventID.EdictsChanged, OnEdictsChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.EdictsChanged, OnEdictsChanged);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Initialize();

        edictManager = HUDLocator.ResolveEdictManager(edictManager);
        SetStatus(string.Empty, successColor);
        RefreshRuleList();
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

    private void Initialize()
    {
        if (initialized)
            return;
        initialized = true;

        variables = new RuleVariable[EdictRuleCatalog.CityVariables.Length + EdictRuleCatalog.ApplicantVariables.Length];
        EdictRuleCatalog.CityVariables.CopyTo(variables, 0);
        EdictRuleCatalog.ApplicantVariables.CopyTo(variables, EdictRuleCatalog.CityVariables.Length);

        foreach (RuleVariable variable in variables)
            variableOptions.Add(EdictRuleCatalog.GetVariableName(variable));

        foreach (ComparisonOp op in EdictRuleCatalog.Operators)
            operatorOptions.Add(EdictRuleCatalog.GetOperatorSymbol(op));

        if (firstConditionRow != null)
        {
            firstConditionRow.Setup(variableOptions, operatorOptions, LogicalLink.None, true, defaultConditionValue);
            rows.Add(firstConditionRow);
        }

        if (ddAction != null)
        {
            List<string> actionOptions = new List<string>(actionPresets.Length + 1) { actionPlaceholder };
            foreach (ActionPreset preset in actionPresets)
                actionOptions.Add(preset.label);

            ddAction.ClearOptions();
            ddAction.AddOptions(actionOptions);
            ddAction.SetValueWithoutNotify(0);
            ddAction.RefreshShownValue();
        }

        if (btnClose != null) btnClose.onClick.AddListener(Hide);
        if (btnAnd != null) btnAnd.onClick.AddListener(() => SetPendingLink(LogicalLink.AND));
        if (btnOr != null) btnOr.onClick.AddListener(() => SetPendingLink(LogicalLink.OR));
        if (btnAddCondition != null) btnAddCondition.onClick.AddListener(AddCondition);
        if (btnActivate != null) btnActivate.onClick.AddListener(Activate);

        if (ruleCardTemplate != null)
            ruleCardTemplate.gameObject.SetActive(false);

        SetPendingLink(LogicalLink.AND);
        UpdateAddButton();
    }

    private void OnEdictsChanged(object param)
    {
        RefreshRuleList();
    }

    // ==========================================
    // TẠO LUẬT MỚI
    // ==========================================
    private void SetPendingLink(LogicalLink link)
    {
        pendingLink = link;
        Tint(btnAnd, link == LogicalLink.AND);
        Tint(btnOr, link == LogicalLink.OR);
    }

    private void Tint(Button button, bool selected)
    {
        if (button != null && button.targetGraphic != null)
            button.targetGraphic.color = selected ? linkSelectedColor : linkNormalColor;
    }

    private void AddCondition()
    {
        if (firstConditionRow == null || rows.Count >= EdictRuleData.MaxConditions)
            return;

        Transform parent = conditionContainer != null ? conditionContainer : firstConditionRow.transform.parent;
        EdictConditionRowView row = Instantiate(firstConditionRow, parent);
        row.name = $"ConditionRow_{rows.Count}";
        row.Setup(variableOptions, operatorOptions, pendingLink, false, defaultConditionValue);
        row.RemoveRequested += RemoveCondition;
        rows.Add(row);

        UpdateAddButton();
        SetStatus(string.Empty, successColor);
    }

    private void RemoveCondition(EdictConditionRowView row)
    {
        if (row == firstConditionRow || !rows.Remove(row))
            return;

        row.gameObject.SetActive(false);
        Destroy(row.gameObject);
        UpdateAddButton();
    }

    private void UpdateAddButton()
    {
        if (btnAddCondition != null)
            btnAddCondition.interactable = rows.Count < EdictRuleData.MaxConditions;
    }

    private void Activate()
    {
        edictManager = HUDLocator.ResolveEdictManager(edictManager);
        if (edictManager == null)
        {
            SetStatus("Không tìm thấy hệ thống luật trong màn chơi.", errorColor);
            return;
        }

        int presetIndex = ddAction != null ? ddAction.value - 1 : -1;
        if (presetIndex < 0 || presetIndex >= actionPresets.Length)
        {
            SetStatus("Hãy chọn hành động cho luật.", errorColor);
            return;
        }

        ActionPreset preset = actionPresets[presetIndex];
        EdictRuleBuilder builder = new EdictRuleBuilder(preset.label);

        for (int i = 0; i < rows.Count; i++)
        {
            EdictConditionRowView row = rows[i];
            if (!row.TryGetValue(out float value))
            {
                SetStatus($"Giá trị ở điều kiện {i + 1} không hợp lệ.", errorColor);
                return;
            }

            RuleVariable variable = variables[Mathf.Clamp(row.VariableIndex, 0, variables.Length - 1)];
            ComparisonOp op = EdictRuleCatalog.Operators[Mathf.Clamp(row.OperatorIndex, 0, EdictRuleCatalog.Operators.Length - 1)];
            builder.AddCondition(i == 0 ? LogicalLink.None : row.Link, variable, op, value);
        }

        builder.ThenDo(preset.type, preset.param0, preset.param1);

        if (!builder.IsValid(out string error))
        {
            SetStatus(error, errorColor);
            return;
        }

        if (edictManager.activeCount >= EdictManager.MAX_EDICTS)
        {
            SetStatus($"Đã đạt tối đa {EdictManager.MAX_EDICTS} luật.", errorColor);
            return;
        }

        if (edictManager.CreateRule(builder) < 0)
        {
            SetStatus("Không thể tạo luật.", errorColor);
            return;
        }

        ResetForm();
        SetStatus($"Đã kích hoạt: {preset.label}", successColor);
        RefreshRuleList();
    }

    private void ResetForm()
    {
        for (int i = rows.Count - 1; i >= 1; i--)
        {
            rows[i].gameObject.SetActive(false);
            Destroy(rows[i].gameObject);
            rows.RemoveAt(i);
        }

        if (firstConditionRow != null)
            firstConditionRow.ResetValues(defaultConditionValue);

        if (ddAction != null)
        {
            ddAction.SetValueWithoutNotify(0);
            ddAction.RefreshShownValue();
        }

        SetPendingLink(LogicalLink.AND);
        UpdateAddButton();
    }

    private void SetStatus(string message, Color color)
    {
        if (txtStatus == null)
            return;

        txtStatus.text = message;
        txtStatus.color = color;
    }

    // ==========================================
    // LUẬT ĐANG HOẠT ĐỘNG
    // ==========================================
    private void RefreshRuleList()
    {
        if (!initialized || ruleListContent == null || ruleCardTemplate == null)
            return;

        foreach (EdictRuleCardView card in cards)
        {
            if (card == null)
                continue;
            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }
        cards.Clear();

        int count = edictManager != null ? edictManager.activeCount : 0;
        StringBuilder links = new StringBuilder();
        StringBuilder details = new StringBuilder();

        for (int i = 0; i < count; i++)
        {
            ref EdictRuleData rule = ref edictManager.GetEdictRef(i);
            DescribeRule(in rule, links, details);

            EdictRuleCardView card = Instantiate(ruleCardTemplate, ruleListContent);
            card.name = $"RuleCard_{rule.ruleID}";
            card.gameObject.SetActive(true);
            card.Bind(rule.ruleID, rule.ruleName, rule.isActive, links.ToString(), details.ToString());
            card.ToggleRequested += OnToggleRule;
            card.DeleteRequested += OnDeleteRule;
            cards.Add(card);
        }

        if (txtEmpty != null)
            txtEmpty.gameObject.SetActive(count == 0);
    }

    private void OnToggleRule(ushort ruleId)
    {
        if (edictManager != null)
            edictManager.ToggleRule(ruleId);
    }

    private void OnDeleteRule(ushort ruleId)
    {
        if (edictManager != null)
            edictManager.RemoveRule(ruleId);
    }

    private static void DescribeRule(in EdictRuleData rule, StringBuilder links, StringBuilder details)
    {
        links.Clear();
        details.Clear();

        int count = rule.ConditionCount;
        for (int i = 0; i < count; i++)
        {
            RuleCondition condition = rule.conditions[i];
            links.Append(i == 0 ? "If" : EdictRuleCatalog.GetLinkName(condition.link)).Append('\n');
            details.Append(EdictRuleCatalog.GetVariableName(condition.variable))
                .Append(' ').Append(EdictRuleCatalog.GetOperatorSymbol(condition.op))
                .Append(' ').Append(FormatValue(condition.value)).Append('\n');
        }

        links.Append("Then");
        details.Append(EdictRuleCatalog.DescribeAction(in rule.thenAction));

        if (!rule.elseAction.IsNone)
        {
            links.Append("\nElse");
            details.Append('\n').Append(EdictRuleCatalog.DescribeAction(in rule.elseAction));
        }
    }

    private static string FormatValue(float value)
    {
        return value.ToString(value % 1f == 0f ? "0" : "0.##", CultureInfo.InvariantCulture);
    }
}
