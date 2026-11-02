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
* `CharacterInput` adds an `Aim` vector for passing and shooting.

### Input Sources

`ICharacterInputSource.ConsumeCharacterInput()` is implemented by:

| Source              | Used for                                                         |
| ------------------- | ---------------------------------------------------------------- |
| `InputProvider`     | Local humans. Wraps an `IInputReader` for one device.            |
| `AIInputProvider`   | AI. Asks an `AIBrain` for its decision for this tick.            |
| `NullInputSource`   | Knocked-out players and cutscenes.                               |
| `ReplayInputProvider` | Replays (later).                                               |

### Changes from Crewfall

1. **No global input source.** Crewfall uses `ServiceLocator.Get<ICharacterInputSource>()` because there is one local player per machine. In Death Ball each character has its input source assigned when it spawns and swapped at runtime with `SetInputSource`.
2. **One reader per device.** Each couch player gets their own `UnityInputReader` built on a `PlayerControls` instance paired to their device through `InputUser`, rather than one shared `PlayerControls`.
3. **Controllers own slots, not bodies.** A human controller belongs to a team slot and is bound to one character at a time. Switching teammates rebinds the controller to a new body, and the old body falls back to AI.

## Player Controllers

```text
PlayerController (abstract)
├── LocalPlayerController   - a device on this machine (P1–P4)
├── RemotePlayerController  - a human on another machine; only exists on the server/host
└── AIPlayerController      - one per AI body
```

* `TeamController` holds the team's 4 characters and its controllers, and handles switching.
* When a human switches, `TeamController` hands their old body to a new `AIPlayerController` and binds their controller to the new body.

## AI

Two layers, so AI produces exactly the same input as a human:

* **Team tactics** (`TeamBrain`): assigns a role to each AI body every few ticks, such as chase the ball, carry, support, mark a player, guard the ring.
* **Per-body brain** (`AIBrain`): turns its role into a `CharacterInput` each tick (move direction, aim, action flags).

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
* Later: client-side prediction (FishNet's prediction) for responsive movement and hits.

## Match Systems

* `MatchController` (server) - clock, score, knockout tracking, win conditions, sudden death, resets after goals.
* `Ball` (server-owned) - carrier, overcharge heat, throws, loose-ball physics.
* `Goal` - ring trigger that reports scores to `MatchController`.
