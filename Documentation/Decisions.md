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
