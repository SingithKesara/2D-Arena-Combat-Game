using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Drives all in-game HUD elements.
/// Uses Image.fillAmount instead of Slider for reliable visible health bars.
/// References wired by AutoSetup ④ — also has runtime fallback FindObjectOfType.
/// </summary>
public class UIManager : MonoBehaviour
{
    // ── Health bar IMAGES (not Sliders — more reliable) ───────
    [Header("Health Bar Fill Images")]
    [Tooltip("The Image inside the P1 health bar — set fillMethod=Horizontal in Inspector")]
    public Image healthFillP1;
    [Tooltip("The Image inside the P2 health bar — set fillMethod=Horizontal in Inspector")]
    public Image healthFillP2;

    [Header("HP Text Labels")]
    public TextMeshProUGUI hpTextP1;
    public TextMeshProUGUI hpTextP2;

    [Header("Direct Health Manager References (wired by AutoSetup)")]
    public HealthManager healthManagerP1;
    public HealthManager healthManagerP2;

    [Header("Timer")]
    public TextMeshProUGUI timerText;

    [Header("Round / Announcement")]
    public TextMeshProUGUI announcementText;

    [Header("Score Dots")]
    public TextMeshProUGUI p1ScoreText;
    public TextMeshProUGUI p2ScoreText;

    [Header("Match Over Panel")]
    public GameObject      matchOverPanel;
    public TextMeshProUGUI resultText;
    public Button          rematchButton;
    public Button          quitButton;
    public Button          menuButton;

    // Health colour thresholds
    private static readonly Color ColHigh = new Color(0.08f, 0.90f, 0.15f);
    private static readonly Color ColMid  = new Color(1.00f, 0.82f, 0.08f);
    private static readonly Color ColLow  = new Color(0.92f, 0.10f, 0.08f);

    private GameStateManager _gsm;
    private Coroutine        _annCo;

    // ── Awake — subscribe immediately so no damage is missed ──
    private void Awake()
    {
        // Fallback: try to find HealthManagers in scene if not assigned
        if (healthManagerP1 == null || healthManagerP2 == null)
        {
            var all = FindObjectsByType<HealthManager>(FindObjectsSortMode.None);
            foreach (var hm in all)
            {
                var pc = hm.GetComponent<PlayerController>();
                if (pc == null) continue;
                if (pc.playerIndex == 1 && healthManagerP1 == null) healthManagerP1 = hm;
                if (pc.playerIndex == 2 && healthManagerP2 == null) healthManagerP2 = hm;
            }
        }

        if (healthManagerP1 != null) healthManagerP1.OnHealthChanged += OnP1HealthChanged;
        if (healthManagerP2 != null) healthManagerP2.OnHealthChanged += OnP2HealthChanged;

        // Ensure fill images are set up correctly for horizontal fill
        SetupFillImage(healthFillP1);
        SetupFillImage(healthFillP2);

        if (matchOverPanel  != null) matchOverPanel.SetActive(false);
        if (announcementText!= null) announcementText.text = "";

        rematchButton?.onClick.AddListener(() => GameStateManager.Instance?.RestartMatch());
        quitButton?.onClick.AddListener(Application.Quit);
        menuButton?.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
    }

    private void Start()
    {
        _gsm = GameStateManager.Instance;
        if (_gsm != null)
        {
            _gsm.OnTimerUpdate    += RefreshTimer;
            _gsm.OnRoundIntroText += ShowAnnouncement;
            _gsm.OnScoreUpdate    += RefreshScore;
            _gsm.OnMatchResult    += ShowMatchOver;
            _gsm.OnRoundStart     += OnRoundStart;
        }

        // Initialise bars immediately
        int mp1 = healthManagerP1 != null ? healthManagerP1.maxHealth : 100;
        int mp2 = healthManagerP2 != null ? healthManagerP2.maxHealth : 100;
        SetHealthBar(healthFillP1, hpTextP1, mp1, mp1);
        SetHealthBar(healthFillP2, hpTextP2, mp2, mp2);
        RefreshTimer(99f);
        RefreshScore(0, 0);
    }

    private void OnDestroy()
    {
        if (healthManagerP1 != null) healthManagerP1.OnHealthChanged -= OnP1HealthChanged;
        if (healthManagerP2 != null) healthManagerP2.OnHealthChanged -= OnP2HealthChanged;
        if (_gsm != null)
        {
            _gsm.OnTimerUpdate    -= RefreshTimer;
            _gsm.OnRoundIntroText -= ShowAnnouncement;
            _gsm.OnScoreUpdate    -= RefreshScore;
            _gsm.OnMatchResult    -= ShowMatchOver;
            _gsm.OnRoundStart     -= OnRoundStart;
        }
    }

    // ── Health callbacks ─────────────────────────────────────
    private void OnP1HealthChanged(int current, int max)
        => SetHealthBar(healthFillP1, hpTextP1, current, max);

    private void OnP2HealthChanged(int current, int max)
        => SetHealthBar(healthFillP2, hpTextP2, current, max);

    private void SetHealthBar(Image fill, TextMeshProUGUI label, int current, int max)
    {
        if (fill == null) return;
        float t = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        fill.fillAmount = t;
        fill.color      = HealthColour(t);
        if (label != null) label.text = current.ToString();
    }

    private static void SetupFillImage(Image img)
    {
        if (img == null) return;
        img.type       = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0; // left-to-right
        img.fillAmount = 1f;
    }

    private static Color HealthColour(float t)
    {
        if (t > 0.5f) return Color.Lerp(ColMid, ColHigh, (t - 0.5f) * 2f);
        return Color.Lerp(ColLow, ColMid, t * 2f);
    }

    // ── Timer ────────────────────────────────────────────────
    private void RefreshTimer(float seconds)
    {
        if (timerText == null) return;
        int s = Mathf.CeilToInt(seconds);
        timerText.text  = s.ToString();
        timerText.color = s <= 10 ? Color.red : Color.white;
        float pulse = s <= 10 ? (1f + 0.12f * Mathf.Sin(Time.time * 8f)) : 1f;
        timerText.transform.localScale = Vector3.one * pulse;
    }

    // ── Announcement ─────────────────────────────────────────
    private void ShowAnnouncement(string msg)
    {
        if (announcementText == null) return;
        if (_annCo != null) StopCoroutine(_annCo);
        announcementText.text  = msg;
        announcementText.color = AnnouncementColour(msg);
        if (!string.IsNullOrEmpty(msg))
            _annCo = StartCoroutine(FadeAnnouncement(1.8f));
    }

    private static Color AnnouncementColour(string m)
    {
        if (m.Contains("FIGHT")) return new Color(1f, 0.3f, 0.1f);
        if (m.Contains("WINS"))  return Color.yellow;
        if (m.Contains("TIME"))  return new Color(1f, 0.6f, 0f);
        if (m.Contains("ROUND")) return Color.white;
        return Color.white;
    }

    private IEnumerator FadeAnnouncement(float stay)
    {
        yield return new WaitForSeconds(stay);
        float t = 0f;
        Color s = announcementText.color;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            announcementText.color = new Color(s.r, s.g, s.b, 1f - t / 0.5f);
            yield return null;
        }
        announcementText.text = "";
    }

    // ── Score ────────────────────────────────────────────────
    private void RefreshScore(int p1, int p2)
    {
        if (p1ScoreText != null) p1ScoreText.text = new string('●', p1) + new string('○', Mathf.Max(0, 2 - p1));
        if (p2ScoreText != null) p2ScoreText.text = new string('○', Mathf.Max(0, 2 - p2)) + new string('●', p2);
    }

    private void OnRoundStart()
    {
        if (timerText != null) timerText.gameObject.SetActive(true);
    }

    private void ShowMatchOver(string result)
    {
        if (matchOverPanel != null) matchOverPanel.SetActive(true);
        if (resultText     != null) resultText.text = result;
        if (timerText      != null) timerText.gameObject.SetActive(false);
    }
}
