using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Settings (optional — overrides inline values when assigned)")]
    public MatchSettings settings;

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

    [HideInInspector] public bool isNetworkAuthority = true;
    [HideInInspector] public bool waitForRemotePlayer = false;

    private float _timeRemaining;
    private bool _matchStarted;
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

        ApplySettings();
    }

    private void ApplySettings()
    {
        if (settings == null) return;

        roundsToWin = settings.roundsToWin;
        matchTimeSec = settings.matchTimeSec;
        roundIntroDelay = settings.roundIntroDelay;
        fightTextDelay = settings.fightTextDelay;
        roundEndDelay = settings.roundEndDelay;
    }

    private void Start()
    {
        if (!isNetworkAuthority) return;

        // If we're in a networked session (Host or Client), wait for the second player
        // to fully connect before starting the match. MultiplayerSceneBinder will call
        // BeginMatch() on the host once Player 2 ownership has transferred.
        bool networked = ArenaNetworkManager.Instance != null &&
                         ArenaNetworkManager.Instance.CurrentMode != ArenaNetworkManager.SessionMode.Local;

        if (networked)
        {
            StartCoroutine(EnterWaitingState());
            return;
        }

        BeginMatch();
    }

    private IEnumerator EnterWaitingState()
    {
        // Wait one frame so UIManager has a chance to subscribe to OnRoundIntroText
        // (their Start methods can fire in any order otherwise).
        yield return null;

        ResetPlayers();
        LockPlayers(true);

        // Re-fire the message periodically so it persists past the announcement-text auto-fade.
        while (!_matchStarted)
        {
            OnRoundIntroText?.Invoke("WAITING FOR OPPONENT...");
            yield return new WaitForSeconds(1.4f);
        }
    }

    /// <summary>
    /// Called from MultiplayerSceneBinder once both players are connected and ownership
    /// has been transferred. In local 2P play, Start() calls this immediately.
    /// </summary>
    public void BeginMatch()
    {
        if (!isNetworkAuthority) return;
        if (_matchStarted) return;

        _matchStarted = true;
        OnRoundIntroText?.Invoke(string.Empty);
        StartCoroutine(DoRoundIntro());
    }

    private void Update()
    {
        if (!isNetworkAuthority) return;
        if (State != MatchState.Fighting) return;

        _timeRemaining = Mathf.Max(0f, _timeRemaining - Time.deltaTime);
        OnTimerUpdate?.Invoke(_timeRemaining);

        if (_timeRemaining <= 0f && !_roundEndStarted)
        {
            _roundEndStarted = true;
            StartCoroutine(OnTimeUp());
        }
    }

    // Helpers used by NetworkGameSync to push server-driven state into local UI events on clients.
    public void RaiseTimerUpdate(float t)
    {
        _timeRemaining = t;
        OnTimerUpdate?.Invoke(t);
    }

    public void RaiseScoreUpdate(int p1, int p2)
    {
        _p1Wins = p1;
        _p2Wins = p2;
        OnScoreUpdate?.Invoke(p1, p2);
    }

    public void RaiseRoundIntroText(string msg) => OnRoundIntroText?.Invoke(msg);
    public void RaiseMatchResult(string msg) => OnMatchResult?.Invoke(msg);

    public void RaiseRoundStart()
    {
        State = MatchState.Fighting;
        OnRoundStart?.Invoke();
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

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayRoundStart();
    }

    public void OnPlayerDied(int deadPlayerIndex)
    {
        if (!isNetworkAuthority) return;
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
        if (!isNetworkAuthority) return;

        // If we're hosting and the opponent has left, there's no one to rematch against —
        // shut the session down and return to main menu instead of starting an empty round.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer &&
            NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            if (ArenaNetworkManager.Instance != null)
                ArenaNetworkManager.Instance.Shutdown();

            SceneManager.LoadScene("MainMenu");
            return;
        }

        StopAllCoroutines();
        _p1Wins = 0;
        _p2Wins = 0;
        _currentRound = 1;
        _roundEndStarted = false;
        _matchStarted = true;

        // Push the cleared scoreboard to the UI (and to clients via NetworkGameSync).
        OnScoreUpdate?.Invoke(_p1Wins, _p2Wins);

        StartCoroutine(DoRoundIntro());
    }

    /// <summary>
    /// Stops the match immediately — the timer halts, the round loop is cancelled, and the
    /// match-over panel shows with the supplied result string. Used when the opponent
    /// disconnects mid-match.
    /// </summary>
    public void EndMatchByForfeit(string result)
    {
        if (!isNetworkAuthority) return;
        if (State == MatchState.MatchOver) return;

        StopAllCoroutines();
        State = MatchState.MatchOver;
        _roundEndStarted = true;

        LockPlayers(true);
        OnRoundIntroText?.Invoke(string.Empty);
        OnMatchResult?.Invoke(result);
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void ResetPlayers()
    {
        // Server (or local play) resets its local copy first so server's view starts at spawn.
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

        // Networked: also send a ClientRpc so each player's OWNER resets their authoritative
        // transform. NetworkTransform is in Owner mode for Player 2, so the server's local
        // position write above wouldn't propagate to the client without this.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (player1 != null && spawnP1 != null)
            {
                NetworkPlayer np1 = player1.GetComponent<NetworkPlayer>();
                if (np1 != null && np1.IsSpawned)
                    np1.ResetForNewRoundClientRpc(spawnP1.position);
            }

            if (player2 != null && spawnP2 != null)
            {
                NetworkPlayer np2 = player2.GetComponent<NetworkPlayer>();
                if (np2 != null && np2.IsSpawned)
                    np2.ResetForNewRoundClientRpc(spawnP2.position);
            }
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
