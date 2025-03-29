namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool, REComponentClass("via.render.SpotLight")]
public partial class SpotLight : LightComponentBase
{
    public override async Task Setup(RszInstance rsz, RszImportType importType)
    {
        await FindOrCreateLightNode<SpotLight3D>();
    }
}