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
- scene import
- scene file editing
    - parse into godot nodes
    - Folder -> RENode (Folder)
    - GameObject -> RENode (GameObject)
    - Components[] -> GameObject/Components (Node)
- scene file export
