#if TOOLS
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Godot;

namespace RFG;

[GlobalClass, Tool]
public partial class AssetConfig : Resource
{
    private const string ConfigResource = "res://asset_config.tres";

    private static AssetConfig? _instance;
    public static AssetConfig Instance {
        get {
            if (_instance == null) {
                if (!ResourceLoader.Exists(ConfigResource)) {
                    _instance = new AssetConfig() { ResourcePath = ConfigResource };
                    ResourceSaver.Save(_instance);
                } else {
                    _instance = ResourceLoader.Load<AssetConfig>("res://asset_config.tres");
                }
            }
            return _instance;
        }
    }

    [Export(PropertyHint.EnumSuggestion)] public string Game = string.Empty;
    [Export(PropertyHint.Dir)] public string AssetDirectory = "assets/";

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsStringName() == PropertyName.Game) {
            property["hint_string"] = ReachForGodot.GameList.Join(",");
        }

        base._ValidateProperty(property);
    }
}
#endif