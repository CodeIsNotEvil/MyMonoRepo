using System.Text.Json;
using System.Text.Json.Serialization;

namespace GroceryTracker.UI.Blazor.Services;

/// <summary>
/// Serializer settings for talking to the API.
/// </summary>
/// <remarks>
/// The string enum converter has to match the API's, which writes enum names. Without it the client
/// would fail to read back a sync response the moment it contained a conflict.
/// </remarks>
public static class GroceryJson
{
  public static JsonSerializerOptions Options { get; } = Create();

  private static JsonSerializerOptions Create()
  {
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter());
    return options;
  }
}
