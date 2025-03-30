namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;

public class MeshConverter : BlenderResourceConverter<MeshResource, PackedScene>
{
    private static string? _script;
    private static string ImportScript => _script ??= File.ReadAllText(ProjectSettings.GlobalizePath("res://addons/ReachForGodot/import_mesh.py"));

    private static readonly byte[] MPLY_mesh_bytes = System.Text.Encoding.ASCII.GetBytes("MPLY");

    public override MeshResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
        => SetupResource(new MeshResource(), reference);

    protected override bool IsSupportedFile(string? sourceFilePath)
    {
        if (string.IsNullOrEmpty(sourceFilePath)) return false;

        if (!System.IO.Path.IsPathRooted(sourceFilePath)) {
            sourceFilePath = PathUtils.FindSourceFilePath(sourceFilePath, Config);
        }
        if (string.IsNullOrEmpty(sourceFilePath)) return false;

        using var meshPreview = File.OpenRead(sourceFilePath);
        var bytes = new byte[4];
        meshPreview.ReadExactly(bytes);
        if (bytes.AsSpan().SequenceEqual(MPLY_mesh_bytes)) {
            return false;
        }
        // empty occlusion or whatever meshes, we can't really import them since they're empty and/or non-existent
        if (sourceFilePath.Contains("occ.mesh.", StringComparison.OrdinalIgnoreCase) || sourceFilePath.Contains("occl.mesh.", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        return true;
    }

    protected override Task<bool> ExecuteImport(string sourceFilePath, string outputPath)
    {
        var includeMaterials = Convert.Options.overrideImportMaterials ?? ReachForGodot.IncludeMeshMaterial;
        var importScript = ImportScript
            .Replace("__FILEPATH__", sourceFilePath)
            .Replace("__FILEDIR__", sourceFilePath.GetBaseDir())
            .Replace("__FILENAME__", sourceFilePath.GetFile())
            .Replace("__OUTPUT_PATH__", outputPath)
            // "80004002 No such interface supported" from texconv when we have it convert mesh textures in background ¯\_(ツ)_/¯
            .Replace("__INCLUDE_MATERIALS__", includeMaterials ? "True" : "False")
            ;

        return ExecuteBlenderScript(importScript, !includeMaterials).ContinueWith((t) => {
            if (!t.IsCompletedSuccessfully || !File.Exists(outputPath)) {
                GD.Print("Unsuccessfully imported asset " + sourceFilePath);
                return false;
            }

            ForceEditorImportNewFile(outputPath);
            return true;
        });
    }
}
