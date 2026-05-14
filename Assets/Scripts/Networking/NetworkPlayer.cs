using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(HealthManager))]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Identity")]
    [Tooltip("1 for Player 1 (host), 2 for Player 2 (joining client). Set this in the inspector on the scene-placed player.")]
    public int defaultPlayerIndex = 1;

    private PlayerController _pc;
    private HealthManager _health;
    private CombatSystem _combat;
    private NetworkAnimator _netAnimator;

    private readonly NetworkVariable<int> _netHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _netPlayerIndex = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Owner-written so the controlling client can broadcast block state to all observers
    // (server + other clients) without a round-trip to the server.
    private readonly NetworkVariable<bool> _netBlocking = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private bool _lastSyncedBlocking;

    // Caps applied to client-supplied damage requests as a basic anti-cheat measure.
    private const int MAX_TRUSTED_DAMAGE = 40;
    private const float MAX_TRUSTED_KNOCKBACK = 30f;
    private const float MAX_TRUSTED_HIT_DISTANCE = 3.5f;

    private void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _health = GetComponent<HealthManager>();
        _combat = GetComponent<CombatSystem>();
        _netAnimator = GetComponent<NetworkAnimator>();

        // Inject ourselves into CombatSystem so it can route damage requests through ServerRpc.
        if (_combat != null)
            _combat.netPlayer = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _netPlayerIndex.Value = defaultPlayerIndex;
            _netHealth.Value = _health != null ? _health.maxHealth : 100;

            if (_health != null)
                _health.OnHealthChanged += OnServerHealthChanged;
        }
        else
        {
            // Client side: react to server-driven HP changes so the UI updates.
            _netHealth.OnValueChanged += OnClientNetHealthChanged;
            if (_health != null)
                _health.ApplyNetworkedHealth(_netHealth.Value);
        }

        // Everyone listens to the block flag so the visual + damage-mitigation reflect
        // the controlling client's input on both screens (and on the server itself).
        _netBlocking.OnValueChanged += OnBlockingChanged;
        if (_pc != null) _pc.isBlocking = _netBlocking.Value;

        ApplyNetworkedIdentity(_netPlayerIndex.Value);
        _netPlayerIndex.OnValueChanged += (_, newIdx) => ApplyNetworkedIdentity(newIdx);

        ConfigureOwnership();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && _health != null)
            _health.OnHealthChanged -= OnServerHealthChanged;
        else
            _netHealth.OnValueChanged -= OnClientNetHealthChanged;

        _netBlocking.OnValueChanged -= OnBlockingChanged;
    }

    private void Update()
    {
        if (!IsSpawned) return;
        if (!IsOwner) return;
        if (_pc == null) return;

        if (_pc.isBlocking != _lastSyncedBlocking)
        {
            _netBlocking.Value = _pc.isBlocking;
            _lastSyncedBlocking = _pc.isBlocking;
        }
    }

    private void OnBlockingChanged(bool previous, bool current)
    {
        if (IsOwner) return;
        if (_pc != null) _pc.isBlocking = current;
    }

    public override void OnGainedOwnership() => ConfigureOwnership();
    public override void OnLostOwnership() => ConfigureOwnership();

    private void OnServerHealthChanged(int current, int max) => _netHealth.Value = current;

    private void OnClientNetHealthChanged(int previous, int current)
    {
        if (_health != null)
            _health.ApplyNetworkedHealth(current);
    }

    private void ApplyNetworkedIdentity(int idx)
    {
        if (_pc != null)
            _pc.playerIndex = idx;
    }

    private void ConfigureOwnership()
    {
        if (_pc == null) return;

        bool localPlay = ArenaNetworkManager.Instance == null ||
                         ArenaNetworkManager.Instance.CurrentMode == ArenaNetworkManager.SessionMode.Local;

        bool hasInputAuthority;
        if (localPlay)
        {
            hasInputAuthority = true;
        }
        else
        {
            // Networked: input authority is role-based.
            //   Host always controls Player 1; joining client always controls Player 2.
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            bool isClient = NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !isHost;

            if (isHost) hasInputAuthority = (defaultPlayerIndex == 1);
            else if (isClient) hasInputAuthority = (defaultPlayerIndex == 2);
            else hasInputAuthority = false;
        }

        _pc.networkInputAuthority = hasInputAuthority;
        _pc.networkUseP1KeyScheme = !localPlay && hasInputAuthority;

        bool simAuth = localPlay || hasInputAuthority;
        _pc.networkSimulationAuthority = simAuth;
        if (_combat != null) _combat.networkSimulationAuthority = simAuth;

        // Damage application stays server-authoritative.
        if (_health != null)
            _health.networkSimulationAuthority = localPlay ||
                (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
    }

    /// <summary>
    /// Called by the server when a round starts. Tells the OWNER of this NetworkObject
    /// (which may be the client for Player 2) to reset their authoritative transform and
    /// gameplay state. Required because NetworkTransform is in Owner mode.
    /// </summary>
    [ClientRpc]
    public void ResetForNewRoundClientRpc(Vector3 spawnPosition)
    {
        if (!IsOwner) return;

        if (_pc != null)
            _pc.ResetForNewRound(spawnPosition);

        if (_health != null)
            _health.ResetHealth();
    }

    /// <summary>
    /// Called by a client when their CombatSystem detects a hit. Server validates and
    /// applies the damage authoritatively.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestDamageServerRpc(
        ulong victimNetworkObjectId,
        int damage,
        Vector2 knockback,
        ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton == null) return;

        GameStateManager gsm = GameStateManager.Instance;
        if (gsm != null && gsm.isNetworkAuthority &&
            gsm.State != GameStateManager.MatchState.Fighting)
            return;

        // Sender must own the NetworkPlayer that the RPC was sent from (no spoofing).
        ulong senderId = rpcParams.Receive.SenderClientId;
        bool senderIsHost = senderId == NetworkManager.ServerClientId && IsOwner;
        if (!senderIsHost && OwnerClientId != senderId)
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                victimNetworkObjectId, out NetworkObject victimNO))
            return;

        HealthManager victim = victimNO.GetComponent<HealthManager>();
        if (victim == null) return;
        if (victimNO == NetworkObject) return; // can't damage self

        int clampedDamage = Mathf.Clamp(damage, 0, MAX_TRUSTED_DAMAGE);
        Vector2 clampedKnockback = Vector2.ClampMagnitude(knockback, MAX_TRUSTED_KNOCKBACK);

        float distance = Vector2.Distance(transform.position, victimNO.transform.position);
        if (distance > MAX_TRUSTED_HIT_DISTANCE) return;

        victim.TakeDamage(clampedDamage, clampedKnockback);
    }
}
