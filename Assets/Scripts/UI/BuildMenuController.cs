using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildMenuController : MonoBehaviour
{
    [Serializable]
    public class Category
    {
        public string name;
        public Button button;
        public BuildingType[] types;
    }

    [Header("Tham Chiếu")]
    [SerializeField] private PlacementManager placementManager;

    [Header("Phá Dỡ")]
    [SerializeField] private Button demolishButton;
    [SerializeField] private Color demolishActiveColor = new Color(1f, 0.45f, 0.4f, 1f);

    [Header("Danh Mục")]
    [SerializeField] private Category[] categories;

    [Header("Danh Sách Công Trình")]
    [SerializeField] private RectTransform optionsPanel;
    [SerializeField] private RectTransform optionsContent;
    [SerializeField] private BuildOptionView optionTemplate;

    private readonly List<BuildOptionView> activeOptions = new List<BuildOptionView>();
    private readonly Stack<BuildOptionView> pooledOptions = new Stack<BuildOptionView>();
    private int openCategory = -1;
    private Color demolishIdleColor = Color.white;

    private void Awake()
    {
        if (placementManager == null)
            placementManager = FindFirstObjectByType<PlacementManager>();

        if (optionTemplate != null)
            optionTemplate.gameObject.SetActive(false);

        if (categories != null)
        {
            for (int i = 0; i < categories.Length; i++)
            {
                int index = i;
                if (categories[i].button != null)
                    categories[i].button.onClick.AddListener(() => ToggleCategory(index));
            }
        }

        if (demolishButton != null)
        {
            if (demolishButton.targetGraphic != null)
                demolishIdleColor = demolishButton.targetGraphic.color;
            demolishButton.onClick.AddListener(OnDemolishClicked);
        }

        Close();
    }

    private void Start()
    {
        RefreshCategoryButtons();
    }

    private void OnEnable()
    {
        GameEvents.Unlisten(EventID.ResourcesChanged, OnResourcesChanged);
        GameEvents.Listen(EventID.ResourcesChanged, OnResourcesChanged);
        GameEvents.Unlisten(EventID.DemolishModeChanged, OnDemolishModeChanged);
        GameEvents.Listen(EventID.DemolishModeChanged, OnDemolishModeChanged);
        GameEvents.Unlisten(EventID.ResearchCompleted, OnResourcesChanged);
        GameEvents.Listen(EventID.ResearchCompleted, OnResourcesChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.ResourcesChanged, OnResourcesChanged);
        GameEvents.Unlisten(EventID.DemolishModeChanged, OnDemolishModeChanged);
        GameEvents.Unlisten(EventID.ResearchCompleted, OnResourcesChanged);
    }

    private void OnDemolishClicked()
    {
        Close();
        if (placementManager != null)
            placementManager.ToggleDemolish();
    }

    // Tô màu nút Phá dỡ khi đang ở chế độ phá
    private void OnDemolishModeChanged(object param)
    {
        if (demolishButton == null || demolishButton.targetGraphic == null)
            return;

        bool active = param is bool b && b;
        UISkin skin = UISkin.Instance;
        if (skin != null && demolishButton.targetGraphic is Image image && skin.squareButton != null)
        {
            image.sprite = active ? skin.squareButtonActive : skin.squareButton;
            return;
        }
        demolishButton.targetGraphic.color = active ? demolishActiveColor : demolishIdleColor;
    }

    public bool IsOpen => openCategory >= 0;

    public int FindCategory(string categoryName)
    {
        if (categories == null)
            return -1;
        for (int i = 0; i < categories.Length; i++)
            if (categories[i].name == categoryName)
                return i;
        return -1;
    }
    public int CategoryCount => categories != null ? categories.Length : 0;

    public void ToggleCategory(int index)
    {
        if (openCategory == index)
        {
            Close();
            return;
        }

        Open(index);
    }

    public void Open(int index)
    {
        if (categories == null || (uint)index >= (uint)categories.Length || optionsPanel == null)
            return;

        ClearOptions();
        openCategory = index;

        BuildingDatabase database = placementManager != null ? placementManager.database : null;
        BuildingType[] types = categories[index].types;
        if (database != null && types != null)
        {
            for (int i = 0; i < types.Length; i++)
            {
                BuildingConfig config = database.GetConfig(types[i]);
                if (config == null)
                    continue;

                BuildOptionView view = RentOption();
                view.Bind(config, OnOptionSelected);
                activeOptions.Add(view);
            }
        }

        optionsPanel.gameObject.SetActive(activeOptions.Count > 0);
        RefreshAffordability();
    }

    public void Close()
    {
        openCategory = -1;
        ClearOptions();
        if (optionsPanel != null)
            optionsPanel.gameObject.SetActive(false);
    }

    private void OnOptionSelected(BuildingType type)
    {
        if (placementManager != null)
            placementManager.BeginPlacement(type);
        Close();
    }

    private void OnResourcesChanged(object param)
    {
        if (openCategory >= 0)
            RefreshAffordability();
    }

    private void RefreshAffordability()
    {
        if (placementManager == null || placementManager.database == null)
            return;

        for (int i = 0; i < activeOptions.Count; i++)
        {
            BuildingConfig config = placementManager.database.GetConfig(activeOptions[i].Type);
            if (config != null && !placementManager.IsResearched(config.type))
            {
                ResearchNode required = ResearchCatalog.Get(ResearchCatalog.GetRequiredResearch(config.type));
                activeOptions[i].SetLocked(required != null ? required.displayName : "?");
                continue;
            }

            activeOptions[i].SetAffordable(config != null && placementManager.CanAfford(config, out _, out _));
        }
    }

    private void RefreshCategoryButtons()
    {
        if (categories == null)
            return;

        BuildingDatabase database = placementManager != null ? placementManager.database : null;
        for (int i = 0; i < categories.Length; i++)
        {
            if (categories[i].button == null)
                continue;

            bool hasAny = false;
            BuildingType[] types = categories[i].types;
            if (database != null && types != null)
            {
                for (int t = 0; t < types.Length && !hasAny; t++)
                    hasAny = database.GetConfig(types[t]) != null;
            }

            categories[i].button.interactable = hasAny;
        }
    }

    private BuildOptionView RentOption()
    {
        BuildOptionView view = pooledOptions.Count > 0
            ? pooledOptions.Pop()
            : Instantiate(optionTemplate, optionsContent != null ? optionsContent : optionsPanel);
        view.gameObject.SetActive(true);
        view.transform.SetAsLastSibling();
        return view;
    }

    private void ClearOptions()
    {
        for (int i = 0; i < activeOptions.Count; i++)
        {
            activeOptions[i].gameObject.SetActive(false);
            pooledOptions.Push(activeOptions[i]);
        }

        activeOptions.Clear();
    }
}
