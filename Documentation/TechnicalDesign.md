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
* **Per-body brain** (`IAIBrain`): turns its role into a `CharacterInput` each tick (move direction, aim, action flags). For now `BallBrain` does both jobs:
  * The AI nearest a loose ball goes for it.
  * The carrier runs at goal and shoots in range (with a few degrees of spread), passing to an open teammate or swerving when pressed, and dodging clear of a defender right on top of it.
  * When the other team has the ball, the AI nearest the carrier hunts them with tackles, the AI nearest our ring keeps goal (stands between carrier and ring with guard up, so shots deflect off it), and the rest mark: shadow an opponent from 2.5 m with guard up, closing in for a swing every 2.5-4 s.
  * When our team has the ball, the rest support in their lane, guarding when an opponent is within 3 m and hitting back if one gets in reach.
  * Every swing waits a random beat once in range, so two AIs never attack on the same tick.

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

* `MatchController` (server) - spawns the teams, ball and `MatchState`, and runs the match: the clock, goals, resets after goals, the three ways a match ends, and restarting.
* `MatchState` (server-owned, spawned) - score, phase and whole seconds remaining as SyncVars, and an `Announced` event raised on every client for the HUD banner.

## Match Flow

* Phases: `Playing` → (draw at full time) `SuddenDeath` → `Ended`. The clock (5 minutes by default) stops during goal celebrations.
* Endings:
  * **Full time** - most points wins.
  * **Sudden death** - from a draw at full time, the next goal wins.
  * **Knockout** - when every player on a team is knocked out, the other team wins straight away, in any phase.
* Once `Ended`, every body's input is replaced with nothing, so everyone stops where they are. After the result banner (8 seconds) a new match starts: scores reset, everyone is revived and put back on their spawn points, and the ball kicks off.
* **Knocked out** is a SyncVar on the body. A knocked-out body takes no input, can't pick up the ball (a carrier drops it), is ignored by AI targeting and teammate switching, and greys out. A human on a body that gets knocked out moves to a teammate still standing. Health reaching zero calls `KnockOut()`.
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

## Combat

* `CharacterHealth` (NetworkBehaviour on each body) holds health as a SyncVar (200 by default, in `CharacterStats`) and owns the rules for taking a `Hit`:
  * A body that is dodging, or already down, isn't hit at all.
  * Blocking lets through 10% of the damage and no stagger, unless the hit breaks blocks. Each blocked hit drains stamina (1 per point of the hit's damage); with no stamina there is no guard, and the hit that empties it staggers anyway (a guard break).
  * Hitting a staggered body does ×1.5 damage.
  * At zero health the body is knocked out; otherwise it is staggered for the hit's stagger time (a lighter hit never cuts a longer stagger short).
* Melee moves are `Attack`s on the `ActionController` (hit, range, cooldown), all tunable in the inspector:

| Move   | Damage | Stagger | Range | Cooldown | Notes |
| ------ | ------ | ------- | ----- | -------- | ----- |
| Light  | 10     | 0.25 s  | 1.5 m | 0.4 s    | |
| Heavy  | 25     | 0.7 s   | 1.7 m | 1.2 s    | Breaks blocks |
| Tackle | 3      | 0.8 s   | 2.2 m | 1.5 s    | Lunges forward; the carrier can still do it (a barge). Mostly for stripping the ball |

* A move hits the nearest opponent within range and 60° of facing. Only the server deals damage; the owning client runs the cooldown and the tackle lunge so it moves straight away.
* A hit that lands (not blocked) on the carrier also strips the ball, as before.
* **Ball hits** - a thrown ball moving faster than 16 m/s can't be caught by opponents: it hits them for 10 damage and 0.6 s stagger, bounces back off them, and can no longer score. Someone the ball just hit can't be hit by it again for 1.5 s, so a shooter can't pin them with rebounds. Passes are slower, so they can still be intercepted; shots are fast, so they hurt anyone in the way.
* Grey-box status bars float over each body, facing the camera: health, then stamina, then the special meter (which flashes when ready).
* No knockback yet, since a human's body position is owned by their client; it needs a message to the owner like the post-goal teleport.

## Stamina

* `CharacterStamina` (NetworkBehaviour) holds stamina as a SyncVar (100). Only the server spends it; every machine reads the synced value, so the owning client stops sprinting or dodging once the server says it has run out. It regenerates at 25/s after 0.8 s without spending.
* Costs: sprinting 15/s (falls back to running when empty), dodge 25, heavy attack 20, tackle 15, blocked hits 1 per point of damage. Light attacks are free.

## Specials

* `CharacterSpecial` (NetworkBehaviour) holds the special meter as a SyncVar. The server charges it by 0.5 per point of damage dealt and 0.4 per point taken; a special's own damage doesn't charge it, so specials can't chain.
* Pressing Special with a full meter performs the body's `SpecialMove`, a ScriptableObject asset, so player types can each have their own. The move runs on the server and its effect plays on every machine through an `ObserversRpc`.
* A move can refuse to fire (Rocket without the ball), in which case the meter is kept. Each move also decides when the AI should use it (`ShouldAIUse`).
* Grey-box effect for every special: an expanding disc in the move's colour.

## Player Types

* Each type is its own prefab (`Assets/Prefabs/Characters`), with its own `CharacterStats` asset (`Assets/ScriptableObjects/Stats`) and `SpecialMove` asset (`Assets/ScriptableObjects/Specials`). The network spawn carries the prefab, so every machine knows the type without syncing anything extra.
* `TeamController` spawns a lineup of prefabs, one per spawn point: Bruiser, Runner, Thrower, Enforcer.
* Stats added for types: `MeleeDamageMultiplier`, `ThrowSpeedMultiplier`, `HasteSpeedMultiplier`, `FortifyCounterStagger`.
* Specials can put timed effects on a body, synced as end ticks like the stagger: **haste** (faster movement, free sprinting) and **fortify** (unbreakable guard).
* The builder makes each type's stats asset once, from the defaults plus the type's changes, then leaves it alone so it can be tuned in the inspector. The same goes for special assets.
* Silhouettes tell the types apart until there are models: a wide Bruiser, a slim Runner, and an Enforcer carrying a shield plate.
