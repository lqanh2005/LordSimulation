using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Skin cho HomeScene (MAIN MENU mockup) và LoadingScene (Loading_Mockup).</summary>
public static partial class UISkinApplier
{
    private const string HomeScenePath = "Assets/Scenes/HomeScene.unity";
    private const string LoadingScenePath = "Assets/Scenes/LoadingScene.unity";

    // ==========================================
    // HOME — menu dọc bên trái
    // ==========================================
    [MenuItem("Tools/LordSimulation/UI Skin/Apply Home Scene")]
    public static void ApplyHomeScene()
    {
        if (!Begin(HomeScenePath, out Scene scene))
            return;

        Transform canvas = FindRoot(scene, "HomeMenu");
        if (canvas == null)
        {
            Debug.LogError("[UISkin] Không thấy HomeMenu trong HomeScene.");
            return;
        }

        FullScreenBackground(R(canvas, "Background"), UISkinSetup.S("Loading/Back.png"));
        SetActive(canvas, "Txt_Title", false);

        RectTransform panel = R(canvas, "Panel");
        PlaceTopLeft(panel, 132f, 0f, 575f, 871f);
        Image panelImage = Img(panel);
        panelImage.sprite = UISkinSetup.S("Home/Bảng menu.png");
        panelImage.type = Image.Type.Simple;
        panelImage.color = Color.white;

        HomeController home = Object.FindFirstObjectByType<HomeController>();
        RectTransform btnNew = R(panel, "Btn_NewGame");
        RectTransform btnContinue = R(panel, "Btn_Continue");
        RectTransform btnQuit = R(panel, "Btn_Quit");
        RectTransform btnLoad = CloneButton(btnNew, "Btn_Load");
        RectTransform btnSettings = CloneButton(btnNew, "Btn_Settings");

        (RectTransform btn, string label)[] order =
        {
            (btnContinue, "TIẾP TỤC"),
            (btnNew, "GAME MỚI"),
            (btnLoad, "TẢI GAME"),
            (btnSettings, "CÀI ĐẶT"),
            (btnQuit, "THOÁT")
        };
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i].btn == null) continue;
            PlaceTopLeft(order[i].btn, 117f, 349f + i * 85f, 340f, 69f);
            order[i].btn.SetSiblingIndex(i);
            OrnateButton(order[i].btn, order[i].label, 20f);
        }

        if (home != null)
        {
            SetRef(home, "btnLoad", btnLoad.GetComponent<Button>());
            SetRef(home, "btnSettings", btnSettings.GetComponent<Button>());
        }

        End(scene);
    }

    private static RectTransform CloneButton(RectTransform source, string name)
    {
        if (source == null) return null;
        Transform existing = source.parent.Find(name);
        if (existing != null)
            return (RectTransform)existing;

        GameObject clone = Object.Instantiate(source.gameObject, source.parent);
        clone.name = name;
        Undo.RegisterCreatedObjectUndo(clone, "Apply UI Skin");
        Button button = clone.GetComponent<Button>();
        if (button != null)
            button.onClick = new Button.ButtonClickedEvent();
        return (RectTransform)clone.transform;
    }

    private static void FullScreenBackground(RectTransform bg, Sprite sprite)
    {
        if (bg == null) return;
        Stretch(bg);
        Image image = Img(bg);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
    }

    // ==========================================
    // LOADING — logo giữa + thanh tiến trình
    // ==========================================
    [MenuItem("Tools/LordSimulation/UI Skin/Apply Loading Scene")]
    public static void ApplyLoadingScene()
    {
        if (!Begin(LoadingScenePath, out Scene scene))
            return;

        Transform canvas = FindRoot(scene, "LoadingScreen");
        if (canvas == null)
        {
            Debug.LogError("[UISkin] Không thấy LoadingScreen trong LoadingScene.");
            return;
        }

        FullScreenBackground(R(canvas, "Background"), UISkinSetup.S("Loading/Back.png"));
        SetActive(canvas, "Txt_Title", false);

        RectTransform logo = Child(canvas, "Logo");
        Place(logo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(688f, 431f));
        Image logoImage = Img(logo);
        logoImage.sprite = UISkinSetup.S("Loading/Avt.png");
        logoImage.type = Image.Type.Simple;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;

        TMP_Text status = T(canvas, "Txt_Loading");
        Place(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -238f), new Vector2(360f, 40f));
        StyleTmp(status, 20f, UISkin.Tan, TextAlignmentOptions.Center, true);
        status.textWrappingMode = TextWrappingModes.NoWrap;
        status.fontStyle = FontStyles.Bold;
        status.text = "Đang khởi động...";

        RectTransform ornament = Child(canvas, "Ornament_Status");
        Place(ornament, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -238f), new Vector2(660f, 12f));
        Ornament(ornament, "Lines", 6f, 370f, 112f);
        ((RectTransform)ornament.Find("Lines")).anchorMin = ((RectTransform)ornament.Find("Lines")).anchorMax = new Vector2(0.5f, 1f);

        RectTransform bar = Child(canvas, "ProgressBar");
        Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -306f), new Vector2(904f, 48f));
        UISkin.SetSliced(Img(bar), UISkinSetup.S("Loading/Slide_Bar.png"), Color.white);
        Img(bar).raycastTarget = false;

        RectTransform fillRt = Child(bar, "Fill");
        Place(fillRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(836f, 18f));
        Image fill = Img(fillRt);
        fill.sprite = UISkinSetup.S("Loading/Slide_Value.png");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0.68f;
        fill.color = Color.white;
        fill.raycastTarget = false;

        TMP_Text percent = Tmp(canvas, "Txt_Percent", "68%");
        Place(percent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -366f), new Vector2(200f, 34f));
        StyleTmp(percent, 21f, UISkin.Cream, TextAlignmentOptions.Center, true);

        LoadingScreenView view = canvas.GetComponent<LoadingScreenView>();
        if (view == null)
            view = Undo.AddComponent<LoadingScreenView>(canvas.gameObject);
        SetRef(view, "fill", fill);
        SetRef(view, "txtPercent", percent);
        SetRef(view, "txtStatus", status);

        End(scene);
    }
}
