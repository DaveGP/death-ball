# Third-party assets

Asset packs that aren't ours to redistribute live here and stay out of git. Only these folders and this README are committed.

| Folder | For |
| --- | --- |
| `Synty/` | Synty packs (POLYGON and others) |
| `AssetStore/` | Anything else from the Unity Asset Store |

Packs import into their own folder under `Assets/`. After importing, drag that folder in here **inside the Unity Editor** (not in Finder), so Unity keeps every reference to it. Until it's moved it isn't ignored, unless it's the default `Assets/Synty/`, so check `git status` after every import.

## Avoiding pink materials

Synty packs arrive with Built-in materials, which render pink in URP. After importing a pack:

1. Import the URP `.unitypackage` inside it (the `Unity_6` one if there's a choice). It updates the materials in place, but can add new files under `Assets/<PackName>/`, so move those in here too.
2. Convert anything still on `Standard` to URP Lit. Legacy particle shaders and Synty's own shaders render fine, so leave those alone.
3. Check prefabs as well as materials: a few can use Unity's built-in `Default-Material` or `Default-ParticleSystem`, which also render pink.

A strong magenta emission colour can look like the error shader but isn't, for example `Graffiti_24`.

## Fresh clones

Anything in here is missing from a fresh clone. Before game content committed elsewhere (a prefab, a material, a scene) relies on one of these assets, either copy the parts you use and have changed out into the project, or note which pack is needed in the root README.
