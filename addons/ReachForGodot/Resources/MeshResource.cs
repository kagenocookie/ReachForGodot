namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class MeshResource : REResourceProxy
{
    protected override async Task<Resource?> Import()
    {
        if (Asset?.AssetFilename == null) return null;

        return await AsyncImporter.QueueAssetImport(Asset.AssetFilename, Game, (res) => {
            ImportedResource = res;
        });
    }
}

