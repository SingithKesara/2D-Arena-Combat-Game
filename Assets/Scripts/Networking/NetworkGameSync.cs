using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mirrors the server's GameStateManager state across all clients via NetworkVariables and ClientRpcs.
/// Add this component (and a NetworkObject) to the GameManager GameObject in the GameplayScene.
///
/// Server side: every frame, copy timer/wins/round into the NetworkVariables.
/// Client side: forward NetworkVariable changes into local GameStateManager events so the UI updates.
/// Round-intro / match-result strings travel via ClientRpc (one-shot events).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkGameSync : NetworkBehaviour
{
    public static NetworkGameSync Instance { get; private set; }

    private GameStateManager _gsm;

    private readonly NetworkVariable<float> _netTime = new(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _netP1Wins = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _netP2Wins = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        Instance = this;

        _gsm = GetComponent<GameStateManager>();
        if (_gsm == null)
            _gsm = FindAnyObjectByType<GameStateManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (_gsm == null) return;

        if (IsServer)
        {
            _gsm.OnTimerUpdate += ServerOnTimerUpdate;
            _gsm.OnScoreUpdate += ServerOnScoreUpdate;
            _gsm.OnRoundIntroText += ServerOnRoundIntroText;
            _gsm.OnMatchResult += ServerOnMatchResult;
            _gsm.OnRoundStart += ServerOnRoundStart;

            _gsm.isNetworkAuthority = true;
        }
        else
        {
            _gsm.isNetworkAuthority = false;

            _netTime.OnValueChanged += ClientOnTimeChanged;
            _netP1Wins.OnValueChanged += ClientOnScoreChanged;
            _netP2Wins.OnValueChanged += ClientOnScoreChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_gsm == null) return;

        if (IsServer)
        {
            _gsm.OnTimerUpdate -= ServerOnTimerUpdate;
            _gsm.OnScoreUpdate -= ServerOnScoreUpdate;
            _gsm.OnRoundIntroText -= ServerOnRoundIntroText;
            _gsm.OnMatchResult -= ServerOnMatchResult;
            _gsm.OnRoundStart -= ServerOnRoundStart;
        }
        else
        {
            _netTime.OnValueChanged -= ClientOnTimeChanged;
            _netP1Wins.OnValueChanged -= ClientOnScoreChanged;
            _netP2Wins.OnValueChanged -= ClientOnScoreChanged;
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    // ───── Server side: GSM events → NetworkVariables / ClientRpc ──────────────

    private void ServerOnTimerUpdate(float t) => _netTime.Value = t;

    private void ServerOnScoreUpdate(int p1, int p2)
    {
        _netP1Wins.Value = p1;
        _netP2Wins.Value = p2;
    }

    private void ServerOnRoundIntroText(string msg) =>
        BroadcastRoundIntroClientRpc(new FixedString128Bytes(msg ?? string.Empty));

    private void ServerOnMatchResult(string msg) =>
        BroadcastMatchResultClientRpc(new FixedString128Bytes(msg ?? string.Empty));

    private void ServerOnRoundStart() => BroadcastRoundStartClientRpc();

    [ClientRpc]
    private void BroadcastRoundIntroClientRpc(FixedString128Bytes msg)
    {
        if (IsServer) return;
        _gsm?.RaiseRoundIntroText(msg.ToString());
    }

    [ClientRpc]
    private void BroadcastMatchResultClientRpc(FixedString128Bytes msg)
    {
        if (IsServer) return;
        _gsm?.RaiseMatchResult(msg.ToString());
    }

    [ClientRpc]
    private void BroadcastRoundStartClientRpc()
    {
        if (IsServer) return;
        _gsm?.RaiseRoundStart();
    }

    // Client → Server request to restart the match (PLAY AGAIN button on client side).
    [ServerRpc(RequireOwnership = false)]
    public void RequestRestartServerRpc()
    {
        if (_gsm != null && _gsm.isNetworkAuthority)
            _gsm.RestartMatch();
    }

    // ───── Client side: NetworkVariables → local events for UI ──────────────────

    private void ClientOnTimeChanged(float prev, float current) =>
        _gsm?.RaiseTimerUpdate(current);

    private void ClientOnScoreChanged(int prev, int current) =>
        _gsm?.RaiseScoreUpdate(_netP1Wins.Value, _netP2Wins.Value);
}
