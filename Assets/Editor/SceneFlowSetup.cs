using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneFlowSetup
{
    private const string LoadingScenePath = "Assets/Scenes/LoadingScene.unity";
    private const string HomeScenePath = "Assets/Scenes/HomeScene.unity";
    private const string GameplayScenePath = "Assets/Scenes/GamePlayScene.unity";

    private const string HomeMenuName = "HomeMenu";
    private const string LoadingScreenName = "LoadingScreen";

    private static readonly Color BackgroundColor = new Color32(0x2E, 0x3A, 0x46, 0xFF);
    private static readonly Color PanelColor = new Color32(0xF4, 0xF5, 0xF6, 0xFF);
    private static readonly Color ButtonColor = new Color32(0xDD, 0xE1, 0xE5, 0xFF);
    private static readonly Color ButtonTextColor = new Color32(0x2E, 0x3A, 0x46, 0xFF);
    private static readonly Color SubTextColor = new Color32(0x9A, 0xA4, 0xAD, 0xFF);

    private static Sprite panelSprite;

    [MenuItem("Tools/LordSimulation/Setup Scene Flow")]
    public static void SetupAll()
    {
        panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        SetupBuildSettings();

        if (!SetupHomeScene())
            return;

        if (!SetupLoadingScene())
            return;

        Debug.Log("[SceneFlowSetup] Hoàn tất. Mở LoadingScene và bấm Play để chạy luồng Loading → Home → GamePlay.");
    }

    [MenuItem("Tools/LordSimulation/Setup Build Settings")]
    public static void SetupBuildSettings()
    {
        string[] ordered = { LoadingScenePath, HomeScenePath, GameplayScenePath };
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();

        foreach (string path in ordered)
        {
            if (File.Exists(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (!File.Exists(existing.path) || scenes.Exists(s => s.path == existing.path))
                continue;
            scenes.Add(existing);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static bool SetupHomeScene()
    {
        if (!TryOpenScene(HomeScenePath, out Scene scene))
            return false;

        HomeController controller = FindInScene<HomeController>(scene);
        if (controller == null)
        {
            GameObject go = new GameObject("HomeController");
            SceneManager.MoveGameObjectToScene(go, scene);
            controller = go.AddComponent<HomeController>();
        }

        GameObject old = FindRoot(scene, HomeMenuName);
        if (old != null)
            Object.DestroyImmediate(old);

        GameObject canvas = CreateCanvas(HomeMenuName);
        SceneManager.MoveGameObjectToScene(canvas, scene);
        Transform root = canvas.transform;

        RectTransform background = CreateRect("Background", root);
        Stretch(background);
        AddImage(background, BackgroundColor, null);

        TextMeshProUGUI title = CreateText("Txt_Title", root, "LORD SIMULATION", 84f, Color.white);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 120f));

        RectTransform panel = CreateRect("Panel", root);
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(440f, 340f));
        AddImage(panel, PanelColor, panelSprite);

        Button btnNewGame = CreateMenuButton("Btn_NewGame", panel, "CHƠI MỚI", 0);
        Button btnContinue = CreateMenuButton("Btn_Continue", panel, "TIẾP TỤC", 1);
        Button btnQuit = CreateMenuButton("Btn_Quit", panel, "THOÁT", 2);

        SetRef(controller, "btnNewGame", btnNewGame);
        SetRef(controller, "btnContinue", btnContinue);
        SetRef(controller, "btnQuit", btnQuit);

        EnsureEventSystem(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        return EditorSceneManager.SaveScene(scene);
    }

    private static bool SetupLoadingScene()
    {
        if (!TryOpenScene(LoadingScenePath, out Scene scene))
            return false;

        if (FindInScene<GameController>(scene) == null)
            Debug.LogWarning("[SceneFlowSetup] LoadingScene chưa có GameController.");

        GameObject old = FindRoot(scene, LoadingScreenName);
        if (old != null)
            Object.DestroyImmediate(old);

        GameObject canvas = CreateCanvas(LoadingScreenName);
        SceneManager.MoveGameObjectToScene(canvas, scene);
        Transform root = canvas.transform;

        RectTransform background = CreateRect("Background", root);
        Stretch(background);
        AddImage(background, BackgroundColor, null);

        TextMeshProUGUI title = CreateText("Txt_Title", root, "LORD SIMULATION", 72f, Color.white);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1200f, 100f));

        TextMeshProUGUI loading = CreateText("Txt_Loading", root, "Đang tải...", 30f, SubTextColor);
        Place(loading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(600f, 50f));

        EditorSceneManager.MarkSceneDirty(scene);
        return EditorSceneManager.SaveScene(scene);
    }

    private static Button CreateMenuButton(string name, Transform parent, string label, int index)
    {
        const float width = 340f;
        const float height = 72f;
        const float top = 40f;
        const float stride = 92f;

        RectTransform rt = CreateRect(name, parent);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top - index * stride);
        rt.sizeDelta = new Vector2(width, height);

        Image image = AddImage(rt, ButtonColor, panelSprite);
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Label", rt, label, 26f, ButtonTextColor);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);
        return button;
    }

    private static bool TryOpenScene(string path, out Scene scene)
    {
        scene = SceneManager.GetSceneByPath(path);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        if (!scene.IsValid())
            return false;

        SceneManager.SetActiveScene(scene);
        return true;
    }

    private static GameObject CreateCanvas(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return root;
    }

    private static void EnsureEventSystem(Scene scene)
    {
        if (FindInScene<EventSystem>(scene) != null)
            return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }
        return null;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image AddImage(RectTransform rt, Color color, Sprite sprite)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
