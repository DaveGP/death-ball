# Decisions

This document is to track decisions and the reasoning behind them throughout the project.

## Unity

02/11/2026 - Unity 6 was chosen to match the team's other projects, so tooling, packages and code from Crewfall can be reused.

## FishNet

02/11/2026 - FishNet was chosen for networking for the same reasons as on Crewfall, and because the Crewfall character controller is already built on it. Single-player and couch play run as host, so offline and online share one code path.

## Character Controller

02/11/2026 - The character controller follows Crewfall's input pipeline (reader → provider → input source → network controller → motor). Every character has an input source assigned and swapped at runtime instead of fetched from the ServiceLocator, so any of the 8 players can be a local, couch, online or AI player.

## Synty

02/11/2026 - As on Crewfall, Synty asset packs stay in an ignored local folder to keep the repository within LFS storage limits.

05/11/2026 - That folder is now `Unity/Assets/ThirdParty`, as on Witch Craft, with `Synty/` for Synty packs and `AssetStore/` for other Asset Store packs. Git tracks the folders and a README but ignores their contents: the repo is public and the packs' licences don't allow redistributing them.

## Ball Possession

02/11/2026 - Catching is the same as picking up: the nearest player within reach of a loose ball below head height gets it, and the ball passes through players rather than bouncing off them. This makes interceptions automatic and keeps the server logic to one rule. Stripping the carrier staggers them, and new carriers get a second of strip immunity; in playtests without these, the carrier and the nearest defender swapped the ball every second and play never left the halfway line.

## Shot Assist

02/11/2026 - Shots have no up/down aim, so a shot aimed near the goal is lofted to arrive at ring height while the stick still controls direction. Without it, shots from beyond about 15 m hit the floor before the ring, and the 2-point long-range goal from your own half would be close to impossible.

## Combat Pacing

02/11/2026 - Tuned with AI-only matches. At first every match ended in a knockout within about 90 seconds, and the same team won every match in a session. Causes and fixes:

* AIs met and swung on the same tick, and whichever body the server processed first staggered the other and won the fight. Every AI now waits a random beat before its first swing.
* Supporting AIs never defended themselves, so the team with the ball took a beating and snowballed to a knockout. Supporters now guard and hit back.
* Markers stood toe to toe constantly, and blocked chip damage alone outweighed everything else. Markers now shadow from a step back and swing every few seconds.
* Shots deflecting off a keeper bounced back to the shooter, who could pin the keeper with rebounds (49 hits in two minutes). A body the ball just hit can't be hit by it again for 1.5 s.
* Numbers: health 200, blocked damage 10%, tackle 3, ball hit 10.

A five-minute AI match now loses about half of all health, usually has one or two knockouts, and goes to full time. Humans fight less constantly than the AI, so real matches should knock out even fewer players.

## Stamina and Specials

02/11/2026 - Stamina is server authoritative like health; the owning client reads the synced value to gate sprinting and dodging, which can lag the server by one round trip online. Specials are ScriptableObject assets so player types can each get their own; until then everyone has Slam.

Retuned pacing afterwards with AI matches and hit logs. With blocked hits draining 2 stamina per damage, slams at 20 damage and meters charging fully from damage dealt, slams did a third of all damage and a knockout win came at about 3.5 minutes. Halving block drain and special charging and dropping slam to 12 damage brought specials down to a few per match and matches back to full time, with knockouts landing late.

## Player Types

02/11/2026 - Each player type is a separate prefab with its own stats and special assets, rather than one prefab configured at runtime. FishNet's spawn already says which prefab it is, so clients know the type with nothing extra to sync, and each type can be tuned in the inspector.

In the first AI matches the Runners were knocked out first every time: as the fastest players they reach the loose ball first and take every tackle. Runner health went from 150 to 170, Bruiser from 260 to 240, and AI carriers now dodge clear of a defender on top of them. Runners still go down earlier than the other types in AI play; that's worth checking with people playing.

## Team Tactics AI

02/11/2026 - AI roles are planned per team rather than each player working out for itself who is nearest. A team brain assigns roles a few times a second with a bias towards keeping roles where they are, and each player brain carries its role out. Compared over AI matches: before, everyone near the ball piled in, the fastest player was swarmed at every kickoff and matches ran to 16 goals; with roles, one player per team chases, defenders mark specific opponents and the keeper holds, and matches were close and lower scoring (10 goals, then 3-2). Runners were first preferred as hunters but kept getting knocked out early, so hunting now prefers Bruisers and Enforcers.

## Online Testing Fixes

02/11/2026 - First online test with two editors (Multiplayer Play Mode). Three problems, all about when a client is given things relative to loading the arena:

* Remote players were given their body before they had loaded the arena, and since owners move their own bodies, it fell through the missing floor. The server now asks a client for its players once FishNet says it is in the arena.
* Clients joining mid-match were sent the match objects while still swapping scenes, and FishNet destroyed them. Match objects now use FishNet's scene condition and are spawned into the arena scene, so they are only sent to clients in it.
* After a client went back to the menu, FishNet kept using holding scenes that the menu load had unloaded. `ReloadSafeSceneProcessor` makes new ones. Chosen over patching FishNet, which stays as shipped.

## Arena Hazards

02/11/2026 - Hazards run on a schedule from FishNet's synced tick instead of being networked objects. Every machine computes the same state, which also keeps the rising barriers in step on a human's own machine, where their body's collisions happen. Checked with two editors: host and client showed identical hazard states in every sample.

In AI matches the AI almost never gets caught (one or two hazard hits in several minutes), because it runs clear of anything warning or live. Hazards matter more against humans and as a place to knock someone into; if AI-only play needs them to bite more, the AI could ignore warnings some of the time.

An AI test match stalled with a carrier that never moved. It turned out to be an idle local player on the client editor holding the ball while the AI hunter was wedged outside its tackle range in a crowd. The AI now goes for a tackle from the tackle's own reach.

## Netcode for GameObjects and Relay

03/11/2026 - Moved from FishNet to Netcode for GameObjects with Multiplayer Services sessions, to put the game on GitHub Pages with online play. Browsers can only connect out, over WebSockets, so everyone meets on Unity Relay with secure WebSockets and a join code. FishNet's Relay support needed a third-party transport, while NGO and Multiplayer Services do sessions, Relay and WebSockets out of the box. Movement stays owner-driven as before; NGO has no built-in prediction, so if hits against remote bodies feel laggy, prediction can be added on top later.

* Local play uses a do-nothing transport (`OfflineTransport`) and runs as host, so it still shares the online code path and works in the browser.
* The host and each joiner pick their own Relay protocol, so both ask for WSS; a joiner left on the default (UDP) can't connect to a WebSocket transport.
* Making a new scene in a builder unloads assets nothing references yet, so the builders load the NetworkManager prefab after making the scene; a copy loaded before pointed at nothing and the scenes saved an empty reference.

## The ball is drawn separately from its networked position

05/11/2026 - Testing with added lag showed the ball trailing its carrier by 1.65 m on the carrier's own screen at a 50 ms round trip, and 2.88 m at 350 ms, because its position goes through the server and back with smoothing on each leg. The ball's visual is now a child that sits at the carrier's hands on every machine, measured at about 0.02 m at every lag. Letting go glides slowly, at about running speed: at the same speed as picking up, a ball shot on the run slid back up to 1.8 m towards its trailing position before flying forward.

The same test found that NGO recalculates a prefab's hash whenever the editor validates it, so the builder's own hash could drift from the saved prefab and clients were refused with a config mismatch. The builder now uses NGO's formula.

## Team art is generated from the packs

06/11/2026 - Each team's models are copied, scaled and recoloured by an Editor tool into an ignored folder, rather than being committed, because the repo is public and the packs' licences don't allow redistributing them. Recolouring works on one copy of the atlas per team, using a table of colours to replace. That is one texture per team instead of one per player, which suits the web build, and a colour change is consistent across the team. The cost is that a shared atlas colour can't differ between teammates.

## Home grounds reskin a copy of the Arena

06/11/2026 - The Night Market is made by a tool that copies the generated Arena scene, then swaps materials and adds scenery outside the walls. That keeps the rules identical to the Arena without changing any game code, and the scene stays out of the public repo because it depends on the Synty packs. The cost is that a change to the Arena needs each home ground rebuilt. The game camera shaped the layout: it never sees the sky, so scenery south of the pitch has to stay low. Scenery casts no shadows, which took a match from about 400k triangles to about 45k with one player.

## Teams are picked by the server and drawn on each machine

06/11/2026 - The server picks two teams for each match and shares two indexes through `MatchState`. Each machine then swaps its own models in, so team changes need no respawning and nothing about the art is networked. The roster is made by the art tool and loaded from a Resources folder, because committed code and scenes can't refer to the ignored packs, and that makes the teams optional: without a roster the game runs with grey-box bodies. Everything in the roster goes into any build made with it, whichever teams get picked. Markers under players keep the Home red and Away blue that the rings use, since team colours can be close and don't say which ring is whose.

## Charged shots fire on release and the owner decides the aim

09/11/2026 - Shots fire when Shoot is released rather than pressed, so one button covers both a quick shot and a charged one. The cost is that a tap now fires when the button comes back up, rather than as it goes down. The aim swings at a fixed rate instead of following the input directly, because keyboard aim only has eight directions and there is no mouse aim, so a slow swing is what makes a precise shot possible with keys.

The owner works out the aim and writes it into the input it sends. The server used to fall back on its own copy of the body's facing, which trails a remote owner by the round trip, so a charged shot could leave in a different direction from the line the player lined up. The charge itself is counted per input tick on both machines, so lag doesn't add power.

The AI doesn't charge. Its numbers were tuned around standard shots, and a charging AI would need its own judgement about when to stand still.
