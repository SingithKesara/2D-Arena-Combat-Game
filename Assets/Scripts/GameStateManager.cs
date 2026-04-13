using UnityEngine;
using System;
using System.Collections;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance;

    public PlayerController player1;
    public PlayerController player2;

    public Transform spawnP1;
    public Transform spawnP2;

    public int roundsToWin = 2;
    public float matchTimeSec = 99f;

    public float roundIntroDelay = 1.2f;
    public float roundEndDelay = 2.5f;

    private int p1Score = 0;
    private int p2Score = 0;

    private float currentTime;
    private bool roundActive = false;
    private bool matchOver = false;

    // EVENTS (needed by UIManager)
    public event Action<float> OnTimerUpdate;
    public event Action<string> OnRoundIntroText;
    public event Action<int, int> OnScoreUpdate;
    public event Action<string> OnMatchResult;
    public event Action OnRoundStart;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        StartCoroutine(StartRound());
    }

    void Update()
    {
        if (!roundActive || matchOver) return;

        currentTime -= Time.deltaTime;

        OnTimerUpdate?.Invoke(currentTime);

        if (currentTime <= 0f)
        {
            EndRoundByTime();
        }
    }

    IEnumerator StartRound()
    {
        roundActive = false;

        ResetPlayers();

        OnRoundIntroText?.Invoke("ROUND START");

        yield return new WaitForSeconds(roundIntroDelay);

        currentTime = matchTimeSec;
        roundActive = true;

        OnRoundStart?.Invoke();
    }

    void ResetPlayers()
    {
        player1.transform.position = spawnP1.position;
        player2.transform.position = spawnP2.position;

        player1.ResetState();
        player2.ResetState();

        player1.GetComponent<HealthManager>().ResetHealth();
        player2.GetComponent<HealthManager>().ResetHealth();
    }

    public void OnPlayerDied(int deadPlayerIndex)
    {
        if (!roundActive) return;

        roundActive = false;

        if (deadPlayerIndex == 1)
            p2Score++;
        else
            p1Score++;

        OnScoreUpdate?.Invoke(p1Score, p2Score);

        StartCoroutine(HandleRoundEnd());
    }

    IEnumerator HandleRoundEnd()
    {
        yield return new WaitForSeconds(roundEndDelay);

        if (p1Score >= roundsToWin || p2Score >= roundsToWin)
        {
            StartCoroutine(HandleMatchEnd());
        }
        else
        {
            StartCoroutine(StartRound());
        }
    }

    IEnumerator HandleMatchEnd()
    {
        matchOver = true;

        string winner = p1Score > p2Score ? "PLAYER 1 WINS" : "PLAYER 2 WINS";
        OnMatchResult?.Invoke(winner);

        yield return new WaitForSeconds(2f);

        // clean reset (NO scene reload = no white screen)
        p1Score = 0;
        p2Score = 0;
        matchOver = false;

        StartCoroutine(StartRound());
    }

    void EndRoundByTime()
    {
        roundActive = false;

        int p1HP = player1.GetComponent<HealthManager>().CurrentHealth;
        int p2HP = player2.GetComponent<HealthManager>().CurrentHealth;

        if (p1HP > p2HP)
            p1Score++;
        else if (p2HP > p1HP)
            p2Score++;

        OnScoreUpdate?.Invoke(p1Score, p2Score);

        StartCoroutine(HandleRoundEnd());
    }
    
    public void RestartMatch()
    {
        StopAllCoroutines();

        p1Score = 0;
        p2Score = 0;
        matchOver = false;

        StartCoroutine(StartRound());
    }
}