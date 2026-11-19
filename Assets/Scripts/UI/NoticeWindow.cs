using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ Thông báo (Notice_Mockup 546x519): lịch sử thông báo mới nhất ở trên.
/// Mục có hành động (›) bấm vào sẽ tới nơi liên quan.
/// </summary>
public static class NoticeWindow
{
    private static UIWidgets.Window _window;
    private static RectTransform _content;
    private static TextMeshProUGUI _empty;
    private static readonly List<GameObject> _rows = new List<GameObject>();

    public static bool IsOpen => _window != null && _window.overlay != null && UIPopupAnim.IsVisible(_window.overlay.gameObject);

    public static void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public static void Open()
    {
        if (!EnsureUI())
            return;
        UIPopupAnim.Show(_window.overlay.gameObject);
        _window.overlay.SetAsLastSibling();
        Rebuild();
    }

    public static void Close()
    {
        if (_window != null && _window.overlay != null)
            UIPopupAnim.Hide(_window.overlay.gameObject);
    }

    public static void RefreshIfOpen()
    {
        if (IsOpen)
            Rebuild();
    }

    private static void Rebuild()
    {
        foreach (GameObject row in _rows)
            Object.Destroy(row);
        _rows.Clear();

        IReadOnlyList<NotificationFeed.HistoryEntry> history = NotificationFeed.Instance != null
            ? NotificationFeed.Instance.History
            : null;
        int count = history != null ? history.Count : 0;
        _empty.gameObject.SetActive(count == 0);

        for (int i = 0; i < count; i++)
        {
            NotificationFeed.HistoryEntry entry = history[i];
            RectTransform row = UIWidgets.Box(_content, "Notice_" + i);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 68f;

            string color = entry.level == NoticeLevel.Critical ? "#E86A5A" : entry.level == NoticeLevel.Warning ? "#F0C24B" : "#F3E6BC";
            string when = entry.year > 0 ? $"<size=12><color=#8A8474>Năm {entry.year}, T{entry.month}</color></size>\n" : string.Empty;
            TextMeshProUGUI text = UIWidgets.Text(row, "Txt", $"{when}<color={color}>{entry.message}</color>{(entry.onClick != null ? "  ›" : "")}",
                16f, UISkin.Cream, TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.Normal;
            RectTransform tr = text.rectTransform;
            UIWidgets.Stretch(tr);
            tr.offsetMin = new Vector2(24f, 4f);
            tr.offsetMax = new Vector2(-16f, -4f);

            if (entry.onClick != null)
            {
                Image box = row.GetComponent<Image>();
                box.raycastTarget = true;
                Button button = row.gameObject.AddComponent<Button>();
                UISkin.SetButtonTint(button);
                System.Action action = entry.onClick;
                button.onClick.AddListener(() =>
                {
                    Close();
                    action();
                });
            }
            _rows.Add(row.gameObject);
        }
    }

    private static bool EnsureUI()
    {
        if (_window != null && _window.overlay != null)
            return true;

        Canvas canvas = RuntimeUIKit.FindHudCanvas();
        if (canvas == null)
            return false;

        _rows.Clear();
        _window = UIWidgets.CreateWindow(canvas.transform, "NoticeWindow", new Vector2(546f, 519f), "Thông báo", Close, 48f, 170f, 70f);

        RectTransform viewport;
        _content = UIWidgets.ScrollList(_window.frame, "List", 9f, new RectOffset(0, 0, 0, 12), out viewport);
        UIWidgets.TopLeft(viewport, 34f, 92f, 480f, 400f);

        _empty = UIWidgets.Text(_window.frame, "Txt_Empty", "Chưa có thông báo nào.", 16f, UISkin.Muted);
        UIWidgets.TopLeft(_empty.rectTransform, 34f, 220f, 480f, 30f);

        _window.overlay.gameObject.SetActive(false);
        return true;
    }
}
