using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ Lưu game / Tải game theo SaveGame_Mockup / LoadData_Mockup (1276x622):
/// danh sách ô lưu bên trái, ô đang chọn + nút hành động bên phải, có hộp thoại xác nhận.
/// </summary>
public static class SaveLoadWindow
{
    public enum Mode { Save, Load }

    private static UIWidgets.Window _window;
    private static Mode _mode;
    private static int _selected;
    private static Action<int> _onLoad;
    private static readonly List<(Image box, TextMeshProUGUI info)> _rows = new List<(Image, TextMeshProUGUI)>();
    private static TextMeshProUGUI _previewText;
    private static TextMeshProUGUI _selectedTitle;
    private static TextMeshProUGUI _selectedInfo;
    private static Button _action;
    private static TextMeshProUGUI _actionLabel;

    public static bool IsOpen => _window != null && _window.overlay != null && UIPopupAnim.IsVisible(_window.overlay.gameObject);

    /// <summary>
    /// Mở cửa sổ. Chế độ Load: onLoad(slot) được gọi sau khi người chơi xác nhận.
    /// Chế độ Save: tự lưu vào ô đã chọn qua SaveLoadManager.
    /// </summary>
    public static void Open(Mode mode, Action<int> onLoad = null)
    {
        SaveLoadManager manager = ResolveManager();
        if (manager == null || !EnsureUI())
            return;

        _mode = mode;
        _onLoad = onLoad;
        _window.title.text = mode == Mode.Save ? "Lưu game" : "Tải game";
        _actionLabel.text = mode == Mode.Save ? "Lưu game" : "Tải game";

        int latest = manager.GetLatestSlot();
        _selected = mode == Mode.Save ? manager.ActiveSlot : Mathf.Max(0, latest);
        Refresh();

        UIPopupAnim.Show(_window.overlay.gameObject);
        _window.overlay.SetAsLastSibling();
    }

    public static void Close()
    {
        if (_window != null && _window.overlay != null)
            UIPopupAnim.Hide(_window.overlay.gameObject);
    }

    private static SaveLoadManager ResolveManager()
    {
        if (GameController.Instance != null && GameController.Instance.saveLoadManager != null)
            return GameController.Instance.saveLoadManager;
        return UnityEngine.Object.FindFirstObjectByType<SaveLoadManager>();
    }

    // ==========================================
    // HIỂN THỊ
    // ==========================================

    private static void Refresh()
    {
        SaveLoadManager manager = ResolveManager();
        if (manager == null)
            return;

        for (int i = 0; i < _rows.Count; i++)
        {
            SaveLoadManager.SlotInfo info = manager.GetSlotInfo(i);
            _rows[i].info.text = Describe(info);
            UIWidgets.SetBoxBright(_rows[i].box, i == _selected);
        }

        SaveLoadManager.SlotInfo selected = manager.GetSlotInfo(_selected);
        _selectedTitle.text = $"Slot {_selected + 1}";
        _selectedInfo.text = Describe(selected);
        _previewText.text = !selected.exists
            ? "<color=#8A8474>Ô trống</color>"
            : selected.hasMeta
                ? $"<size=26><color=#FFD77E>Năm {selected.year}</color></size>\nTháng {selected.month} · Mùa {SeasonName(selected.month)}\nDân số {selected.population}"
                : "Bản lưu cũ\n<size=14>(chưa có thông tin)</size>";

        _action.interactable = _mode == Mode.Save || selected.exists;
    }

    private static string Describe(SaveLoadManager.SlotInfo info)
    {
        if (!info.exists)
            return "<color=#8A8474>Trống</color>";
        string time = info.savedAt.ToString("dd/MM/yyyy HH:mm");
        return info.hasMeta
            ? $"Năm {info.year}, Tháng {info.month}  /  Dân số {info.population}  /  {time}"
            : time;
    }

    private static string SeasonName(int month) => (month - 1) switch
    {
        >= 0 and <= 2 => "Xuân",
        >= 3 and <= 5 => "Hạ",
        >= 6 and <= 8 => "Thu",
        _ => "Đông"
    };

    private static void Select(int slot)
    {
        _selected = slot;
        Refresh();
    }

    private static void OnAction()
    {
        SaveLoadManager manager = ResolveManager();
        if (manager == null)
            return;

        int slot = _selected;
        bool exists = manager.GetSlotInfo(slot).exists;
        if (_mode == Mode.Save)
        {
            if (exists)
                ConfirmDialog.Ask(ConfirmDialog.Icon.Save, $"Xác nhận lưu đè Slot {slot + 1}", "Lưu", () => DoSave(slot));
            else
                DoSave(slot);
            return;
        }

        if (!exists)
            return;
        ConfirmDialog.Ask(ConfirmDialog.Icon.Load, "Xác nhận tải", "Tải", () =>
        {
            Close();
            _onLoad?.Invoke(slot);
        });
    }

    private static void DoSave(int slot)
    {
        SaveLoadManager manager = ResolveManager();
        if (manager == null)
            return;
        manager.SaveToSlot(slot);
        Refresh();
    }

    // ==========================================
    // DỰNG UI (toạ độ theo mockup 1276x622)
    // ==========================================

    private static bool EnsureUI()
    {
        if (_window != null && _window.overlay != null)
            return true;

        Canvas canvas = RuntimeUIKit.FindHudCanvas();
        if (canvas == null)
            return false;

        _rows.Clear();
        _window = UIWidgets.CreateWindow(canvas.transform, "SaveLoadWindow", new Vector2(1276f, 622f), "Lưu game", Close, 56f, 190f);

        RectTransform listViewport;
        RectTransform list = UIWidgets.ScrollList(_window.frame, "SlotList", 17f, new RectOffset(0, 0, 0, 0), out listViewport);
        UIWidgets.TopLeft(listViewport, 32f, 99f, 697f, 472f);

        for (int i = 0; i < SaveLoadManager.SlotCount; i++)
        {
            int slot = i;
            RectTransform row = UIWidgets.Box(list, "Slot_" + (i + 1));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 87f;
            Image box = row.GetComponent<Image>();
            box.raycastTarget = true;
            Button button = row.gameObject.AddComponent<Button>();
            UISkin.SetButtonTint(button);
            button.onClick.AddListener(RuntimeUIKit.PlayClick);
            button.onClick.AddListener(() => Select(slot));

            TextMeshProUGUI name = UIWidgets.Text(row, "Txt_Slot", $"Slot {i + 1}", 18f, UISkin.Cream, TextAlignmentOptions.Left);
            Anchor(name.rectTransform, 24f, 80f);
            TextMeshProUGUI info = UIWidgets.Text(row, "Txt_Info", "", 16f, UISkin.Cream, TextAlignmentOptions.Left);
            Anchor(info.rectTransform, 98f, 580f);
            _rows.Add((box, info));
        }

        RectTransform preview = UIWidgets.Box(_window.frame, "Thumbnail");
        UIWidgets.TopLeft(preview, 856f, 155f, 285f, 131f);
        _previewText = UIWidgets.Text(preview, "Txt_Preview", "", 16f, UISkin.Cream);
        _previewText.textWrappingMode = TextWrappingModes.Normal;
        UIWidgets.Stretch(_previewText.rectTransform, 8f);

        RectTransform selected = UIWidgets.Box(_window.frame, "Selected", true);
        UIWidgets.TopLeft(selected, 753f, 302f, 491f, 151f);
        _selectedTitle = UIWidgets.Text(selected, "Txt_Slot", "", 18f, UISkin.Cream, TextAlignmentOptions.Left);
        UIWidgets.TopLeft(_selectedTitle.rectTransform, 24f, 30f, 80f, 30f);
        _selectedInfo = UIWidgets.Text(selected, "Txt_Info", "", 16f, UISkin.Cream, TextAlignmentOptions.TopLeft);
        _selectedInfo.textWrappingMode = TextWrappingModes.Normal;
        UIWidgets.TopLeft(_selectedInfo.rectTransform, 98f, 35f, 370f, 100f);

        _action = UIWidgets.BoxButton(_window.frame, "Btn_Action", "Lưu game", 18f, OnAction, false);
        UIWidgets.TopLeft((RectTransform)_action.transform, 753f, 501f, 491f, 70f);
        _actionLabel = _action.GetComponentInChildren<TextMeshProUGUI>();

        _window.overlay.gameObject.SetActive(false);
        return true;
    }

    /// <summary>Chữ căn giữa dọc theo hàng, cách mép trái x, rộng width.</summary>
    private static void Anchor(RectTransform rt, float x, float width)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(width, 30f);
    }
}
