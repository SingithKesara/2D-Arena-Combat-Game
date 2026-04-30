using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public static class MenuRecoveryBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Arena Combat/8 - Recover Main Menu Scene")]
    public static void BuildMainMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.16f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        CreateEventSystemIfMissing();

        GameObject canvasGO = new GameObject("MainMenu_Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        CreateFullscreenImage(canvasGO.transform, "Background", new Color(0.10f, 0.14f, 0.22f, 1f));

        TextMeshProUGUI title = CreateTMP(
            canvasGO.transform,
            "Title",
            "2D ARENA COMBAT",
            72f,
            new Color(1f, 0.88f, 0.25f, 1f),
            TextAlignmentOptions.Center
        );
        SetCentered(title.rectTransform, new Vector2(0f, 300f), new Vector2(1100f, 120f));

        TextMeshProUGUI subtitle = CreateTMP(
            canvasGO.transform,
            "Subtitle",
            "Final Year Project Prototype",
            28f,
            new Color(1f, 1f, 1f, 0.8f),
            TextAlignmentOptions.Center
        );
        SetCentered(subtitle.rectTransform, new Vector2(0f, 230f), new Vector2(900f, 50f));

        GameObject menuPanel = CreatePanel(canvasGO.transform, "MenuPanel", new Color(0f, 0f, 0f, 0.35f));
        SetCentered(menuPanel.GetComponent<RectTransform>(), new Vector2(0f, -20f), new Vector2(500f, 320f));

        Button playButton = CreateButton(menuPanel.transform, "PlayButton", "PLAY", new Vector2(0f, 90f));
        Button controlsButton = CreateButton(menuPanel.transform, "ControlsButton", "CONTROLS", new Vector2(0f, 0f));
        Button quitButton = CreateButton(menuPanel.transform, "QuitButton", "QUIT", new Vector2(0f, -90f));

        GameObject controlsPanel = CreatePanel(canvasGO.transform, "ControlsPanel", new Color(0f, 0f, 0f, 0.55f));
        SetCentered(controlsPanel.GetComponent<RectTransform>(), new Vector2(0f, -20f), new Vector2(950f, 500f));
        controlsPanel.SetActive(false);

        TextMeshProUGUI controlsTitle = CreateTMP(
            controlsPanel.transform,
            "ControlsTitle",
            "CONTROLS",
            42f,
            Color.yellow,
            TextAlignmentOptions.Center
        );
        SetCentered(controlsTitle.rectTransform, new Vector2(0f, 180f), new Vector2(500f, 60f));

        TextMeshProUGUI p1Text = CreateTMP(
            controlsPanel.transform,
            "P1Controls",
            "PLAYER 1\n\nA / D  - Move\nW or Space - Jump\nS - Fast Fall\nJ - Light Attack\nK - Heavy Attack",
            28f,
            new Color(0.45f, 0.85f, 1f, 1f),
            TextAlignmentOptions.Left
        );
        SetCentered(p1Text.rectTransform, new Vector2(-220f, 10f), new Vector2(360f, 260f));

        TextMeshProUGUI p2Text = CreateTMP(
            controlsPanel.transform,
            "P2Controls",
            "PLAYER 2\n\nNum4 / Num6 - Move\nNum8 - Jump\nNum5 - Fast Fall\nNum0 - Light Attack\nNumEnter - Heavy Attack",
            28f,
            new Color(1f, 0.45f, 0.45f, 1f),
            TextAlignmentOptions.Left
        );
        SetCentered(p2Text.rectTransform, new Vector2(220f, 10f), new Vector2(360f, 260f));

        Button backButton = CreateButton(controlsPanel.transform, "BackButton", "BACK", new Vector2(0f, -180f));

        GameObject fadeGO = CreateFullscreenImage(canvasGO.transform, "FadeOverlay", Color.black);
        CanvasGroup fadeGroup = fadeGO.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        GameObject mgrGO = new GameObject("MainMenuManager");
        MainMenuManager mgr = mgrGO.AddComponent<MainMenuManager>();
        mgr.menuPanel = menuPanel;
        mgr.controlsPanel = controlsPanel;
        mgr.playButton = playButton;
        mgr.controlsButton = controlsButton;
        mgr.backButton = backButton;
        mgr.quitButton = quitButton;
        mgr.fadeGroup = fadeGroup;
        mgr.gameplaySceneName = "GameplayScene";

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Main menu recovered successfully.");
    }

    private static void CreateEventSystemIfMissing()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static GameObject CreateFullscreenImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.18f, 0.32f, 0.95f);

        Button btn = go.AddComponent<Button>();

        RectTransform rt = go.GetComponent<RectTransform>();
        SetCentered(rt, pos, new Vector2(280f, 64f));

        TextMeshProUGUI txt = CreateTMP(
            go.transform,
            "Label",
            label,
            28f,
            Color.white,
            TextAlignmentOptions.Center
        );
        Stretch(txt.rectTransform);

        return btn;
    }

    private static TextMeshProUGUI CreateTMP(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;

        return tmp;
    }

    private static void SetCentered(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}