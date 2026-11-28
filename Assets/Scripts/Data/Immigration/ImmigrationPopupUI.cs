using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Màn kiểm duyệt ở Cổng theo Gate_Mockup: nền cổng thành toàn màn hình, khung chân dung,
/// thẻ tên + bảng hồ sơ, 3 nút Từ chối / Cho vào / Cách ly. UI dựng lúc chạy trên canvas riêng.
/// </summary>
public class ImmigrationPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;      // tên người xin vào
    [SerializeField] private TMP_Text bodyText;       // dòng gợi ý + "Vì sao" / thông báo khi trống
    [SerializeField] private TMP_Text queueText;
    private TMP_Text _entryTaxLabel;   // Nút chọn mức thuế nhập thành (GDD 6.4)
    private TMP_Text _autoLogText;     // Gate Clerk + danh sách gia đình đã được rule xử lý
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button denyButton;
    [SerializeField] private Button quarantineButton;
    [SerializeField] private Button closeButton;

    private const float RowStride = 34f;
    private const int MaxRows = 10;

    private ImmigrationManager _manager;
    private bool _built;
    private Image _portrait;
    private Image _background;    // ảnh nền cổng: nạp khi mở, giải phóng khi đóng (Addressables)
    private RectTransform _rowsRoot;
    private readonly List<(TMP_Text label, TMP_Text value, Image line)> _rowViews = new List<(TMP_Text, TMP_Text, Image)>();
    private readonly List<(string label, string value)> _rows = new List<(string, string)>();

    public void Bind(ImmigrationManager manager)
    {
        _manager = manager;
        EnsureUi();
        Subscribe();
        // Tắt thẳng, không chạy hiệu ứng đóng: nếu không màn Cổng vừa dựng sẽ loé lên lúc vào scene
        if (root != null)
            root.SetActive(false);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetGateTime(false);
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
        if (!IsOpen || _manager == null)
            return;

        // Vẽ lại qua manager để giữ đủ dòng "Vì sao", thân nhiệt (Nhiệt kế) và kết quả thẩm vấn
        if (_manager.HasApplicants)
            _manager.RefreshCurrent();
        else
            ShowEmpty();
    }

    private void OnPopulationOrResourcesChanged(object param)
    {
        RefreshEntryTaxLabel();
        if (!IsOpen || _manager == null)
            return;
        if (!_manager.TryGetCurrent(out _))
            return;
        if (queueText != null)
            queueText.text = FormatQueue(_manager.waitingCount);
    }

    /// <summary>
    /// GDD v0.3 mục 4.1–4.3: thẻ hồ sơ của cả gia đình — dữ liệu từng thành viên, dữ liệu cả nhà, và note
    /// (chỉ hiện khi có vấn đề, theo cấp hồ sơ). Phán quyết áp dụng cho cả nhà.
    /// </summary>
    public void ShowFamily(in ApplicantFamily family, RuleAction suggested, int waitingCount, int familyCount, string edictReason = null)
    {
        EnsureUi();
        UIPopupAnim.Show(root);
        SetGateTime(true);
        RefreshEntryTaxLabel();
        RefreshAutoLog();
        root.transform.SetAsLastSibling();
        LoadBackground();

        ref ResidentData head = ref family.Head;
        if (titleText != null)
            titleText.text = $"Nhà {NameDatabase.GetLastName(head.lastNameID)}";
        if (queueText != null)
            queueText.text = FormatQueue(waitingCount, familyCount);

        _rows.Clear();
        int workers = FamilyRules.CountWorkers(in family);
        _rows.Add(("Gia đình:", $"{family.count} người · {workers} làm được việc"));
        _rows.Add(("Nguồn gốc:", ImmigrationRules.OriginLabel(head.originRegion)));
        _rows.Add(("Wealth:", family.profileLevel >= FamilyRules.ProfileLevelExam
            ? $"{FamilyRules.TotalWealth(in family)} ({FamilyRules.WealthClassLabel(FamilyRules.GetWealthClass(in family))})"
            : "<i>cần Khám sơ bộ</i>"));
        if (head.hungerMonths > 0)
            _rows.Add(("Chờ ngoài tường:", $"<color=#E86A5A>{head.hungerMonths} tháng — đang đói</color>"));
        if ((FamilyRules.GetFamilyNotes(in family) & NoteFlags.MarketRumor) != 0)
        {
            GlobalSystemManager g = ResolveGlobal();
            string rumor = g != null ? MarketSystem.DescribeRumor(in g.GetGlobalDataRef()) : null;
            if (!string.IsNullOrEmpty(rumor))
                _rows.Add(("Tin đồn:", $"<size=15><color=#FFD77E>{rumor}</color></size>"));
        }

        for (int i = 0; i < family.count && _rows.Count < MaxRows; i++)
        {
            ref ResidentData m = ref family[i];
            string info = $"{FamilyRules.AgeStageLabel(m.GetAgeGroup())} {m.age}t · {ResidentInfoPopup.GetProfessionName(m.professionType)}";
            NoteFlags notes = FamilyRules.GetVisibleNotes(in m, family.profileLevel);
            if (notes != NoteFlags.None)
                info += $"\n<color=#E86A5A>{FamilyRules.DescribeNotes(notes)}</color>";
            _rows.Add((NameDatabase.FirstNames[m.firstNameID % NameDatabase.FirstNames.Length] + ":", $"<size=15>{info}</size>"));
        }
        ShowRows();

        if (bodyText != null)
        {
            string text = suggested == RuleAction.ManualReview
                ? "Duyệt tay: <color=#FFD77E>bạn quyết định cho cả nhà</color>"
                : $"Gợi ý: <color=#FFD77E>{ImmigrationRules.ActionLabel(suggested)}</color> (cả nhà)";
            if (!string.IsNullOrEmpty(edictReason))
                text += $"\n<size=14><i>Vì sao: {edictReason}</i></size>";
            if (EmergencyOrders.GateClosed)
                text = $"<color=#E86A5A>Cổng đang đóng ({EmergencyOrders.State.gateClosedMonths} tháng) — chỉ có thể Từ chối</color>\n" + text;
            bodyText.text = text;
        }

        UISkin skin = UISkin.Instance;
        if (_portrait != null)
        {
            Sprite sprite = skin == null ? null : head.GetAgeGroup() == AgeGroupType.Child ? skin.portraitKid : skin.portraitAdult;
            _portrait.sprite = sprite;
            _portrait.enabled = sprite != null;
        }

        SetButtons(true);
        if (EmergencyOrders.GateClosed)
        {
            if (acceptButton != null) acceptButton.interactable = false;
            if (quarantineButton != null) quarantineButton.interactable = false;
        }
        HighlightSuggestion(suggested);
    }

    /// <summary>Cột trái: Gate Clerk + các gia đình rule đã xử lý tự động tháng này.</summary>
    private void RefreshAutoLog()
    {
        if (_autoLogText == null || _manager == null)
            return;
        var sb = new System.Text.StringBuilder(256);
        if (!EdictRules.EdictsUnlocked)
            sb.Append("<b>Gate Clerk</b>: cần nghiên cứu Bộ luật");
        else
        {
            int clerks = _manager.ActiveClerks;
            sb.Append("<b>Gate Clerk</b>: ").Append(clerks > 0
                ? $"{clerks} người — tự duyệt tối đa {clerks * ImmigrationManager.FamiliesPerClerk} gia đình/tháng theo sắc lệnh"
                : "<color=#E86A5A>chưa có ai rảnh việc để giao</color>");
        }
        if (_manager.RecentAutoDecisions.Count > 0)
        {
            sb.Append("\n\n<b>Đã được sắc lệnh xử lý:</b>");
            foreach (string line in _manager.RecentAutoDecisions)
                sb.Append("\n• ").Append(line);
        }
        _autoLogText.text = sb.ToString();
    }

    /// <summary>"Số người di cư đang chờ: 20/32" — đỏ khi hàng đông tới mức gây bất ổn.</summary>
    private static string FormatQueue(int waitingCount, int familyCount = -1)
    {
        return familyCount >= 0
            ? $"Đang chờ: {familyCount} gia đình ({waitingCount} người)"
            : $"Số người di cư đang chờ: {waitingCount}";
    }

    public void ShowEmpty()
    {
        EnsureUi();
        UIPopupAnim.Show(root);
        SetGateTime(true);
        RefreshEntryTaxLabel();
        root.transform.SetAsLastSibling();
        LoadBackground();
        if (titleText != null)
            titleText.text = "Cổng thành";
        if (queueText != null)
            queueText.text = FormatQueue(0);
        _rows.Clear();
        ShowRows();
        if (bodyText != null)
            bodyText.text = "Không có ai đang chờ ở Cổng.";
        if (_portrait != null)
            _portrait.enabled = false;
        SetButtons(false);
        HighlightSuggestion((RuleAction)255);
    }

    public bool IsOpen => UIPopupAnim.IsVisible(root);

    private void LoadBackground()
    {
        if (_background == null || _background.sprite != null)
            return;

        GameAssets.LoadAsync<Sprite>(GameAssets.GateBackgroundKey).Completed += handle =>
        {
            if (_background == null || handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                return;
            _background.sprite = handle.Result;
            _background.color = Color.white;
        };
    }

    /// <summary>Màn Cổng đã tắt hẳn (sau hiệu ứng): trả ảnh nền 1920x1080 về bộ nhớ.</summary>
    private void ReleaseBackground()
    {
        if (_background == null)
            return;
        _background.sprite = null;
        _background.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        GameAssets.Release(GameAssets.GateBackgroundKey);
    }

    public void Hide()
    {
        UIPopupAnim.Hide(root);
        SetGateTime(false);
    }

    private static GlobalSystemManager ResolveGlobal() =>
        GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
            ? GamePlayController.Instance.playerContain.globalSystemManager
            : null;

    private void CycleEntryTax()
    {
        GlobalSystemManager global = ResolveGlobal();
        if (global == null)
            return;
        EntryTaxLevel next = (EntryTaxLevel)(((int)global.GetGlobalDataRef().pendingEntryTaxLevel + 1) % 5);
        global.SetPendingEntryTax(next);
        RefreshEntryTaxLabel();
    }

    private void RefreshEntryTaxLabel()
    {
        if (_entryTaxLabel == null)
            return;
        GlobalSystemManager global = ResolveGlobal();
        if (global == null)
            return;
        ref GlobalSystemData d = ref global.GetGlobalDataRef();
        string text = $"Thuế nhập thành: {StabilityRules.EntryTaxLabel(d.entryTaxLevel)} ({StabilityRules.GetEntryTaxPercent(d.entryTaxLevel):0}%)";
        if (d.pendingEntryTaxLevel != d.entryTaxLevel)
            text += $" → <color=#FFD77E>{StabilityRules.EntryTaxLabel(d.pendingEntryTaxLevel)} từ tháng sau</color>";
        _entryTaxLabel.text = text;
    }

    /// <summary>GDD v0.3 mục 3: ở màn Cổng thời gian chạy chậm 1/4, không dừng / tăng tốc được.</summary>
    private static void SetGateTime(bool open)
    {
        GlobalSystemManager global = GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
            ? GamePlayController.Instance.playerContain.globalSystemManager
            : null;
        if (global != null)
            global.SetGateView(open);
    }


    private void ShowRows()
    {
        for (int i = 0; i < _rowViews.Count; i++)
        {
            bool on = i < _rows.Count;
            _rowViews[i].label.gameObject.SetActive(on);
            _rowViews[i].value.gameObject.SetActive(on);
            _rowViews[i].line.gameObject.SetActive(on);
            if (!on) continue;
            _rowViews[i].label.text = _rows[i].label;
            _rowViews[i].value.text = _rows[i].value;
        }
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

    /// <summary>Nút được gợi ý: phóng to nhẹ + chữ vàng sáng.</summary>
    private void HighlightSuggestion(RuleAction suggested)
    {
        Mark(acceptButton, suggested == RuleAction.Admit);
        Mark(denyButton, suggested == RuleAction.Deny);
        Mark(quarantineButton, suggested == RuleAction.Quarantine);
    }

    private static void Mark(Button button, bool suggested)
    {
        if (button == null)
            return;
        button.transform.localScale = suggested ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = suggested ? UISkin.GoldBright : UISkin.Cream;
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
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    // ==========================================
    // DỰNG UI (toạ độ theo Gate_Mockup 1920x1080)
    // ==========================================

    private void BuildDefaultUi()
    {
        UISkin skin = UISkin.Instance;

        GameObject canvasGo = new GameObject("ImmigrationCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Nền cổng thành phủ kín (che cả HUD như mockup)
        RectTransform screen = UIWidgets.Rect(canvasGo.transform, "GateScreen");
        UIWidgets.Stretch(screen);
        _background = UIWidgets.Image(screen, null, new Color(0.08f, 0.08f, 0.1f, 1f), true);
        _background.type = Image.Type.Simple;
        root = screen.gameObject;
        UIPopupAnim.Attach(root, null).Hidden += ReleaseBackground;
        UIPopupAnim.Attach(root, null).Hidden += () => SetGateTime(false);

        // Góc trái trên: số người đang chờ
        RectTransform queue = UIWidgets.Rect(screen, "Queue");
        UIWidgets.TopLeft(queue, 50f, 34f, 420f, 60f);
        UIWidgets.Image(queue, skin != null ? skin.gateStackFrame : null, skin != null ? Color.white : RuntimeUIKit.PanelColor);
        queueText = UIWidgets.Text(queue, "Txt_Queue", "", 19f, UISkin.Cream, TextAlignmentOptions.Left);
        RectTransform qr = (RectTransform)queueText.transform;
        UIWidgets.Stretch(qr);
        qr.offsetMin = new Vector2(26f, 0f);

        // Dưới số người chờ: mức thuế nhập thành — bấm để đổi, có hiệu lực từ tháng sau (GDD 6.4)
        Button taxButton = UIWidgets.BoxButton(screen, "Btn_EntryTax", "", 17f, CycleEntryTax, false);
        UIWidgets.TopLeft((RectTransform)taxButton.transform, 50f, 104f, 420f, 46f);
        _entryTaxLabel = taxButton.GetComponentInChildren<TMP_Text>();
        RefreshEntryTaxLabel();

        _autoLogText = UIWidgets.Text(screen, "Txt_AutoLog", "", 15f, UISkin.Cream, TextAlignmentOptions.TopLeft, false);
        _autoLogText.textWrappingMode = TextWrappingModes.Normal;
        UIWidgets.TopLeft(_autoLogText.rectTransform, 50f, 166f, 360f, 520f);

        // Góc phải trên: quay về thành
        RectTransform back = UIWidgets.Rect(screen, "Btn_Back");
        back.anchorMin = back.anchorMax = back.pivot = Vector2.one;
        back.anchoredPosition = new Vector2(-50f, -34f);
        back.sizeDelta = new Vector2(190f, 60f);
        Image backImg = UIWidgets.Image(back, skin != null ? skin.gateStackFrame : null, skin != null ? Color.white : RuntimeUIKit.ButtonColor, true);
        closeButton = back.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = backImg;
        UISkin.SetButtonTint(closeButton);
        TextMeshProUGUI backLabel = UIWidgets.Text(back, "Label", "Quay về thành", 20f, UISkin.Cream, TextAlignmentOptions.Center, true);
        UIWidgets.Stretch(backLabel.rectTransform);

        // Khung chân dung
        RectTransform portraitFrame = UIWidgets.Rect(screen, "PortraitFrame");
        UIWidgets.TopLeft(portraitFrame, 435f, 182f, 433f, 521f);
        Image frameImg = UIWidgets.Image(portraitFrame, skin != null ? skin.gatePortraitFrame : null, skin != null ? Color.white : RuntimeUIKit.PanelColor);
        frameImg.type = Image.Type.Simple;
        RectTransform portrait = UIWidgets.Rect(portraitFrame, "Portrait");
        portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0f);
        portrait.pivot = new Vector2(0.5f, 0f);
        // Ảnh nhân vật có nhiều viền trong suốt -> phóng to rồi cắt bằng mask của khung
        portrait.anchoredPosition = new Vector2(0f, -256f);
        portrait.sizeDelta = new Vector2(763f, 986f);
        RectTransform mask = UIWidgets.Rect(portraitFrame, "Mask");
        UIWidgets.Stretch(mask, 8f);
        mask.gameObject.AddComponent<RectMask2D>();
        portrait.SetParent(mask, false);
        portrait.anchoredPosition = new Vector2(0f, -248f);
        _portrait = portrait.gameObject.AddComponent<Image>();
        _portrait.preserveAspect = true;
        _portrait.raycastTarget = false;

        // Bảng hồ sơ
        RectTransform info = UIWidgets.Rect(screen, "InfoPanel");
        UIWidgets.TopLeft(info, 902f, 212f, 581f, 481f);
        Image infoImg = UIWidgets.Image(info, skin != null ? skin.popupFrame : null, skin != null ? Color.white : RuntimeUIKit.PanelColor);
        infoImg.pixelsPerUnitMultiplier = 1.6f;

        RectTransform nameTab = UIWidgets.Rect(screen, "NameTab");
        UIWidgets.TopLeft(nameTab, 1058f, 178f, 250f, 77f);
        Image tabImg = UIWidgets.Image(nameTab, skin != null ? skin.popupFrame : null, skin != null ? Color.white : RuntimeUIKit.PanelColor);
        tabImg.pixelsPerUnitMultiplier = 2.6f;
        titleText = UIWidgets.Text(nameTab, "Txt_Name", "", 28f, UISkin.Cream, TextAlignmentOptions.Center, true);
        titleText.fontStyle = FontStyles.Bold;
        UIWidgets.Stretch((RectTransform)titleText.transform, 10f);

        _rowsRoot = UIWidgets.Rect(info, "Rows");
        UIWidgets.TopLeft(_rowsRoot, 70f, 54f, 441f, MaxRows * RowStride);
        for (int i = 0; i < MaxRows; i++)
        {
            float y = i * RowStride;
            TextMeshProUGUI label = UIWidgets.Text(_rowsRoot, "Label_" + i, "", 18f, UISkin.Tan, TextAlignmentOptions.Left, true);
            label.fontSize = 17f;
            UIWidgets.TopLeft(label.rectTransform, 28f, y, 160f, RowStride - 4f);
            TextMeshProUGUI value = UIWidgets.Text(_rowsRoot, "Value_" + i, "", 18f, UISkin.Cream, TextAlignmentOptions.Left, true);
            value.fontSize = 17f;
            UIWidgets.TopLeft(value.rectTransform, 190f, y, 251f, RowStride - 4f);
            RectTransform lineRt = UIWidgets.Rect(_rowsRoot, "Line_" + i);
            UIWidgets.TopLeft(lineRt, 28f, y + RowStride - 3f, 413f, 1f);
            Image line = UIWidgets.Image(lineRt, null, new Color32(0x4A, 0x46, 0x40, 0xFF));
            _rowViews.Add((label, value, line));
        }

        bodyText = UIWidgets.Text(info, "Txt_Suggestion", "", 17f, UISkin.Cream, TextAlignmentOptions.TopLeft, true);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        RectTransform br = (RectTransform)bodyText.transform;
        br.anchorMin = br.anchorMax = new Vector2(0f, 0f);
        br.pivot = new Vector2(0f, 0f);
        br.anchoredPosition = new Vector2(98f, 22f);
        br.sizeDelta = new Vector2(413f, 60f);

        // Ba nút quyết định
        denyButton = DecisionButton(screen, "Btn_Deny", "Từ chối", 558f, skin != null ? skin.gateDeny : null);
        acceptButton = DecisionButton(screen, "Btn_Accept", "Cho vào", 837f, skin != null ? skin.gateAccept : null);
        quarantineButton = DecisionButton(screen, "Btn_Quarantine", "Cách ly", 1116f, skin != null ? skin.gateQuarantine : null);
    }

    private static Button DecisionButton(RectTransform parent, string name, string label, float x, Sprite sprite)
    {
        RectTransform rt = UIWidgets.Rect(parent, name);
        // Pivot giữa để phóng to khi được gợi ý mà không lệch vị trí
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x + 123.5f, -(800f + 38.5f));
        rt.sizeDelta = new Vector2(247f, 77f);
        Image img = UIWidgets.Image(rt, sprite, sprite != null ? Color.white : RuntimeUIKit.ButtonColor, true);
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        UISkin.SetButtonTint(button);
        TextMeshProUGUI text = UIWidgets.Text(rt, "Label", label, 22f, UISkin.Cream, TextAlignmentOptions.Center, true);
        text.fontStyle = FontStyles.Bold;
        UIWidgets.Stretch(text.rectTransform);
        return button;
    }
}
