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
    float lastTime = 0;
    bool startedLoading = false;
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
        if(transform.GetComponent<CanvasGroup>() == null)
        {
            myCanvas = gameObject.AddComponent<CanvasGroup>();
        }
        if (transform.GetComponentInChildren<Image>())
        {
            bg = transform.GetComponent<Image>();
            bg.color = fadeColor;
        }
        if(myCanvas && bg)
        {
            myCanvas.alpha = 0.0f;
            StartCoroutine(FadeIt());
        }
        else Debug.LogWarning("Fader: CanvasGroup or Image component is missing.");
    }
    IEnumerator FadeIt()
    {
        while (!start)
        {
            yield return null;
        }
        lastTime = Time.time;
        float coDelta = lastTime;
        bool hasFadedIn = false;
        while (!hasFadedIn)
        {
            coDelta = Time.deltaTime - lastTime;
            if (!hasFadedIn)
            {
                alpha = newAlpha(coDelta, 1, alpha);
                if (alpha == 1 && !startedLoading)
                {
                    startedLoading = true;
                    SceneManager.LoadScene(fadeScene);
                }
            }
            else
            {
                alpha = newAlpha(coDelta, 0, alpha);
                if (alpha == 0) hasFadedIn = true;
            }
            lastTime = Time.time;
            myCanvas.alpha = alpha;
            yield return null;
        }
        Initiate.DoneFading();
        Destroy(gameObject);
        yield return null;
    }
    float newAlpha(float delta, int to, float currentAlpha)
    {
        switch (to)
        {
            case 1:
                currentAlpha += delta * fadeDamp;
                if (currentAlpha > 1) currentAlpha = 1;
                break;
            case 0:
                currentAlpha -= delta * fadeDamp;
                if (currentAlpha < 0) currentAlpha = 0;
                break;
        }
        return currentAlpha;
    }
    void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(FadeIt());
        //We can now fade in
        isFadeIn = true;
    }
}
