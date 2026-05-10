using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Lives in the GameplayScene. On the server (Host), when a client connects, it
/// transfers ownership of Player 2 to that client so each player only controls
/// their own character. Drag Player1 and Player2 NetworkObjects into the
/// inspector slots after the scene is set up.
/// </summary>
public class MultiplayerSceneBinder : MonoBehaviour
{
    [Header("Drag the Player1 and Player2 GameObjects from the Hierarchy here")]
    public NetworkObject player1NetworkObject;
    public NetworkObject player2NetworkObject;

    private bool _disconnectHandled;

    private void OnEnable()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        // If we're networked, hold the match in "waiting for opponent" until the client connects.
        // Local play (no NGO running) doesn't reach this branch and starts immediately.
        if (NetworkManager.Singleton.IsServer)
        {
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.waitForRemotePlayer = true;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsServer) return;

        if (clientId == NetworkManager.ServerClientId) return;

        if (player2NetworkObject == null)
        {
            Debug.LogWarning("[MultiplayerSceneBinder] player2NetworkObject is not assigned in the Inspector.");
            return;
        }

        StartCoroutine(TransferOwnershipWhenSpawned(player2NetworkObject, clientId));
    }

    private IEnumerator TransferOwnershipWhenSpawned(NetworkObject target, ulong clientId)
    {
        // Scene-placed NetworkObjects can take a frame or two to fully spawn after the
        // client's scene-sync completes. Wait until the object is spawned before
        // attempting ownership transfer, otherwise NGO throws SpawnStateException.
        const float maxWait = 5f;
        float waited = 0f;

        while (target != null && !target.IsSpawned && waited < maxWait)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (target == null) yield break;

        if (!target.IsSpawned)
        {
            Debug.LogWarning("[MultiplayerSceneBinder] Player2 NetworkObject didn't spawn within timeout — skipping ownership transfer.");
            yield break;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            yield break;

        if (target.OwnerClientId != clientId)
        {
            try
            {
                target.ChangeOwnership(clientId);
                Debug.Log($"[MultiplayerSceneBinder] Player2 ownership transferred to client {clientId}.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MultiplayerSceneBinder] Failed to transfer Player2 ownership: {ex.Message}");
            }
        }

        // Brief settle delay so NetworkAnimator + NetworkTransform have caught up
        // before the round intro begins.
        yield return new WaitForSeconds(0.3f);

        if (GameStateManager.Instance != null && GameStateManager.Instance.isNetworkAuthority)
            GameStateManager.Instance.BeginMatch();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        // NetworkManager may already be torn down by the time this fires (e.g. on shutdown).
        if (NetworkManager.Singleton == null) return;

        // Guard against multiple disconnect callbacks firing during shutdown.
        if (_disconnectHandled) return;

        // If WE are the client and we got disconnected, return to main menu.
        if (!NetworkManager.Singleton.IsServer)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                _disconnectHandled = true;
                ReturnToMainMenu();
            }
            return;
        }

        // We are the server: a remote client left.
        if (clientId == NetworkManager.ServerClientId) return; // server disconnecting itself — ignore

        _disconnectHandled = true;

        // Reclaim Player2 ownership.
        if (player2NetworkObject != null && player2NetworkObject.IsSpawned &&
            player2NetworkObject.OwnerClientId == clientId)
        {
            try
            {
                player2NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
            }
            catch (System.Exception) { /* shutdown race — ignore */ }
        }

        // Properly end the match — stop coroutines, lock players, show forfeit screen.
        // Without this the timer kept running and the round loop continued in the background.
        if (GameStateManager.Instance != null && GameStateManager.Instance.isNetworkAuthority)
        {
            GameStateManager.Instance.EndMatchByForfeit("OPPONENT LEFT\nMATCH ENDED");
        }
    }

    private void ReturnToMainMenu()
    {
        if (ArenaNetworkManager.Instance != null)
            ArenaNetworkManager.Instance.Shutdown();

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
