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

        ApplyNetworkedIdentity(_netPlayerIndex.Value);
        _netPlayerIndex.OnValueChanged += (_, newIdx) => ApplyNetworkedIdentity(newIdx);

        ConfigureOwnership();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && _health != null)
            _health.OnHealthChanged -= OnServerHealthChanged;
    }

    public override void OnGainedOwnership() => ConfigureOwnership();
    public override void OnLostOwnership() => ConfigureOwnership();

    private void OnServerHealthChanged(int current, int max) => _netHealth.Value = current;

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
            // Local 2P (split keyboard): both players controllable from this machine.
            hasInputAuthority = true;
        }
        else
        {
            // Networked: input authority based on role + slot.
            //   - Host always controls Player 1 (defaultPlayerIndex == 1)
            //   - Joining client always controls Player 2 (defaultPlayerIndex == 2)
            // This is independent of NGO ownership, so the host doesn't accidentally drive
            // Player 2 just because the server still owns the NetworkObject before the client connects.
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            bool isClient = NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !isHost;

            if (isHost)
                hasInputAuthority = (defaultPlayerIndex == 1);
            else if (isClient)
                hasInputAuthority = (defaultPlayerIndex == 2);
            else
                hasInputAuthority = false;
        }

        _pc.networkInputAuthority = hasInputAuthority;
        _pc.networkUseP1KeyScheme = !localPlay && hasInputAuthority;

        // Simulation authority for the player physics:
        //   - Local: both controlled here.
        //   - Networked: whoever has input authority also runs the physics for that character.
        // NetworkTransform set to Owner mode replicates the position from the controlling side.
        bool simAuth = localPlay || hasInputAuthority;
        _pc.networkSimulationAuthority = simAuth;
        if (_combat != null) _combat.networkSimulationAuthority = simAuth;

        // Damage application stays server-authoritative regardless of who detected the hit.
        if (_health != null)
            _health.networkSimulationAuthority = localPlay ||
                (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
    }

    /// <summary>
    /// Called by CombatSystem on the attacking client when a hit lands.
    /// Server applies the damage authoritatively so HP stays in sync.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestDamageServerRpc(ulong victimNetworkObjectId, int damage, Vector2 knockback)
    {
        if (NetworkManager.Singleton == null) return;

        // Server gate: ignore client-requested damage outside the active fighting window.
        GameStateManager gsm = GameStateManager.Instance;
        if (gsm != null && gsm.isNetworkAuthority &&
            gsm.State != GameStateManager.MatchState.Fighting)
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                victimNetworkObjectId, out NetworkObject victimNO))
            return;

        HealthManager victim = victimNO.GetComponent<HealthManager>();
        if (victim == null) return;

        victim.TakeDamage(damage, knockback);
    }
}
