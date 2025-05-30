namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, REComponentClass("via.render.SpotLight")]
public partial class SpotLight : LightComponentBase
{
    public override async Task Setup(RszImportType importType)
    {
        await FindOrCreateLightNode<SpotLight3D>();
    }
}