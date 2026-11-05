# Third-party assets

Asset packs that aren't ours to redistribute live here and stay out of git. Only these folders and this README are committed.

| Folder | For |
| --- | --- |
| `Synty/` | Synty packs (POLYGON and others) |
| `AssetStore/` | Anything else from the Unity Asset Store |

Packs import into their own folder under `Assets/`. After importing, drag that folder in here **inside the Unity Editor** (not in Finder), so Unity keeps every reference to it. Until it's moved it isn't ignored, unless it's the default `Assets/Synty/`, so check `git status` after every import.

Anything in here is missing from a fresh clone. Before game content committed elsewhere (a prefab, a material, a scene) relies on one of these assets, either copy the parts you use and have changed out into the project, or note which pack is needed in the root README.
