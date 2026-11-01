using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    CanvasGroup myCanvas;
    Image bg;
    bool startedLoading = false;

    const float LoadTimeoutSeconds = 10f;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnLevelFinishedLoading;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinishedLoading;
    }

    public void InitiateFader()
    {
        DontDestroyOnLoad(gameObject);
        myCanvas = GetComponent<CanvasGroup>();
        if (myCanvas == null)
            myCanvas = gameObject.AddComponent<CanvasGroup>();

        bg = GetComponent<Image>();
        if (bg == null)
            bg = gameObject.AddComponent<Image>();
        bg.color = fadeColor;

        alpha = 0f;
        isFadeIn = false;
        startedLoading = false;
        myCanvas.alpha = 0f;
        myCanvas.blocksRaycasts = true;
        StartCoroutine(FadeIt());
    }

    IEnumerator FadeIt()
    {
        while (!start)
            yield return null;

        while (alpha < 1f)
        {
            alpha = Mathf.MoveTowards(alpha, 1f, Time.unscaledDeltaTime * fadeDamp);
            myCanvas.alpha = alpha;
            yield return null;
        }

        if (!startedLoading)
        {
            startedLoading = true;
            if (Application.CanStreamedLevelBeLoaded(fadeScene))
            {
                SceneManager.LoadScene(fadeScene);
            }
            else
            {
                // Scene sai tên / chưa có trong Build Settings: fade ra lại để không kẹt Initiate.IsFading
                Debug.LogError($"[Fader] Không thể load scene '{fadeScene}'.");
                isFadeIn = true;
            }
        }

        float waited = 0f;
        while (!isFadeIn && waited < LoadTimeoutSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!isFadeIn)
            Debug.LogWarning($"[Fader] Chờ load scene '{fadeScene}' quá {LoadTimeoutSeconds}s, tự fade ra.");

        while (alpha > 0f)
        {
            alpha = Mathf.MoveTowards(alpha, 0f, Time.unscaledDeltaTime * fadeDamp);
            myCanvas.alpha = alpha;
            yield return null;
        }

        start = false;
        Initiate.DoneFading();
        Destroy(gameObject);
    }

    void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        if (startedLoading)
            isFadeIn = true;
    }
}
