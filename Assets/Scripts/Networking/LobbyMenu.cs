using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyMenu : MonoBehaviour
{
    [Header("Mode Buttons")]
    public Button localPlayButton;
    public Button hostButton;
    public Button joinButton;
    public Button backButton;

    [Header("Join Inputs")]
    public TMP_InputField ipField;
    public TMP_InputField portField;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    [Header("Scene")]
    public string gameplaySceneName = "GameplayScene";
    public string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        if (localPlayButton != null) localPlayButton.onClick.AddListener(OnLocalPlay);
        if (hostButton != null) hostButton.onClick.AddListener(OnHost);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoin);
        if (backButton != null) backButton.onClick.AddListener(OnBack);

        if (ArenaNetworkManager.Instance != null)
            ArenaNetworkManager.Instance.OnNetworkError += OnError;

        if (ipField != null && ArenaNetworkManager.Instance != null)
            ipField.text = ArenaNetworkManager.Instance.LastJoinAddress;

        if (portField != null && ArenaNetworkManager.Instance != null)
            portField.text = ArenaNetworkManager.Instance.LastJoinPort.ToString();

        SetStatus(string.Empty);

        // If the user came back from a previous match, the NetworkManager may still be running.
        // Wait until any active session has fully shut down before letting them host/join again.
        StartCoroutine(EnsureCleanNetworkState());
    }

    private IEnumerator EnsureCleanNetworkState()
    {
        if (NetworkManager.Singleton == null) yield break;

        bool wasRunning = NetworkManager.Singleton.IsListening ||
                          NetworkManager.Singleton.IsHost ||
                          NetworkManager.Singleton.IsServer ||
                          NetworkManager.Singleton.IsClient;

        if (!wasRunning) yield break;

        SetStatus("Cleaning up previous session...");
        if (ArenaNetworkManager.Instance != null)
            ArenaNetworkManager.Instance.Shutdown();
        else
            NetworkManager.Singleton.Shutdown();

        // Wait until NGO has fully stopped listening.
        float timeout = 5f;
        while (timeout > 0f &&
               (NetworkManager.Singleton.IsListening ||
                NetworkManager.Singleton.ShutdownInProgress))
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // Defensive: one more frame so any async cleanup completes.
        yield return null;
        SetStatus(string.Empty);
    }

    private void OnDestroy()
    {
        if (localPlayButton != null) localPlayButton.onClick.RemoveListener(OnLocalPlay);
        if (hostButton != null) hostButton.onClick.RemoveListener(OnHost);
        if (joinButton != null) joinButton.onClick.RemoveListener(OnJoin);
        if (backButton != null) backButton.onClick.RemoveListener(OnBack);

        if (ArenaNetworkManager.Instance != null)
            ArenaNetworkManager.Instance.OnNetworkError -= OnError;
    }

    private void OnLocalPlay()
    {
        if (ArenaNetworkManager.Instance != null)
            ArenaNetworkManager.Instance.StartLocal();

        SceneManager.LoadScene(gameplaySceneName);
    }

    private void OnHost()
    {
        ushort port = ParsePort();

        if (ArenaNetworkManager.Instance == null)
        {
            SetStatus("ArenaNetworkManager is not in the scene.");
            return;
        }

        if (!ArenaNetworkManager.Instance.StartHost(port))
            return;

        SetStatus($"Hosting on port {port}. Loading arena...");

        // Use Netcode's scene manager so scene NetworkObjects spawn correctly and clients are auto-loaded.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
        else
        {
            // Fallback to regular scene load if Netcode isn't running (shouldn't happen after StartHost).
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    private void OnJoin()
    {
        string ip = ipField != null ? ipField.text.Trim() : "127.0.0.1";
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

        ushort port = ParsePort();

        if (ArenaNetworkManager.Instance == null)
        {
            SetStatus("ArenaNetworkManager is not in the scene.");
            return;
        }

        if (!ArenaNetworkManager.Instance.StartClient(ip, port))
            return;

        // The server controls scene transitions for clients — do NOT call SceneManager.LoadScene here.
        // The host's NetworkSceneManager.LoadScene call will tell us to load the gameplay scene automatically.
        SetStatus($"Connecting to {ip}:{port}...");
    }

    private void OnBack()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private ushort ParsePort()
    {
        if (portField != null && ushort.TryParse(portField.text, out ushort p))
            return p;
        return 7777;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private void OnError(string msg)
    {
        SetStatus(msg);
    }
}
