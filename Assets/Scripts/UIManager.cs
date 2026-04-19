using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Health Bar Fill Images")]
    public Image healthFillP1;
    public Image healthFillP2;

    [Header("HP Text Labels")]
    public TextMeshProUGUI hpTextP1;
    public TextMeshProUGUI hpTextP2;

    [Header("Direct Health Manager References")]
    public HealthManager healthManagerP1;
    public HealthManager healthManagerP2;

    [Header("Timer")]
    public TextMeshProUGUI timerText;

    [Header("Round / Announcement")]
    public TextMeshProUGUI announcementText;

    [Header("Score")]
    public TextMeshProUGUI p1ScoreText;
    public TextMeshProUGUI p2ScoreText;

    [Header("Match Over Panel")]
    public GameObject matchOverPanel;
    public TextMeshProUGUI resultText;
    public Button rematchButton;
    public Button quitButton;
    public Button menuButton;

    [Header("Health Bar Animation")]
    public float mainBarLerpSpeed = 3.5f;
    public float healBarLerpSpeed = 5.5f;
    public float lagBarLerpSpeed = 1.2f;
    public float lagBarDelay = 0.35f;

    [Header("Health Segments")]
    public int segmentCount = 10;
    public float segmentLineWidth = 3f;
    public Color segmentLineColor = new Color(1f, 1f, 1f, 0.22f);

    private static readonly Color ColHigh = new Color(0.08f, 0.90f, 0.15f);
    private static readonly Color ColMid = new Color(1.00f, 0.82f, 0.08f);
    private static readonly Color ColLow = new Color(0.92f, 0.10f, 0.08f);
    private static readonly Color LagBarColor = new Color(1.00f, 0.48f, 0.10f, 0.90f);

    private GameStateManager _gsm;
    private Coroutine _annCo;

    private Image _healthLagP1;
    private Image _healthLagP2;

    private float _targetFillP1 = 1f;
    private float _targetFillP2 = 1f;
    private float _displayFillP1 = 1f;
    private float _displayFillP2 = 1f;
    private float _lagFillP1 = 1f;
    private float _lagFillP2 = 1f;

    private float _lagDelayTimerP1;
    private float _lagDelayTimerP2;

    private int _currentHpP1 = 100;
    private int _currentHpP2 = 100;
    private int _maxHpP1 = 100;
    private int _maxHpP2 = 100;

    private void Awake()
    {
        if (healthManagerP1 == null || healthManagerP2 == null)
        {
            var all = FindObjectsByType<HealthManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var hm in all)
            {
                var pc = hm.GetComponent<PlayerController>();
                if (pc == null) continue;

                if (pc.playerIndex == 1 && healthManagerP1 == null) healthManagerP1 = hm;
                if (pc.playerIndex == 2 && healthManagerP2 == null) healthManagerP2 = hm;
            }
        }

        SetupFillImage(healthFillP1);
        SetupFillImage(healthFillP2);

        _healthLagP1 = CreateLagBar(healthFillP1, "LagFill_P1");
        _healthLagP2 = CreateLagBar(healthFillP2, "LagFill_P2");

        CreateSegments(healthFillP1, "Segments_P1");
        CreateSegments(healthFillP2, "Segments_P2");

        if (healthManagerP1 != null) healthManagerP1.OnHealthChanged += OnP1HealthChanged;
        if (healthManagerP2 != null) healthManagerP2.OnHealthChanged += OnP2HealthChanged;

        if (matchOverPanel != null) matchOverPanel.SetActive(false);
        if (announcementText != null) announcementText.text = string.Empty;

        rematchButton?.onClick.AddListener(() => GameStateManager.Instance?.RestartMatch());
        quitButton?.onClick.AddListener(Application.Quit);
        menuButton?.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
    }

    private void Start()
    {
        _gsm = GameStateManager.Instance;
        if (_gsm != null)
        {
            _gsm.OnTimerUpdate += RefreshTimer;
            _gsm.OnRoundIntroText += ShowAnnouncement;
            _gsm.OnScoreUpdate += RefreshScore;
            _gsm.OnMatchResult += ShowMatchOver;
            _gsm.OnRoundStart += OnRoundStart;
        }

        _maxHpP1 = healthManagerP1 != null ? healthManagerP1.maxHealth : 100;
        _maxHpP2 = healthManagerP2 != null ? healthManagerP2.maxHealth : 100;
        _currentHpP1 = _maxHpP1;
        _currentHpP2 = _maxHpP2;

        ForceHealthStateP1(_currentHpP1, _maxHpP1);
        ForceHealthStateP2(_currentHpP2, _maxHpP2);

        RefreshTimer(99f);
        RefreshScore(0, 0);
    }

    private void Update()
    {
        AnimateHealthBars();
    }

    private void OnDestroy()
    {
        if (healthManagerP1 != null) healthManagerP1.OnHealthChanged -= OnP1HealthChanged;
        if (healthManagerP2 != null) healthManagerP2.OnHealthChanged -= OnP2HealthChanged;

        if (_gsm != null)
        {
            _gsm.OnTimerUpdate -= RefreshTimer;
            _gsm.OnRoundIntroText -= ShowAnnouncement;
            _gsm.OnScoreUpdate -= RefreshScore;
            _gsm.OnMatchResult -= ShowMatchOver;
            _gsm.OnRoundStart -= OnRoundStart;
        }
    }

    private void OnP1HealthChanged(int current, int max)
    {
        UpdateHealthTarget(
            current,
            max,
            ref _currentHpP1,
            ref _maxHpP1,
            ref _targetFillP1,
            ref _displayFillP1,
            ref _lagFillP1,
            ref _lagDelayTimerP1
        );

        if (hpTextP1 != null) hpTextP1.text = current.ToString();
    }

    private void OnP2HealthChanged(int current, int max)
    {
        UpdateHealthTarget(
            current,
            max,
            ref _currentHpP2,
            ref _maxHpP2,
            ref _targetFillP2,
            ref _displayFillP2,
            ref _lagFillP2,
            ref _lagDelayTimerP2
        );

        if (hpTextP2 != null) hpTextP2.text = current.ToString();
    }

    private void UpdateHealthTarget(
        int current,
        int max,
        ref int cachedCurrent,
        ref int cachedMax,
        ref float targetFill,
        ref float displayFill,
        ref float lagFill,
        ref float lagDelayTimer)
    {
        float oldTarget = targetFill;

        cachedCurrent = current;
        cachedMax = max;
        targetFill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

        bool tookDamage = targetFill < oldTarget;
        bool healed = targetFill > oldTarget;

        if (tookDamage)
        {
            lagDelayTimer = lagBarDelay;

            if (lagFill < displayFill)
                lagFill = displayFill;
        }
        else if (healed)
        {
            lagDelayTimer = 0f;
            lagFill = targetFill;
        }
    }

    private void AnimateHealthBars()
    {
        AnimateOneBar(
            healthFillP1,
            _healthLagP1,
            ref _displayFillP1,
            ref _lagFillP1,
            _targetFillP1,
            ref _lagDelayTimerP1
        );

        AnimateOneBar(
            healthFillP2,
            _healthLagP2,
            ref _displayFillP2,
            ref _lagFillP2,
            _targetFillP2,
            ref _lagDelayTimerP2
        );
    }

    private void AnimateOneBar(
        Image mainFill,
        Image lagFillImage,
        ref float displayFill,
        ref float lagFill,
        float targetFill,
        ref float lagDelayTimer)
    {
        if (mainFill == null) return;

        float speed = targetFill >= displayFill ? healBarLerpSpeed : mainBarLerpSpeed;
        displayFill = Mathf.MoveTowards(displayFill, targetFill, speed * Time.deltaTime);

        mainFill.fillAmount = displayFill;
        mainFill.color = HealthColour(displayFill);

        if (lagFillImage != null)
        {
            if (lagFill < displayFill)
                lagFill = displayFill;

            if (lagDelayTimer > 0f)
            {
                lagDelayTimer -= Time.deltaTime;
            }
            else
            {
                lagFill = Mathf.MoveTowards(lagFill, targetFill, lagBarLerpSpeed * Time.deltaTime);
            }

            lagFillImage.fillAmount = lagFill;
        }
    }

    private void ForceHealthStateP1(int current, int max)
    {
        _currentHpP1 = current;
        _maxHpP1 = max;
        _targetFillP1 = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        _displayFillP1 = _targetFillP1;
        _lagFillP1 = _targetFillP1;
        _lagDelayTimerP1 = 0f;

        if (healthFillP1 != null)
        {
            healthFillP1.fillAmount = _displayFillP1;
            healthFillP1.color = HealthColour(_displayFillP1);
        }

        if (_healthLagP1 != null)
            _healthLagP1.fillAmount = _lagFillP1;

        if (hpTextP1 != null)
            hpTextP1.text = current.ToString();
    }

    private void ForceHealthStateP2(int current, int max)
    {
        _currentHpP2 = current;
        _maxHpP2 = max;
        _targetFillP2 = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        _displayFillP2 = _targetFillP2;
        _lagFillP2 = _targetFillP2;
        _lagDelayTimerP2 = 0f;

        if (healthFillP2 != null)
        {
            healthFillP2.fillAmount = _displayFillP2;
            healthFillP2.color = HealthColour(_displayFillP2);
        }

        if (_healthLagP2 != null)
            _healthLagP2.fillAmount = _lagFillP2;

        if (hpTextP2 != null)
            hpTextP2.text = current.ToString();
    }

    private static void SetupFillImage(Image img)
    {
        if (img == null) return;

        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = 1f;
        img.raycastTarget = false;
    }

    private Image CreateLagBar(Image mainFill, string objectName)
    {
        if (mainFill == null) return null;

        Transform parent = mainFill.transform.parent;
        if (parent == null) return null;

        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            Image oldImg = existing.GetComponent<Image>();
            if (oldImg != null)
            {
                SetupFillImage(oldImg);
                oldImg.color = LagBarColor;
                oldImg.transform.SetSiblingIndex(mainFill.transform.GetSiblingIndex());
                mainFill.transform.SetAsLastSibling();
                return oldImg;
            }
        }

        GameObject lagGO = new GameObject(objectName);
        lagGO.transform.SetParent(parent, false);

        RectTransform src = mainFill.rectTransform;
        RectTransform rt = lagGO.AddComponent<RectTransform>();
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.anchoredPosition = src.anchoredPosition;
        rt.sizeDelta = src.sizeDelta;
        rt.localScale = Vector3.one;

        Image lagImg = lagGO.AddComponent<Image>();
        lagImg.sprite = mainFill.sprite;
        lagImg.material = mainFill.material;
        lagImg.type = Image.Type.Filled;
        lagImg.fillMethod = Image.FillMethod.Horizontal;
        lagImg.fillOrigin = mainFill.fillOrigin;
        lagImg.fillClockwise = mainFill.fillClockwise;
        lagImg.fillAmount = mainFill.fillAmount;
        lagImg.color = LagBarColor;
        lagImg.raycastTarget = false;

        lagGO.transform.SetSiblingIndex(mainFill.transform.GetSiblingIndex());
        mainFill.transform.SetAsLastSibling();

        return lagImg;
    }

    private void CreateSegments(Image mainFill, string objectName)
    {
        if (mainFill == null || segmentCount <= 1) return;

        Transform parent = mainFill.transform.parent;
        if (parent == null) return;

        Transform existing = parent.Find(objectName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject segGO = new GameObject(objectName);
        segGO.transform.SetParent(parent, false);

        RectTransform mainRT = mainFill.rectTransform;
        RectTransform segRT = segGO.AddComponent<RectTransform>();
        segRT.anchorMin = mainRT.anchorMin;
        segRT.anchorMax = mainRT.anchorMax;
        segRT.pivot = mainRT.pivot;
        segRT.anchoredPosition = mainRT.anchoredPosition;
        segRT.sizeDelta = mainRT.sizeDelta;
        segRT.localScale = Vector3.one;

        segGO.transform.SetAsLastSibling();
        mainFill.transform.SetAsLastSibling();

        for (int i = 1; i < segmentCount; i++)
        {
            GameObject lineGO = new GameObject("Line_" + i);
            lineGO.transform.SetParent(segGO.transform, false);

            RectTransform lineRT = lineGO.AddComponent<RectTransform>();
            lineRT.anchorMin = new Vector2((float)i / segmentCount, 0f);
            lineRT.anchorMax = new Vector2((float)i / segmentCount, 1f);
            lineRT.pivot = new Vector2(0.5f, 0.5f);
            lineRT.anchoredPosition = Vector2.zero;
            lineRT.sizeDelta = new Vector2(segmentLineWidth, 0f);
            lineRT.localScale = Vector3.one;

            Image lineImg = lineGO.AddComponent<Image>();
            lineImg.color = segmentLineColor;
            lineImg.raycastTarget = false;
        }
    }

    private static Color HealthColour(float t)
    {
        if (t > 0.5f)
            return Color.Lerp(ColMid, ColHigh, (t - 0.5f) * 2f);

        return Color.Lerp(ColLow, ColMid, t * 2f);
    }

    private void RefreshTimer(float seconds)
    {
        if (timerText == null) return;

        int s = Mathf.CeilToInt(seconds);
        timerText.text = s.ToString();
        timerText.color = s <= 10 ? Color.red : Color.white;
    }

    private void ShowAnnouncement(string msg)
    {
        if (announcementText == null) return;

        if (_annCo != null) StopCoroutine(_annCo);

        announcementText.text = msg;
        announcementText.color = AnnouncementColour(msg);

        if (!string.IsNullOrEmpty(msg))
            _annCo = StartCoroutine(FadeAnnouncement(1.4f));
    }

    private static Color AnnouncementColour(string m)
    {
        if (m.Contains("FIGHT")) return new Color(1f, 0.3f, 0.1f);
        if (m.Contains("WINS")) return Color.yellow;
        if (m.Contains("TIME")) return new Color(1f, 0.6f, 0f);
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

        announcementText.text = string.Empty;
    }

    private void RefreshScore(int p1, int p2)
    {
        if (p1ScoreText != null)
            p1ScoreText.text = new string('●', p1) + new string('○', Mathf.Max(0, 2 - p1));

        if (p2ScoreText != null)
            p2ScoreText.text = new string('○', Mathf.Max(0, 2 - p2)) + new string('●', p2);
    }

    private void OnRoundStart()
    {
        if (matchOverPanel != null) matchOverPanel.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(true);

        if (healthManagerP1 != null)
            ForceHealthStateP1(healthManagerP1.CurrentHealth, healthManagerP1.maxHealth);

        if (healthManagerP2 != null)
            ForceHealthStateP2(healthManagerP2.CurrentHealth, healthManagerP2.maxHealth);
    }

    private void ShowMatchOver(string result)
    {
        if (matchOverPanel != null) matchOverPanel.SetActive(true);
        if (resultText != null) resultText.text = result;
        if (timerText != null) timerText.gameObject.SetActive(false);
    }
}