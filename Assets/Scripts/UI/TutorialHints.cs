using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hướng dẫn chơi lần đầu: một dòng gợi ý dưới thanh tài nguyên, tự chuyển bước khi người chơi làm xong
/// (đi đúng MVP Flow trong GDD 5.3). Bỏ qua được; xem lại từ Cài đặt. Trạng thái lưu PlayerPrefs.
/// </summary>
public class TutorialHints : MonoBehaviour
{
    private const string PrefStep = "tutorial.step";
    private const float CheckInterval = 0.5f;

    private struct Step
    {
        public string text;
        public Func<TutorialHints, bool> done;
    }

    private static TutorialHints _instance;

    private Step[] _steps;
    private int _step;
    private float _nextCheck;
    private PlayerContain _contain;
    private RectTransform _panel;
    private TextMeshProUGUI _label;
    private bool _gateOpened;

    public static void EnsureInstance()
    {
        if (_instance == null)
            _instance = new GameObject("TutorialHints").AddComponent<TutorialHints>();
    }

    /// <summary>Bắt đầu lại từ bước đầu (nút "Xem lại hướng dẫn" trong Cài đặt).</summary>
    public static void Restart()
    {
        PlayerPrefs.SetInt(PrefStep, 0);
        PlayerPrefs.Save();
        EnsureInstance();
        _instance._step = 0;
        _instance._gateOpened = false;
        _instance.Refresh();
    }

    private void Awake()
    {
        _steps = new[]
        {
            new Step { text = "Người tị nạn đang chờ ở Cổng. Bấm <b>G</b> để kiểm duyệt: Cho vào / Từ chối / Cách ly.",
                       done = t => t._gateOpened },
            new Step { text = "Dân cần chỗ ở, vô gia cư sẽ chết rét mùa đông. Bấm <b>2</b> (Nhà ở) và xây Lều tạm hoặc Nhà dân.",
                       done = t => t.HasBuilding(b => ResidentAssignmentRules.IsNormalHousing(b.buildingType)) },
            new Step { text = "Cần lương thực: bấm <b>4</b> (Nông nghiệp) xây Nông trại rồi bấm vào nó để chọn trồng Lúa.",
                       done = t => t.HasBuilding(b => b.buildingType == BuildingType.Farm && b.cropType != CropType.None) },
            new Step { text = "Gỗ để xây, than để sưởi mùa đông: bấm <b>3</b> (Sản xuất) xây Trại gỗ và Mỏ.",
                       done = t => t.HasBuilding(b => b.buildingType == BuildingType.LumberCamp || b.buildingType == BuildingType.Mine) },
            new Step { text = "Bấm <b>T</b> mở Cây nghiên cứu. Nghiên cứu <b>Nhiệt kế</b> để đo thân nhiệt người xin vào.",
                       done = t => t._contain != null && t._contain.researchManager != null
                                   && t._contain.researchManager.CurrentResearch != ResearchID.None
                                   || t.ResearchDone() },
            new Step { text = "Bấm <b>E</b> đặt Sắc lệnh để tự động gợi ý quyết định ở Cổng (tối đa 3 luật).",
                       done = t => t._contain != null && t._contain.edictManager != null && t._contain.edictManager.activeCount > 0 },
            new Step { text = "Mục tiêu: sống qua mùa đông đầu (tháng 10–12) với ≥20 dân. Tích trữ than & lương thực! (Space: dừng, Tab: tốc độ)",
                       done = t => t.Month() >= 10 || t.Year() >= 2 },
        };

        _step = Mathf.Clamp(PlayerPrefs.GetInt(PrefStep, 0), 0, _steps.Length);
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.ImmigrationQueueChanged, OnGateUsed);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.ImmigrationQueueChanged, OnGateUsed);
    }

    private void OnGateUsed(object param)
    {
        // Mở bàn kiểm duyệt (kể cả bằng chuột vào Cổng) hoặc đã xử lý một người
        ImmigrationPopupUI ui = FindFirstObjectByType<ImmigrationPopupUI>(FindObjectsInactive.Include);
        if (ui != null && ui.IsOpen)
            _gateOpened = true;
    }

    private void Start()
    {
        _contain = FindFirstObjectByType<PlayerContain>();
        EnsureUI();
        Refresh();
    }

    private void Update()
    {
        if (_step >= _steps.Length || Time.unscaledTime < _nextCheck)
            return;
        _nextCheck = Time.unscaledTime + CheckInterval;

        if (!_gateOpened)
        {
            ImmigrationPopupUI ui = FindFirstObjectByType<ImmigrationPopupUI>(FindObjectsInactive.Include);
            _gateOpened = ui != null && ui.IsOpen;
        }

        if (_steps[_step].done(this))
        {
            _step++;
            PlayerPrefs.SetInt(PrefStep, _step);
            PlayerPrefs.Save();
            Refresh();
        }
    }

    private void Skip()
    {
        _step = _steps.Length;
        PlayerPrefs.SetInt(PrefStep, _step);
        PlayerPrefs.Save();
        Refresh();
    }

    private void Refresh()
    {
        EnsureUI();
        if (_panel == null)
            return;

        bool show = _step < _steps.Length;
        _panel.gameObject.SetActive(show);
        if (show)
            _label.text = $"<b>Hướng dẫn {_step + 1}/{_steps.Length}:</b> {_steps[_step].text}";
    }

    // ===== Điều kiện =====

    private bool HasBuilding(Func<BuildingData, bool> match)
    {
        BuildingManager buildings = _contain != null ? _contain.buildingManager : null;
        if (buildings == null)
            return false;
        for (int i = 0; i < buildings.activeCount; i++)
        {
            if (match(buildings.GetBuildingRef(i)))
                return true;
        }
        return false;
    }

    private bool ResearchDone() =>
        _contain != null && _contain.researchManager != null && _contain.researchManager.CompletedCount > 0;

    private int Month() => _contain != null && _contain.globalSystemManager != null
        ? _contain.globalSystemManager.GetGlobalDataRef().currentMonth : 1;

    private int Year() => _contain != null && _contain.globalSystemManager != null
        ? _contain.globalSystemManager.GetGlobalDataRef().currentYear : 1;

    // ===== UI =====

    private void EnsureUI()
    {
        if (_panel != null)
            return;

        Canvas canvas = RuntimeUIKit.FindHudCanvas();
        if (canvas == null)
            return;

        GameObject go = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        _panel = (RectTransform)go.transform;
        _panel.SetParent(canvas.transform, false);
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 1f);
        _panel.pivot = new Vector2(0.5f, 1f);
        _panel.anchoredPosition = new Vector2(0f, -130f);
        _panel.sizeDelta = new Vector2(900f, 54f);
        go.GetComponent<Image>().color = new Color32(0xFF, 0xF4, 0xD6, 0xF0);

        HorizontalLayoutGroup row = go.GetComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(14, 8, 6, 6);
        row.spacing = 10f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        _label = RuntimeUIKit.CreateText(_panel, "Text", 16f, FontStyles.Normal, TextAlignmentOptions.Left);
        _label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        Image skip = RuntimeUIKit.CreateButton(_panel, "Skip", "Bỏ qua", Skip);
        skip.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;

        _panel.gameObject.SetActive(false);
    }
}
