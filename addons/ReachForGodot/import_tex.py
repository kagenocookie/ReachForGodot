import bpy

filename = '__FILEPATH__'
dir = '__FILEDIR__'
files = [{'name': '__FILENAME__'}]

prefName = 'RE-Mesh-Editor-main'

lastShowConsole = bpy.context.preferences.addons[prefName].preferences.showConsole
bpy.context.preferences.addons[prefName].preferences.showConsole = False

try:
    bpy.ops.re_tex.convert_tex_dds_files('INVOKE_DEFAULT', filepath=filename, files=files, directory=dir)
finally:
    bpy.context.preferences.addons[prefName].preferences.showConsole = lastShowConsole
