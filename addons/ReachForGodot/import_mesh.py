import bpy
import sys

# filename = 'E:/mods/dd2/REtool/re_chunk_000/natives/stm/character/_kit/_equipment/tops/002/tops_002_am_f.mesh.231011879'
# dir = 'E:/mods/dd2/REtool/re_chunk_000/natives/stm/character/_kit/_equipment/tops/002/'
# files = [{'name': 'tops_002_am_f.mesh.231011879'}]
filename = '__FILEPATH__'
dir = '__FILEDIR__'
files = [{'name': '__FILENAME__'}]
outpath = '__OUTPUT_PATH__'

prefName = 'RE-Mesh-Editor-main'

lastDragDropOptions = bpy.context.preferences.addons[prefName].preferences.dragDropImportOptions

bpy.context.preferences.addons[prefName].preferences.dragDropImportOptions = False

timelimit = 30.0
def wait_for_import_finish():
    if bpy.context.window.modal_operators.get("re_mesh.importfile"):
        timelimit -= timelimit
        if timelimit <= 0:
            print('import probably failed, timeouting')
            bpy.ops.wm.quit_blender()
        return 0.1
    else:
        print('import finished!')
        bpy.ops.wm.save_as_mainfile(filepath=outpath)
        bpy.ops.wm.quit_blender()

try:
    bpy.ops.re_mesh.importfile('INVOKE_DEFAULT', filepath=filename, files=files, directory=dir)
    bpy.app.timers.register(wait_for_import_finish)
finally:
    bpy.context.preferences.addons[prefName].preferences.dragDropImportOptions = lastDragDropOptions
