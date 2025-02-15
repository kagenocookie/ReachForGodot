import bpy

# filename = 'E:/mods/dd2/REtool/re_chunk_000/natives/stm/character/_kit/_equipment/tops/001/bd_001_0_0_albd.tex.760230703'
# dir = 'E:/mods/dd2/REtool/re_chunk_000/natives/stm/character/_kit/_equipment/tops/001/'
# files = [{'name': 'bd_001_0_0_albd.tex.760230703'}]
filename = '__FILEPATH__'
dir = '__FILEDIR__'
files = [{'name': '__FILENAME__'}]

prefName = 'RE-Mesh-Editor-main'

def wait_for_import_finish():
    if bpy.context.window.modal_operators.get("re_tex.convert_tex_dds_files"):
        return 0.1
    else:
        print('conversion finished!')
        bpy.ops.wm.quit_blender()

bpy.ops.re_tex.convert_tex_dds_files('INVOKE_DEFAULT', filepath=filename, files=files, directory=dir)
bpy.app.timers.register(wait_for_import_finish)
