# Main Menu and Lobby

`Assets/Scenes/MainMenu.unity` is the first scene in the build. It has a start screen (name, play local, host online, join by code, quit) and a lobby where every player picks a team and a player type, and the host sets the match length and starts the match. Local play runs as host on `OfflineTransport`; online play is a Multiplayer Services session over Unity Relay with secure WebSockets, so it works in the browser too.

## Building the scene

* **Death Ball > Build Main Menu Scene** regenerates the scene, the `NetworkManager` and `LobbyState` prefabs and the networked prefab list from scratch, and puts the menu first in the build settings with the arena after it.
* **Death Ball > Build Arena Scene** regenerates the arena and keeps it after the menu.

Layout lives in `Assets/Editor/MainMenuSceneBuilder.cs`. Hand edits to the scene are lost on the next rebuild. See [UI art](UiArt.md) for its art and type.

## Flow

1. **Play Local** hosts with no network, for solo and couch play (a controller's Start button does this too). **Host Online** signs in anonymously to Unity Services and creates a Relay session; the lobby shows its join code. **Join** joins the session whose code is typed in. The name is remembered.
2. In the lobby, every keyboard or controller that presses a button becomes a local player. Each one picks a team with left and right, and a player type with up and down. Choices are kept on the player's own machine and sent to `LobbyState` so everyone sees them.
3. The host cycles the match length (3, 5 or 7 minutes) and presses **Start Match** (or Enter, or Start on a controller). `GameSession` loads the arena for everyone through NGO's scene manager.
4. In the arena the server spawns the teams, then the match state. When the match state reaches a machine, which only happens once it has loaded the arena, each of its local players asks for a body on its team, of its chosen type if that one is free (`MatchState.RequestBody`). Players added mid-match ask straight away.
5. **Escape** or a controller's **Select** leaves: the host ends the session, a client leaves it. A client that loses its connection outside the menu goes back to the menu.

## Scripts

| Script | Where | Job |
| ------ | ----- | --- |
| `Menus.MainMenuController` | Menu canvas | Buttons, panels, connection status, team lists, each local player's choices |
| `Lobby.LobbyState` | Spawned by `GameSession` while hosting | Synced list of every local player on every machine, and the match length |
| `Lobby.GameSession` | NetworkManager prefab (persists) | Plays local, hosts or joins online, spawns the lobby, starts the match, leaves, returns to the menu on disconnect |
| `Networking.NetworkStarter` | Both scenes | Makes the NetworkManager from its prefab the first time, and in the arena starts local play when played directly |
| `Players.LocalPlayerManager` | Menu (persists) | Local players and their devices, carried from the lobby into the match |
| `Match.MatchSettings` | Service | Match length the host picked, read by `MatchController` |

## Decisions

* **One persistent NetworkManager, local player manager and system runner.** The NetworkManager is a prefab that `NetworkStarter` makes in whichever scene loads first, so the arena can still be played directly in the editor (starting local play). The local player manager and system runner from the menu last the whole session, and the arena's remove themselves.
* **Players ask for bodies once the match reaches them.** Players are made in the lobby, before there is a match to join. A human's body is moved by its owner, so one handed over while the owner was still loading would fall through a floor that wasn't there yet.
* **Each camera registers itself as the input view.** Stick directions are relative to the arena camera in a match and raw in the menu, where there is none.
* **Mouse-only UI.** Sticks and keys pick teams and types, so the UI doesn't navigate with them; the host can also start with Enter or a controller's Start button.

## Testing online in the editor

`Assets/Settings/PlayMode/OnlineTest.asset` is a Multiplayer Play Mode scenario with the main editor and one cloned editor. Make it the active scenario (Window > Multiplayer Play Mode), make sure **both** editors have `MainMenu` open (a clone keeps whichever scene it last had; if it plays the arena directly it hosts, and the main editor ends up joining it), then press Play. Host Online in one and join with its code from the other. Each editor signs in under its own profile, so they count as different players. The clone only takes keyboard input while its Game view has focus.

Tested this way after moving to NGO and Relay: hosting and joining by code over secure WebSockets; lobby sync both ways; starting a match; the joiner getting a body (a Runner, as the host already had the Bruiser) and moving it, with the host seeing the same positions; and local play through the menu. The WebGL build was also tested against the editor both ways round (browser hosting with the editor joining, and the editor hosting with the browser joining), including the match starting and each side's player moving and carrying the ball.

Before the move, on FishNet, also tested: remote attacks, passes and shots; the server staggering and teleporting a remote player; switching teammates; leaving and rejoining mid-match; and the host leaving. These go through the same code paths now but haven't been rechecked on NGO.
