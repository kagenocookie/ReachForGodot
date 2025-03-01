import bpy
import sys

filename = '__FILEPATH__'
dir = '__FILEDIR__'
files = [{'name': '__FILENAME__'}]
outpath = '__OUTPUT_PATH__'

prefName = 'RE-Mesh-Editor-main'

lastDragDropOptions = bpy.context.preferences.addons[prefName].preferences.dragDropImportOptions
lastShowConsole = bpy.context.preferences.addons[prefName].preferences.showConsole

bpy.context.preferences.addons[prefName].preferences.dragDropImportOptions = False
bpy.context.preferences.addons[prefName].preferences.showConsole = False

try:
    bpy.ops.re_mesh.importfile('INVOKE_DEFAULT', filepath=filename, files=files, directory=dir)
    bpy.ops.wm.save_as_mainfile(filepath=outpath)
except Exception as e:
    print('something failed ... ')
    if hasattr(e, 'message'):
        print(e.message)
    else:
        print(e)
except:
    print('something failed but I have absolutely no idea what')
finally:
    bpy.context.preferences.addons[prefName].preferences.dragDropImportOptions = lastDragDropOptions
    bpy.context.preferences.addons[prefName].preferences.showConsole = lastShowConsole
