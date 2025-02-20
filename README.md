# Reach for Godot Engine (RGE)
Godot-based visual editor for RE Engine games.

Integrates various open source tools dealing with RE Engine games and packs them into a fully functional game data and content editor using Godot.

## Prerequisites
- Blender
- [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) - used for mesh and texture import / export
- Extract all relevant chunk files you wish to edit somewhere - [written guide here](https://github.com/Modding-Haven/REEngine-Modding-Documentation/wiki/Extracting-Game-Files)
- Godot 4.4+ w/ .NET (aiming for 4.5 or whatever version this PR gets merged into: https://github.com/godotengine/godot/pull/101994)
- Download the latest RSZ json for the game you're trying to edit, place it wherever
- Generate the il2cpp dump json for the game you're trying to edit

## Godot setup
- Configure the blender path in Godot's editor settings (filesystem/import/blender/blender_path)
- Enable the ReachForGodot plugin in Project settings>Plugins
- Restart the editor?
- Configure the paths in Editor Settings > `Reach for Godot/Paths/{game name}`
- Create an AssetBrowser resource anywhere in the project and press "Import Assets"
- If it doesn't exist yet, an asset config resource file will be created automatically
- back in Asset Browser: pick the file you wish to import

## Current status
- basic import of tex and mesh files

## Planned features
- enums
    - autodetect if it's a flag enum
    - configurable place for overriding enum settings (IsFlags, custom entries)
- scn/pfb
    - properly show and resolve guid gameobject references
- export any changes back over their original file formats (into a configurable output folder so we don't override the source files)
- support serializing objects to JSON - Content Editor integration
- RETool integration - automatically extract files from paks as needed instead of requiring everything to be pre-extracted

## Room for improvement
- import files in batches, keep blender open and just re-clear the file
- force blend file import afterwards, so the .blend files are immediately usable as packed scenes
- find a way to not put blender in the foreground while its doing the imports
- some sort of progress bar and cancellation support for importing
- when re-deserializing RSZ values from godot formats, sometimes the types get mismatched because the base rsz json doesn't always contain correct types. Can be fixed by modifying the patch json files for the game, or by re-importing from source file. Will add automatic re-import of just the RSZ Data from the source file when potential cases of this are detected.

## Hard blockers
- mesh textures don't show up (requires Godot DDS image format support - https://github.com/godotengine/godot/pull/101994)
- no MPLY format support (meaning levels are mostly empty)
- some meshes fail to import on RE Mesh Editor's side (AttributeError: 'NoneType' object has no attribute 'count' - if reMesh.skeletonHeader.remapCount != reMesh.boneBoundingBoxHeader.count)

## Self notes
- Nice and chunky env: AppData/Contents/TWN01_02/Env_6216/Environment.scn.20

## Credits
- RE Mesh Editor - NSACloud
- RszTool - czastack
- All the members of the RE engine modding community
