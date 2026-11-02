# Main Menu and Lobby

`Assets/Scenes/MainMenu.unity` is the first scene in the build. It has a start screen (name, host, join by address, quit) and a lobby where every player picks a team and a player type, and the host sets the match length and starts the match. The networking is FishNet over Tugboat (port 7770).

## Building the scene

* **Death Ball > Build Main Menu Scene** regenerates the scene and the `LobbyState` prefab from scratch, and puts the menu first in the build settings with the arena after it.
* **Death Ball > Build Arena Scene** regenerates the arena and keeps it after the menu.

Layout lives in `Assets/Editor/MainMenuSceneBuilder.cs`. Hand edits to the scene are lost on the next rebuild. The UI is grey-box: Unity's built-in UI text and default controls.

## Flow

1. **Host** starts a server and a local client; **Join** connects to the address typed in (defaults to `localhost`). A controller's Start button also hosts. The name and last address are remembered.
2. In the lobby, every keyboard or controller that presses a button becomes a local player. Each one picks a team with left and right, and a player type with up and down. Choices are kept on the player's own machine and sent to `LobbyState` so everyone sees them.
3. The host cycles the match length (3, 5 or 7 minutes) and presses **Start Match** (or Enter, or Start on a controller). `GameSession` loads the arena as a FishNet global scene for everyone.
4. In the arena the server spawns the teams, then asks every client for its players (`RequestPlayersBroadcast`). Each local player asks for a body on its team, of its chosen type if that one is free (`JoinTeamBroadcast`). Clients connecting mid-match are asked once authenticated, and players added mid-match ask straight away.
5. **Escape** or a controller's **Select** leaves: the host stops the server, a client disconnects. A client that loses its connection outside the menu goes back to the menu.

## Scripts

| Script | Where | Job |
| ------ | ----- | --- |
| `Menus.MainMenuController` | Menu canvas | Buttons, panels, connection status, team lists, each local player's choices |
| `Lobby.LobbyState` | Spawned by `GameSession` while hosting | Synced list of every local player on every machine, and the match length |
| `Lobby.GameSession` | Menu NetworkManager (persists) | Spawns the lobby, starts the match, leaves, returns to the menu on disconnect |
| `Players.LocalPlayerManager` | Menu (persists) | Local players and their devices, carried from the lobby into the match |
| `Match.MatchSettings` | Service | Match length the host picked, read by `MatchController` |

## Decisions

* **One persistent NetworkManager, local player manager and system runner.** The menu's copies last the whole session. The arena keeps its own so it can still be played directly in the editor (starting as host through `NetworkStarter`); when it is loaded from the menu, FishNet discards the arena's NetworkManager, and the other two remove their own duplicates.
* **The server asks for players rather than clients announcing them.** Players are made in the lobby, before there is a match to join, so the arena's `MatchController` asks for them once its teams are spawned.
* **Each camera registers itself as the input view.** Stick directions are relative to the arena camera in a match and raw in the menu, where there is none.
* **Mouse-only UI.** Sticks and keys pick teams and types, so the UI doesn't navigate with them; the host can also start with Enter or a controller's Start button.

## Testing online in the editor

`Assets/Settings/PlayMode/OnlineTest.asset` is a Multiplayer Play Mode scenario with the main editor and one cloned editor. Make it the active scenario (Window > Multiplayer Play Mode), make sure **both** editors have `MainMenu` open (a clone keeps whichever scene it last had; if it plays the arena directly it hosts, and the main editor ends up joining it), then press Play. Host in one, join `localhost` from the other.

Tested this way: lobby sync both ways; starting a match; the remote player getting the team and type they picked and moving it; remote attacks, passes and shots landing on the server; the server staggering and teleporting a remote player (including the reset after a goal); switching teammates with ownership following; a client leaving and rejoining mid-match twice; the host leaving; and joining a directly played arena.

## Online decisions

* **Match objects only go to clients in the arena.** Bodies, the ball and the match state have a `NetworkObserver` with FishNet's `SceneCondition`, and are spawned into the arena scene. Without this, a client joining mid-match was sent them while still swapping scenes, and FishNet lost them. When the arena is played directly it was loaded by Unity, so `MatchController` adds each connection to it.
* **Players are asked for once a client is in the arena.** A human's body is moved by its owner, so one handed over while the owner was still loading fell through a floor that wasn't there yet.
* **`ReloadSafeSceneProcessor`.** Going back to the menu is a plain Unity scene load, which unloads FishNet's holding scenes, but FishNet kept using them. This processor makes them again when they are gone.
