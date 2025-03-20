# Reach for Godot Engine (ReaGE)
<p align="center">
  <img src="addons/ReachForGodot/icons/logo.png" alt="Reach for Godot Engine" />
</p>

Godot-based visual scene editor for RE Engine games.

Integrates various open source tools dealing with RE Engine games and packs them into a full game data and content / scene editor inside Godot.

![scene example](docs/images/scene.jpg)

## Supported games
Should work for any RE engine game (mostly based on RszTool's support), but I can only test what I own

- Dragon's Dogma 2*
- Resident Evil 2 RT
- Devil May Cry 5**
- Resident Evil 2 non-RT** (untested)
- Resident Evil 3 non-RT (untested)
- Resident Evil 3 RT (untested)
- Resident Evil 4 (untested)
- Resident Evil 7 non-RT** (untested)
- Resident Evil 7 RT (untested)
- Resident Evil 8 (untested)
- Monster Hunter Rise (untested)
- Street Fighter 6 (untested)
- Monster Hunter Wilds*** (untested)

For the open world games: The scene structure is a mess thanks to how it's structured, slow and tedious to edit, but functional; could be improved with future game-specific tooling additions once the common core functionality is stabilized.

\* Many of the terrain meshes use MPLY format meshes which are currently unsupported by RE Mesh Editor and will therefore be loaded as placeholders

** Partial support: files with embedded userdata do not fully load and therefore won't export correctly either

*** Partial support: some user files work fine; most pfbs or scn, probably due to rsz data issues, don't

## Current features
The addon integrates [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) for meshes/textures and a [custom fork](https://github.com/kagenocookie/RszTool) of RszTool for reading structured data.

- mesh: automatic import through RE Mesh editor into .glb files (MPLY / composite meshes: currently unsupported); default untextured but can be enabled through editor settings (not recommended because texture conversions need blender in the foreground)
- tex: can be imported through RE Mesh editor; not useable directly in Godot yet due to lacking DDS support but the files otherwise convert fine
- pfb files [info](https://github.com/kagenocookie/ReachForGodot/wiki/Prefabs)
- scn files [info](https://github.com/kagenocookie/ReachForGodot/wiki/Scenes)
- rcol files (not yet fully tested)  [info](https://github.com/kagenocookie/ReachForGodot/wiki/RCOL)
- user files
- other resource files are imported as placeholders so they can be drag-dropped into resource fields but have no actual logic or data to them

## Prerequisites
- Godot 4.4+ w/ .NET
- Blender and [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) - used for mesh and texture import / export; data editing will still work without it, but no meshes will be generated.
- Extract all relevant chunk files you wish to edit somewhere - [guide](https://github.com/Modding-Haven/REEngine-Modding-Documentation/wiki/Extracting-Game-Files)
- Download the latest RSZ json for the game you're trying to edit, place it wherever
- The addon stores its own cache of relevant il2cpp json data (`addons/ReachForGodot/game_settings/{game}/il2cpp_cache.json`), but for games that don't have those in the repository or if the game gets updated, the il2cpp dump json for the game you're trying to edit is required to (re-)generate the cache file.

## Setup
- Create a fresh godot project anywhere
- Clone or download the `addons/ReachForGodot` folder into it (the file should stay in the same relative folder; the other files in this project are not needed)
- Next, you need to Build the project with the hammer icon in the top right; if it's not available, go under menu: Project > Tools > C# > Create C# solution
- Enable the ReachForGodot plugin in Project Settings > Plugins
- Restart the editor (menu: Project > Reload Current Project)
- Configure the blender path in Godot's Editor Settings (filesystem/import/blender/blender_path)
- Configure the game paths in Editor Settings > `Reach for Godot/Paths/{game name}`
    - **Game chunk path** and **Rsz Json file** are required for most functionality
    - **File list** and **il2cpp Dump file** are only needed for new games that don't have cached data in the repository yet
    - **Additional paths** is a list of secondary additional filepaths for exporting to and importing from of assets, like a custom mod folder or the game's natives path

## Usage
Start by going into the top menu: Project > Tools > RE ENGINE > Import assets and pick a file. Something like the appdata/contents.scn.20 (DD2) might be a good start since it's basically the root scene for the world. Once you have a scene file imported, you can import any further child scenes and referenced resources from the inspector there.

If you've ever worked with a game engine before, the basic UI should be more or less familiar - scene node tree on the left, inspector with all the data for a selected node on the right (for the default layout at least). The addon provides some additional tools in the inspector and node context menu for dealing with RE Engine files and file formats.

### Mapping of engine objects
- <img src="addons/ReachForGodot/icons/folder.png" alt="isolated" width="16"/> `via.Folder` (scn file) => Node (`SceneFolder` or `SceneFolderProxy`)
- <img src="addons/ReachForGodot/icons/gear.png" alt="isolated" width="16"/> `via.GameObject` => Node3D (`GameObject` or `PrefabNode`)
    - Inspector button additions:
        - "Clone GameObject": creates a clone of this gameobject, including fixing all child nodes and their references. Use this instead of Godot's default duplicate feature as that one breaks references
- `via.Component` => Components array inside `GameObject`
    - all components have their full data editable
    - specific components can have additional logic, listed in the next section

General components support:
- `via.Transform`: transforms are transformed to Godot equivalents on the game object node
    - you can move the GameObject nodes directly and the transforms will get updated on export
    - all other Node transforms will be ignored and not transferred over to the game
- `via.render.Mesh`: meshes are imported automatically into an additional mesh Node3D child of the game object with a `__{mesh_name}` prefix
    - meshes can be swapped around by changing the mesh field within the component's data
- `via.render.CompositeMesh`: composite meshes are mapped into several MultiMeshInstance3D nodes
- `via.physics.Colliders`: Basic shape colliders get converted to godot equivalents and can be moved around in 3D. Mesh colliders can only be swapped from the component data with no actual visualization or editing support yet.

## Asset exporting
- every exportable asset has a base path picker and export button at the top of its inspector
- configure any export folders you need in editor settings
- You can specify either just the full path, or also add a label for it to make it easier to identify with a | separator, e.g. `my awesome DD2 mod|D:/mods/dd2/awesome_mod/natives/stm/`

## Planned and potential features
- enums
    - flag enums (autodetect flag enums maybe)
    - support manually overriding enum settings (IsFlags, custom entries)
- support editing of as many file formats as possible
- support serializing objects to JSON - for Content Editor integration and for upgrading changes in case of game updates
- unpacker integration and packed file browser - automatically extract files from paks as needed instead of requiring everything to be pre-extracted
- game specific tooling to make navigation between scenes easier (mainly looking at DD2 / open world assets)
- look into potential GUI support

## Known issues
- no MPLY format mesh support yet (meaning DD2 levels are mostly placeholder meshes aside from the occasional simple mesh) - waiting for RE Mesh Editor
- meshes are by default untextured because of complications with exporting them from blender; Can be enabled with the `reach_for_godot/general/import_mesh_materials_note` editor setting, but your machine will become unusable while importing is in progress because of blender opening in the foreground.
- converted DDS textures aren't directly usable from Godot - waiting for Godot to fix DDS support for incomplete mipmaps; there are open pull requests that will likely fix it
- some PFBs with `via.GameObjectRef` fields might not export correctly by default, as they rely on some arcane propertyId values that don't seem to have any direct correlation with RSZ or class data; some cases can be automated fairly accurately, but otherwise need to be manually reversed out of existing pfbs and defined in `addons/ReachForGodot/game_configs/{game}/pfb_ref_props.json` files. Feel free to make a PR adding more of these entries as you come across them
- there tends to be some godot/c++ errors spewed out while it's doing mass importing of assets, most of them are safe to ignore
- pfb/scn files with embedded userdata aren't supported yet (DMC5 and earlier engine versions), I might extend support for those if there's demand
- while the addon does support multiple games in one project, if you're going to import a lot of data, consider making separate projects, because Godot doesn't scale nicely with lots of files. The first time saving the project after opening (and re-opening) also takes a hot minute because from what I can tell, Godot rechecks _all_ files in the project just in case any of them changed.
- some scn and pfb files might not import or export quite correctly, as the RE_RSZ jsons don't always contain full and correct data. These values get updated as they get loaded in but can sometimes be wrong as they're mainly guesses. Any overrides are stored in `addons/ReachForGodot/game_configs/{game}/rsz_patches.json` files, can be modified manually for cases when the automation doesn't do a good job. Feel free to make PRs adding more of these overrides, so that eventually we'll have everything mapped out correctly.

## Credits
- [NSACloud](https://github.com/NSACloud) - RE Mesh Editor
- [czastack](https://github.com/czastack) - RszTool
- [praydog](https://github.com/praydog) - REFramework and related tools
- [alphazolam](https://github.com/alphazolam) - RE_RSZ and the binary templates
