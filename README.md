# Reach for Godot Engine (RGE)
Godot-based editor for RE Engine games.

Integrates various open source tools dealing with RE Engine games and packs them into a fully functional game data and content editor using Godot.

## Prerequisites
- Blender
- [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) - used for mesh and texture import / export
- Extract all relevant chunk files you wish to edit somewhere - [written guide here](https://github.com/Modding-Haven/REEngine-Modding-Documentation/wiki/Extracting-Game-Files)
- Godot 4.5 w/ .NET (Or whatever version this PR gets merged into: https://github.com/godotengine/godot/pull/101994)

## Godot setup
- Configure the blender path in Godot's editor settings (filesystem/import/blender/blender_path)
- Enable the ReachForGodot plugin in Project settings>Plugins
- Restart the editor?
- Configure `Reach for Godot/Paths/Game Chunk Paths/{game name}`
- Create an AssetBrowser resource anywhere in the project and press "Import Assets"
- If it doesn't exist yet, an asset config file will be created automatically
- back in Asset Browser: pick any file you wish to import

## Current status
- basic import of tex and mesh files

## Planned features
- enums
    - autodetect if it's a flag enum
    - configurable place for overriding enum settings (IsFlags, custom entries)
- scn/pfb
    - properly show and resolve guid gameobject references
- support all remaining rsz types
- export any changes back over their original file formats (into a configurable output folder so we don't override the source files)
- support serializing objects to JSON - Content Editor integration

## Room for improvement
- import files in batches, keep blender open and just re-clear the file
- force blend file import afterwards, so the .blend files are immediately usable as packed scenes
- some sort of progress bar and cancellation support for the editor

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
