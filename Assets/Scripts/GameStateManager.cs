using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Match Settings")]
    public int roundsToWin = 2;
    public float matchTimeSec = 99f;

    [Header("Round Timing")]
    public float roundIntroDelay = 1.0f;
    public float fightTextDelay = 0.7f;
    public float roundEndDelay = 2.0f;

    [Header("Spawn Points")]
    public Transform spawnP1;
    public Transform spawnP2;

    [Header("Players")]
    public PlayerController player1;
    public PlayerController player2;

    public enum MatchState { Intro, Fighting, RoundEnd, MatchOver }
    public MatchState State { get; private set; } = MatchState.Intro;

    private float _timeRemaining;
    private int _p1Wins;
    private int _p2Wins;
    private int _currentRound = 1;
    private bool _roundEndStarted;

    public event Action<float> OnTimerUpdate;
    public event Action<string> OnRoundIntroText;
    public event Action<int, int> OnScoreUpdate;
    public event Action<string> OnMatchResult;
    public event Action OnRoundStart;
    public event Action OnRoundEnd;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(DoRoundIntro());
    }

    private void Update()
    {
        if (State != MatchState.Fighting) return;

        _timeRemaining = Mathf.Max(0f, _timeRemaining - Time.deltaTime);
        OnTimerUpdate?.Invoke(_timeRemaining);

        if (_timeRemaining <= 0f && !_roundEndStarted)
        {
            _roundEndStarted = true;
            StartCoroutine(OnTimeUp());
        }
    }

    private IEnumerator DoRoundIntro()
    {
        State = MatchState.Intro;
        _roundEndStarted = false;

        ResetPlayers();
        LockPlayers(true);

        yield return new WaitForSeconds(0.35f);
        OnRoundIntroText?.Invoke($"ROUND {_currentRound}");
        yield return new WaitForSeconds(roundIntroDelay);
        OnRoundIntroText?.Invoke("FIGHT!");
        yield return new WaitForSeconds(fightTextDelay);
        OnRoundIntroText?.Invoke(string.Empty);

        LockPlayers(false);
        _timeRemaining = matchTimeSec;
        OnTimerUpdate?.Invoke(_timeRemaining);
        State = MatchState.Fighting;
        OnRoundStart?.Invoke();
    }

    public void OnPlayerDied(int deadPlayerIndex)
    {
        if (State != MatchState.Fighting || _roundEndStarted) return;
        _roundEndStarted = true;
        StartCoroutine(EndRound(deadPlayerIndex));
    }

    private IEnumerator EndRound(int loserIndex)
    {
        State = MatchState.RoundEnd;
        LockPlayers(true);
        OnRoundEnd?.Invoke();

        if (loserIndex == 1) _p2Wins++; else _p1Wins++;
        OnScoreUpdate?.Invoke(_p1Wins, _p2Wins);

        string msg = loserIndex == 1 ? "PLAYER 2 WINS ROUND!" : "PLAYER 1 WINS ROUND!";
        OnRoundIntroText?.Invoke(msg);

        yield return new WaitForSeconds(roundEndDelay);

        if (_p1Wins >= roundsToWin) EndMatch("PLAYER 1\nWINS!");
        else if (_p2Wins >= roundsToWin) EndMatch("PLAYER 2\nWINS!");
        else
        {
            _currentRound++;
            StartCoroutine(DoRoundIntro());
        }
    }

    private IEnumerator OnTimeUp()
    {
        State = MatchState.RoundEnd;
        LockPlayers(true);
        OnRoundEnd?.Invoke();

        HealthManager hm1 = player1.GetComponent<HealthManager>();
        HealthManager hm2 = player2.GetComponent<HealthManager>();

        string msg;
        if (hm1.CurrentHealth > hm2.CurrentHealth) { _p1Wins++; msg = "TIME! PLAYER 1 WINS ROUND!"; }
        else if (hm2.CurrentHealth > hm1.CurrentHealth) { _p2Wins++; msg = "TIME! PLAYER 2 WINS ROUND!"; }
        else { msg = "TIME! DRAW!"; }

        OnScoreUpdate?.Invoke(_p1Wins, _p2Wins);
        OnRoundIntroText?.Invoke(msg);

        yield return new WaitForSeconds(roundEndDelay);

        if (_p1Wins >= roundsToWin) EndMatch("PLAYER 1\nWINS!");
        else if (_p2Wins >= roundsToWin) EndMatch("PLAYER 2\nWINS!");
        else
        {
            _currentRound++;
            StartCoroutine(DoRoundIntro());
        }
    }

    private void EndMatch(string result)
    {
        State = MatchState.MatchOver;
        LockPlayers(true);
        OnMatchResult?.Invoke(result);
    }

    public void RestartMatch()
    {
        StopAllCoroutines();
        _p1Wins = 0;
        _p2Wins = 0;
        _currentRound = 1;
        _roundEndStarted = false;
        StartCoroutine(DoRoundIntro());
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void ResetPlayers()
    {
        if (player1 != null && spawnP1 != null)
        {
            player1.ResetForNewRound(spawnP1.position);
            player1.GetComponent<HealthManager>()?.ResetHealth();
        }

        if (player2 != null && spawnP2 != null)
        {
            player2.ResetForNewRound(spawnP2.position);
            player2.GetComponent<HealthManager>()?.ResetHealth();
        }

        if (player1 != null && player2 != null)
        {
            player1.FaceTarget(player2.transform);
            player2.FaceTarget(player1.transform);
        }
    }

    private void LockPlayers(bool locked)
    {
        if (player1 != null) player1.SetControlsLocked(locked);
        if (player2 != null) player2.SetControlsLocked(locked);
    }
}
