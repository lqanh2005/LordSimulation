using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Đèn cục bộ của công trình (lửa trại, lò…): Point Light 2D sáng dần khi trời tối theo
/// <see cref="DayNightOverlay.Darkness"/>, ban ngày gần như tắt, lập loè nhẹ như lửa.
/// Chỉ sáng khi công trình đã xây xong (<see cref="BuildingView.SetLit"/>); bóng đặt công trình không sáng.
/// Gắn sẵn trong prefab art (ArtAssetSync), thông số chỉnh trên Inspector.
/// </summary>
public class BuildingNightLight : MonoBehaviour
{
    [SerializeField] private Light2D lightSource;
    [SerializeField, Min(0f)] private float nightIntensity = 1.6f;
    [SerializeField, Range(0f, 1f)] private float dayIntensity = 0.15f;   // Ban ngày vẫn hơi ấm quanh lửa
    [SerializeField, Range(0f, 0.5f)] private float flickerAmount = 0.12f;
    [SerializeField, Min(0f)] private float flickerSpeed = 6f;
    [SerializeField, Range(0f, 0.3f)] private float radiusFlicker = 0.06f;

    private bool _lit;
    private float _seed;
    private float _baseOuterRadius;

    private void Awake()
    {
        if (lightSource == null)
            lightSource = GetComponentInChildren<Light2D>(true);
        if (lightSource != null)
            _baseOuterRadius = lightSource.pointLightOuterRadius;
        _seed = Random.value * 100f;
        Apply();
    }

    public void SetLit(bool lit)
    {
        _lit = lit;
        Apply();
    }

    private void Apply()
    {
        if (lightSource != null)
            lightSource.enabled = _lit;
    }

    private void Update()
    {
        if (!_lit || lightSource == null)
            return;

        // Sáng dần từ chạng vạng, đủ sáng khi đã tối hẳn
        float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.8f, DayNightOverlay.Darkness));
        float t = Time.time * flickerSpeed + _seed;
        float noise = Mathf.PerlinNoise(t, _seed) * 2f - 1f;   // -1..1

        lightSource.intensity = Mathf.Lerp(dayIntensity, nightIntensity, dark) * (1f + noise * flickerAmount);
        if (_baseOuterRadius > 0f)
            lightSource.pointLightOuterRadius = _baseOuterRadius * (1f + noise * radiusFlicker);
    }
}
