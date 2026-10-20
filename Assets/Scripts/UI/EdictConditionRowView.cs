using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private void Awake()
    {
        if (btnLink != null) btnLink.onClick.AddListener(ToggleLink);
        if (btnRemove != null) btnRemove.onClick.AddListener(() => RemoveRequested?.Invoke(this));
    }

    public void Setup(List<string> variableOptions, List<string> operatorOptions, LogicalLink link, bool first, string defaultValue)
    {
        isFirst = first;
        Link = first ? LogicalLink.None : (link == LogicalLink.None ? LogicalLink.AND : link);

        Fill(ddVariable, variableOptions);
        Fill(ddOperator, operatorOptions);

        if (btnLink != null) btnLink.interactable = !first;
        if (btnRemove != null) btnRemove.gameObject.SetActive(!first);

        ResetValues(defaultValue);
        RefreshLink();
    }

    public void ResetValues(string defaultValue)
    {
        if (ddVariable != null) ddVariable.SetValueWithoutNotify(0);
        if (ddOperator != null) ddOperator.SetValueWithoutNotify(0);
        if (inputValue != null) inputValue.SetTextWithoutNotify(defaultValue);
    }

    public bool TryGetValue(out float value)
    {
        value = 0f;
        if (inputValue == null)
            return false;

        string text = inputValue.text.Trim().Replace(',', '.');
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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
            txtLink.text = EdictRuleCatalog.GetLinkName(Link);
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
