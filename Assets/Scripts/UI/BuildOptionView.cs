using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildOptionView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text txtName;
    [SerializeField] private TMP_Text txtCost;
    [SerializeField] private Color affordableCostColor = new Color32(0x6B, 0x6F, 0x73, 0xFF);
    [SerializeField] private Color missingCostColor = new Color32(0xE5, 0x48, 0x48, 0xFF);

    public BuildingType Type { get; private set; }

    public void Bind(BuildingConfig config, Action<BuildingType> onSelected)
    {
        Type = config.type;

        if (txtName != null)
            txtName.text = string.IsNullOrEmpty(config.displayName) ? config.type.ToString() : config.displayName;

        if (txtCost != null)
            txtCost.text = FormatCost(config);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected?.Invoke(Type));
        }
    }

    public void SetAffordable(bool affordable)
    {
        if (button != null)
            button.interactable = affordable;

        if (txtCost != null)
            txtCost.color = affordable ? affordableCostColor : missingCostColor;
    }

    private static string FormatCost(BuildingConfig config)
    {
        if (config.costWood <= 0 && config.costGold <= 0)
            return "Miễn phí";

        if (config.costGold <= 0)
            return $"Gỗ {config.costWood}";

        if (config.costWood <= 0)
            return $"Vàng {config.costGold}";

        return $"Gỗ {config.costWood} · Vàng {config.costGold}";
    }
}
