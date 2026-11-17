using System.Collections;
using UnityEngine;

/// <summary>
/// Hiệu ứng hiện / ẩn popup: lớp phủ mờ dần + cửa sổ phóng nhẹ từ 92% (hơi nảy) khi hiện,
/// thu về 95% + mờ khi ẩn. Chạy bằng thời gian unscaled nên vẫn chạy khi game Pause (timeScale = 0).
///
/// Gắn trên GameObject gốc của popup (lớp phủ). Hiện: tự chạy khi OnEnable, hoặc gọi UIPopupAnim.Show(go).
/// Ẩn: gọi UIPopupAnim.Hide(go) thay cho go.SetActive(false) để có hiệu ứng.
/// </summary>
[DisallowMultipleComponent]
public class UIPopupAnim : MonoBehaviour
{
    [SerializeField] private RectTransform window;          // null = chỉ mờ dần (vd. màn Cổng toàn màn hình)
    [SerializeField, Min(0.01f)] private float showSeconds = 0.22f;
    [SerializeField, Min(0.01f)] private float hideSeconds = 0.14f;
    [SerializeField, Range(0.5f, 1f)] private float startScale = 0.92f;

    private CanvasGroup _group;
    private CanvasGroup _windowGroup;
    private Coroutine _routine;

    /// <summary>Đang chạy hiệu ứng ẩn (coi như đã đóng: không nhận click, IsVisible = false).</summary>
    public bool IsClosing { get; private set; }

    /// <summary>Gắn hiệu ứng vào popup dựng lúc chạy.</summary>
    public static UIPopupAnim Attach(GameObject root, RectTransform window)
    {
        UIPopupAnim anim = root.GetComponent<UIPopupAnim>();
        if (anim == null)
            anim = root.AddComponent<UIPopupAnim>();
        anim.window = window;
        return anim;
    }

    /// <summary>Bật popup kèm hiệu ứng (kể cả khi nó đang chạy dở hiệu ứng ẩn).</summary>
    public static void Show(GameObject root)
    {
        if (root == null)
            return;
        UIPopupAnim anim = root.GetComponent<UIPopupAnim>();
        if (root.activeSelf && anim != null)
            anim.PlayShow();
        else
            root.SetActive(true);
    }

    /// <summary>Tắt popup kèm hiệu ứng; không có UIPopupAnim thì tắt ngay.</summary>
    public static void Hide(GameObject root)
    {
        if (root == null || !root.activeSelf)
            return;
        UIPopupAnim anim = root.GetComponent<UIPopupAnim>();
        if (anim != null && anim.isActiveAndEnabled)
            anim.PlayHide();
        else
            root.SetActive(false);
    }

    /// <summary>Đang hiện và không trong lúc đóng.</summary>
    public static bool IsVisible(GameObject root)
    {
        if (root == null || !root.activeInHierarchy)
            return false;
        UIPopupAnim anim = root.GetComponent<UIPopupAnim>();
        return anim == null || !anim.IsClosing;
    }

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        PlayShow();
    }

    /// <summary>Popup đã tắt hẳn (sau hiệu ứng ẩn hoặc bị SetActive(false)) — dùng để giải phóng asset.</summary>
    public event System.Action Hidden;

    private void OnDisable()
    {
        Hidden?.Invoke();
        // Bị tắt thẳng bằng SetActive(false): trả về trạng thái chuẩn cho lần mở sau
        _routine = null;
        IsClosing = false;
        ApplyState(1f, 1f);
    }

    public void PlayShow()
    {
        IsClosing = false;
        Restart(Animate(true));
    }

    public void PlayHide()
    {
        if (IsClosing)
            return;
        IsClosing = true;
        Restart(Animate(false));
    }

    private void Restart(IEnumerator routine)
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    private IEnumerator Animate(bool show)
    {
        if (_group == null)
            Awake();

        float duration = show ? showSeconds : hideSeconds;
        float fromAlpha = show ? 0f : _group.alpha;
        float toAlpha = show ? 1f : 0f;
        float fromScale = show ? startScale : (window != null ? window.localScale.x : 1f);
        float toScale = show ? 1f : 0.95f;

        _group.interactable = show;
        _group.blocksRaycasts = show;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, show ? EaseOutCubic(k) : EaseInCubic(k));
            float scale = Mathf.LerpUnclamped(fromScale, toScale, show ? EaseOutBack(k) : EaseInCubic(k));
            ApplyState(alpha, scale);
            yield return null;
        }

        ApplyState(toAlpha, toScale);
        _routine = null;

        if (!show)
        {
            IsClosing = false;
            gameObject.SetActive(false);
        }
    }

    private void ApplyState(float alpha, float scale)
    {
        if (_group != null)
        {
            _group.alpha = alpha;
            if (!IsClosing)
            {
                _group.interactable = true;
                _group.blocksRaycasts = true;
            }
        }
        if (window != null)
            window.localScale = new Vector3(scale, scale, 1f);
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    private static float EaseInCubic(float x) => x * x * x;

    // Nảy nhẹ (~4%) ở cuối khi hiện
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
