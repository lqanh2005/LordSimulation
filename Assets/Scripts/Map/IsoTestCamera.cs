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

    [Header("Giới hạn vùng di chuyển")]
    [Tooltip("Giữ tâm camera trong bản đồ (lưới isometric hình thoi)")]
    public bool clampToMap = true;
    [Tooltip("Số ô được phép ra ngoài mép bản đồ")]
    [Min(0f)] public float mapMargin = 2f;
    [Tooltip("Khung hình không vượt ra ngoài nền cảnh quan (world). Rộng/cao = 0 là không giới hạn")]
    public Rect viewBounds;

    private Camera cam;
    private Vector3 lastMouseWorld;
    private GridController grid;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    // LateUpdate: chặn cả khi chỗ khác dời camera (nhảy tới công trình, phím H/G...)
    private void LateUpdate()
    {
        if (clampToMap)
            ClampToMap();
        ClampToView();
    }

    /// <summary>Giữ cả khung hình nằm trong nền cảnh quan; zoom xa quá thì thu lại cho vừa nền.</summary>
    private void ClampToView()
    {
        if (viewBounds.width <= 0f || viewBounds.height <= 0f)
            return;

        float maxSize = Mathf.Min(viewBounds.height * 0.5f, viewBounds.width * 0.5f / cam.aspect);
        if (cam.orthographicSize > maxSize)
            cam.orthographicSize = maxSize;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, viewBounds.xMin + halfW, viewBounds.xMax - halfW);
        pos.y = Mathf.Clamp(pos.y, viewBounds.yMin + halfH, viewBounds.yMax - halfH);
        transform.position = pos;
    }

    /// <summary>Đổi vị trí camera sang toạ độ ô (số thực), kẹp trong [-margin, size-1+margin] rồi đổi lại.</summary>
    private void ClampToMap()
    {
        if (grid == null)
        {
            grid = FindFirstObjectByType<GridController>();
            if (grid == null)
                return;
        }

        Vector2 tile = grid.TileSize;
        if (grid.Width <= 0 || grid.Height <= 0 || tile.x <= 0f || tile.y <= 0f)
            return;

        Vector3 pos = transform.position;
        Vector3 local = grid.transform.InverseTransformPoint(pos);
        float a = local.x / (tile.x * 0.5f);
        float b = local.y / (tile.y * 0.5f);
        float gx = (a + b) * 0.5f;
        float gy = (b - a) * 0.5f;

        float cx = Mathf.Clamp(gx, -mapMargin, grid.Width - 1 + mapMargin);
        float cy = Mathf.Clamp(gy, -mapMargin, grid.Height - 1 + mapMargin);
        if (Mathf.Approximately(cx, gx) && Mathf.Approximately(cy, gy))
            return;

        Vector3 clampedLocal = new Vector3((cx - cy) * tile.x * 0.5f, (cx + cy) * tile.y * 0.5f, local.z);
        Vector3 world = grid.transform.TransformPoint(clampedLocal);
        world.z = pos.z;
        transform.position = world;
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
            // Kẹp ngay trước khi tính lại điểm neo chuột, nếu không phần kéo lố bị cộng dồn -> camera "dính" ở mép
            if (clampToMap)
                ClampToMap();
            ClampToView();
            mouseWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        }

        lastMouseWorld = mouseWorld;
    }
}
