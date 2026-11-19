using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ánh sáng theo vòng ngày–đêm (DayCycle: 1 tháng = 1 vòng), dùng Global Light 2D của URP 2D Renderer.
/// Ánh sáng NHÂN vào sprite (Sprite-Lit-Default) nên giữ được tương phản: trưa trắng, hoàng hôn vàng ấm,
/// đêm xanh tối, mùa đông ban ngày hơi lạnh màu. UI (Canvas overlay) không bị ảnh hưởng.
/// Sprite cần luôn rõ (icon trạng thái, bóng đặt công trình) dùng <see cref="UnlitMaterial"/>.
/// Tự dựng lúc chạy (PlacementManager.Start). Tên lớp giữ nguyên để không đổi chỗ gọi.
/// </summary>
public class DayNightOverlay : MonoBehaviour
{
    // Màu ánh sáng (nhân vào sprite) + cường độ. Chỉnh trên Inspector lúc Play để thử.
    [SerializeField] private Color dayColor = Color.white;
    [SerializeField, Min(0f)] private float dayIntensity = 1f;
    [SerializeField] private Color winterDayColor = new Color(0.86f, 0.92f, 1f);
    [SerializeField, Min(0f)] private float winterDayIntensity = 0.95f;
    [SerializeField] private Color duskColor = new Color(1f, 0.8f, 0.58f);
    [SerializeField, Min(0f)] private float duskIntensity = 0.92f;
    [SerializeField] private Color nightColor = new Color(0.45f, 0.53f, 0.85f);
    [SerializeField, Min(0f)] private float nightIntensity = 0.55f;
    [SerializeField, Min(0f)] private float winterNightIntensity = 0.48f;   // Đêm đông tối hơn
    [SerializeField, Min(0f)] private float smoothing = 3f;

    private const string UnlitShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    private static DayNightOverlay _instance;
    private static Material _unlitMaterial;
    private Light2D _light;
    private GlobalSystemManager _global;
    private float _nextResolve;
    private Color _currentColor;
    private float _currentIntensity;
    private bool _hasColor;

    /// <summary>Độ tối hiện tại (0 = trưa, 1 = nửa đêm) — cho hiệu ứng khác dùng chung (đèn, khói…).</summary>
    public static float Darkness { get; private set; }

    /// <summary>
    /// Material sprite không nhận ánh sáng — cho thứ phải luôn dễ thấy kể cả ban đêm
    /// (icon trạng thái cư dân, bóng đặt công trình). Null nếu shader không có trong bản build.
    /// </summary>
    public static Material UnlitMaterial
    {
        get
        {
            if (_unlitMaterial == null)
            {
                Shader shader = Shader.Find(UnlitShaderName);
                if (shader != null)
                    _unlitMaterial = new Material(shader) { name = "Sprite-Unlit (runtime)" };
            }
            return _unlitMaterial;
        }
    }

    /// <summary>Gán material không nhận ánh sáng cho sprite (bỏ qua nếu thiếu shader).</summary>
    public static void MakeUnlit(SpriteRenderer renderer)
    {
        Material unlit = UnlitMaterial;
        if (renderer != null && unlit != null)
            renderer.sharedMaterial = unlit;
    }

    public static void EnsureInstance()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject("DayNightLight");
        _instance = go.AddComponent<DayNightOverlay>();

        Light2D light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.blendStyleIndex = 0;   // Multiply
        foreach (SortingLayer layer in SortingLayer.layers)
            light.AddTargetSortingLayer(layer.id);
        light.color = Color.white;
        light.intensity = 1f;
        _instance._light = light;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void LateUpdate()
    {
        if (_light == null)
            return;

        if (_global == null)
        {
            if (Time.unscaledTime < _nextResolve)
                return;
            _nextResolve = Time.unscaledTime + 1f;
            _global = HUDLocator.ResolveGlobalSystem(null);
            if (_global == null)
                return;
        }

        ref GlobalSystemData data = ref _global.GetGlobalDataRef();
        SeasonType season = data.CurrentSeason;
        bool winter = season == SeasonType.Winter;
        float dark = DayCycle.GetDarkness(DayCycle.GetHour(in data), season);
        Darkness = dark;

        // Ngày -> hoàng hôn (đúng lúc mặt trời lặn/mọc) -> đêm
        Color day = winter ? winterDayColor : dayColor;
        float dayI = winter ? winterDayIntensity : dayIntensity;
        float nightI = winter ? winterNightIntensity : nightIntensity;
        Color targetColor;
        float targetIntensity;
        if (dark < 0.5f)
        {
            float t = dark * 2f;
            targetColor = Color.Lerp(day, duskColor, t);
            targetIntensity = Mathf.Lerp(dayI, duskIntensity, t);
        }
        else
        {
            float t = (dark - 0.5f) * 2f;
            targetColor = Color.Lerp(duskColor, nightColor, t);
            targetIntensity = Mathf.Lerp(duskIntensity, nightI, t);
        }

        if (!_hasColor || smoothing <= 0f)
        {
            _currentColor = targetColor;
            _currentIntensity = targetIntensity;
            _hasColor = true;
        }
        else
        {
            // unscaled: tải save / đổi tốc độ không làm màu nhảy đột ngột
            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            _currentColor = Color.Lerp(_currentColor, targetColor, k);
            _currentIntensity = Mathf.Lerp(_currentIntensity, targetIntensity, k);
        }

        _light.color = _currentColor;
        _light.intensity = _currentIntensity;
    }
}
