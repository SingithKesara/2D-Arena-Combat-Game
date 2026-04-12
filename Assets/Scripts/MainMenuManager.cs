using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// Main Menu controller.
/// Key fixes:
///   - ASCII-only button labels (no Unicode arrows that break LiberationSans)
///   - Ensures EventSystem exists so mouse & keyboard work
///   - Correct gameplay scene name match
///   - Keyboard navigation: Enter/Space = Play, Esc = Quit
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuPanel;
    public GameObject controlsPanel;

    [Header("Buttons")]
    public Button playButton;
    public Button controlsButton;
    public Button backButton;
    public Button quitButton;

    [Header("Audio")]
    public AudioSource menuMusic;
    public AudioClip   menuMusicClip;
    public AudioClip   clickSFX;

    [Header("Transition")]
    public CanvasGroup fadeGroup;
    public float       fadeDuration = 0.5f;

    [Header("Scene Name")]
    public string gameplaySceneName = "Gameplayscene";

    private AudioSource _sfx;
    private bool        _transitioning;

    private void Awake()
    {
        // CRITICAL: Ensure there is an EventSystem for mouse/keyboard UI input
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.volume = 0.9f;

        if (menuPanel    != null) menuPanel.SetActive(true);
        if (controlsPanel!= null) controlsPanel.SetActive(false);

        if (fadeGroup != null) { fadeGroup.alpha = 1f; StartCoroutine(FadeIn()); }
    }

    private void Start()
    {
        if (menuMusic != null && menuMusicClip != null)
        {
            menuMusic.clip = menuMusicClip; menuMusic.loop = true; menuMusic.volume = 0.35f;
            menuMusic.Play();
        }

        // Wire buttons via code (belt-and-suspenders alongside Inspector wiring)
        playButton?.onClick.AddListener(OnPlay);
        controlsButton?.onClick.AddListener(OnShowControls);
        backButton?.onClick.AddListener(OnHideControls);
        quitButton?.onClick.AddListener(OnQuit);

        // Select first button so keyboard navigation works immediately
        if (playButton != null) playButton.Select();
    }

    private void Update()
    {
        if (_transitioning) return;

        // Keyboard shortcuts
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            if (controlsPanel != null && controlsPanel.activeSelf) OnHideControls();
            else OnQuit();
        }
    }

    // ── Handlers ──────────────────────────────────────────────
    public void OnPlay()
    {
        if (_transitioning) return;
        _transitioning = true;
        PlayClick();
        StartCoroutine(LoadScene());
    }

    public void OnShowControls()
    {
        PlayClick();
        if (menuPanel    != null) menuPanel.SetActive(false);
        if (controlsPanel!= null) controlsPanel.SetActive(true);
        if (backButton   != null) backButton.Select();
    }

    public void OnHideControls()
    {
        PlayClick();
        if (controlsPanel!= null) controlsPanel.SetActive(false);
        if (menuPanel    != null) menuPanel.SetActive(true);
        if (playButton   != null) playButton.Select();
    }

    public void OnQuit()
    {
        PlayClick();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ── Scene load with fade ──────────────────────────────────
    private IEnumerator LoadScene()
    {
        if (fadeGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = t / fadeDuration;
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }
        else yield return new WaitForSecondsRealtime(0.05f);

        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator FadeIn()
    {
        float t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.unscaledDeltaTime;
            if (fadeGroup != null) fadeGroup.alpha = t / fadeDuration;
            yield return null;
        }
        if (fadeGroup != null) fadeGroup.alpha = 0f;
    }

    private void PlayClick()
    {
        if (_sfx != null && clickSFX != null) _sfx.PlayOneShot(clickSFX);
    }
}
