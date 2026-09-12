using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ImmigrationPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Text queueText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button denyButton;
    [SerializeField] private Button quarantineButton;
    [SerializeField] private Button closeButton;

    private ImmigrationManager _manager;
    private bool _built;

    public void Bind(ImmigrationManager manager)
    {
        _manager = manager;
        EnsureUi();
        Subscribe();
        Hide();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        GameEvents.Unlisten(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Unlisten(EventID.PopulationChanged, OnPopulationOrResourcesChanged);
        GameEvents.Unlisten(EventID.ResourcesChanged, OnPopulationOrResourcesChanged);
        GameEvents.Listen(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Listen(EventID.PopulationChanged, OnPopulationOrResourcesChanged);
        GameEvents.Listen(EventID.ResourcesChanged, OnPopulationOrResourcesChanged);
    }

    private void Unsubscribe()
    {
        GameEvents.Unlisten(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Unlisten(EventID.PopulationChanged, OnPopulationOrResourcesChanged);
        GameEvents.Unlisten(EventID.ResourcesChanged, OnPopulationOrResourcesChanged);
    }

    private void OnQueueChanged(object param)
    {
        if (root == null || !root.activeSelf || _manager == null)
            return;

        if (_manager.TryGetCurrent(out ResidentData applicant))
            ShowApplicant(in applicant, _manager.GetSuggestedAction(in applicant), _manager.waitingCount);
        else
            ShowEmpty();
    }

    private void OnPopulationOrResourcesChanged(object param)
    {
        if (root == null || !root.activeSelf || _manager == null)
            return;
        if (!_manager.TryGetCurrent(out _))
            return;
        if (queueText != null)
            queueText.text = $"Waiting: {_manager.waitingCount}";
    }

    public void ShowApplicant(in ResidentData applicant, RuleAction suggested, int waitingCount)
    {
        EnsureUi();
        root.SetActive(true);
        if (titleText != null)
            titleText.text = "City Gate — Immigration Check";
        if (queueText != null)
            queueText.text = $"Waiting: {waitingCount}";
        if (bodyText != null)
            bodyText.text = ImmigrationRules.FormatProfile(in applicant, suggested);
        SetButtons(true);
        HighlightSuggestion(suggested);
    }

    public void ShowEmpty()
    {
        EnsureUi();
        root.SetActive(true);
        if (titleText != null)
            titleText.text = "City Gate";
        if (queueText != null)
            queueText.text = "Waiting: 0";
        if (bodyText != null)
            bodyText.text = "No arrivals waiting at the gate.";
        SetButtons(false);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void SetButtons(bool interactable)
    {
        if (acceptButton != null)
            acceptButton.interactable = interactable;
        if (denyButton != null)
            denyButton.interactable = interactable;
        if (quarantineButton != null)
            quarantineButton.interactable = interactable;
    }

    private void HighlightSuggestion(RuleAction suggested)
    {
        ColorBlock normal = ColorBlock.defaultColorBlock;
        ApplyBlock(acceptButton, suggested == RuleAction.Admit ? Color.green : normal.normalColor);
        ApplyBlock(denyButton, suggested == RuleAction.Deny || suggested == RuleAction.EscortToDesk ? new Color(0.85f, 0.3f, 0.3f) : normal.normalColor);
        ApplyBlock(quarantineButton, suggested == RuleAction.Quarantine ? new Color(0.95f, 0.75f, 0.2f) : normal.normalColor);
    }

    private static void ApplyBlock(Button button, Color color)
    {
        if (button == null)
            return;
        ColorBlock block = button.colors;
        block.normalColor = color;
        block.highlightedColor = color * 1.1f;
        button.colors = block;
    }

    private void EnsureUi()
    {
        if (_built && root != null)
            return;

        if (root == null)
            BuildDefaultUi();

        WireButtons();
        EnsureEventSystem();
        _built = true;
    }

    private void WireButtons()
    {
        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveListener(OnAccept);
            acceptButton.onClick.AddListener(OnAccept);
        }

        if (denyButton != null)
        {
            denyButton.onClick.RemoveListener(OnDeny);
            denyButton.onClick.AddListener(OnDeny);
        }

        if (quarantineButton != null)
        {
            quarantineButton.onClick.RemoveListener(OnQuarantine);
            quarantineButton.onClick.AddListener(OnQuarantine);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnAccept()
    {
        if (_manager != null)
            _manager.AcceptCurrent();
    }

    private void OnDeny()
    {
        if (_manager != null)
            _manager.DenyCurrent();
    }

    private void OnQuarantine()
    {
        if (_manager != null)
            _manager.QuarantineCurrent();
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private void BuildDefaultUi()
    {
        GameObject canvasGo = new GameObject("ImmigrationCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        root = CreatePanel(canvasGo.transform, "Popup", new Vector2(520f, 460f), new Color(0.08f, 0.08f, 0.1f, 0.94f));
        titleText = CreateText(root.transform, "Title", new Vector2(0f, 190f), 22, FontStyle.Bold, 480f, 36f);
        queueText = CreateText(root.transform, "Queue", new Vector2(0f, 155f), 16, FontStyle.Normal, 480f, 28f);
        bodyText = CreateText(root.transform, "Body", new Vector2(0f, 20f), 16, FontStyle.Normal, 460f, 240f);
        bodyText.alignment = TextAnchor.UpperLeft;

        acceptButton = CreateButton(root.transform, "Accept", "Accept", new Vector2(-160f, -180f), new Color(0.25f, 0.55f, 0.28f));
        denyButton = CreateButton(root.transform, "Deny", "Deny", new Vector2(0f, -180f), new Color(0.6f, 0.22f, 0.22f));
        quarantineButton = CreateButton(root.transform, "Quarantine", "Quarantine", new Vector2(160f, -180f), new Color(0.65f, 0.5f, 0.15f));
        closeButton = CreateButton(root.transform, "Close", "Close", new Vector2(210f, 190f), new Color(0.3f, 0.3f, 0.32f));
        RectTransform closeRt = closeButton.GetComponent<RectTransform>();
        closeRt.sizeDelta = new Vector2(80f, 32f);
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    private static Text CreateText(Transform parent, string name, Vector2 pos, int fontSize, FontStyle style, float width, float height)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = ResolveFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = pos;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        Button button = go.AddComponent<Button>();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(140f, 40f);
        rt.anchoredPosition = pos;

        Text text = CreateText(go.transform, "Label", Vector2.zero, 16, FontStyle.Bold, 140f, 40f);
        text.text = label;
        text.color = Color.white;
        return button;
    }

    private static Font ResolveFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
