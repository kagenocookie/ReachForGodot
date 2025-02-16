# Reach For Godot
Godot-based editor for RE Engine games.

## Prerequisites
- Blender
- [RE Mesh Editor](https://github.com/NSACloud/RE-Mesh-Editor) - used for mesh and texture import / export
- Extract all relevant chunk files you wish to edit somewhere - [written guide here](https://github.com/Modding-Haven/REEngine-Modding-Documentation/wiki/Extracting-Game-Files)
- Godot 4.5 (Or whatever version this PR gets merged into: https://github.com/godotengine/godot/pull/101994)

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
- export scenes/prefabs back over their original files (and into a different folder so we don't override the source files)
- make component fields viewable and editable (plan: override GetPropertyList() and keep a DB of field lists somewhere)
- user.2 files

## Room for improvement
- import files in batches, keep blender open and just re-clear the file
- force blend file import afterwards, so the .blend files are immediately usable as packed scenes

## Hard blockers
- mesh textures don't show up (requires Godot DDS image format support - https://github.com/godotengine/godot/pull/101994)
- no MPLY format support (meaning levels are mostly empty)
- some meshes fail to import on RE Mesh Editor's side (AttributeError: 'NoneType' object has no attribute 'count' - if reMesh.skeletonHeader.remapCount != reMesh.boneBoundingBoxHeader.count)

## Self notes
- Nice and chunky env: AppData/Contents/TWN01_02/Env_6216/Environment.scn.20

## Credits
- RE Mesh Editor - NSACloud
- RszTool - czastack
