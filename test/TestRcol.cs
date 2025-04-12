using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestRcol : TestBase
{
    public TestRcol(Node testScene) : base(testScene) { }

    [Test]
    public async Task FullReadTest()
    {
        var converter = new AssetConverter(GodotImportOptions.testImport);
        await ExecuteFullReadTest("rcol", async (game, fileOption, filepath) => {
            converter.Game = game;
            using var file = converter.Rcol.CreateFile(filepath);
            converter.Rcol.LoadFile(file).ShouldBe(true);

            if (file.FileHandler.FileVersion >= 25) {
                file.Groups.SelectMany(gg => gg.Shapes.Where(sh => sh.Info.UserDataIndex != 0)).Count().ShouldBe(0, "shapes have userdata again");
                file.Groups.SelectMany(gg => gg.ExtraShapes.Where(sh => sh.Info.UserDataIndex != 1)).Count().ShouldBe(0, "shapes have userdata again");

                file.Groups
                    .Where(grp => grp.Info.NumExtraShapes > 0)
                    .ShouldAllBe(grp => grp.ExtraShapes.Count == grp.Info.NumExtraShapes && grp.ExtraShapes.Count == grp.Shapes.Count);
            }
            file.Groups.Count(gg => gg.Info.UserData != null || gg.Info.UserDataIndex != 0).ShouldBe(0, "groups do indeed have userdata");

            var node = new RcolRootNode() { Asset = new AssetReference(PathUtils.FullToRelativePath(filepath, converter.AssetConfig)!) };
            try {
                await converter.Rcol.Import(file, node);
                var sets = node.Sets.ToArray();

                sets.Length.ShouldBe(file.RequestSets.Count);
                node.Groups.Count().ShouldBe(file.Groups.Count);
                sets.Count(s => s.Group == null).ShouldBe(0);
                sets
                    .GroupBy(s => s.Group)
                    .ShouldAllBe(grp => grp.Key!.Shapes.All(s => s.SetDatas!.Count == grp.Count()));

                // export and verify equality with original data
                using var exportFile = converter.Rcol.CreateFile(new MemoryStream(), file.FileHandler.FileVersion);
                (await converter.Rcol.Export(node, exportFile)).ShouldBe(true);
                exportFile.Groups.Count.ShouldBe(file.Groups.Count);
                exportFile.RequestSets.Count.ShouldBe(file.RequestSets.Count);
                exportFile.IgnoreTags.Select(t => t.ignoreTagString).ShouldBeEquivalentTo(file.IgnoreTags.Select(t => t.ignoreTagString));
                exportFile.RSZ.ObjectList.Count.ShouldBe(file.RSZ.ObjectList.Count);
                exportFile.RSZ.InstanceInfoList.Count.ShouldBe(file.RSZ.InstanceInfoList.Count);

                // exportFile.RSZ.InstanceInfoList.Select(a => a.CRC).ShouldBeEquivalentTo(file.RSZ.InstanceInfoList.Select(b => b.CRC));

                foreach (var (a, b) in exportFile.Groups.Select((grp, i) => (grp, file.Groups[i]))) {
                    a.Info.guid.ShouldBe(b.Info.guid);
                    a.Info.LayerGuid.ShouldBe(b.Info.LayerGuid);
                    a.Info.MaskBits.ShouldBe(b.Info.MaskBits);
                    a.Info.MaskGuids.ShouldBe(b.Info.MaskGuids);
                    a.Info.LayerIndex.ShouldBe(b.Info.LayerIndex);
                    a.Info.NumExtraShapes.ShouldBe(b.Info.NumExtraShapes);

                    a.Shapes.Select(s => s.Info.Guid).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Info.Guid));
                    a.Shapes.Select(s => s.Info.Name).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Info.Name));
                    a.Shapes.Select(s => s.Info.primaryJointNameStr).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Info.primaryJointNameStr));
                    a.Shapes.Select(s => s.Info.secondaryJointNameStr).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Info.secondaryJointNameStr));
                    a.Shapes.Select(s => s.Info.shapeType).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Info.shapeType));
                    a.Shapes.Select(s => s.Info.SkipIdBits).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Info.SkipIdBits));
                    a.Shapes.Select(s => s.Info.IgnoreTagBits).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Info.IgnoreTagBits));
                    a.Shapes.Select(s => s.Info.Attribute).ShouldBeEquivalentTo(b.Shapes.Select(s => s.Info.Attribute));
                }

                foreach (var (a, b) in exportFile.RequestSets.Select((grp, i) => (grp.Info, file.RequestSets[i].Info))) {
                    a.ID.ShouldBe(b.ID);
                    a.GroupIndex.ShouldBe(b.GroupIndex);
                    a.Name.ShouldBe(b.Name);
                    a.KeyName.ShouldBe(b.KeyName);
                    a.status.ShouldBe(b.status);
                    a.ShapeOffset.ShouldBe(b.ShapeOffset);
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
