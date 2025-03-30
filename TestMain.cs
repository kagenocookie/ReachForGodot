#define ENABLE_TESTS

namespace ReaGE.Tests;

using Godot;

#if DEBUG
using System.Reflection;
using Chickensoft.GoDotTest;
#endif

public partial class TestMain : Node
{
#if DEBUG

  public TestEnvironment Environment = default!;

  private void RunTests()
    => _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, Environment);

  public override void _Ready()
  {
    // If this is a debug build, use GoDotTest to examine the
    // command line arguments and determine if we should run tests.
    Environment = TestEnvironment.From(OS.GetCmdlineArgs());
    if (Environment.ShouldRunTests) {
      CallDeferred(MethodName.RunTests);
      return;
    }
  }

#else
  public override void _Ready()
  {
    // we don't have an actual game to run here so just shut down
    GD.Print("Shutting down");
    GetTree().Quit();
  }
#endif
}
