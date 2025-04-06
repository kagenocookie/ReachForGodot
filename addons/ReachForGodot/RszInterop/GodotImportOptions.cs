namespace ReaGE;

using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

public class GodotImportOptions
{
    public static readonly GodotImportOptions placeholderImport = new(RszImportType.Placeholders, RszImportType.Placeholders,  RszImportType.Placeholders, RszImportType.Placeholders);
    public static readonly GodotImportOptions thisFolderOnly = new(RszImportType.Placeholders, RszImportType.Reimport, RszImportType.Reimport, RszImportType.CreateOrReuse) { linkedScenes = false};
    public static readonly GodotImportOptions importMissing = new(RszImportType.CreateOrReuse, RszImportType.CreateOrReuse, RszImportType.CreateOrReuse, RszImportType.CreateOrReuse);
    public static readonly GodotImportOptions importTreeChanges = new(RszImportType.Reimport, RszImportType.Reimport, RszImportType.Reimport, RszImportType.CreateOrReuse);
    public static readonly GodotImportOptions forceReimportStructure = new(RszImportType.ForceReimport, RszImportType.ForceReimport,  RszImportType.ForceReimport, RszImportType.CreateOrReuse);
    public static readonly GodotImportOptions forceReimportThisStructure = new(RszImportType.ForceReimport, RszImportType.ForceReimport,  RszImportType.ForceReimport, RszImportType.CreateOrReuse) { linkedScenes = false };
    public static readonly GodotImportOptions fullReimport = new(RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport);
    public static readonly GodotImportOptions fullReimportTextured = new(RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport) { overrideImportMaterials = true };
    public static readonly GodotImportOptions fullReimportUntextured = new(RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport, RszImportType.ForceReimport) { overrideImportMaterials = false };

    public static readonly GodotImportOptions testImport = new(RszImportType.Reimport, RszImportType.Reimport, RszImportType.CreateOrReuse, RszImportType.Placeholders) {
        logInfo = false,
        allowWriting = false,
        linkedScenes = false,
    };

    public RszImportType folders = RszImportType.Placeholders;
    public RszImportType prefabs = RszImportType.CreateOrReuse;
    public RszImportType userdata = RszImportType.Placeholders;
    public RszImportType assets = RszImportType.Placeholders;
    public bool linkedScenes = true;
    public bool? overrideImportMaterials;

    public bool logInfo = true;
    public bool logErrors = true;
    public bool allowWriting = true;

    public GodotImportOptions(RszImportType folders, RszImportType prefabs, RszImportType userdata, RszImportType assets)
    {
        this.folders = folders;
        this.prefabs = prefabs;
        this.userdata = userdata;
        this.assets = assets;
    }
};

public enum RszImportType
{
    /// <summary>If an asset does not exist, only create a placeholder resource for it.</summary>
    Placeholders,
    /// <summary>If an asset does not exist or is merely a placeholder, import and generate its data. Do nothing if any of its contents are already imported.</summary>
    CreateOrReuse,
    /// <summary>Reimport the full asset from the source file, maintaining any local changes as much as possible.</summary>
    Reimport,
    /// <summary>Discard any local data and regenerate assets.</summary>
    ForceReimport,
}
