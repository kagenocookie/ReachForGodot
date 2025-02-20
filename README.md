# Reach for Godot Engine (RGE)
Godot-based visual scene editor for RE Engine games.

Integrates various open source tools dealing with RE Engine games and packs them into a fully functional game data and content editor using Godot.

## Prerequisites
- Blender
- [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) - used for mesh and texture import / export
- Extract all relevant chunk files you wish to edit somewhere - [written guide here](https://github.com/Modding-Haven/REEngine-Modding-Documentation/wiki/Extracting-Game-Files)
- Godot 4.4+ w/ .NET (aiming for 4.5 or whatever version this PR gets merged into: https://github.com/godotengine/godot/pull/101994)
- Download the latest RSZ json for the game you're trying to edit, place it wherever
- Generate the il2cpp dump json for the game you're trying to edit

## Godot setup
- Enable the ReachForGodot plugin in Project settings>Plugins
- Restart the editor if needed
- Configure the blender path in Godot's editor settings (filesystem/import/blender/blender_path)
- Configure the game paths in Editor Settings > `Reach for Godot/Paths/{game name}`
- Create an AssetBrowser resource anywhere in the project and press "Import Assets"
- If it doesn't exist yet, an asset config resource file will be created automatically, select the game there
- back in Asset Browser: pick the file you wish to import

## Current features
- normal meshes: automatic import through RE Mesh editor into blend files
- MPLY / composite meshes: unsupported
- textures: automatic import through RE Mesh editor
- pfb files: import through integrated RszTool (no support for exporting yet)
- scn files: import through integrated RszTool (no support for exporting yet); although without composite MPLY meshes, they can look pretty empty sometimes
- user files: import through integrated RszTool (no support for exporting yet)
- other resource files: imported as placeholders

Specific components support:
- `via.Transform`: transforms are transformed to Godot equivalents
- `via.render.Mesh`: meshes are imported automatically

## Planned features
- enums
    - autodetect if it's a flag enum
    - configurable place for overriding enum settings (IsFlags, custom entries)
- scn/pfb
    - properly show and resolve guid gameobject references
- export any changes back over their original file formats (into a configurable output folder so we don't override the source files)
- support serializing objects to JSON - Content Editor integration
- RETool integration - automatically extract files from paks as needed instead of requiring everything to be pre-extracted
- look into GUI support

## Room for improvement
- import files in batches, keep blender instance open and just re-clear the file
- find a way to not put blender in the foreground while its doing the imports
- when re-deserializing RSZ values from godot formats, sometimes the types get mismatched because the base rsz json doesn't always contain correct types. Can be fixed by manually fixing the data in the game specific patch json files, or by re-importing from source file. Will eventually add automatic re-import of just the RSZ Data from the source file when potential cases of this are detected, maybe automatic patch file modifications as well.

## Hard blockers
- mesh textures don't show up (likely fixed once Godot gets DDS image format support - https://github.com/godotengine/godot/pull/101994)
- no MPLY format support (meaning DD2 levels are mostly empty aside from the occasional simple mesh)
- some meshes fail to import on RE Mesh Editor's side (AttributeError: 'NoneType' object has no attribute 'count' - if reMesh.skeletonHeader.remapCount != reMesh.boneBoundingBoxHeader.count)

## Self notes
- Nice and chunky env: AppData/Contents/TWN01_02/Env_6216/Environment.scn.20

## Credits
- RE Mesh Editor - NSACloud
- RszTool - czastack
- REFramework and related tools - praydog
- All the members of the RE engine modding community for getting it to where it is
