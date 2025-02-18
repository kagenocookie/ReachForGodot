namespace RGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class MeshResource : REResourceProxy
{
    protected override Task<Resource?> Import() => Asset?.AssetFilename == null ?
        Task.FromResult((Resource?)null) : Importer.ImportMesh(Asset.AssetFilename, Game).ContinueWith(t => ImportedResource = t.Result);
}

