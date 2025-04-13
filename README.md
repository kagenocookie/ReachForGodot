# Reach for Godot Engine (ReaGE)
<p align="center">
  <img src="addons/ReachForGodot/icons/logo.png" alt="Reach for Godot Engine" />
</p>

Godot-based visual scene editor for RE Engine games.

Integrates various open source tools dealing with RE Engine games and packs them into a full game data and content / scene editor inside Godot.

![scene example](.gdignore/docs/images/scene.jpg)

## Main features
- Integration of [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) for meshes, a [custom fork of RszTool](https://github.com/kagenocookie/RszTool) with additional file support and fixes, and [ree-pak-rs](https://github.com/eigeen/ree-pak-rs) for on-the-fly PAK file extraction.
- Direct support for many RE Engine file formats: mesh, tex, pfb, scn, mdf2, rcol, user, cfil, fol, ...
- Default Godot features (3d visualization and editing, resource pickers, data editing UI)
- PAK content browser and extractor GUI
- Object template system for easily reusing common components and nested game object structures
- Detailed info in [wiki](https://github.com/kagenocookie/ReachForGodot/wiki)

## Supported games
Should work for any RE engine game, but I can only test what I own

- Dragon's Dogma 2*
- Resident Evil 2 RT
- Devil May Cry 5
- Resident Evil 2 non-RT
- Resident Evil 3 non-RT (untested)
- Resident Evil 3 RT (untested)
- Resident Evil 4
- Resident Evil 7 non-RT (untested)
- Resident Evil 7 RT (untested)
- Resident Evil 8 (untested)
- Monster Hunter Rise (untested)
- Street Fighter 6 (untested)
- Monster Hunter Wilds (untested)

For the open world games: The scene structure is a mess thanks to how it's structured, slow and tedious to edit, but functional; could be improved with future game-specific tooling additions once the common core functionality is stabilized.

\* Many of the terrain meshes use MPLY format meshes which are currently unsupported by RE Mesh Editor and will therefore be loaded as placeholders

## Prerequisites
- Godot 4.4+ w/ .NET 8.0
- Blender and [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) - used for mesh and texture import; data editing will still work without it, but no meshes will be generated.
- Either pre-extract all resources somewhere ([guide](https://github.com/Modding-Haven/REEngine-Modding-Documentation/wiki/Extracting-Game-Files)), or configure the **File Unpacker** setting to extract required files on the fly
- Download the latest RSZ json for the game you're trying to edit, place it wherever
- The addon stores its own cache of relevant il2cpp json data (`addons/ReachForGodot/game_settings/{game}/il2cpp_cache.json`), but for games that don't have those in the repository or if the game gets updated, the il2cpp dump json for the game you're trying to edit is required to (re-)generate the cache file.

## Setup
<details>
<summary>Setup video</summary>

(note: if you don't have all the assets unpacked, you can use "Open packed file browser" from the menu at 1:00 instead.)

https://github.com/user-attachments/assets/eb170792-2950-46b8-a020-3f5d710d79e1

</details>

- Create a fresh godot project anywhere
- Clone or download the `addons/ReachForGodot` folder into it (the file should stay in the same relative folder; the other files in this project are not needed)
- Next, you need to Build the project with the hammer icon in the top right; if it's not available, go under menu: Project > Tools > C# > Create C# solution
    - you may need to modify the default .csproj file for it to compile, adding the following inside a <PropertyGroup> entry (or copying the whole csproj contents from this repository):
        ```xml
        <Nullable>enable</Nullable>
	    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        ```
- Enable the ReachForGodot plugin in Project Settings > Plugins
- Restart the editor (menu: Project > Reload Current Project)
- Configure the blender path in Godot's Editor Settings (filesystem/import/blender/blender_path)
- Configure any relevant Editor Settings > `Reach for Godot/General` and `Reach for Godot/Paths/{game name}`
    - **Game chunk path** and **Rsz Json file** are required for most functionality
    - **File list** and **il2cpp Dump file** are needed for new games that don't have cached data in the repository yet or when there's game updates
    - **File list**, **File Unpacker Executable** and **Pak list** and are needed if you don't have all the assets already extracted and would like them to get auto extracted as needed.
    - Detailed information regarding every available setting is available [here](https://github.com/kagenocookie/ReachForGodot/wiki/Addon-editor-settings)

## Usage
Start by going into the top menu: Project > Tools > RE ENGINE > Import assets and pick a file. Something like the appdata/contents.scn.20 (DD2) might be a good start since it's basically the root scene for the world. Once you have a scene file imported, you can import any further child scenes and referenced resources from the inspector there.

If you've ever worked with a game engine before, the basic UI should be more or less familiar - scene node tree on the left, inspector with all the data for a selected node on the right (for the default layout at least). The addon provides some additional tools in the inspector and node context menu for dealing with RE Engine files and file formats.

### Mapping of engine objects
- <img src="addons/ReachForGodot/icons/folder.png" alt="isolated" width="16"/> `via.Folder` (scn file) => Node (`SceneFolder` or `SceneFolderProxy`)
- <img src="addons/ReachForGodot/icons/gear.png" alt="isolated" width="16"/> `via.GameObject` => Node3D (`GameObject` or `PrefabNode`)
    - all components have their full data editable
    - specific components can have additional logic and tooling

See [the wiki](https://github.com/kagenocookie/ReachForGodot/wiki) for more details.

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
- improvements to the packed file browser
- game specific tooling to make navigation between scenes easier (mainly looking at DD2 / open world assets)
- look into potential GUI support

## Known issues
- no MPLY format mesh support yet (meaning DD2 levels are mostly placeholder meshes aside from the occasional simple mesh) - waiting for RE Mesh Editor
- converted DDS textures aren't directly usable from Godot - waiting for Godot to fix DDS support for incomplete mipmaps; there are open pull requests that will likely fix it
- some PFBs with `via.GameObjectRef` fields might not export correctly by default, as they rely on some arcane propertyId values that don't seem to have any direct correlation with RSZ or class data; some cases can be automated fairly accurately, but otherwise need to be manually reversed out of existing pfbs and defined in `addons/ReachForGodot/game_configs/{game}/pfb_ref_props.json` files. Feel free to make a PR adding more of these entries as you come across them
- there tends to be some godot/c++ errors spewed out while it's doing mass importing of assets, most of them are safe to ignore
- while the addon does support multiple games in one project, if you're going to import a lot of data, consider making separate projects, because Godot doesn't scale nicely with lots of files. The first time saving the project after opening (and re-opening) also takes a hot minute because from what I can tell, Godot rechecks _all_ files in the project just in case any of them changed.
- some scn and pfb files might not import or export quite correctly, as the RE_RSZ jsons don't always contain full and correct data. These values get updated as they get loaded in but can sometimes be wrong as they're mainly guesses. Any overrides are stored in `addons/ReachForGodot/game_configs/{game}/rsz_patches.json` files, can be modified manually for cases when the automation doesn't do a good job. Feel free to make PRs adding more of these overrides, so that eventually we'll have everything mapped out correctly.

## Credits
- [NSACloud](https://github.com/NSACloud) - RE Mesh Editor
- [czastack](https://github.com/czastack) - RszTool
- [praydog](https://github.com/praydog) - REFramework and related tools
- [alphazolam](https://github.com/alphazolam) - RE_RSZ and the binary templates
