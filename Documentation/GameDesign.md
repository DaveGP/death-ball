# Death Ball - Game Design

Working draft. Spiritual successor to the early-2000s "violent future sport" genre, built around its own sport, rules and world.

## Pitch

In a collapsed future, corporations settle disputes and sell broadcasts through Death Ball, a sanctioned blood sport. Two teams of four fight over one steel ball inside a sealed arena. You can win by outscoring the other team, but you can also win by leaving them all on the floor.

## The Sport

### Arena

* An enclosed arena with walls players and the ball can be slammed into.
* One goal at each end: a raised **ring** that the ball must be thrown through.
* Arenas vary in shape, size and hazards (see [Arenas](#arenas)).

### Teams

* 4 players per side on the field.
* A squad of up to 8 in league play, with substitutes coming in between matches.

### Winning a Match

A match is won by whichever happens first:

1. **Knockout** - every opponent on the field is knocked out.
2. **Time** - the team with the most points when the clock runs out wins. A draw goes to sudden death: the next goal or knockout wins.

### Scoring

* A throw through the ring scores points. More difficult shots score more:
  * Standard throw: 1 point
  * Long-range throw (from behind the halfway line): 2 points
  * Shot after a pass that a teammate caught in the air: bonus point
* After a goal the ball resets to the centre launcher.

### The Ball

* One heavy steel ball. Any player can carry it.
* The carrier moves slightly slower and cannot attack. They can pass, shoot, or barge (shoulder charge).
* **Overcharge**: the ball heats up the longer one player holds it. After a few seconds it starts damaging the carrier, which stops anyone holding on to the ball to run the clock down.
* A thrown ball that hits an opponent hard enough damages and staggers them. Throwing the ball is also an attack.

## Combat

* **Health**: each player has health. At zero they are knocked out and stay down for the rest of the match.
* **Stamina**: spent on sprinting, dodging and heavy attacks, and regenerates over time.
* **Attacks**: light and heavy melee, tackles, and a heavy hit on a player who is staggered.
* **Defence**: block (reduces damage, costs stamina), dodge (brief invulnerability).
* **Specials**: each player type has one special move charged by dealing and taking hits.
* Knocking out the carrier makes them drop the ball loose.

## Player Control

* Each human controls one player at a time and can **switch** to a teammate:
  * Auto-switch: when your team regains the ball, control jumps to the carrier.
  * Manual switch: cycle to the teammate nearest the ball, or the one you're aiming at.
* Teammates not controlled by a human are AI.
* Any of the eight slots can be filled by a local player, a couch player, an online player or AI. Two couch players on the same team control two separate bodies.

## Player Types

Each player has a type that sets their stats and special move. A team fields one of each.

| Type     | Strengths                                   | Weaknesses                 | Special |
| -------- | ------------------------------------------- | -------------------------- | ------- |
| Bruiser  | 240 health, hits 30% harder                 | Slower, less stamina       | **Slam** - shockwave hitting every opponent nearby through their guard, knocking the ball loose |
| Runner   | Fastest, most stamina, cheap dodges         | 170 health, weaker hits    | **Second Wind** - full stamina and 5 s of extra speed with free sprinting |
| Thrower  | Passes and shots 20% faster                 | Weaker in melee            | **Rocket** - a perfectly aimed, very fast shot at the ring (needs the ball) |
| Enforcer | 220 health, blocks let through less and cost less stamina | No standout trait | **Iron Wall** - 5 s where blocked hits do nothing and stagger the attacker |

A Thrower's passes are fast enough that opponents in the way get hit rather than intercepting them.

## Arenas

* Each arena has its own layout and hazards, for example electrified walls, floor vents and sections that close or open during the match.
* Hazards hurt both teams, so knocking an opponent into one is a tactic.

## Modes

* **Exhibition** - a single match with any mix of local, couch, online and AI players.
* **League** - take a team through a season of fixtures:
  * Injuries carry over between matches. A player knocked out may miss later games.
  * Prize money pays for training, healing and signing new players.
  * Rival teams have their own identity and tactics.
* **Online** - quick match and private lobbies.

## Tone

Gritty, violent and darkly satirical: corporate sponsors, commentators who love the carnage, and a crowd that wants blood.

## Open Questions

* Camera: third-person behind the controlled player, or a wider isometric view?
* How long is a match (time limit)?
* Does a knocked-out player come back with reduced health in sudden death, or stay down?
* Ring design: fixed or moving goals?
