using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng chuyển scene: màn che tối phủ dần → logo game hiện lên (phóng nhẹ) kèm 3 hình thoi
/// vàng nhấp nháy trong lúc load scene mới (async) → logo mờ trước, màn che tan sau.
/// Tạo bởi Initiate.Fade; chạy bằng thời gian unscaled.
/// </summary>
public class Fader : MonoBehaviour
{
    [HideInInspector]
    public bool start = false;
    [HideInInspector]
    public float fadeDamp = 0.0f;
    [HideInInspector]
    public string fadeScene;
    [HideInInspector]
    public float alpha = 0.0f;
    [HideInInspector]
    public Color fadeColor;
    [HideInInspector]
    public bool isFadeIn = false;

    // Màu màn che theo theme UI (khi gọi với màu đen mặc định)
    private static readonly Color ThemeCurtain = new Color32(0x14, 0x13, 0x12, 0xFF);

    const float LoadTimeoutSeconds = 10f;
    const float MinHoldSeconds = 0.25f;   // giữ logo tối thiểu để không nháy khi scene load quá nhanh

    CanvasGroup myCanvas;
    Image bg;
    RectTransform logo;
    CanvasGroup logoGroup;
    readonly Image[] dots = new Image[3];
    bool startedLoading = false;

    public void InitiateFader()
    {
        DontDestroyOnLoad(gameObject);
        myCanvas = GetComponent<CanvasGroup>();
        if (myCanvas == null)
            myCanvas = gameObject.AddComponent<CanvasGroup>();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        bg = GetComponent<Image>();
        if (bg == null)
            bg = gameObject.AddComponent<Image>();
        bg.color = fadeColor == Color.black ? ThemeCurtain : fadeColor;

        BuildDecor();

        alpha = 0f;
        isFadeIn = false;
        startedLoading = false;
        myCanvas.alpha = 0f;
        myCanvas.blocksRaycasts = true;
        StartCoroutine(FadeIt());
    }

    /// <summary>Logo + 3 hình thoi lấy từ UISkin; thiếu skin thì chỉ còn màn che như cũ.</summary>
    void BuildDecor()
    {
        UISkin skin = UISkin.Instance;
        if (skin == null || skin.logo == null)
            return;

        GameObject logoGo = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        logo = (RectTransform)logoGo.transform;
        logo.SetParent(transform, false);
        logo.anchorMin = logo.anchorMax = logo.pivot = new Vector2(0.5f, 0.5f);
        logo.anchoredPosition = new Vector2(0f, 40f);
        logo.sizeDelta = new Vector2(560f, 351f);
        Image logoImage = logoGo.GetComponent<Image>();
        logoImage.sprite = skin.logo;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        logoGroup = logoGo.GetComponent<CanvasGroup>();
        logoGroup.alpha = 0f;

        for (int i = 0; i < dots.Length; i++)
        {
            GameObject dot = new GameObject("Dot_" + i, typeof(RectTransform), typeof(Image));
            RectTransform rt = (RectTransform)dot.transform;
            rt.SetParent(logo, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((i - 1) * 28f, -46f);
            rt.sizeDelta = new Vector2(12f, 12f);
            dots[i] = dot.GetComponent<Image>();
            dots[i].sprite = skin.rhombus;
            dots[i].raycastTarget = false;
        }
    }

    IEnumerator FadeIt()
    {
        while (!start)
            yield return null;

        float duration = fadeDamp > 0f ? 1f / fadeDamp : 0.5f;

        // 1. Màn che phủ dần, logo hiện ở nửa sau
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            alpha = EaseInOut(k);
            myCanvas.alpha = alpha;
            SetLogo(Mathf.InverseLerp(0.4f, 1f, k), Mathf.Lerp(0.94f, 1f, EaseOut(Mathf.InverseLerp(0.4f, 1f, k))));
            AnimateDots();
            yield return null;
        }
        alpha = 1f;
        myCanvas.alpha = 1f;
        SetLogo(1f, 1f);

        // 2. Load scene mới (async để hình thoi vẫn nhấp nháy)
        if (!startedLoading)
        {
            startedLoading = true;
            if (Application.CanStreamedLevelBeLoaded(fadeScene))
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(fadeScene);
                float waited = 0f;
                while ((op != null && !op.isDone || waited < MinHoldSeconds) && waited < LoadTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    AnimateDots();
                    yield return null;
                }
                if (waited >= LoadTimeoutSeconds)
                    Debug.LogWarning($"[Fader] Chờ load scene '{fadeScene}' quá {LoadTimeoutSeconds}s, tự mở màn.");
            }
            else
            {
                // Scene sai tên / chưa có trong Build Settings: mở màn lại để không kẹt Initiate.IsFading
                Debug.LogError($"[Fader] Không thể load scene '{fadeScene}'.");
            }
        }
        isFadeIn = true;

        // 3. Logo mờ + phóng nhẹ trước, màn che tan sau
        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float logoK = Mathf.Clamp01(k / 0.45f);
            SetLogo(1f - logoK, Mathf.Lerp(1f, 1.04f, EaseOut(logoK)));
            alpha = 1f - EaseInOut(Mathf.InverseLerp(0.2f, 1f, k));
            myCanvas.alpha = alpha;
            AnimateDots();
            yield return null;
        }

        start = false;
        Initiate.DoneFading();
        Destroy(gameObject);
    }

    void SetLogo(float a, float scale)
    {
        if (logo == null)
            return;
        logoGroup.alpha = Mathf.Clamp01(a);
        logo.localScale = new Vector3(scale, scale, 1f);
    }

    // 3 hình thoi sáng lần lượt như "đang tải"
    void AnimateDots()
    {
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
                continue;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f - i * 0.9f);
            dots[i].color = new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 1f, wave));
            float s = Mathf.Lerp(0.85f, 1.15f, wave);
            dots[i].rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    static float EaseInOut(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    static float EaseOut(float x) => 1f - Mathf.Pow(1f - x, 3f);
}
