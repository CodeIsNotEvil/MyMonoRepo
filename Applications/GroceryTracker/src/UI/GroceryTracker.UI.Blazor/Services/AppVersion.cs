using System.Reflection;

namespace CINE.GroceryTracker.UI.Blazor.Services;

/// <summary>The release this PWA was built as, from <c>&lt;Version&gt;</c> in Directory.Build.props.</summary>
/// <remarks>
/// Shown under Settings → About so a user can tell whether the service worker has picked up an update,
/// and match it against the changelog on the download page.
/// </remarks>
public static class AppVersion
{
  public static string Current { get; } = Clean(
    typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

  /// <summary>Drops the "+commit" the SDK appends to the informational version (SourceLink).</summary>
  public static string Clean(string? informationalVersion) =>
    string.IsNullOrEmpty(informationalVersion) ? "unknown" : informationalVersion.Split('+')[0];
}
