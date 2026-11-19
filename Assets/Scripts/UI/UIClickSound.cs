using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tiếng click chung cho MỌI nút UI (nút có sẵn trong scene, prefab, nút dựng lúc chạy) mà không cần gắn listener từng nút.
/// Bắt chước cách EventSystem xác định một cú click: nhấn chuột trái trên một Selectable đang bật
/// rồi thả ra trên đúng Selectable đó thì phát tiếng. Gắn tự động trên MusicManager (sống suốt game).
/// </summary>
public class UIClickSound : MonoBehaviour
{
    private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
    private PointerEventData _pointer;
    private EventSystem _pointerOwner; // EventSystem tạo lại mỗi scene
    private GameObject _pressed;

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame)
            _pressed = FindClickable(mouse.position.ReadValue());

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (_pressed != null && FindClickable(mouse.position.ReadValue()) == _pressed)
                RuntimeUIKit.PlayClick();
            _pressed = null;
        }
    }

    /// <summary>Đối tượng nhận click (Button, Toggle, Dropdown…) dưới con trỏ, null nếu không có hoặc đang bị khoá.</summary>
    private GameObject FindClickable(Vector2 screenPos)
    {
        EventSystem es = EventSystem.current;
        if (es == null)
            return null;

        if (_pointer == null || _pointerOwner != es)
        {
            _pointer = new PointerEventData(es);
            _pointerOwner = es;
        }
        _pointer.position = screenPos;

        Hits.Clear();
        es.RaycastAll(_pointer, Hits);
        if (Hits.Count == 0)
            return null;

        // Giống EventSystem: chỉ lấy đối tượng trên cùng, rồi đi lên cha tìm handler click
        GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
        if (handler == null)
            return null;

        Selectable selectable = handler.GetComponent<Selectable>();
        if (selectable == null || !selectable.IsInteractable())
            return null;
        return handler;
    }
}
