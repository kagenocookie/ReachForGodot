namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

public class UserdataConverter : RszAssetConverter<UserdataResource, UserFile, UserdataResource, UserdataResource>
{
    public override UserFile CreateFile(FileHandler fileHandler) => new UserFile(FileOption, fileHandler);

    public override Task<bool> Import(UserFile file, UserdataResource target)
    {
        file.Read();
        target.Clear();

        GenerateResources(target, file.ResourceInfoList);
        Debug.Assert(file.RSZ.ObjectList.Count == 1);

        foreach (var instance in file.RSZ.ObjectList) {
            if (string.IsNullOrEmpty(target.Classname) || target.Classname != instance.RszClass.name) {
                target.Data.ChangeClassname(instance.RszClass.name);
            }
            ApplyObjectValues(target.Data, instance);
            if (WritesEnabled) ResourceSaver.Save(target);
            return Task.FromResult(true);
        }
        return Task.FromResult(true);
    }

    public override Task<bool> Export(UserdataResource source, UserFile file)
    {
        StoreResources(source.Resources, file.ResourceInfoList, false);
        file.RSZ.ClearInstances();
        var instance = ExportREObject(source.Data, file.RSZ, FileOption, file);
        file.RSZ.AddToObjectTable(instance);
        return Task.FromResult(true);
    }
}
