using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Một dòng điều kiện của sắc lệnh: [Trường] [Phép so sánh] [Giá trị].
/// GDD v0.3 mục 8.2: mọi phần chọn từ danh sách — trường xét gia đình có danh sách giá trị riêng
/// (ô giá trị đổi thành dropdown dựng lúc chạy), trường thành phố vẫn nhập số.
/// </summary>
public class EdictConditionRowView : MonoBehaviour
{
    [SerializeField] private Button btnLink;
    [SerializeField] private TMP_Text txtLink;
    [SerializeField] private TMP_Dropdown ddVariable;
    [SerializeField] private TMP_Dropdown ddOperator;
    [SerializeField] private TMP_InputField inputValue;
    [SerializeField] private Button btnRemove;

    public event Action<EdictConditionRowView> RemoveRequested;

    public LogicalLink Link { get; private set; }
    public int VariableIndex => ddVariable != null ? ddVariable.value : 0;
    public int OperatorIndex => ddOperator != null ? ddOperator.value : 0;

    private bool isFirst;
    private RuleVariable[] _variables;
    private ComparisonOp[] _ops = EdictRuleCatalog.Operators;
    private List<(string label, float value)> _values;
    private TMP_Dropdown _ddValue;   // Dropdown giá trị (clone từ ddOperator), hiện thay ô nhập số khi trường có danh sách

    private void Awake()
    {
        if (btnLink != null) btnLink.onClick.AddListener(ToggleLink);
        if (btnRemove != null) btnRemove.onClick.AddListener(() => RemoveRequested?.Invoke(this));
    }

    public void Setup(RuleVariable[] variables, List<string> variableOptions, LogicalLink link, bool first, string defaultValue)
    {
        isFirst = first;
        _variables = variables;
        Link = first ? LogicalLink.None : (link == LogicalLink.None ? LogicalLink.AND : link);

        Fill(ddVariable, variableOptions);
        if (ddVariable != null)
        {
            ddVariable.onValueChanged.RemoveListener(OnVariableChanged);
            ddVariable.onValueChanged.AddListener(OnVariableChanged);
        }

        if (btnLink != null) btnLink.interactable = !first;
        if (btnRemove != null) btnRemove.gameObject.SetActive(!first);

        ResetValues(defaultValue);
        RefreshLink();
    }

    public void ResetValues(string defaultValue)
    {
        if (ddVariable != null) ddVariable.SetValueWithoutNotify(0);
        if (inputValue != null) inputValue.SetTextWithoutNotify(defaultValue);
        RefreshForVariable();
    }

    public RuleVariable CurrentVariable =>
        _variables != null && _variables.Length > 0 ? _variables[Mathf.Clamp(VariableIndex, 0, _variables.Length - 1)] : RuleVariable.Food;

    public ComparisonOp CurrentOperator => _ops[Mathf.Clamp(OperatorIndex, 0, _ops.Length - 1)];

    public bool TryGetValue(out float value)
    {
        value = 0f;
        if (_values != null)
        {
            if (_values.Count == 0 || _ddValue == null)
                return false;
            value = _values[Mathf.Clamp(_ddValue.value, 0, _values.Count - 1)].value;
            return true;
        }

        if (inputValue == null)
            return false;
        string text = inputValue.text.Trim().Replace(',', '.');
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void OnVariableChanged(int _) => RefreshForVariable();

    private void RefreshForVariable()
    {
        RuleVariable variable = CurrentVariable;
        _ops = EdictRuleCatalog.GetAllowedOperators(variable);
        var opLabels = new List<string>(_ops.Length);
        foreach (ComparisonOp op in _ops)
            opLabels.Add(EdictRuleCatalog.GetOperatorSymbol(op));
        Fill(ddOperator, opLabels);
        if (ddOperator != null)
            ddOperator.gameObject.SetActive(variable != RuleVariable.FamilyNoHealthNote);   // GDD: "không cần so sánh"

        _values = EdictRuleCatalog.GetValueOptions(variable);
        if (_values != null)
        {
            EnsureValueDropdown();
            var labels = new List<string>(_values.Count);
            foreach (var v in _values)
                labels.Add(v.label);
            if (labels.Count == 0)
                labels.Add("(chưa mở note nào)");
            Fill(_ddValue, labels);
        }

        if (_ddValue != null)
            _ddValue.gameObject.SetActive(_values != null);
        if (inputValue != null)
            inputValue.gameObject.SetActive(_values == null);
    }

    private void EnsureValueDropdown()
    {
        if (_ddValue != null || ddOperator == null || inputValue == null)
            return;

        // Dòng nhân bản từ dòng đầu đã mang sẵn dropdown giá trị -> dùng lại
        Transform existing = inputValue.transform.parent.Find("ddValue");
        if (existing != null && existing.TryGetComponent(out _ddValue))
            return;

        GameObject go = Instantiate(ddOperator.gameObject, inputValue.transform.parent);
        go.name = "ddValue";
        RectTransform src = (RectTransform)inputValue.transform;
        RectTransform dst = (RectTransform)go.transform;
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta = src.sizeDelta;
        dst.SetSiblingIndex(src.GetSiblingIndex());
        LayoutElement srcLayout = inputValue.GetComponent<LayoutElement>();
        LayoutElement dstLayout = go.GetComponent<LayoutElement>();
        if (srcLayout != null)
        {
            if (dstLayout == null) dstLayout = go.AddComponent<LayoutElement>();
            dstLayout.preferredWidth = srcLayout.preferredWidth;
            dstLayout.minWidth = srcLayout.minWidth;
            dstLayout.flexibleWidth = srcLayout.flexibleWidth;
            dstLayout.preferredHeight = srcLayout.preferredHeight;
        }
        _ddValue = go.GetComponent<TMP_Dropdown>();
        _ddValue.onValueChanged.RemoveAllListeners();
    }

    private void ToggleLink()
    {
        if (isFirst)
            return;

        Link = Link == LogicalLink.AND ? LogicalLink.OR : LogicalLink.AND;
        RefreshLink();
    }

    private void RefreshLink()
    {
        if (txtLink != null)
            txtLink.text = isFirst ? EdictRuleCatalog.GetLinkLabel(LogicalLink.None) : EdictRuleCatalog.GetLinkLabel(Link);
    }

    private static void Fill(TMP_Dropdown dropdown, List<string> options)
    {
        if (dropdown == null)
            return;

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
    }
}
