using UnityEngine;

/// <summary>
/// Ánh sáng sáng–tối theo vòng ngày–đêm (DayCycle: 1 tháng = 1 vòng). Chỉ để tạo không khí:
/// một sprite phủ màu bám theo camera, vẽ trên cùng của thế giới nhưng luôn DƯỚI mọi UI (UI overlay vẽ sau camera),
/// không bắt click. Đêm xanh thẫm, bình minh / chạng vạng ngả cam, mùa đông lạnh màu hơn.
/// Tự dựng lúc chạy (PlacementManager.Start).
/// </summary>
public class DayNightOverlay : MonoBehaviour
{
    private const short TopSortingOrder = short.MaxValue;

    // Dự án dùng Linear color space: alpha trộn tuyến tính nên phải đậm hơn cảm giác (0.85 ≈ còn ~45% độ sáng)
    [SerializeField] private Color nightColor = new Color(0.02f, 0.04f, 0.16f, 0.85f);
    [SerializeField] private Color duskColor = new Color(0.95f, 0.42f, 0.18f, 0.35f);
    [SerializeField] private Color winterDayColor = new Color(0.55f, 0.70f, 0.95f, 0.15f);
    [SerializeField, Min(0f)] private float smoothing = 3f;

    private static DayNightOverlay _instance;
    private SpriteRenderer _renderer;
    private Camera _camera;
    private GlobalSystemManager _global;
    private float _nextResolve;
    private Color _current;
    private bool _hasColor;

    /// <summary>Độ tối hiện tại (0 = trưa, 1 = nửa đêm) — cho hiệu ứng khác dùng chung (đèn, khói…).</summary>
    public static float Darkness { get; private set; }

    public static void EnsureInstance()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject("DayNightOverlay");
        _instance = go.AddComponent<DayNightOverlay>();
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Texture2D tex = Texture2D.whiteTexture;
        sr.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        sr.color = Color.clear;

        // Lớp sorting cao nhất của dự án, order tối đa -> phủ lên map / công trình / cư dân
        SortingLayer[] layers = SortingLayer.layers;
        if (layers.Length > 0)
            sr.sortingLayerID = layers[layers.Length - 1].id;
        sr.sortingOrder = TopSortingOrder;
        _instance._renderer = sr;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void LateUpdate()
    {
        if (_renderer == null)
            return;

        if (_camera == null)
            _camera = Camera.main;
        if (_global == null)
        {
            if (Time.unscaledTime < _nextResolve)
                return;
            _nextResolve = Time.unscaledTime + 1f;
            _global = HUDLocator.ResolveGlobalSystem(null);
            if (_global == null)
                return;
        }

        FollowCamera();

        ref GlobalSystemData data = ref _global.GetGlobalDataRef();
        SeasonType season = data.CurrentSeason;
        float dark = DayCycle.GetDarkness(DayCycle.GetHour(in data), season);
        Darkness = dark;

        // Ngày: trong suốt (mùa đông hơi lạnh màu) -> chạng vạng ngả cam -> đêm xanh thẫm
        Color day = season == SeasonType.Winter ? winterDayColor : new Color(duskColor.r, duskColor.g, duskColor.b, 0f);
        Color target = dark < 0.5f
            ? Color.Lerp(day, duskColor, dark * 2f)
            : Color.Lerp(duskColor, nightColor, (dark - 0.5f) * 2f);
        if (season == SeasonType.Winter && dark >= 0.5f)
            target.a = Mathf.Min(0.9f, target.a + 0.04f);   // Đêm đông dài và tối hơn

        if (!_hasColor || smoothing <= 0f)
        {
            _current = target;
            _hasColor = true;
        }
        else
        {
            // unscaled: tải save / đổi tốc độ không làm màu nhảy đột ngột
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            _current = Color.Lerp(_current, target, t);
        }

        _renderer.color = _current;
    }

    /// <summary>Phủ kín khung nhìn camera (theo zoom), nằm ngay trước camera.</summary>
    private void FollowCamera()
    {
        if (_camera == null)
            return;

        Transform cam = _camera.transform;
        float height = _camera.orthographic
            ? _camera.orthographicSize * 2f
            : 2f * 50f * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * _camera.aspect;
        Vector3 pos = cam.position + cam.forward * Mathf.Max(_camera.nearClipPlane + 0.05f, 1f);
        transform.SetPositionAndRotation(pos, cam.rotation);
        transform.localScale = new Vector3(width * 1.1f, height * 1.1f, 1f);
    }
}
