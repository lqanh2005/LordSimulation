using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EdictRuleCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text txtTitle;
    [SerializeField] private TMP_Text txtLinks;
    [SerializeField] private TMP_Text txtDetails;
    [SerializeField] private Button btnToggle;
    [SerializeField] private Image imgToggleTrack;
    [SerializeField] private RectTransform toggleKnob;
    [SerializeField] private Button btnDelete;
    [SerializeField] private LayoutElement layoutElement;

    [Header("Kích Thước")]
    [SerializeField] private float headerHeight = 58f;
    [SerializeField] private float bottomPadding = 14f;
    [SerializeField] private float knobPadding = 3f;

    [Header("Màu")]
    [SerializeField] private Color toggleOnColor = new Color32(0x7C, 0xB0, 0x7F, 0xFF);
    [SerializeField] private Color toggleOffColor = new Color32(0xA8, 0xA8, 0xA8, 0xFF);
    [SerializeField] private Color activeTextColor = new Color32(0x33, 0x33, 0x33, 0xFF);
    [SerializeField] private Color inactiveTextColor = new Color32(0x80, 0x80, 0x80, 0xFF);

    public event Action<ushort> ToggleRequested;
    public event Action<ushort> DeleteRequested;

    public ushort RuleId { get; private set; }

    private void Awake()
    {
        if (btnToggle != null) btnToggle.onClick.AddListener(() => ToggleRequested?.Invoke(RuleId));
        if (btnDelete != null) btnDelete.onClick.AddListener(() => DeleteRequested?.Invoke(RuleId));
    }

    public void Bind(ushort ruleId, string title, bool isActive, string links, string details)
    {
        RuleId = ruleId;

        if (txtTitle != null) txtTitle.text = title;
        if (txtLinks != null) txtLinks.text = links;
        if (txtDetails != null) txtDetails.text = details;

        SetActiveVisual(isActive);

        if (layoutElement != null)
        {
            float linksHeight = txtLinks != null ? txtLinks.preferredHeight : 0f;
            float detailsHeight = txtDetails != null ? txtDetails.preferredHeight : 0f;
            layoutElement.preferredHeight = headerHeight + Mathf.Max(linksHeight, detailsHeight) + bottomPadding;
        }
    }

    private void SetActiveVisual(bool isActive)
    {
        if (imgToggleTrack != null)
            imgToggleTrack.color = isActive ? toggleOnColor : toggleOffColor;

        if (toggleKnob != null)
        {
            Vector2 anchor = new Vector2(isActive ? 1f : 0f, 0.5f);
            toggleKnob.anchorMin = anchor;
            toggleKnob.anchorMax = anchor;
            toggleKnob.pivot = anchor;
            toggleKnob.anchoredPosition = new Vector2(isActive ? -knobPadding : knobPadding, 0f);
        }

        Color textColor = isActive ? activeTextColor : inactiveTextColor;
        if (txtTitle != null) txtTitle.color = textColor;
        if (txtLinks != null) txtLinks.color = textColor;
        if (txtDetails != null) txtDetails.color = textColor;
    }
}
