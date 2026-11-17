using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchNodeView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image outline;
    [SerializeField] private TMP_Text txtName;
    [SerializeField] private TMP_Text txtInfo;
    [SerializeField] private Image progressFill;
    [SerializeField] private Image icon;

    public ResearchID Id { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    public void Bind(ResearchNode node, Action<ResearchID> onClick)
    {
        Id = node.id;
        if (txtName != null)
            txtName.text = node.displayName;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(Id));
        }
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon == null)
            return;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    public void Refresh(ResearchNodeState state, string info, float progress01, Color fill, Color text, bool selected, Color selectedColor)
    {
        UISkin skin = UISkin.Instance;
        if (skin != null && background != null && skin.boxGold != null)
        {
            // Skin mockup: khoá = hộp xám, đang chọn / đang nghiên cứu = viền vàng sáng
            Sprite sprite = state == ResearchNodeState.Locked && !selected ? skin.boxDim
                : selected || state == ResearchNodeState.Researching ? skin.boxGoldBright
                : skin.boxGold;
            UISkin.SetSliced(background, sprite, Color.white);
            if (outline != null)
                outline.color = Color.clear;
            if (icon != null)
                icon.color = state == ResearchNodeState.Locked ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }
        else if (background != null)
            background.color = fill;
        if (txtName != null)
            txtName.color = text;
        if (txtInfo != null)
        {
            txtInfo.text = info;
            txtInfo.color = text;
        }
        if (outline != null && skin == null)
            outline.color = selected ? selectedColor : new Color(0f, 0f, 0f, 0.25f);
        if (progressFill != null)
        {
            progressFill.gameObject.SetActive(state == ResearchNodeState.Researching);
            ResearchTreePopupController.SetBarFill(progressFill, progress01);
        }
    }
}
