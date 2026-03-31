using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives all in-game HUD elements.
/// References wired directly by AutoSetup so no timing issues.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Health Bars")]
    public Slider           healthBarP1;
    public Slider           healthBarP2;
    public Image            healthFillP1;
    public Image            healthFillP2;

    [Header("HP Text Labels")]
    public TextMeshProUGUI  hpTextP1;
    public TextMeshProUGUI  hpTextP2;

    [Header("Direct Health Manager References")]
    public HealthManager    healthManagerP1;
    public HealthManager    healthManagerP2;

    [Header("Timer")]
    public TextMeshProUGUI  timerText;

    [Header("Round / Announcement")]
    public TextMeshProUGUI  announcementText;

    [Header("Score")]
    public TextMeshProUGUI  p1ScoreText;
    public TextMeshProUGUI  p2ScoreText;

    [Header("Match Over Panel")]
    public GameObject       matchOverPanel;
    public TextMeshProUGUI  resultText;
    public Button           rematchButton;
    public Button           quitButton;

    private static readonly Color ColHigh = new Color(0.08f, 0.90f, 0.15f);
    private static readonly Color ColMid  = new Color(1.00f, 0.82f, 0.08f);
    private static readonly Color ColLow  = new Color(0.92f, 0.10f, 0.08f);

    private GameStateManager _gsm;
    private Coroutine        _annCo;

    // ── Awake: subscribe before any Start() runs ──────────────
    private void Awake()
    {
        // Wire from direct references (assigned by AutoSetup)
        if (healthManagerP1 != null)
            healthManagerP1.OnHealthChanged += (c, m) => RefreshHealth(1, c, m);
        if (healthManagerP2 != null)
            healthManagerP2.OnHealthChanged += (c, m) => RefreshHealth(2, c, m);

        // Buttons
        rematchButton?.onClick.AddListener(() => GameStateManager.Instance?.RestartMatch());
        quitButton?.onClick.AddListener(Application.Quit);

        if (matchOverPanel != null) matchOverPanel.SetActive(false);
        if (announcementText != null) announcementText.text = "";
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

        // Show initial full bars
        int maxP1 = healthManagerP1 != null ? healthManagerP1.maxHealth : 100;
        int maxP2 = healthManagerP2 != null ? healthManagerP2.maxHealth : 100;
        RefreshHealth(1, maxP1, maxP1);
        RefreshHealth(2, maxP2, maxP2);
        RefreshTimer(99f);
        RefreshScore(0, 0);
    }

    private void OnDestroy()
    {
        if (healthManagerP1 != null)
            healthManagerP1.OnHealthChanged -= (c, m) => RefreshHealth(1, c, m);
        if (healthManagerP2 != null)
            healthManagerP2.OnHealthChanged -= (c, m) => RefreshHealth(2, c, m);
        if (_gsm != null)
        {
            _gsm.OnTimerUpdate    -= RefreshTimer;
            _gsm.OnRoundIntroText -= ShowAnnouncement;
            _gsm.OnScoreUpdate    -= RefreshScore;
            _gsm.OnMatchResult    -= ShowMatchOver;
            _gsm.OnRoundStart     -= OnRoundStart;
        }
    }

    // ── Health bars ───────────────────────────────────────────
    private void RefreshHealth(int idx, int current, int max)
    {
        float t = max > 0 ? (float)current / max : 0f;
        Color c = HealthColour(t);

        if (idx == 1)
        {
            if (healthBarP1  != null) healthBarP1.value  = t;
            if (healthFillP1 != null) healthFillP1.color  = c;
            if (hpTextP1     != null) hpTextP1.text       = current.ToString();
        }
        else
        {
            if (healthBarP2  != null) healthBarP2.value  = t;
            if (healthFillP2 != null) healthFillP2.color  = c;
            if (hpTextP2     != null) hpTextP2.text       = current.ToString();
        }
    }

    private static Color HealthColour(float t)
    {
        if (t > 0.5f) return Color.Lerp(ColMid, ColHigh, (t - 0.5f) * 2f);
        return Color.Lerp(ColLow, ColMid, t * 2f);
    }

    // ── Timer ─────────────────────────────────────────────────
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
        announcementText.color = GetAnnouncementColour(msg);
        if (!string.IsNullOrEmpty(msg))
            _annCo = StartCoroutine(FadeAnnouncement(2f));
    }

    private static Color GetAnnouncementColour(string m)
    {
        if (m.Contains("FIGHT"))  return new Color(1f, 0.3f, 0.1f);
        if (m.Contains("WINS"))   return Color.yellow;
        if (m.Contains("TIME"))   return new Color(1f, 0.6f, 0f);
        return Color.white;
    }

    private IEnumerator FadeAnnouncement(float stay)
    {
        yield return new WaitForSeconds(stay);
        float t = 0f;
        Color start = announcementText.color;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            announcementText.color = new Color(start.r, start.g, start.b, 1f - t / 0.5f);
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
