# Teams

Five proposed teams built from the Synty packs in `Unity/Assets/ThirdParty/Synty` (POLYGON Cyber City and POLYGON SciFi City). Each match picks two of them at random, on machines that have the packs. Each team uses models from one pack so its art style stays consistent, and no model is used twice.

| Team | Identity | Colours | Pack | Style | Star |
| --- | --- | --- | --- | --- | --- |
| [The Ninth Life](#the-ninth-life) | Street cat gang | Yellow and cyan | Cyber City | Chaotic, fast | Mittens (Bruiser) |
| [The Recalled](#the-recalled) | Robots avoiding the crusher | Silver and orange | Cyber City | Relentless | Marshal Zero (Thrower) |
| [Halcyon Blue Chips](#halcyon-blue-chips) | Rich owners playing themselves | White and ice blue | SciFi City | Clean, expensive | Cordelia Vane (Runner) |
| [Local 66](#local-66) | Sanitation workers' union | Olive and hazard yellow | SciFi City | Physical, stubborn | Tam Rook (Thrower) |
| [Order of the Last Ring](#order-of-the-last-ring) | Monks who worship the goal | Rust and cyan | SciFi City | Patient, defensive | Abbot Kessan (Enforcer) |

## The Ninth Life

A cat café in the undercity lost its licence when its robot cats were reclassified as weapons. The staff and the cats started playing Death Ball for rent money and never stopped. They are the league's most loved team and its most fined, and every player has officially "died" at least once.

| Role | Model | Look |
| --- | --- | --- |
| Bruiser | `SM_Chr_MurderKitten_01` | Armoured robot cat with claws, and the tallest model at 2.16 m |
| Runner | `SM_Chr_Cat_01` | Yellow hoodie and a glowing cat helmet |
| Thrower | `SM_Chr_Mercenary_Female_01` | Yellow jacket and shorts |
| Enforcer | `SM_Chr_Mercenary_Male_01` | White hair, face mask, black vest and cyan sleeves |

**Star: Mittens, Bruiser.** Mittens was a café greeter until a firmware update removed "gentle" from the settings menu. Mittens hasn't spoken since, only purred at frightening volume, and always saves Slam for an opponent standing beside a live hazard. Fans throw plushies onto the pitch after every knockout.

**Home ground: The Night Market**, a noodle market under an elevated railway.

## The Recalled

All four are defective products with recall notices. A loophole says a machine can't be scrapped while it's under contract, so they signed with the only employer that would take them. Each win delays their crushing date by one season, and they're the only team who really are playing for their lives.

| Role | Model | Look |
| --- | --- | --- |
| Bruiser | `SM_Chr_PleasureBot_Male_01` | Sculpted white body with an orange mohawk and trim |
| Runner | `SM_Chr_Robot_Female_01` | Orange and grey with a visor |
| Thrower | `SM_Chr_Robot_Mercenary_01` | Silver and orange gunslinger robot in a cowboy hat |
| Enforcer | `SM_Chr_Robot_Damaged_01` | Scarred grey robot with orange bands, still standing |

**Star: Marshal Zero, Thrower.** Zero was built as a theme park gunslinger, then recalled for refusing to miss on purpose. Zero's Rocket special fits exactly: a perfectly aimed shot, announced with one line of pre-recorded frontier dialogue. Zero still believes the league is a town that needs cleaning up.

**Home ground: The Crusher**, a live robot recycling plant.

## Halcyon Blue Chips

Halcyon Holdings owns the broadcast, the league and the ball's manufacturer. Its board decided that watching wasn't enough. They now play in person with the best cybernetics money can buy, and a small army of lawyers appeals every loss. Nobody at the club has ever paid for a replacement limb.

| Role | Model | Look |
| --- | --- | --- |
| Bruiser | `Character_Cop_01` | White and ice-blue armour, the club's "Head of Compliance" |
| Runner | `Character_Rich_Female_01` | White dress with cyan trim |
| Thrower | `Character_Rich_Male_01` | White suit and matching hat |
| Enforcer | `Character_Android_Female_01` | White and ice-blue android, a company asset |

**Star: Cordelia Vane, Runner.** Cordelia is the heiress to Halcyon and plays in couture she refuses to get dirty. Cordelia has had three hearts, so Second Wind is less a special than a scheduled replacement. Opponents who tackle Cordelia sometimes receive a legal letter before they've stood back up.

**Home ground: The Vault**, an arena on the floor of a private bank, under a dome.

## Local 66

When the city stopped paying its bin workers, Local 66 went on strike, and the undercity disappeared under rubbish in a week. To break the strike, the corporations offered the union one Death Ball match for its pension fund. Local 66 won, and it has played every season since with its union rules intact. Tea breaks are mandatory.

| Role | Model | Look |
| --- | --- | --- |
| Bruiser | `Character_Garbage_Male_01` | Olive overalls and a gas mask |
| Runner | `Character_Junky_Female_01` | Yellow top and olive striped trousers |
| Thrower | `Character_Junky_Male_01` | Yellow hoodie, olive trousers and goggles |
| Enforcer | `Character_Medical_Male_01` | Yellow hazmat suit and helmet |

**Star: Tam Rook, Thrower.** Tam spent 20 years throwing bags into a moving truck from the back of another moving truck, so a stationary ring feels like an insult. Tam is the league's best long-range scorer, specialising in 2-point shots from behind halfway. Tam also refuses to take a free kick during a designated rest period, and there are no free kicks in Death Ball.

**Home ground: Depot 66**, a working refuse depot under a flyover.

## Order of the Last Ring

After the collapse, a monastic order concluded that the ring is a perfect circle and therefore sacred, and that passing the ball through it is prayer. They play in silence, don't celebrate, and pray for everyone they knock out. They've never been caught cheating, which unnerves the rest of the league far more than cheating would.

| Role | Model | Look |
| --- | --- | --- |
| Bruiser | `Character_Muscle_Male_01` | Moustache, metal arms and a rust vest with cyan trim, a repentant ex-enforcer |
| Runner | `Character_CyborgNinja_01` | One-eyed white mask, cream and rust body and a rust sash |
| Thrower | `Character_Alien_Male_01` | Blue-skinned pilgrim with antennae, in rust and cyan |
| Enforcer | `Character_Monk_Male_01` | Rust-coloured robe with cyan trim |

**Star: Abbot Kessan, Enforcer.** Kessan has kept a vow of silence for 31 seasons and has never thrown a first punch. With Iron Wall active, Kessan simply stands in the goal mouth and lets attackers hurt themselves. Kessan's only known expression is a slight nod, which has made grown Bruisers forfeit.

**Home ground: The Cloister**, a monastery on a floating platform above the clouds.

## Team art

**Death Ball > Build Team Art** ([TeamArtBuilder.cs](../Unity/Assets/Editor/TeamArtBuilder.cs)) makes each team's models in `Unity/Assets/ThirdParty/Synty/DeathBallTeams`, with one folder per team and one prefab per role, such as `NinthLife/Bruiser_SM_Chr_MurderKitten_01.prefab`. Git ignores the output, so run the tool on any machine with the packs. Running it again gives the same result.

* **Copies:** each team has its own copy of the pack's character and attachment FBX files, materials and colour atlas. Prefabs keep only that character, not the pack's other switched-off bodies. Normal and metallic maps still come from the pack, as does the Ninth Life's glow map, so the packs need to stay in the project.
* **Scale:** based on the old grey-box proportions. With the Thrower as 1, Bruisers are 1.33 wide and 1.05 tall, and Runners are 0.8 wide and 1.05 tall. Throwers and Enforcers aren't scaled. The scale is on each prefab's root, so the whole posed body stretches, including stride and arm swing.
* **Colours:** the packs colour everything from one atlas, so each team has a table in the tool of which atlas colours to replace, plus an optional glow colour. Skin isn't in the tables, so it stays as it is, including the Alien's blue. A colour in a table changes on every player on that team who uses it. Team atlases import at up to 2048 px, half the size of Cyber City's originals.

The tool also makes an idle, walk, run and sprint Animator from Synty's Base Locomotion pack, and the `TeamRoster` asset the game loads the teams from.

## In a match

* **Picking teams:** the server picks two different teams at random for every match, including the automatic rematch, and shares the choice through `MatchState`. A banner shows the teams at kickoff, and the score and other banners use their names.
* **Models:** `CharacterArt` on each body shows the model for its role and hides the grey-box shapes. Knocked out models fall over.
* **Sides:** a disc under each player shows Home in red and Away in blue, matching the rings, and is bigger under a body a human controls. It turns grey on a knockout.
* **Without the packs:** there's no roster, so matches are Home against Away with grey-box bodies. A fresh clone still plays.
* **Not animated:** attacks, blocks, dodges and specials have no animation yet, because the Base Locomotion pack only covers movement. Strafing and running backwards play the forward animations.

## Home grounds

**The Night Market** is built. **Death Ball > Build Night Market Arena** ([NightMarketBuilder.cs](../Unity/Assets/Editor/NightMarketBuilder.cs)) makes it at `Unity/Assets/ThirdParty/Synty/DeathBallArenas/NightMarket/NightMarket.unity`, from the current Arena scene. Run **Build Arena Scene** first if that has changed. Git ignores the scene, like the packs it uses. To play it, open the scene and press Play. It isn't in the menu or the web build yet, because the game only loads the scene called Arena, so matches play there whichever teams are picked.

* **Gameplay:** it's a copy of the Arena, with the same layout, colliders, spawn points and hazard timings. Only materials and hazard colours change inside the walls. Scenery has no colliders and stays outside the walls.
* **Hazards:** they use unlit materials, so their colours stay readable in the dark. Electric walls are dark bars that flash yellow and turn bright cyan. Vents are kitchen grates that flash amber and glow orange with a steam blast. Barriers are yellow hazard-striped shutters.
* **Rings:** they keep the red and blue team colours, so you can still see whose is whose.
* **Crowd:** spare characters baked into static meshes, so they cost no animation.
* **Camera limits:** the game camera looks down at 55 degrees and never sees the sky, so anything south of the pitch is kept low. With one player, you mostly see the pitch and nearby stalls. The train, skyline and dragon only come into view as the camera pulls back for couch players.

Measured in the Editor during a match, using a 1280 x 720 Game view:

| View | Triangles | Draw calls |
| --- | --- | --- |
| Plain Arena, one player mid-pitch | about 30k | about 160 |
| Night Market, one player mid-pitch | about 45k | about 200 |
| Night Market, one player next to stalls or claw machines | 58k to 97k | 138 to 200 |
| Night Market, camera pulled back 40 m | about 290k | about 400 |
| Night Market, camera pulled back 60 m (the furthest two far-apart players could reach) | about 420k | about 385 |

It hasn't been run in a browser, so the pulled-back views are the ones to check on the web build.

## Level prompts

Paste the shared rules first, followed by one team's prompt. Every prefab named here exists in the packs.

### Shared rules

```text
Build a new Death Ball arena scene for Unity URP using the Synty packs in Assets/ThirdParty/Synty/PolygonCyberCity and PolygonSciFiCity (both packs' prefabs can be mixed). Keep the gameplay layout of the current Arena scene: a 30 x 50 m flat floor, 3 m walls, a raised ring at each end (2 m high, 3 m in from the end wall), electric wall panels, 3 m floor vents and rising barriers either side of halfway. Reskin those existing pieces rather than changing how they work, and leave colliders, spawn points and hazard scripts intact. Hazards must still look different when off, warning and live, and the two halves must stay fair. Keep the playing area clear: no decoration inside the walls may block movement, the ball or the top-down camera. Put scenery outside the walls and strip colliders from it. Keep the scene light enough for the WebGL build. Treat the prefabs I name as a starting point, and check every material renders correctly in URP.
```

### The Night Market

```text
Theme: a neon noodle market at night under an elevated railway, wet and crowded, in yellow and cyan. Surround the pitch with stalls using SM_Veh_Food_Truck_01, SM_Veh_Food_Trailer_01, SM_Prop_Canopy_01 to 06, SM_Prop_Food_Shelf_01, SM_Prop_Vending_Machine_01 and SM_Prop_Lantern_01. Run SM_Bld_Train_Rail_Straight_01 on SM_Bld_Train_Rail_Strut_01 along one long side, with a parked train from SM_Veh_Train_Cockpit_01 and SM_Veh_Train_Middle_01. Dress the pitch edge with SM_Env_Hologram_Cherry_Tree_01 and 02, FX_Leaves_Blossom_01, and SM_Prop_Holo_Sign_01 to 17. Put a row of SM_Prop_Claw_Machine_01 behind the home ring, with SM_Prop_Plushie_01 piled around them. Add Hologram_Dragon_01 circling high above, FX_Steam_01 from the stalls and SM_Bld_Background_Building_01 to 04 as the skyline. Lighting: dark blue night with strong neon. Theme the floor vents as kitchen extraction grates.
```

### The Crusher

```text
Theme: a working robot recycling plant, grim and industrial, in silver and hazard orange. Fill the outside of the walls with SM_Env_Junk_Pile_01 to 05, SM_Env_Junk_Background_01, SM_Prop_Robot_Posed_Pile_01 and 02, and SM_Prop_Trash_Skip_01. Hang SM_Prop_Robot_Posed_Hanging_01 in rows overhead like a conveyor, leaving the camera view clear. Line one side with SM_Prop_Assembly_Machine_01 and SM_Prop_Robot_Charger_01, and the other with SM_Prop_Hibernation_Pod_01 as the team's "waiting room". Scatter SM_Prop_Robot_Part_01 to 05 outside the walls. Use SM_Bld_Floor_Grating_01 for the vents and SM_Bld_Wall_Laser_01 and SM_Prop_Laser_Beam_01 for the electric walls. Add FX_TactorBeam_01 lifting junk in the background, with FX_Sparks_01 and FX_Fog_Big_01 for atmosphere. Lighting: harsh overhead industrial light with orange warning glows.
```

### The Vault

```text
Theme: a private bank's marble trading floor turned into an arena, spotless and arrogant, in white and ice blue. Build the outer shell from SM_Bld_Bank_01, SM_Bld_Advanced_01 and 02, and SM_Bld_Advanced_Doorway_01, with SM_Prop_Bank_Door_01 as the huge vault door behind the home ring. Use SM_Prop_Bank_Booth_01 and SM_Prop_Bank_Booth_Pillar_01 as executive viewing boxes, separated by SM_Prop_Glass_01 and 02. Add SM_Prop_Atm_01 and SM_Prop_Advertisement_Pillar_01 along the walls, SM_Prop_Security_Camera_01 and 02 watching the pitch, and SM_Prop_Shield_Bubble_01 as the protected owners' enclosure. Beyond it, show a manicured garden with SM_Env_Hedge_01 to 03 and SM_Env_Tree_01 to 03, plus SM_Bld_LandingPad_01 with SM_Veh_Future_01 parked on it. Enclose everything with SM_Env_Living_Dome_Skybox_01. Lighting: bright, cool and clean with polished reflections. The rising barriers should look like security shutters.
```

### Depot 66

```text
Theme: a council refuse depot under a concrete flyover, dirty and proud, in olive and hazard yellow. Place SM_Bld_Underpass_01 over part of the surroundings, without blocking the camera over the pitch, with SM_Bld_Warehouse_01 behind it. Park SM_Veh_Garbage_01 and SM_Veh_Sweepo_01 along the walls. Pile up SM_Prop_RubbishPile_01 to 03, SM_Prop_TrashBag_01 to 03, SM_Prop_Dumpster_01, SM_Prop_Rubbish_Bin_01 and SM_Prop_Rubbish_Paper_01 to 04 outside the walls. Mark the pitch with SM_Env_Road_YellowLines_01_SF and SM_Env_Road_Patch_01_SF as decals only, staying completely flat, and use SM_Env_Sidewalk_Grate_02_SF for the vents. Add SM_Env_Graffiti_Wall_01 with union slogans, SM_Prop_Powerpoll_01 and 02 with sagging cables, and SM_Prop_Barrier_01 to 03 as improvised fan fencing. Use Smoke_Drift_FX and Fire_01_FX in burn barrels. Lighting: overcast daylight outside, sodium orange under the flyover.
```

### The Cloister

```text
Theme: a serene monastery courtyard floating above the clouds, in rust red and cyan. Set the arena on SM_Env_Ground_Tile_Floating_Platform_01 with SM_Env_Ground_Tile_Supports_01 and 02 beneath it. Surround it with SM_Env_Cloud_01 to 05 and show SM_Env_Planet_Rotate_Object_01 in the sky, using SkyDome as the backdrop. Build temple roofs around the outside with SM_Bld_Roof_Pagoda_01 and 02 on SM_Prop_Pillar_01 to 04. Hang SM_Prop_Paper_Lantern_01 and 02 and SM_Prop_Paper_Banner_01 from SM_Prop_Chain_01 to 03, and add SM_Prop_Flags_01 along the walls. Join a smaller outlying shrine with SM_Prop_Bridge_01. Treat each ring as a holy object: FX_RingLight around it, FX_LightRayCube above it and FX_Beacon behind it. The electric walls should look like glowing prayer panels. Lighting: warm low sun above the clouds, calm and golden, with the cyan glow of the hazards as the only cold colour.
```

## Before building these

* **Not tested in the game:** all but one of the packs' 39 character models report a humanoid rig, so they should work with shared animations, but none has been animated yet. `SM_Chr_Fancy_Bot_01`, the top-hatted bot, has no rig, so it isn't used.
* **Public repo:** the packs aren't in git, so any committed scene or prefab that uses them will have missing assets on a fresh clone. The WebGL build will also grow as each level pulls in pack assets.

## Spare models

18 usable characters are left for substitutes or a sixth team:

* **Cyber City:** `SM_Chr_Cyborg_Female_01`, `SM_Chr_Cyborg_Male_01`, `SM_Chr_Female_Soldier_01`, `SM_Chr_Helper_Bot_01`, `SM_Chr_Male_Soldier_01`, `SM_Chr_Racer_Female_01`, `SM_Chr_Rich_Female_01`, `SM_Chr_Rich_Male_01`, `SM_Chr_Robot_01`, `SM_Chr_Street_Male_01`
* **SciFi City:** `Character_Alien_Male_02`, `Character_Augmented_Male_01`, `Character_Cyber_Female_01`, `Character_Cyber_Male_01`, `Character_CyberPunk_Male_01`, `Character_Hacker_Female_01`, `Character_Hologram_Female_01`, `Character_Robot_01`
