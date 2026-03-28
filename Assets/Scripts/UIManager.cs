using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives all in-game UI:
///   • Two health bars (one left-aligned, one right-aligned like SF/MK)
///   • Centered 99-second countdown timer
///   • Round intro / FIGHT! / WIN text overlay
///   • Win icons (dots below each name)
///   • Match-over panel with rematch button
/// </summary>
public class UIManager : MonoBehaviour
{
    // ─────────────── Inspector references ────────────────────
    [Header("Health Bars")]
    public Slider healthBarP1;     // fills left-to-right  (pivot left)
    public Slider healthBarP2;     // fills right-to-left  (pivot right, value inverted)
    public Image  healthFillP1;
    public Image  healthFillP2;

    [Header("Timer")]
    public TextMeshProUGUI timerText;

    [Header("Round / Announcement Text")]
    public TextMeshProUGUI announcementText;

    [Header("Score / Round Wins")]
    public TextMeshProUGUI p1ScoreText;
    public TextMeshProUGUI p2ScoreText;

    [Header("Match Over Panel")]
    public GameObject matchOverPanel;
    public TextMeshProUGUI resultText;
    public Button rematchButton;
    public Button quitButton;

    [Header("Color Theming")]
    public Color healthHighColor = new Color(0.1f, 0.9f, 0.1f);
    public Color healthMidColor  = Color.yellow;
    public Color healthLowColor  = Color.red;

    // ─────────────── Private ─────────────────────────────────
    private HealthManager _hmP1;
    private HealthManager _hmP2;
    private GameStateManager _gsm;

    private Coroutine _announceCo;

    // ─────────────── Unity lifecycle ─────────────────────────
    private void Start()
    {
        _gsm = GameStateManager.Instance;

        // Get health managers from the game state manager's player refs
        if (_gsm != null)
        {
            _hmP1 = _gsm.player1?.GetComponent<HealthManager>();
            _hmP2 = _gsm.player2?.GetComponent<HealthManager>();
        }

        // Subscribe to events
        if (_gsm != null)
        {
            _gsm.OnTimerUpdate    += UpdateTimer;
            _gsm.OnRoundIntroText += ShowAnnouncement;
            _gsm.OnScoreUpdate    += UpdateScore;
            _gsm.OnMatchResult    += ShowMatchResult;
            _gsm.OnRoundStart     += OnRoundStart;
            _gsm.OnRoundEnd       += OnRoundEnd;
        }

        if (_hmP1 != null) _hmP1.OnHealthChanged += UpdateHealthP1;
        if (_hmP2 != null) _hmP2.OnHealthChanged += UpdateHealthP2;

        // Initialise health bars
        UpdateHealthP1(_hmP1 != null ? _hmP1.CurrentHealth : 100,
                       _hmP1 != null ? _hmP1.maxHealth : 100);
        UpdateHealthP2(_hmP2 != null ? _hmP2.CurrentHealth : 100,
                       _hmP2 != null ? _hmP2.maxHealth : 100);

        // Rematch / quit buttons
        if (rematchButton != null) rematchButton.onClick.AddListener(() => _gsm?.RestartMatch());
        if (quitButton    != null) quitButton.onClick.AddListener(Application.Quit);

        // Hide overlay panels at start
        if (matchOverPanel != null) matchOverPanel.SetActive(false);
        if (announcementText != null) announcementText.text = "";
    }

    private void OnDestroy()
    {
        if (_gsm != null)
        {
            _gsm.OnTimerUpdate    -= UpdateTimer;
            _gsm.OnRoundIntroText -= ShowAnnouncement;
            _gsm.OnScoreUpdate    -= UpdateScore;
            _gsm.OnMatchResult    -= ShowMatchResult;
            _gsm.OnRoundStart     -= OnRoundStart;
            _gsm.OnRoundEnd       -= OnRoundEnd;
        }
        if (_hmP1 != null) _hmP1.OnHealthChanged -= UpdateHealthP1;
        if (_hmP2 != null) _hmP2.OnHealthChanged -= UpdateHealthP2;
    }

    // ─────────────── Health bars ─────────────────────────────
    private void UpdateHealthP1(int current, int max)
    {
        if (healthBarP1 == null) return;
        float t = (float)current / max;
        healthBarP1.value = t;
        if (healthFillP1 != null) healthFillP1.color = HealthColor(t);
    }

    private void UpdateHealthP2(int current, int max)
    {
        if (healthBarP2 == null) return;
        float t = (float)current / max;
        // P2 bar fills from right; we just invert the value
        healthBarP2.value = t;
        if (healthFillP2 != null) healthFillP2.color = HealthColor(t);
    }

    private Color HealthColor(float t)
    {
        if (t > 0.5f) return Color.Lerp(healthMidColor,  healthHighColor, (t - 0.5f) * 2f);
        else          return Color.Lerp(healthLowColor,   healthMidColor,  t * 2f);
    }

    // ─────────────── Timer ────────────────────────────────────
    private void UpdateTimer(float seconds)
    {
        if (timerText == null) return;
        int s = Mathf.CeilToInt(seconds);
        timerText.text = s.ToString();

        // Pulse red when under 10 seconds
        timerText.color = (s <= 10) ? Color.red : Color.white;
    }

    // ─────────────── Announcement overlay ────────────────────
    private void ShowAnnouncement(string msg)
    {
        if (announcementText == null) return;
        if (_announceCo != null) StopCoroutine(_announceCo);
        announcementText.text = msg;

        // Auto-clear after a short period unless msg is empty
        if (!string.IsNullOrEmpty(msg))
            _announceCo = StartCoroutine(ClearAnnouncement(2.5f));
    }

    private IEnumerator ClearAnnouncement(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (announcementText != null) announcementText.text = "";
    }

    // ─────────────── Score display ────────────────────────────
    private void UpdateScore(int p1Wins, int p2Wins)
    {
        if (p1ScoreText != null) p1ScoreText.text = new string('●', p1Wins);
        if (p2ScoreText != null) p2ScoreText.text = new string('●', p2Wins);
    }

    // ─────────────── Round events ─────────────────────────────
    private void OnRoundStart()
    {
        if (timerText != null) timerText.gameObject.SetActive(true);
    }

    private void OnRoundEnd()
    {
        // Timer keeps showing but stops updating (GameStateManager stops calling OnTimerUpdate)
    }

    // ─────────────── Match over ───────────────────────────────
    private void ShowMatchResult(string result)
    {
        if (matchOverPanel != null) matchOverPanel.SetActive(true);
        if (resultText     != null) resultText.text = result;
        if (timerText      != null) timerText.gameObject.SetActive(false);
    }
}
