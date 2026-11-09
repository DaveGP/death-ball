# Death Ball UI art

The broadcast uses off-white lettering on opaque steel plates, with chamfered corners and wear at the edges. Blood red marks the broadcast and primary action. Hazard yellow marks focus and local-player rows. Home and Away plate markings use `#E5352A` and `#1F5CE0`.

The start screen uses the stacked lockup beside Match Access. The lobby keeps join codes and instructions above the two scrollable rosters. The in-match score and clock end 122 px from the top at 1920 × 1080 and about 81 px at 1280 × 720. The event plate has a separate headline and detail line.

## Credit

The PNGs in [Assets/UI](../Unity/Assets/UI) are AI-generated, made with OpenAI's ChatGPT. Keep this credit if they are replaced with other generated art.

## Sprite sheet specifications

Source PNGs live in [Assets/UI](../Unity/Assets/UI). Each border value is in source pixels, in Unity order **left, bottom, right, top**. States in the same family use identical borders.

| PNG basename | Source size | Nine-slice border |
| --- | ---: | ---: |
| `Panel` | 384 × 224 | 28, 28, 28, 28 |
| `Button-Normal`, `Button-Highlighted`, `Button-Pressed`, `Button-Disabled` | 380 × 70 each | 24, 24, 24, 24 |
| `Input-Normal`, `Input-Selected` | 500 × 64 each | 24, 24, 24, 24 |
| `HUD-Score` | 1180 × 68 | 24, 24, 24, 24 |
| `HUD-Clock` | 234 × 42 | 12, 12, 12, 12 |
| `HUD-Event` | 1400 × 260 | 38, 38, 38, 38 |
| `Lobby-Home`, `Lobby-Away` | 620 × 58 each | 16, 16, 16, 16 |
| `Lobby-Row`, `Lobby-Row-Selected` | 620 × 62 each | 12, 12, 12, 12 |
| `Death-Ball` | 1400 × 400 | — |
| `Death-Ball-Stacked` | 640 × 440 | — |
| `Menu-Arena` (opaque) | 1920 × 1080 | — |
| `Role-Bruiser`, `Role-Runner`, `Role-Thrower`, `Role-Enforcer` | 128 × 128 each | — |
| The five team-name PNGs under `Resources/TeamCrests` | 512 × 512 each | — |

Crests load by the team name from `MatchState.NameFor`. A missing crest leaves its slot empty. The Recalled crest uses the warning-tag silhouette so it remains legible at 64 px.

## Type and import

- Barlow Condensed Bold and ExtraBold for labels, team names and announcements.
- IBM Plex Mono Medium for the join code, clock, score and broadcast fine print. The supplied TTFs and their SIL Open Font License files are under [Fonts](../Unity/Assets/UI/Fonts).
- The UI importer keeps small plates lossless, compresses the large art for WebGL, caps crest imports at 256 px, and disables UI mipmaps.

Use **Death Ball → Build Main Menu Scene** and **Build Arena Scene** to regenerate. With the local art packs installed, follow with **Build Team Art** and **Build Night Market Arena**.

## Verification

Unity 6000.5.3f1 ran all four scene and art builders in a temporary project copy. The saved Main Menu and Arena scenes were regenerated from their existing UI roots. The verification checked the serialized UI references, button states, font assignment, crest loading and plain-side fallback. It rendered the menu, lobby and HUD with sample data at both 1920 × 1080 and 1280 × 720. The prefab directory matched the original byte for byte.

The WebGL build succeeded. Unity’s detailed build report measured **2,443,838 packed bytes** from `Assets/UI`.

A headless Chrome smoke test verified that Quit is hidden, the join-code field accepts input and shows its yellow focus outline, Play Local opens the lobby, Leave returns to the menu, and Start Match loads the scoreboard. Live Relay and physical couch co-op controllers were outside this smoke test.

Those checks ran on Netcode for GameObjects 2.13.3. After moving back to 3.1.0, the project compiled and an Editor Play mode run went from the menu through Play Local and the lobby into a match with no console errors from the game. That run didn't capture the screens, and the WebGL build, the Chrome smoke test and Relay haven't been repeated on 3.1.0.
