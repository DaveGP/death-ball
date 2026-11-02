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
