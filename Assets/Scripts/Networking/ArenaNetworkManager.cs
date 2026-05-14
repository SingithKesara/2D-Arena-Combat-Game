using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ArenaNetworkManager : MonoBehaviour
{
    public static ArenaNetworkManager Instance { get; private set; }

    public enum SessionMode { Local, Host, Client }

    public SessionMode CurrentMode { get; private set; } = SessionMode.Local;
    public string LastJoinAddress { get; private set; } = "127.0.0.1";
    public ushort LastJoinPort { get; private set; } = 7777;
    public string LastError { get; private set; }

    public event Action<SessionMode> OnSessionModeChanged;
    public event Action<string> OnNetworkError;
    public event Action OnClientConnected;
    public event Action OnClientDisconnected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsNetworked =>
        NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsHost ||
         NetworkManager.Singleton.IsServer ||
         NetworkManager.Singleton.IsClient);

    public bool IsServerAuthority =>
        NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer);

    public void StartLocal()
    {
        ShutdownIfActive();

        CurrentMode = SessionMode.Local;
        OnSessionModeChanged?.Invoke(CurrentMode);
    }

    public bool StartHost(ushort port)
    {
        ShutdownIfActive();

        if (IsNetworkBusy())
        {
            ReportError("Previous network session is still shutting down — try again in a moment.");
            return false;
        }

        UnityTransport transport = ResolveTransport();
        if (transport == null) return false;

        transport.SetConnectionData("0.0.0.0", port);

        bool ok = NetworkManager.Singleton.StartHost();
        if (!ok)
        {
            ReportError("NetworkManager.StartHost returned false.");
            return false;
        }

        CurrentMode = SessionMode.Host;
        LastJoinPort = port;
        SubscribeNetworkEvents();
        OnSessionModeChanged?.Invoke(CurrentMode);
        return true;
    }

    public bool StartClient(string ip, ushort port)
    {
        ShutdownIfActive();

        if (IsNetworkBusy())
        {
            ReportError("Previous network session is still shutting down — try again in a moment.");
            return false;
        }

        UnityTransport transport = ResolveTransport();
        if (transport == null) return false;

        transport.SetConnectionData(ip, port);

        bool ok = NetworkManager.Singleton.StartClient();
        if (!ok)
        {
            ReportError("NetworkManager.StartClient returned false.");
            return false;
        }

        CurrentMode = SessionMode.Client;
        LastJoinAddress = ip;
        LastJoinPort = port;
        SubscribeNetworkEvents();
        OnSessionModeChanged?.Invoke(CurrentMode);
        return true;
    }

    /// <summary>
    /// Finds (or auto-attaches) a UnityTransport on the NetworkManager and ensures
    /// NetworkConfig.NetworkTransport points at it. Avoids the "No transport has been selected" error
    /// when the inspector slot wasn't wired up.
    /// </summary>
    private UnityTransport ResolveTransport()
    {
        if (NetworkManager.Singleton == null)
        {
            ReportError("NetworkManager.Singleton is null — add a NetworkManager to the scene.");
            return null;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
            Debug.Log("[ArenaNetworkManager] UnityTransport was missing on NetworkManager — added one at runtime.");
        }

        if (NetworkManager.Singleton.NetworkConfig == null)
        {
            ReportError("NetworkManager.NetworkConfig is null. Re-create the NetworkManager GameObject.");
            return null;
        }

        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport != transport)
            NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;

        return transport;
    }

    public void Shutdown()
    {
        ShutdownIfActive();

        CurrentMode = SessionMode.Local;
        OnSessionModeChanged?.Invoke(CurrentMode);
    }

    private void ShutdownIfActive()
    {
        if (NetworkManager.Singleton == null) return;

        UnsubscribeNetworkEvents();

        if (NetworkManager.Singleton.IsHost ||
            NetworkManager.Singleton.IsServer ||
            NetworkManager.Singleton.IsClient ||
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown(discardMessageQueue: true);
        }
    }

    private bool IsNetworkBusy()
    {
        if (NetworkManager.Singleton == null) return false;
        return NetworkManager.Singleton.IsListening ||
               NetworkManager.Singleton.ShutdownInProgress;
    }

    private void SubscribeNetworkEvents()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void UnsubscribeNetworkEvents()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        OnClientConnected?.Invoke();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        OnClientDisconnected?.Invoke();

        if (CurrentMode == SessionMode.Client && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ReportError("Disconnected from host.");
            CurrentMode = SessionMode.Local;
            OnSessionModeChanged?.Invoke(CurrentMode);
        }
    }

    private void ReportError(string msg)
    {
        LastError = msg;
        Debug.LogWarning($"[ArenaNetworkManager] {msg}");
        OnNetworkError?.Invoke(msg);
    }

    private void OnDestroy()
    {
        UnsubscribeNetworkEvents();

        if (Instance == this)
            Instance = null;
    }
}
