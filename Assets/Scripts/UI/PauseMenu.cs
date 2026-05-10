using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drop this on any GameObject in the GameplayScene. It builds its own pause UI at
/// runtime so no inspector wiring is needed. Press Escape to toggle.
///
/// Resume → unpauses.
/// Quit to Menu → shuts down networking (if active) and loads MainMenu.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private GameObject _root;
    private bool _isPaused;
    private float _previousTimeScale = 1f;

    private void Start()
    {
        BuildUI();
        Hide();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (_isPaused) Resume();
            else Pause();
        }
    }

    private void Pause()
    {
        _isPaused = true;
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Show();
    }

    private void Resume()
    {
        _isPaused = false;
        Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
        Hide();
    }

    private void QuitToMenu()
    {
        Time.timeScale = 1f;

        if (ArenaNetworkManager.Instance != null)
            ArenaNetworkManager.Instance.Shutdown();

        SceneManager.LoadScene("MainMenu");
    }

    private void Show() { if (_root != null) _root.SetActive(true); }
    private void Hide() { if (_root != null) _root.SetActive(false); }

    private void BuildUI()
    {
        // Canvas on top of everything, ignores Time.timeScale.
        _root = new GameObject("PauseMenu_Canvas");
        Canvas canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();

        // Dim full-screen overlay
        GameObject dim = CreateImage(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.65f));
        StretchFull(dim.GetComponent<RectTransform>());

        // Centered panel
        GameObject panel = CreateImage(_root.transform, "Panel", new Color(0.08f, 0.10f, 0.16f, 0.96f));
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(560f, 420f);
        SetCenter(panelRT, 0f, 0f);

        // Title
        TextMeshProUGUI title = CreateLabel(panel.transform, "Title", "PAUSED", 72, FontStyles.Bold, Color.white);
        SetCenter(title.rectTransform, 0f, 130f);
        title.rectTransform.sizeDelta = new Vector2(500f, 100f);

        // Resume button
        Button resume = CreateButton(panel.transform, "ResumeButton", "RESUME", new Vector2(0f, 10f));
        resume.onClick.AddListener(Resume);

        // Quit button
        Button quit = CreateButton(panel.transform, "QuitButton", "QUIT TO MENU", new Vector2(0f, -100f));
        quit.onClick.AddListener(QuitToMenu);
    }

    private static GameObject CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.18f, 0.32f, 0.95f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360f, 70f);
        SetCenter(rt, anchoredPos.x, anchoredPos.y);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.95f, 1f);
        cb.pressedColor = new Color(0.6f, 0.6f, 0.7f);
        cb.selectedColor = new Color(1f, 0.95f, 0.6f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;
        btn.targetGraphic = img;

        TextMeshProUGUI text = CreateLabel(go.transform, "Label", label, 32, FontStyles.Bold, Color.white);
        StretchFull(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static void SetCenter(RectTransform rt, float x, float y)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (Time.timeScale == 0f) Time.timeScale = 1f;
        if (_root != null) Destroy(_root);
    }
}
