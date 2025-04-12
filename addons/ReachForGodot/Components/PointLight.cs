namespace ReaGE;

using System.Threading.Tasks;
using Godot;

[GlobalClass, Tool, REComponentClass("via.render.PointLight")]
public partial class PointLight : LightComponentBase
{
    public override async Task Setup(RszImportType importType)
    {
        await FindOrCreateLightNode<OmniLight3D>();
    }
}