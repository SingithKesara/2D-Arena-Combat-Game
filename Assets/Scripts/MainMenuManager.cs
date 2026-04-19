using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

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

    [Header("Fade")]
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.4f;

    [Header("Scene")]
    public string gameplaySceneName = "GameplayScene";

    private bool _busy;

    private void Awake()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (controlsPanel != null) controlsPanel.SetActive(false);

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
        }

        if (playButton != null) playButton.onClick.AddListener(OnPlay);
        if (controlsButton != null) controlsButton.onClick.AddListener(OnControls);
        if (backButton != null) backButton.onClick.AddListener(OnBack);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
    }

    private void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnPlay);
        if (controlsButton != null) controlsButton.onClick.RemoveListener(OnControls);
        if (backButton != null) backButton.onClick.RemoveListener(OnBack);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuit);
    }

    private void OnPlay()
    {
        if (_busy) return;
        StartCoroutine(LoadGameplay());
    }

    private void OnControls()
    {
        if (_busy) return;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(true);
    }

    private void OnBack()
    {
        if (_busy) return;
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    private void OnQuit()
    {
        if (_busy) return;
        Application.Quit();
    }

    private IEnumerator LoadGameplay()
    {
        _busy = true;

        if (fadeGroup != null)
        {
            fadeGroup.blocksRaycasts = true;
            yield return StartCoroutine(Fade(0f, 1f));
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeGroup == null) yield break;

        float t = 0f;
        fadeGroup.alpha = from;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = to;
    }
}