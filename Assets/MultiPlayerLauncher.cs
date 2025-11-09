using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies; // for CreateLobbyOptions, QueryLobbiesOptions, LobbyService
using Unity.Services.Lobbies.Models; // for Lobby, DataObject
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization; // FormerlySerializedAs
using System.Globalization;

// Minimal host/join via UGS Lobby + Relay + NGO.
// - Two buttons: Host and Join (Join can quick-join if no code is provided).
// - No PlayerPrefab required on NetworkManager (we disable auto player spawn via ConnectionApproval).
// - Join code is stored in the Lobby's public data so Join can auto-connect without extra UI.
public class MultiplayerLauncher : MonoBehaviour
{
    [Header("UI")]
    public Button hostButton;
    [Tooltip("Join button. If a Join Code field is provided, it will use it; otherwise it will join any open lobby.")]
    [FormerlySerializedAs("refreshButton")] public Button joinButton;

    [Tooltip("Optional: input field to paste a Relay Join Code. If empty, we'll quick-join a lobby instead.")]
    public TMP_InputField joinCodeInput;
    [Tooltip("Optional: label to display the host's generated Join Code.")]
    public TMP_Text joinCodeLabel;
    [Tooltip("Optional: status label for progress and errors")] public TMP_Text statusLabel;

    [Header("Settings")]
    public string lobbyName = "GoofyRoom";
    [Range(2, 16)] public int maxPlayers = 4; // includes host
    [Header("Auth (optional)")]
    [Tooltip("Authentication profile to ensure unique local player IDs. Leave empty to auto-pick per instance.")]
    public string profileOverride = "";

    [Header("Debug/Test (optional)")]
    [Tooltip("If true, only try UDP/DTLS. Useful to validate UDP path explicitly.")]
    public bool forceUdpOnly = false;
    [Tooltip("If true, only try WSS/TCP 443. Helps test around blocked UDP networks.")]
    public bool forceWssOnly = false;
    [Tooltip("When quick-joining with empty code, ignore lobbies older than this many seconds. 0 disables the age gate.")]
    public int maxLobbyAgeSeconds = 60;

    private Lobby _hostLobby;
    private Coroutine _heartbeat;
    private bool _hosting;
    private string _latchedJoinCode;
    private float _hostAllocationTime;
    private string _hostedMode; // "dtls" or "wss"

    private void OnEnable()
    {
    if (hostButton != null) hostButton.onClick.AddListener(() => _ = HostAsync());
    if (joinButton != null) joinButton.onClick.AddListener(() => _ = JoinAsync());

        // No ConnectionApproval: leave Player Prefab empty to avoid auto-spawn; we'll spawn manually when needed.
        try { var nm = NetworkManager.Singleton; if (nm != null) nm.NetworkConfig.ConnectionApproval = false; } catch { }

        // Helpful log when any client connects (so you can see when a non-host joins)
        try
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnAnyClientConnected;
                // Surface transport failures (e.g., allocation invalidated/network blocks)
                NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
            }
        }
        catch { }
    }

    private void OnDisable()
    {
    if (hostButton != null) hostButton.onClick.RemoveAllListeners();
    if (joinButton != null) joinButton.onClick.RemoveAllListeners();
        if (_heartbeat != null) StopCoroutine(_heartbeat);
        _heartbeat = null;

        try
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnAnyClientConnected;
                NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
            }
        }
        catch { }
    }

    // HOST: create Relay allocation, start NGO host, then create a Lobby and publish the join code in lobby data
    public async Task HostAsync()
    {
        try
        {
            if (_hosting)
            {
                Debug.LogWarning("[Launcher] HostAsync ignored: already hosting.");
                return;
            }
            await EnsureUgsAsync();
            Debug.Log("[Launcher] Host: EnsureUgsAsync OK.");

            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("NetworkManager not present in the scene.");
                return;
            }
            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport not found on NetworkManager.");
                return;
            }

            // 1) Relay allocation (Host)
            int clientSlots = Mathf.Max(1, maxPlayers - 1);
            SetStatus("Allocating Relay (Host)…");
            _hostAllocationTime = Time.realtimeSinceStartup;
            var allocation = await RelayService.Instance.CreateAllocationAsync(clientSlots);
            Debug.Log($"[Launcher] Host: Allocation OK ip={allocation.RelayServer.IpV4} port={allocation.RelayServer.Port}");
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("[Launcher] Host: JoinCode OK");
            if (string.IsNullOrEmpty(_latchedJoinCode)) _latchedJoinCode = joinCode; // latch first code

            // Try DTLS then WSS fallback
            bool hostStarted = false; Exception hostEx = null; _hostedMode = null;
            var modes = forceWssOnly ? new[] { "wss" } : (forceUdpOnly ? new[] { "dtls" } : new[] { "dtls", "wss" });
            foreach (var mode in modes)
            {
                try
                {
                    transport.UseWebSockets = mode == "wss";
                    transport.SetRelayServerData(
                        allocation.RelayServer.IpV4,
                        (ushort)allocation.RelayServer.Port,
                        allocation.AllocationIdBytes,
                        allocation.Key,
                        allocation.ConnectionData,
                        null,
                        mode == "dtls"
                    );
                    SetStatus($"Starting Host ({mode.ToUpper()})…");
                    if (nm.StartHost()) { Debug.Log("[Launcher] Host: StartHost OK"); hostStarted = true; _hostedMode = mode; break; }
                }
                catch (Exception ex)
                {
                    hostEx = ex;
                }
            }
            if (!hostStarted)
            {
                Debug.LogError("Failed to start host via Relay." + (hostEx!=null?" " + hostEx.Message:""));
                SetStatus("Host start failed.");
                return;
            }
            _hosting = true;
            Debug.Log($"Host started. Relay Join Code: {_latchedJoinCode}");
            if (joinCodeLabel != null) joinCodeLabel.text = $"Code: {_latchedJoinCode}";
            SetStatus($"Hosting. Code: {_latchedJoinCode}");

            // 3) Create a lobby and publish the code so clients can quick-join
            long allocAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var createOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "joinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) },
                    { "allocAt",  new DataObject(DataObject.VisibilityOptions.Public, allocAt.ToString(CultureInfo.InvariantCulture)) },
                    { "mode",     new DataObject(DataObject.VisibilityOptions.Public, (_hostedMode ?? "dtls").ToUpperInvariant()) }
                }
            };
            _hostLobby = await Unity.Services.Lobbies.LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createOptions);
            Debug.Log("[Launcher] Host: CreateLobby OK");
            _heartbeat = StartCoroutine(LobbyHeartbeatCoroutine(_hostLobby.Id));

            // optional: hide buttons after success
            TryHideLauncherUi();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Host failed: {ex.Message}");
        }
    }

    // JOIN: if a code is provided, use it; otherwise query the first available lobby and use its published code
    public async Task JoinAsync()
    {
        try
        {
            await EnsureUgsAsync();
            Debug.Log("[Launcher] Client: EnsureUgsAsync OK.");

            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("NetworkManager not present in the scene.");
                return;
            }
            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport not found on NetworkManager.");
                return;
            }

            string code = joinCodeInput != null ? (joinCodeInput.text ?? string.Empty).Trim().ToUpperInvariant() : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                // Query lobbies and pick the newest, within freshness gate if set
                var q = new QueryLobbiesOptions
                {
                    Count = 20,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                    }
                };
                var result = await Unity.Services.Lobbies.LobbyService.Instance.QueryLobbiesAsync(q);
                Debug.Log("[Launcher] Client: QueryLobbies OK");
                Lobby chosen = null;
                long newestAllocTs = long.MinValue;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var l in result.Results)
                {
                    if (l.Data != null && l.Data.TryGetValue("joinCode", out var data) && !string.IsNullOrEmpty(data.Value))
                    {
                        long allocTs = long.MinValue;
                        if (l.Data.TryGetValue("allocAt", out var allocAt) && long.TryParse(allocAt.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                            allocTs = parsed;
                        // Skip too-old lobbies when a freshness gate is set
                        if (maxLobbyAgeSeconds > 0 && allocTs > 0)
                        {
                            var age = now - allocTs;
                            if (age > maxLobbyAgeSeconds) continue;
                        }
                        // Prefer newest allocation timestamp; if none, still consider but lower priority
                        if (chosen == null || allocTs > newestAllocTs)
                        {
                            chosen = l; newestAllocTs = allocTs;
                        }
                    }
                }
                // If none met the freshness gate, fall back to the absolute newest so we still try something
                if (chosen == null && result.Results != null)
                {
                    foreach (var l in result.Results)
                    {
                        if (l.Data != null && l.Data.TryGetValue("joinCode", out var data) && !string.IsNullOrEmpty(data.Value))
                        {
                            long allocTs = long.MinValue;
                            if (l.Data.TryGetValue("allocAt", out var allocAt) && long.TryParse(allocAt.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                                allocTs = parsed;
                            if (chosen == null || allocTs > newestAllocTs)
                            {
                                chosen = l; newestAllocTs = allocTs;
                            }
                        }
                    }
                }
                if (chosen == null)
                {
                    Debug.LogWarning("No joinable lobby found. Ask the host to start first.");
                    return;
                }
                code = chosen.Data["joinCode"].Value.ToUpperInvariant();
                Debug.Log($"[Launcher] Client: Selected lobby '{chosen.Name}' joinCode={code}");

                // Age warning if allocation is old
                if (chosen.Data.TryGetValue("allocAt", out var allocAtData) && long.TryParse(allocAtData.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chosenAllocTs))
                {
                    var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - chosenAllocTs;
                    if (age > 25)
                    {
                        Debug.LogWarning($"[Launcher] Join code may be stale (age {age}s). If connect fails, ask host to re-host.");
                        SetStatus($"Code age {age}s; may be stale.");
                    }
                }
            }
            else
            {
                code = code.ToUpperInvariant();

                // If user entered a code, try to find a matching lobby to validate age
                try
                {
                    var rq = new QueryLobbiesOptions { Count = 25 };
                    var rr = await Unity.Services.Lobbies.LobbyService.Instance.QueryLobbiesAsync(rq);
                    foreach (var l in rr.Results)
                    {
                        if (l.Data != null && l.Data.TryGetValue("joinCode", out var d) && string.Equals(d.Value?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        {
                            if (l.Data.TryGetValue("allocAt", out var allocAtData) && long.TryParse(allocAtData.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var enteredAllocTs))
                            {
                                var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - enteredAllocTs;
                                if (age > 25)
                                {
                                    Debug.LogWarning($"[Launcher] Entered join code appears older ({age}s). It might be expired or host lost Relay.");
                                    SetStatus($"Code age {age}s; may be stale.");
                                }
                            }
                            break;
                        }
                    }
                }
                catch { }
            }

            bool ok = await TryClientConnectWithFallback(nm, transport, code, 30f);
            Debug.Log(ok ? "[Launcher] Client connected to host via Relay." : "[Launcher] Client did not connect within timeout.");
            SetStatus(ok ? "Connected." : "Connection failed.");
            TryHideLauncherUi();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Join failed: {ex.Message}");
            SetStatus("Join failed: " + Short(ex.Message));
        }
    }

    private async Task<bool> WaitForClientConnectedAsync(float timeoutSeconds)
    {
        float end = Time.realtimeSinceStartup + Mathf.Max(0.5f, timeoutSeconds);
        while (Time.realtimeSinceStartup < end)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsClient && nm.IsConnectedClient) return true;
            await Task.Delay(150);
        }
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsConnectedClient;
    }

    private async Task<bool> TryClientConnectWithFallback(NetworkManager nm, UnityTransport transport, string code, float timeoutSeconds)
    {
        var modes = forceWssOnly ? new[] { "wss" } : (forceUdpOnly ? new[] { "dtls" } : new[] { "dtls", "wss" });
        foreach (var mode in modes)
        {
            try
            {
                SetStatus($"Connecting ({mode.ToUpper()})…"); Debug.Log("[Launcher] Client: JoinAllocation starting");
                // Handle 429 Too Many Requests with a brief backoff and one retry
                JoinAllocation joinAllocation = null;
                int attempts = 0;
                while (attempts < 2 && joinAllocation == null)
                {
                    attempts++;
                    try
                    {
                        joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);
                    }
                    catch (Exception ex) when (ex.Message != null && ex.Message.IndexOf("Too Many Requests", StringComparison.OrdinalIgnoreCase) >= 0 && attempts < 2)
                    {
                        Debug.LogWarning("[Launcher] 429 Too Many Requests; backing off 1.5s and retrying once…");
                        await Task.Delay(1500);
                    }
                }
                if (joinAllocation == null) throw new Exception("JoinAllocation failed after retries");
                Debug.Log($"[Launcher] Client: JoinAllocation OK ip={joinAllocation.RelayServer.IpV4} port={joinAllocation.RelayServer.Port}");
                transport.UseWebSockets = mode == "wss";
                transport.SetRelayServerData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData,
                    mode == "dtls"
                );
                if (!nm.StartClient())
                {
                    Debug.LogWarning($"StartClient failed for {mode}");
                }
                else if (await WaitForClientConnectedAsync(timeoutSeconds))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($" {mode} connect exception: {ex.Message}");
                // If join code appears invalid, try to re-query for the same code first; if not found, pick any open lobby
                if (ex.Message != null && ex.Message.IndexOf("join code not found", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        var rq = new QueryLobbiesOptions { Count = 25 };
                        var rr = await Unity.Services.Lobbies.LobbyService.Instance.QueryLobbiesAsync(rq);
                        string matched = null;
                        foreach (var l in rr.Results)
                        {
                            if (l.Data != null && l.Data.TryGetValue("joinCode", out var d) && !string.IsNullOrEmpty(d.Value))
                            {
                                var candidate = d.Value.Trim().ToUpperInvariant();
                                if (string.Equals(candidate, code, StringComparison.OrdinalIgnoreCase))
                                {
                                    matched = candidate; break;
                                }
                                if (matched == null) matched = candidate; // fallback to first available if exact match not found
                            }
                        }
                        if (!string.IsNullOrEmpty(matched))
                        {
                            code = matched;
                            Debug.Log("[Launcher] Client: Refreshed join code=" + code);
                        }

                        // immediate retry once with refreshed code
                        var joinAllocation2 = await RelayService.Instance.JoinAllocationAsync(code);
                        transport.UseWebSockets = mode == "wss";
                        transport.SetRelayServerData(
                            joinAllocation2.RelayServer.IpV4,
                            (ushort)joinAllocation2.RelayServer.Port,
                            joinAllocation2.AllocationIdBytes,
                            joinAllocation2.Key,
                            joinAllocation2.ConnectionData,
                            joinAllocation2.HostConnectionData,
                            mode == "dtls"
                        );
                        if (nm.StartClient() && await WaitForClientConnectedAsync(timeoutSeconds)) return true;
                    }
                    catch (Exception ex2)
                    {
                        Debug.LogWarning("[Launcher] Client: refresh+retry failed: " + ex2.Message);
                    }
                }
            }
            // reset before next attempt
            try { if (nm.IsClient || nm.IsConnectedClient) nm.Shutdown(); } catch { }
            await Task.Delay(250);
        }
        return false;
    }

    private void OnTransportFailure()
    {
        Debug.LogWarning("[Launcher] Transport failure detected. Relay allocation likely invalid. If hosting, please re-host.");
        SetStatus("Transport failure. Please re-host or re-join.");
        _hosting = false;
    }

    private void OnAnyClientConnected(ulong clientId)
    {
        Debug.Log($"[Launcher] Client connected: {clientId}");
        SetStatus(NetworkManager.Singleton!=null && NetworkManager.Singleton.IsHost && clientId==NetworkManager.Singleton.LocalClientId ? "Host active" : $"Client {clientId} joined");
    }

    private IEnumerator LobbyHeartbeatCoroutine(string lobbyId)
    {
        var wait = new WaitForSecondsRealtime(15f);
        while (true)
        {
            var task = Unity.Services.Lobbies.LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            while (!task.IsCompleted) yield return null;
            yield return wait;
        }
    }

    private async Task EnsureUgsAsync()
    {
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            // Switch to a unique profile per process to avoid identical PlayerIds when testing on same PC
            try
            {
                string profile = string.IsNullOrWhiteSpace(profileOverride)
                    ? (Application.isEditor ? "editor" : ("build-" + Guid.NewGuid().ToString("N").Substring(0, 8)))
                    : profileOverride.Trim();
                AuthenticationService.Instance.SwitchProfile(profile);
                Debug.Log("[Launcher] Auth profile=" + profile);
            }
            catch { }
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private void TryHideLauncherUi()
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        else
        {
            if (hostButton) hostButton.interactable = false;
            if (joinButton) joinButton.interactable = false;
        }
    }

    private void SetStatus(string msg)
    {
        if (statusLabel != null) statusLabel.text = msg;
        Debug.Log("[Launcher] " + msg);
    }

    private string Short(string s) => string.IsNullOrEmpty(s) ? s : (s.Length > 80 ? s.Substring(0, 80) + "…" : s);
}
