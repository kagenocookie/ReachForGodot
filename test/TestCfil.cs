using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using GodotTestDriver;
using RszTool;
using Shouldly;

namespace ReaGE.Tests;

public partial class TestCfil : TestBase
{
    public TestCfil(Node testScene) : base(testScene) { }

    [Test]
    public void FullReadTest()
    {
        ExecuteFullReadTest("cfil", (game, fileOption, filepath) => {
            using var file = new CfilFile(new FileHandler(filepath));
            file.Read();

            (file.ukn3 + file.ukn4 + file.ukn5 + file.ukn6).ShouldBe(0, "Found unhandled CFIL bytes");
            (file.uknOffset >= file.guidListOffset + sizeof(long) * 2 * file.guidCount).ShouldBeTrue("Found unknown data in CFIL");
        });
    }
}
