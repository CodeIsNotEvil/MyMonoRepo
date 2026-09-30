using System.Xml.Linq;
using CINE.GroceryTracker.UI.Blazor.Services;

namespace CINE.GroceryTracker.UI.Blazor.Tests;

public class AppVersionTests
{
  [Theory]
  [InlineData("0.2.0+6064bb8a1f", "0.2.0")]
  [InlineData("0.2.0", "0.2.0")]
  [InlineData(null, "unknown")]
  [InlineData("", "unknown")]
  public void The_commit_suffix_is_dropped(string? informational, string expected) =>
    Assert.Equal(expected, AppVersion.Clean(informational));

  [Fact]
  public void The_built_version_comes_from_directory_build_props()
  {
    var props = new DirectoryInfo(AppContext.BaseDirectory);
    while (!File.Exists(Path.Combine(props.FullName, "Directory.Build.props")))
    {
      props = props.Parent ?? throw new FileNotFoundException("Directory.Build.props not found above the test output.");
    }

    var version = XDocument.Load(Path.Combine(props.FullName, "Directory.Build.props")).Descendants("Version").Single().Value;

    Assert.Equal(version, AppVersion.Current);
  }
}
