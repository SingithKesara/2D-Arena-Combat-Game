using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the LobbyMenu scene with the same minimalist dark theme as the existing MainMenu:
/// dark-navy camera background, semi-transparent black panel, solid dark-blue buttons, white text.
/// Run via: Tools / Arena Combat / Build Lobby Menu Scene.
///
/// SAFE: only writes to Assets/Scenes/LobbyMenu.unity. Does NOT modify MainMenu.unity or Gameplayscene.unity.
/// </summary>
public static class LobbyMenuBuilder
{
    private const string ScenePath = "Assets/Scenes/LobbyMenu.unity";

    private static readonly Color CameraBg = new Color(0.08f, 0.10f, 0.16f, 1f);
    private static readonly Color PanelBg = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color ButtonBg = new Color(0.14f, 0.18f, 0.32f, 0.95f);
    private static readonly Color ButtonHover = new Color(0.22f, 0.30f, 0.50f, 1f);
    private static readonly Color ButtonPressed = new Color(0.10f, 0.13f, 0.22f, 1f);
    private static readonly Color InputBg = new Color(0.12f, 0.16f, 0.28f, 0.85f);

    [MenuItem("Tools/Arena Combat/Build Lobby Menu Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        SetupCamera();
        EnsureEventSystem();

        GameObject canvasGO = CreateCanvas();

        TextMeshProUGUI title = CreateLabel(canvasGO.transform, "Title", "MULTIPLAYER LOBBY", 72, FontStyles.Bold);
        SetMiddleCenter(title.rectTransform, 0f, 300f);
        title.rectTransform.sizeDelta = new Vector2(1100f, 110f);
        title.color = Color.white;

        TextMeshProUGUI subtitle = CreateLabel(canvasGO.transform, "Subtitle", "Choose how to play", 28, FontStyles.Normal);
        SetMiddleCenter(subtitle.rectTransform, 0f, 230f);
        subtitle.rectTransform.sizeDelta = new Vector2(900f, 50f);
        subtitle.color = new Color(0.85f, 0.86f, 0.92f, 1f);

        GameObject panel = CreatePanel(canvasGO.transform, "MenuPanel", new Vector2(560f, 600f));
        SetMiddleCenter(panel.GetComponent<RectTransform>(), 0f, -80f);

        Button localBtn = CreateSolidButton(panel.transform, "LocalPlayButton", "LOCAL 1V1", new Vector2(0f, 220f));
        Button hostBtn = CreateSolidButton(panel.transform, "HostButton", "HOST GAME", new Vector2(0f, 130f));

        TMP_InputField ipField = CreateInputField(panel.transform, "IPField", "IP Address", "127.0.0.1", new Vector2(0f, 35f));
        TMP_InputField portField = CreateInputField(panel.transform, "PortField", "Port", "7777", new Vector2(0f, -35f));

        Button joinBtn = CreateSolidButton(panel.transform, "JoinButton", "JOIN GAME", new Vector2(0f, -130f));
        Button backBtn = CreateSolidButton(panel.transform, "BackButton", "BACK", new Vector2(0f, -220f));

        TextMeshProUGUI status = CreateLabel(canvasGO.transform, "StatusText", string.Empty, 24, FontStyles.Normal);
        SetMiddleCenter(status.rectTransform, 0f, -460f);
        status.rectTransform.sizeDelta = new Vector2(900f, 48f);
        status.color = new Color(1f, 0.85f, 0.2f, 1f);
        status.alignment = TextAlignmentOptions.Center;

        GameObject lobbyGO = new GameObject("LobbyController");
        LobbyMenu menu = lobbyGO.AddComponent<LobbyMenu>();
        menu.localPlayButton = localBtn;
        menu.hostButton = hostBtn;
        menu.joinButton = joinBtn;
        menu.backButton = backBtn;
        menu.ipField = ipField;
        menu.portField = portField;
        menu.statusText = status;
        menu.gameplaySceneName = "Gameplayscene";
        menu.mainMenuSceneName = "MainMenu";

        GameObject autoSelectGO = new GameObject("MenuAutoSelect");
        autoSelectGO.transform.SetParent(canvasGO.transform, false);
        MenuAutoSelect autoSelect = autoSelectGO.AddComponent<MenuAutoSelect>();
        autoSelect.firstButton = localBtn.gameObject;

        AddNetworkInfrastructure();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        AddSceneToBuildSettings(ScenePath, insertAtIndex: 1);

        Debug.Log($"Lobby scene built at {ScenePath} and added to Build Profiles. Press Play on the MainMenu to test.");
    }

    private static void AddSceneToBuildSettings(string scenePath, int insertAtIndex)
    {
        var existing = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Remove any existing entry for this path so we can re-insert at the right index with the fresh GUID.
        existing.RemoveAll(s => s.path == scenePath);

        EditorBuildSettingsScene entry = new EditorBuildSettingsScene(scenePath, true);

        int idx = Mathf.Clamp(insertAtIndex, 0, existing.Count);
        existing.Insert(idx, entry);

        EditorBuildSettings.scenes = existing.ToArray();
    }

    private static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = CameraBg;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreateCanvas()
    {
        GameObject canvasGO = new GameObject("Lobby_Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        return canvasGO;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        Image img = panel.AddComponent<Image>();
        img.sprite = null;
        img.color = PanelBg;

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        return panel;
    }

    private static Button CreateSolidButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        Image img = btnGO.AddComponent<Image>();
        img.sprite = null;
        img.color = ButtonBg;

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360f, 70f);
        SetMiddleCenter(rt, anchoredPos.x, anchoredPos.y);

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = ButtonHover * (1f / ButtonBg.r); // tint multiplied by image color
        cb.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        cb.selectedColor = new Color(1f, 0.95f, 0.7f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        TextMeshProUGUI text = CreateLabel(btnGO.transform, "Label", label, 32, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        Stretch(text.rectTransform);

        return btn;
    }

    private static TMP_InputField CreateInputField(
        Transform parent,
        string name,
        string placeholder,
        string defaultText,
        Vector2 anchoredPos)
    {
        GameObject fieldGO = new GameObject(name);
        fieldGO.transform.SetParent(parent, false);

        Image img = fieldGO.AddComponent<Image>();
        img.sprite = null;
        img.color = InputBg;

        RectTransform rt = fieldGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360f, 50f);
        SetMiddleCenter(rt, anchoredPos.x, anchoredPos.y);

        TMP_InputField field = fieldGO.AddComponent<TMP_InputField>();
        field.targetGraphic = img;

        TextMeshProUGUI textArea = CreateLabel(fieldGO.transform, "Text", string.Empty, 26, FontStyles.Normal);
        textArea.alignment = TextAlignmentOptions.Left;
        textArea.color = Color.white;
        textArea.textWrappingMode = TextWrappingModes.NoWrap;
        textArea.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(textArea.rectTransform, 16f, 6f);
        field.textComponent = textArea;
        field.text = defaultText;

        TextMeshProUGUI placeholderText = CreateLabel(fieldGO.transform, "Placeholder", placeholder, 26, FontStyles.Italic);
        placeholderText.alignment = TextAlignmentOptions.Left;
        placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
        placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
        placeholderText.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(placeholderText.rectTransform, 16f, 6f);
        field.placeholder = placeholderText;

        return field;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
        float fontSize,
        FontStyles style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = style;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 60f);
        return tmp;
    }

    private static void AddNetworkInfrastructure()
    {
        System.Type nmType = FindTypeByName("Unity.Netcode.NetworkManager");
        if (nmType == null)
        {
            Debug.LogWarning("Unity Netcode for GameObjects not found — skipping NetworkManager creation. Install the package and re-run this tool.");
            return;
        }

        GameObject nmGO = new GameObject("NetworkManager");
        Component nmComp = nmGO.AddComponent(nmType);

        System.Type transportType = FindTypeByName("Unity.Netcode.Transports.UTP.UnityTransport");
        if (transportType == null)
        {
            Debug.LogError(
                "UnityTransport type not found. The Unity Transport package may be missing — open Window / Package Manager and confirm 'Unity Transport' is installed.");
        }
        else
        {
            Component transportComp = nmGO.AddComponent(transportType);

            // Wire NetworkManager.NetworkConfig.NetworkTransport so it picks up the UnityTransport automatically.
            try
            {
                var configProp = nmType.GetProperty("NetworkConfig");
                object config = configProp != null ? configProp.GetValue(nmComp) : null;
                if (config != null)
                {
                    var transportField = config.GetType().GetField("NetworkTransport");
                    transportField?.SetValue(config, transportComp);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Couldn't auto-wire NetworkTransport on NetworkManager: {ex.Message}. " +
                    "Open the NetworkManager in the Inspector and drag the UnityTransport component into the Network Transport field manually.");
            }
        }

        GameObject arenaNetGO = new GameObject("ArenaNetworkManager");
        arenaNetGO.AddComponent<ArenaNetworkManager>();
    }

    private static System.Type FindTypeByName(string fullName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            catch { /* ignored */ }
        }
        return null;
    }

    private static void SetMiddleCenter(RectTransform rt, float x, float y)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    private static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}
