# Minimal Lobby + Relay + NGO (Host/Join)

This project is wired for a tiny multiplayer flow using Unity Gaming Services (Lobby + Relay) and Netcode for GameObjects (NGO): two buttons (Host, Join) and no PlayerPrefab on the NetworkManager. Player objects are spawned manually by a `PlayerSpawner` component when clients connect.

## Requirements

- Packages (Package Manager)
  - Netcode for GameObjects (com.unity.netcode.gameobjects)
  - Unity Transport (com.unity.transport)
  - UGS Core (com.unity.services.core)
  - Authentication (com.unity.services.authentication)
  - Lobby (com.unity.services.lobbies)
  - Relay (com.unity.services.relay)
- In the scene:
  - A `NetworkManager` with `UnityTransport` component.
  - Leave `Player Prefab` empty on the `NetworkManager`.
  - Add `MultiplayerLauncher` to any UI GameObject (or an empty) and assign:
    - Host button to `hostButton`
    - Join button to `refreshButton` (treated as Join)
    - Optional: TMP input to `joinCodeInput` (paste code to join directly)
    - Optional: TMP label to `joinCodeLabel` (shows host code)
  - (Optional) Add `PlayerSpawner` somewhere persistent (same scene). Assign a player prefab with `NetworkObject`.

## How it works

- Host:
  1. Initializes UGS and signs in anonymously.
  2. Creates a Relay allocation and starts NGO host through UnityTransport (DTLS/UDP).
  3. Creates a Lobby and writes the Relay Join Code to `lobby.Data["joinCode"]`.
  4. Heartbeats the lobby every ~15s.
- Join:
  - If a code is typed, it joins Relay with that code and starts a client.
  - If no code is typed, it queries lobbies by name and uses the first lobby's `joinCode`.

Auto player-spawn is disabled via Connection Approval (CreatePlayerObject=false). Use `PlayerSpawner` to spawn player objects on the server when clients connect.

## Notes

- If UDP is blocked, you can switch to WSS/TCP by replacing `"dtls"` with `"wss"` where `RelayServerData` is created.
- To scope which lobbies are joined automatically, set the `lobbyName` string on `MultiplayerLauncher` in the Inspector.
- This is a minimal sample: add error handling and UI feedback for production.
