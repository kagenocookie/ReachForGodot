using System.Reflection;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using GodotTestDriver;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestRcol : TestBase
{
    public TestRcol(Node testScene) : base(testScene) { }

    [Test]
    public void FullReadTest()
    {
        var converter = new AssetConverter(new GodotImportOptions(RszImportType.Placeholders, RszImportType.Reimport, RszImportType.CreateOrReuse, RszImportType.Reimport) {
            logInfo = false,
            allowWriting = false,
        });
        ExecuteFullReadTest("rcol", (game, fileOption, filepath) => {
            converter.Game = game;
            using var file = converter.Rcol.CreateFile(filepath);
            converter.Rcol.LoadFile(file).ShouldBe(true);

            if (file.FileHandler.FileVersion >= 25) {
                file.GroupInfoList.SelectMany(gg => gg.Shapes.Where(sh => sh.userDataIndex != 0)).Count().ShouldBe(0, "shapes have userdata again");

                file.GroupInfoList
                    .Where(grp => grp.Info.extraShapes > 0)
                    .ShouldAllBe(grp => grp.ExtraShapes.Count == grp.Info.extraShapes && grp.ExtraShapes.Count == grp.Shapes.Count);
            }
            file.GroupInfoList.Count(gg => gg.Info.UserData != null || gg.Info.userDataIndex != 0).ShouldBe(0, "groups do indeed have userdata");

            var node = new RcolRootNode() { Asset = new AssetReference(PathUtils.FullToRelativePath(filepath, converter.AssetConfig)!) };
            try {
                converter.Rcol.Import(file, node);
                var sets = node.Sets.ToArray();

                sets.Length.ShouldBe(file.RequestSetInfoList.Count);
                node.Groups.Count().ShouldBe(file.GroupInfoList.Count);
                sets.Count(s => s.Group == null).ShouldBe(0);
                sets
                    .GroupBy(s => s.Group)
                    .ShouldAllBe(grp => grp.Key!.Shapes.All(s => s.SetDatas!.Count == grp.Count()));

                // export and verify equality with original data
                using var exportFile = converter.Rcol.CreateFile(new MemoryStream(), file.FileHandler.FileVersion);
                converter.Rcol.ExportSync(node, exportFile).ShouldBe(true);
                exportFile.GroupInfoList.Count.ShouldBe(file.GroupInfoList.Count);
                exportFile.RequestSetInfoList.Count.ShouldBe(file.RequestSetInfoList.Count);
                exportFile.IgnoreTags.ShouldBeEquivalentTo(file.IgnoreTags);
                exportFile.RSZ.ObjectList.Count.ShouldBe(file.RSZ.ObjectList.Count);
                exportFile.RSZ.InstanceInfoList.Count.ShouldBe(file.RSZ.InstanceInfoList.Count);

                exportFile.RSZ.InstanceInfoList.Select(a => a.CRC).ShouldBeEquivalentTo(file.RSZ.InstanceInfoList.Select(b => b.CRC));

                foreach (var (a, b) in exportFile.GroupInfoList.Select((grp, i) => (grp, file.GroupInfoList[i]))) {
                    a.Info.guid.ShouldBe(b.Info.guid);
                    a.Info.layerGuid.ShouldBe(b.Info.layerGuid);
                    a.Info.maskBits.ShouldBe(b.Info.maskBits);
                    a.Info.MaskGuids.ShouldBe(b.Info.MaskGuids);
                    a.Info.layerIndex.ShouldBe(b.Info.layerIndex);
                    a.Info.extraShapes.ShouldBe(b.Info.extraShapes);

                    a.Shapes.Select(s => s.Guid).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Guid));
                    a.Shapes.Select(s => s.name).ShouldBeEquivalentTo(b.Shapes.Select(s => s.name));
                    a.Shapes.Select(s => s.primaryJointNameStr).ShouldBeEquivalentTo(b.Shapes.Select(s => s.primaryJointNameStr));
                    a.Shapes.Select(s => s.secondaryJointNameStr).ShouldBeEquivalentTo(b.Shapes.Select(s => s.secondaryJointNameStr));
                    a.Shapes.Select(s => s.shapeType).ShouldBeEquivalentTo(b.Shapes.Select(s => s.shapeType));
                    a.Shapes.Select(s => s.skipIdBits).ShouldBeEquivalentTo(b.Shapes.Select(s => s.skipIdBits));
                    a.Shapes.Select(s => s.ignoreTagBits).ShouldBeEquivalentTo(b.Shapes.Select(s => s.ignoreTagBits));
                    a.Shapes.Select(s => s.attribute).ShouldBeEquivalentTo(b.Shapes.Select(s => s.attribute));
                }

                foreach (var (a, b) in exportFile.RequestSetInfoList.Select((grp, i) => (grp, file.RequestSetInfoList[i]))) {
                    a.id.ShouldBe(b.id);
                    a.groupIndex.ShouldBe(b.groupIndex);
                    a.name.ShouldBe(b.name);
                    a.keyName.ShouldBe(b.keyName);
                    a.status.ShouldBe(b.status);
                    a.shapeOffset.ShouldBe(b.shapeOffset);
                    if (file.FileHandler.FileVersion >= 25) {
                        a.groupUserdataIndexStart.ShouldBe(b.groupUserdataIndexStart);
                        a.requestSetUserdataIndex.ShouldBe(b.requestSetUserdataIndex);
                        a.requestSetIndex.ShouldBe(b.requestSetIndex);
                    }
                }

            } finally {
                node.QueueFree();
            }
        });
    }
}
