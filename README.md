# Reach for Godot Engine (ReaGE)
<p align="center">
  <img src="addons/ReachForGodot/icons/logo.png" alt="Reach for Godot Engine" />
</p>

Godot-based visual scene editor for RE Engine games.

Integrates various open source tools dealing with RE Engine games and packs them into a full game data and content / scene editor inside Godot.

![scene example](.gdignore/docs/images/scene.jpg)

## Main features
- Integration of [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) for meshes and [REE-Lib](https://github.com/kagenocookie/RE-Engine-Lib) for all file editing and PAK file extraction.
- Direct support for many RE Engine file formats: mesh, tex, scn, pfb, mdf2, efx, rcol, mcol, uvar, user, ...
- Default Godot features (3d visualization and editing, resource pickers, data editing UI)
- PAK content browser and extractor GUI
- Object template system for easily reusing common components and nested game object structures
- Detailed info in [wiki](https://github.com/kagenocookie/ReachForGodot/wiki)

## Supported games
- Dragon's Dogma 2* ([Field/Env ID lookup map](https://kagenocookie.github.io/dd2map/))
- Devil May Cry 5
- Resident Evil 2 (RT and non-RT)
- Resident Evil 3 (RT and non-RT)
- Resident Evil 4
- Resident Evil 7 (RT and non-RT)
- Resident Evil 8

Other RE Engine games should still work but may have issues in some cases, as I don't own them to be able to build the necessary data cache ([guide for anyone interested in contributing](https://github.com/kagenocookie/ReachForGodot/wiki/Adding-support-for-new-games)):
- Monster Hunter Rise
- Street Fighter 6
- Monster Hunter Wilds
- Other games

\* Many of the terrain meshes use MPLY format meshes which are currently unsupported by RE Mesh Editor and will therefore be loaded as placeholders

## Prerequisites
- [Godot 4.4+](https://godotengine.org/download/windows/) (.NET build, not the standard one)
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- [Blender](https://www.blender.org/download/) and [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) - used for mesh and texture import; data editing will still work without it, but no meshes will be generated.

## Setup
<details>
<summary>Setup video</summary>

(note: if you don't have all the assets unpacked, you can use "Open packed file browser" from the menu at 0:55 instead.)

https://github.com/user-attachments/assets/09653d5c-56af-48a2-a894-573f5f822813
</details>

- If unsure about any part of the setup, follow the setup video above
- Create a fresh Godot project anywhere
- [Download the latest release](https://github.com/kagenocookie/ReachForGodot/releases)
- Extract the `addons` folder into the Godot project (you should end up with a `<project folder>/addons/ReachForGodot` folder)
- Next, you need to Build the project with the hammer icon in the top right; if it's not available, go under menu: Project > Tools > C# > Create C# solution
    - you will need to modify the default .csproj file for it to compile, adding the following entries (or simply copy the full [csproj file contents from here](https://github.com/kagenocookie/ReachForGodot/tree/master/.gdignore/docs/example.csproj)):
        ```xml
        <PropertyGroup>
            <Nullable>enable</Nullable>
            <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        </PropertyGroup>

        <ItemGroup>
            <Reference Include="RszTool" HintPath="$(ProjectDir)/addons/ReachForGodot/Plugins/RszTool.dll"></Reference>
            <Reference Include="ZstdSharp" HintPath="$(ProjectDir)/addons/ReachForGodot/Plugins/ZstdSharp.dll"></Reference>
        </ItemGroup>
        ```
- Enable the ReachForGodot plugin in Project Settings > Plugins
- Restart the editor (menu: Project > Reload Current Project)
- Configure the blender path in Godot's Editor Settings (`filesystem/import/blender/blender_path` or `Reach for Godot/General/Blender Override Path`)
- Configure any relevant Editor Settings > `Reach for Godot/General` and `Reach for Godot/Paths/{game name}`
    - **Game chunk path** setting is required for most functionality, represents the path in which raw assets are to be extracted
    - **Game path** are needed if you don't have all the assets already extracted and would like them to get auto extracted as needed. The addon can read files directly from the PAK files without extraction for some features.
    - Detailed information regarding every available setting is available [here](https://github.com/kagenocookie/ReachForGodot/wiki/Addon-editor-settings)

The tool requires some game specific data that is (automatically) fetched from [REE-Lib-Resources](https://github.com/kagenocookie/REE-Lib-Resources), but if you'd rather have it locally (to avoid extra network requests), there's instructions in the linked repository; change the "Ree Lib Resource Source" editor setting to the `local-resource-info.json` filepath

### Development setup
Mainly for developing or debugging the addon itself, but feel free to use the raw repository data if you prefer that, there is also some dev-only tooling available in this case.
- Clone or download as ZIP
- `git submodule init`
- `git submodule update`
- Open the project and setup editor settings as described in the normal setup section

## Usage
Files can be imported through the tool menu (Project > Tools > RE ENGINE > Import assets) or the addon's embedded file browser (button in the top center of the editor). Something like the appdata/contents.scn.20 (DD2) might be a good start since it's basically the root scene for the world. Once you have a scene file imported, you can import any further child scenes and referenced resources from the inspector there.

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

## Known issues
- no MPLY format mesh support yet (meaning DD2 levels are mostly placeholder meshes aside from the occasional simple mesh) - waiting for RE Mesh Editor
- some PFBs with `via.GameObjectRef` fields might not export correctly by default, as they rely on some arcane propertyId values that don't seem to have any direct correlation with RSZ or class data; some cases can be automated fairly accurately, but otherwise need to be manually reversed out of existing pfbs and defined in [Resources](https://github.com/kagenocookie/REE-Lib-Resources) `{game}/pfb_ref_props.json` files. Feel free to make a PR adding more of these entries as you come across them
- there tends to be some godot/c++ errors spewed out while it's doing mass importing of assets, most of them are safe to ignore
- while the addon does support multiple games in one project, if you're going to import a lot of data, consider making separate projects, because Godot doesn't scale nicely with lots of files. The first time saving the project after opening (and re-opening) also takes a hot minute because from what I can tell, Godot rechecks _all_ files in the project just in case any of them changed.

## Credits
- [NSACloud](https://github.com/NSACloud) - RE Mesh Editor, RE4 EFX file structure
- [chenstack](https://github.com/czastack) - original RszTool
- [praydog](https://github.com/praydog) - REFramework and related tools
- [alphaZomega](https://github.com/alphazolam) - RE_RSZ and the binary templates
- [Battlezone](https://github.com/seifhassine) - misc advice and ideas
