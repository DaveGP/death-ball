# Death Ball - Technical Design

## Goals

* Every one of the 8 players on the field can be driven by a local player, a couch player, an online player or AI.
* Gameplay code (movement, combat, ball handling) never knows or cares what is driving a character.
* The server is the authority for the match, the ball and AI.

## Character Control Pipeline

Based on the Crewfall character controller.

```text
Device ──► IInputReader ──► InputProvider ──┐
                                            ├──► ICharacterInputSource ──► CharacterNetworkController ──► CharacterMotor ──► MovementController / ActionController
AI Brain ───────────────────────────────────┘
```

### Input Data

* `InputData` - raw per-frame input read from a device (move, look/aim, action flags).
* `CharacterInput` - the input a character consumes in FixedUpdate. This is the only thing gameplay code reads, and it is what gets sent to the server.
* `CharacterAction` - a flags enum split into `Continuous` (held: Sprint, Block) and `Buffered` (pressed: Pass, Shoot, Attack, HeavyAttack, Tackle, Dodge, Special, SwitchPlayer) groups. Buffered actions stay set until consumed so presses between fixed ticks are never lost.
* `CharacterInput.Move` and `Aim` are **world-space** directions on the XZ plane. `InputProvider` turns stick input into world space using the camera's yaw (`IInputView`), so AI can produce the same struct directly.

### Input Sources

`ICharacterInputSource.ConsumeCharacterInput()` is implemented by:

| Source              | Used for                                                         |
| ------------------- | ---------------------------------------------------------------- |
| `LocalPlayerController` | Local humans. Wraps an `InputProvider` and handles SwitchPlayer itself. |
| `InputProvider`     | Buffers one local player's `IInputReader` between Update and FixedUpdate. |
| `AIInputProvider`   | AI. Asks an `AIBrain` for its decision for this tick.            |
| `NullInputSource`   | Bodies nobody on this machine drives, knocked-out players, cutscenes. |

### Changes from Crewfall

1. **No global input source.** Crewfall uses `ServiceLocator.Get<ICharacterInputSource>()` because there is one local player per machine. In Death Ball each character has its input source assigned when it spawns and swapped at runtime with `SetInputSource`.
2. **One reader per device.** Each couch player gets their own `UnityInputReader` built on a `PlayerControls` instance restricted to that player's devices (`PlayerControls.devices`), rather than one shared `PlayerControls`. Keyboard and mouse join together as one player.
3. **Controllers own slots, not bodies.** A human controller belongs to a team slot and is bound to one character at a time. Switching teammates rebinds the controller to a new body, and the old body falls back to AI.

## Player Controllers

```text
PlayerController (abstract)  - Bind(body) sets the body's input source
├── LocalPlayerController    - a human on this machine (P1–P4), created when their device first presses a button
└── AIPlayerController       - one per body, server only
```

* A human on another machine has no controller on the server: the body is owned by their connection, and their own client binds its `LocalPlayerController` to it.
* `LocalPlayerManager` creates local players as devices join (alternating Home/Away by default for couch versus) and sends a `JoinTeamBroadcast` for each once the client is authenticated.
* `TeamController` (server) spawns its team's bodies, gives each its own `AIPlayerController`, assigns joining humans to free bodies, and handles switching: the human moves to the nearest teammate no human controls, and the old body goes back to its AI.

### Binding a body

* Server: `AssignHuman(connection, localIndex)` gives ownership to the human's connection and sets a `localPlayerIndex` SyncVar. `AssignAI(controller)` removes ownership and binds the AI.
* Owning client: when it owns the body and `localPlayerIndex` is set, it binds that local player. Ownership and the SyncVar can arrive in either order, so both trigger the check.
* Input authority: the owner drives a human's body, and the server drives a body nobody owns. The host's own players are owned by the host's client connection, so offline, couch and online go through the same path.

## AI

Two layers, so AI produces exactly the same input as a human:

* **Team tactics** (`TeamBrain`): assigns a role to each AI body every few ticks, such as chase the ball, carry, support, mark a player, guard the ring.
* **Per-body brain** (`IAIBrain`): turns its role into a `CharacterInput` each tick (move direction, aim, action flags). For now `ChaseBrain` runs at the nearest opponent and attacks.

AI runs only on the server. Because it goes through the same motor code as humans, it needs no special networking.

## Networking (FishNet)

* **Ownership**
  * A character controlled by a human is owned by that human's client connection.
  * AI characters and the ball are owned by the server.
  * Switching teammates transfers ownership of the old and new bodies on the server (`GiveOwnership` / `RemoveOwnership`).
* **Input flow** - same as Crewfall: the owner consumes input in FixedUpdate and sends it with `[ServerRpc(RunLocally = true)]`; the server runs `CharacterMotor`.
* **State** - movement state (velocity, grounded, gait) and combat state (health, stamina, stagger, KO) go back through SyncVars for animation and HUD.
* **Couch players online** - several local players on one client each get their own owned body. A client can own more than one character.
* **Offline** - single-player and couch play run as host with no remote clients, so there is only one code path.
* Bodies use `NetworkTransform` with client authority, as in Crewfall, and `NetworkObject` is set to not despawn when its owner disconnects; the body goes back to AI instead.
* `NetworkStarter` starts as host on play. Launch a build with `-client [-address <ip>]` to join one. A menu and lobby replace this later.
* Later: client-side prediction (FishNet's prediction) for responsive movement and hits, and server authority for anti-cheat.

## Match Systems

* `MatchController` (server) - clock, score, knockout tracking, win conditions, sudden death, resets after goals.
* `Ball` (server-owned) - carrier, overcharge heat, throws, loose-ball physics.
* `Goal` - ring trigger that reports scores to `MatchController`.
