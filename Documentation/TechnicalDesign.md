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
* **Per-body brain** (`IAIBrain`): turns its role into a `CharacterInput` each tick (move direction, aim, action flags). For now `BallBrain` does both jobs: the AI nearest a loose ball goes for it, the carrier runs at goal and shoots in range (passing to an open teammate or swerving when pressed), the AI nearest an opponent carrier hunts them, and the rest support in their lane or mark.

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

* `MatchController` (server) - spawns the teams, ball and `MatchState`, scores goals and resets for the kickoff after each one. Clock, knockout tracking, win conditions and sudden death still to come.
* `MatchState` (server-owned, spawned) - the score as SyncVars, and a `GoalScored` event raised on every client for the HUD.
* `Ball` (server-owned) - see below. Overcharge heat still to come.

## The Ball

* Owned by the server and simulated only there; clients see it through a `carrier` SyncVar and a `NetworkTransform`. It registers itself in the `ServiceLocator` on every machine, so any code can find it with `TryGet`.
* **Pickup and catching** are the same thing: each tick a loose ball below head height goes to the nearest body within reach. The ball doesn't collide with players, so a pass through a crowd can be intercepted by anyone it passes.
* **Carrying** - the ball is kinematic and held in front of the carrier, who moves at `CarrySpeedMultiplier` and can't attack (tackling still works).
* **Pass** snaps to the teammate nearest the aim (within 35°) and leads them by their velocity. **Shoot** throws hard along the aim, or facing if not aiming.
* **Stripping** - any attack or tackle that lands on an opponent carrier knocks the ball back towards the hitter and staggers the carrier for a second. A new carrier can't be stripped for a second, and a stripped or throwing player can't grab the ball straight back. Without these two rules, two players just traded the ball on the spot.
* **Stagger** is a tick-based SyncVar on the body, so the owning client and the server agree on it; a staggered body's input is replaced with nothing.
* Only the server acts on the ball. Actions also run on the owning client (`RunLocally`), but there they just draw debug rays.
* `Goal` - a raised ring facing the field. `IsThrough(from, to)` checks the ball's path between ticks, so fast shots can't skip past it.

## Scoring

* Each physics tick the `MatchController` checks the ball's movement against the goal the thrower's team attacks. Only a thrown ball scores (not one knocked loose), and only in the right goal, so there are no own goals.
* 1 point, 2 if thrown from behind the halfway line, +1 if the thrower caught a teammate's pass before it touched the ground. The ball keeps track of the last throw and whether the catch came off an air pass.
* After a goal the ball is out of play for a few seconds, then everyone is teleported back to their spawn points and the ball kicks off again. A human's body is teleported on its owner's machine (`TargetRpc`), since the owner has authority over its transform, and `NetworkTransform.Teleport` makes everyone else snap instead of sliding.
* **Shot assist** - a shot aimed within 30° of the goal is lofted to arrive at ring height. The aim still decides left and right, so shots can miss, and the AI adds a few degrees of random spread.
