namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

[GlobalClass, Tool]
public partial class REComponentPlaceholder : REComponent
{
    public REComponentPlaceholder() { }
    public REComponentPlaceholder(SupportedGame game, string classname) : base(game, classname) {}

    public override Task Setup(REGameObject gameObject, RszInstance rsz, RszImportType importType) => Task.CompletedTask;
}
