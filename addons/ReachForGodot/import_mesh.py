import bpy
import sys

filename = '__FILEPATH__'
dir = '__FILEDIR__'
files = [{'name': '__FILENAME__'}]

prefName = 'RE-Mesh-Editor-main'
includeMaterials = __INCLUDE_MATERIALS__

lastDragDropOptions = bpy.context.preferences.addons[prefName].preferences.dragDropImportOptions
lastShowConsole = bpy.context.preferences.addons[prefName].preferences.showConsole

bpy.context.preferences.addons[prefName].preferences.dragDropImportOptions = False
bpy.context.preferences.addons[prefName].preferences.showConsole = False

# %AppData%\Roaming\Blender Foundation\Blender\4.3\scripts\presets\operator\export_scene.gltf/
op = {
'filepath' : '__OUTPUT_PATH__',
'export_import_convert_lighting_mode' : 'SPEC',
'export_use_gltfpack' : False,
'export_gltfpack_tc' : True,
'export_gltfpack_tq' : 8,
'export_gltfpack_si' : 1.0,
'export_gltfpack_sa' : False,
'export_gltfpack_slb' : False,
'export_gltfpack_vp' : 14,
'export_gltfpack_vt' : 12,
'export_gltfpack_vn' : 8,
'export_gltfpack_vc' : 8,
'export_gltfpack_vpi' : 'Integer',
'export_gltfpack_noq' : True,
'export_format' : 'GLB',
'export_copyright' : '',
'export_image_format' : 'JPEG' if includeMaterials else 'NONE',
'export_image_add_webp' : False,
'export_image_webp_fallback' : False,
'export_texture_dir' : '',
'export_jpeg_quality' : 75,
'export_image_quality' : 25,
'export_keep_originals' : False,
'export_texcoords' : True,
'export_normals' : True,
'export_gn_mesh' : False,
'export_draco_mesh_compression_enable' : False,
'export_tangents' : False,
'export_materials' : 'EXPORT',
'export_unused_images' : False,
'export_unused_textures' : False,
'export_vertex_color' : 'MATERIAL',
'export_all_vertex_colors' : True,
'export_active_vertex_color_when_no_material' : True,
'export_attributes' : False,
'use_mesh_edges' : False,
'use_mesh_vertices' : False,
'export_cameras' : False,
'use_selection' : False,
'use_visible' : False,
'use_renderable' : True,
'use_active_collection_with_nested' : True,
'use_active_collection' : False,
'use_active_scene' : False,
'collection' : '',
'at_collection_center' : False,
'export_extras' : False,
'export_yup' : True,
'export_apply' : False,
'export_shared_accessors' : False,
'export_animations' : True,
'export_frame_range' : False,
'export_frame_step' : 1,
'export_force_sampling' : True,
'export_pointer_animation' : False,
'export_animation_mode' : 'ACTIONS',
'export_nla_strips_merged_animation_name' : 'Animation',
'export_def_bones' : False,
'export_hierarchy_flatten_bones' : False,
'export_hierarchy_flatten_objs' : False,
'export_armature_object_remove' : False,
'export_leaf_bone' : False,
'export_optimize_animation_size' : True,
'export_optimize_animation_keep_anim_armature' : True,
'export_optimize_animation_keep_anim_object' : False,
'export_optimize_disable_viewport' : False,
'export_negative_frame' : 'SLIDE',
'export_anim_slide_to_zero' : False,
'export_bake_animation' : False,
'export_anim_single_armature' : True,
'export_reset_pose_bones' : True,
'export_current_frame' : False,
'export_rest_position_armature' : True,
'export_anim_scene_split_object' : True,
'export_skins' : True,
'export_influence_nb' : 4,
'export_all_influences' : True,
'export_morph' : True,
'export_morph_normal' : True,
'export_morph_tangent' : False,
'export_morph_animation' : True,
'export_morph_reset_sk_data' : True,
'export_lights' : False,
'export_try_sparse_sk' : True,
'export_try_omit_sparse_sk' : False,
'export_gpu_instances' : False,
'export_action_filter' : False,
'export_convert_animation_pointer' : False,
'export_nla_strips' : True,
'export_original_specular' : False,
'will_save_settings' : False,
'export_hierarchy_full_collections' : False,
'export_extra_animations' : False,
'export_loglevel' : -1,
}

def clean_materials_for_export():
    albedoNames = set([
        'ALBD',
        'ALBDmap',
        'BackMap',
        'BaseMap',
        'BackMap_1',
        'BaseMetalMap',
        'BaseMetalMapArray',
        'BaseShiftMap',
        'BaseAnisoShiftMap',
        'BaseDielectricMapBase',
        # 'BaseAlphaMap',
        'BaseDielectricMap',
        #Vertex Color
        #'BaseDielectricMap_B',
        #'BaseDielectricMap_G',
        #'BaseDielectricMap_R',
        'BaseMap',
        'CloudMap',
        'CloudMap_1',
        'FaceBaseMap',
        'Face_BaseDielectricMap',
        'Moon_Tex',
        'Sky_Top_Tex',
        # 'BaseColor',
    ])

    for obj in bpy.data.objects:
        if obj.active_material:
            tree = obj.active_material.node_tree
            albedo = next((node for node in tree.nodes if node.name in albedoNames), None)
            output = next((node for node in tree.nodes if node.name == 'Principled BSDF'), None)
            if albedo and output:
                for input in output.inputs:
                    if input.is_linked and input.links:
                        for link in input.links:
                            tree.links.remove(link)
                tree.links.new(albedo.outputs[0], output.inputs[0])

try:
    bpy.ops.re_mesh.importfile('INVOKE_DEFAULT', filepath=filename, files=files, directory=dir, loadMDFData=False, loadMaterials=includeMaterials)
    clean_materials_for_export()
    bpy.ops.export_scene.gltf(**op)
    bpy.ops.wm.quit_blender()
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
