namespace ReaGE;

using System.Threading.Tasks;
using RszTool;

public class MotbankConverter : ResourceConverter<MotionBankResource, MotbankFile>
{
    public override MotbankFile CreateFile(FileHandler fileHandler) => new MotbankFile(fileHandler);

    public override Task<bool> Import(MotbankFile file, MotionBankResource target)
    {
        target.Uvar = string.IsNullOrEmpty(file.UvarPath) ? null : Importer.FindOrImportResource<UvarResource>(file.UvarPath, Config, WritesEnabled);
        target.MotionList = new MotionBankEntry[file.motlistCount];
        for (int i = 0; i < file.motlistCount; ++i) {
            var exportItem = file.MotlistItems[i];
            var import = new MotionBankEntry();
            import.BankID = (int)exportItem.BankID;
            import.BankType = exportItem.BankType;
            import.BankTypeMaskBits = (uint)exportItem.BankTypeMaskBits;

            import.Motion = Importer.FindOrImportResource<MotionListResource>(exportItem.Path, Config, WritesEnabled);
            import.ResourceName = exportItem.BankID + ": " + Path.GetFileNameWithoutExtension(exportItem.Path);
            target.MotionList[i] = import;
        }
        return Task.FromResult(true);
    }

    public override Task<bool> Export(MotionBankResource source, MotbankFile file)
    {
        file.UvarPath = source.Uvar?.Asset?.ExportedFilename ?? string.Empty;
        if (source.MotionList == null) {
            file.motlistCount = 0;
            file.MotlistItems.Clear();
            return Task.FromResult(true);
        }
        file.motlistCount = source.MotionList.Length;
        for (int i = 0; i < file.motlistCount; ++i) {
            var importItem = source.MotionList[i];
            var exportItem = new RszTool.Motbank.MotlistItem(file.FileHandler.FileVersion);

            exportItem.BankID = importItem.BankID;
            exportItem.BankType = importItem.BankType;
            exportItem.BankTypeMaskBits = importItem.BankTypeMaskBits;
            exportItem.Path = importItem.Motion?.Asset?.ExportedFilename ?? string.Empty;
            file.MotlistItems.Add(exportItem);
        }

        return Task.FromResult(true);
    }
}
