using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Main Menu scene.
/// Shows game title, Play / Quit buttons, and a controls reference panel.
/// Built and wired entirely by AutoSetup ⑦.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ── Inspector references (set by AutoSetup) ───────────────
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
    public AudioClip   hoverSFX;

    [Header("Transition")]
    public CanvasGroup fadeGroup;
    public float       fadeDuration = 0.6f;

    [Header("Scene")]
    [Tooltip("Name of the gameplay scene to load")]
    public string gameplaySceneName = "Gameplay";

    private AudioSource _sfxSource;

    // ── Unity lifecycle ────────────────────────────────────────
    private void Awake()
    {
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.volume = 0.8f;

        if (menuPanel    != null) menuPanel.SetActive(true);
        if (controlsPanel!= null) controlsPanel.SetActive(false);

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 1f;
            StartCoroutine(FadeIn());
        }
    }

    private void Start()
    {
        // Play menu music
        if (menuMusic != null && menuMusicClip != null)
        {
            menuMusic.clip = menuMusicClip;
            menuMusic.loop = true;
            menuMusic.volume = 0.4f;
            menuMusic.Play();
        }

        // Wire buttons
        playButton?.onClick.AddListener(OnPlay);
        controlsButton?.onClick.AddListener(OnShowControls);
        backButton?.onClick.AddListener(OnHideControls);
        quitButton?.onClick.AddListener(OnQuit);
    }

    // ── Button handlers ────────────────────────────────────────
    public void OnPlay()
    {
        PlayClick();
        StartCoroutine(LoadGameplay());
    }

    public void OnShowControls()
    {
        PlayClick();
        if (menuPanel     != null) menuPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(true);
    }

    public void OnHideControls()
    {
        PlayClick();
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (menuPanel     != null) menuPanel.SetActive(true);
    }

    public void OnQuit()
    {
        PlayClick();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ── Scene transition ──────────────────────────────────────
    private IEnumerator LoadGameplay()
    {
        if (fadeGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                fadeGroup.alpha = t / fadeDuration;
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }
        else
        {
            yield return new WaitForSeconds(0.1f);
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator FadeIn()
    {
        float t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            if (fadeGroup != null) fadeGroup.alpha = t / fadeDuration;
            yield return null;
        }
        if (fadeGroup != null) fadeGroup.alpha = 0f;
    }

    // ── Audio ────────────────────────────────────────────────
    private void PlayClick()
    {
        if (_sfxSource != null && clickSFX != null)
            _sfxSource.PlayOneShot(clickSFX);
    }

    public void PlayHover()
    {
        if (_sfxSource != null && hoverSFX != null)
            _sfxSource.PlayOneShot(hoverSFX, 0.5f);
    }
}
