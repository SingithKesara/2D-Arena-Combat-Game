using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the entire match flow:
///   1. "ROUND X – FIGHT!" countdown (Street Fighter / MK style)
///   2. Live 99-second match timer
///   3. Round tracking (best of 3 by default)
///   4. Win / Loss declaration when health hits 0 or time expires
///   5. Respawn / rematch on button press
/// </summary>
public class GameStateManager : MonoBehaviour
{
    // ─────────────── Singleton ────────────────────────────────
    public static GameStateManager Instance { get; private set; }

    // ─────────────── Inspector ────────────────────────────────
    [Header("Match Settings")]
    public int   roundsToWin  = 2;
    public float matchTimeSec = 99f;

    [Header("Round Timing")]
    public float roundIntroDelay = 1.2f;
    public float fightTextDelay  = 0.8f;
    public float roundEndDelay   = 2.5f;

    [Header("Spawn Points")]
    public Transform spawnP1;
    public Transform spawnP2;

    [Header("Players")]
    public PlayerController player1;
    public PlayerController player2;

    // ─────────────── State ────────────────────────────────────
    public enum MatchState { Intro, Fighting, RoundEnd, MatchOver }
    public MatchState State { get; private set; } = MatchState.Intro;

    private float _timeRemaining;
    private int   _p1Wins;
    private int   _p2Wins;
    private int   _currentRound = 1;
    private bool  _roundEndStarted;

    // ─────────────── Events for UIManager ─────────────────────
    public System.Action<float>   OnTimerUpdate;
    public System.Action<string>  OnRoundIntroText;
    public System.Action<int,int> OnScoreUpdate;
    public System.Action<string>  OnMatchResult;
    public System.Action          OnRoundStart;
    public System.Action          OnRoundEnd;

    // ─────────────── Unity lifecycle ──────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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

    // ─────────────── Round intro ──────────────────────────────
    private IEnumerator DoRoundIntro()
    {
        State            = MatchState.Intro;
        _roundEndStarted = false;

        ResetPlayers();
        LockPlayers(true);

        yield return new WaitForSeconds(0.4f);
        OnRoundIntroText?.Invoke($"ROUND  {_currentRound}");
        AudioManager.Instance?.PlayRoundStart();

        yield return new WaitForSeconds(roundIntroDelay);
        OnRoundIntroText?.Invoke("FIGHT!");

        yield return new WaitForSeconds(fightTextDelay);
        OnRoundIntroText?.Invoke("");

        LockPlayers(false);
        _timeRemaining = matchTimeSec;
        State = MatchState.Fighting;
        OnRoundStart?.Invoke();
    }

    // ─────────────── Death callback (called by HealthManager) ─
    public void OnPlayerDied(HealthManager victim)
    {
        if (State != MatchState.Fighting || _roundEndStarted) return;
        _roundEndStarted = true;
        StartCoroutine(EndRound(victim.GetComponent<PlayerController>().playerIndex));
    }

    private IEnumerator EndRound(int loserIndex)
    {
        State = MatchState.RoundEnd;
        LockPlayers(true);
        OnRoundEnd?.Invoke();
        ScreenFlash.Instance?.Flash(isHeavy: true);

        if (loserIndex == 1) _p2Wins++; else _p1Wins++;
        OnScoreUpdate?.Invoke(_p1Wins, _p2Wins);

        string msg = (loserIndex == 1) ? "PLAYER 2  WINS ROUND!" : "PLAYER 1  WINS ROUND!";
        OnRoundIntroText?.Invoke(msg);

        yield return new WaitForSeconds(roundEndDelay);

        if      (_p1Wins >= roundsToWin) EndMatch("PLAYER 1\nWINS!");
        else if (_p2Wins >= roundsToWin) EndMatch("PLAYER 2\nWINS!");
        else
        {
            _currentRound++;
            StartCoroutine(DoRoundIntro());
        }
    }

    // ─────────────── Time-out ─────────────────────────────────
    private IEnumerator OnTimeUp()
    {
        State = MatchState.RoundEnd;
        LockPlayers(true);
        OnRoundEnd?.Invoke();

        HealthManager hm1 = player1.GetComponent<HealthManager>();
        HealthManager hm2 = player2.GetComponent<HealthManager>();

        string msg;
        if (hm1.CurrentHealth > hm2.CurrentHealth)      { _p1Wins++; msg = "TIME!  PLAYER 1 WINS ROUND!"; }
        else if (hm2.CurrentHealth > hm1.CurrentHealth) { _p2Wins++; msg = "TIME!  PLAYER 2 WINS ROUND!"; }
        else { _p1Wins++; _p2Wins++; msg = "TIME!  DRAW!"; }

        OnScoreUpdate?.Invoke(_p1Wins, _p2Wins);
        OnRoundIntroText?.Invoke(msg);

        yield return new WaitForSeconds(roundEndDelay);

        if      (_p1Wins >= roundsToWin) EndMatch("PLAYER 1\nWINS!");
        else if (_p2Wins >= roundsToWin) EndMatch("PLAYER 2\nWINS!");
        else { _currentRound++; StartCoroutine(DoRoundIntro()); }
    }

    // ─────────────── Match over ───────────────────────────────
    private void EndMatch(string result)
    {
        State = MatchState.MatchOver;
        LockPlayers(true);
        OnMatchResult?.Invoke(result);
        AudioManager.Instance?.PlayUIConfirm();
    }

    public void RestartMatch()
    {
        AudioManager.Instance?.PlayUIConfirm();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ─────────────── Helpers ──────────────────────────────────
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
        if (player1 != null) player1.isAttacking = locked;
        if (player2 != null) player2.isAttacking = locked;
    }
}
