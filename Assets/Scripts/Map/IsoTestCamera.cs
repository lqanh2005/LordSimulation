using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Camera test: WASD/kéo chuột phải hoặc giữa để pan, cuộn để zoom.</summary>
[RequireComponent(typeof(Camera))]
public class IsoTestCamera : MonoBehaviour
{
    public float panSpeed = 10f;
    public float zoomStep = 0.5f;
    public Vector2 zoomRange = new Vector2(2f, 10f);
    [Tooltip("Tắt trong gameplay vì chuột phải dùng để huỷ đặt nhà")]
    public bool rightDragPan = true;

    private Camera cam;
    private Vector3 lastMouseWorld;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && !GameHotkeys.IsTypingInUI())
        {
            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed) dir.y -= 1f;
            if (kb.aKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed) dir.x += 1f;
            // unscaled: vẫn pan được khi game đang Pause (timeScale = 0)
            transform.position += (Vector3)(dir * panSpeed * cam.orthographicSize / 9f * Time.unscaledDeltaTime);
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - Mathf.Sign(scroll) * zoomStep, zoomRange.x, zoomRange.y);
        }

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        bool dragging = mouse.middleButton.isPressed || (rightDragPan && mouse.rightButton.isPressed);
        bool started = mouse.middleButton.wasPressedThisFrame || (rightDragPan && mouse.rightButton.wasPressedThisFrame);
        if (dragging && !started)
        {
            transform.position += lastMouseWorld - mouseWorld;
            mouseWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        }

        lastMouseWorld = mouseWorld;
    }
}
