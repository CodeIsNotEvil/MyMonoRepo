using System.Security.Cryptography;
using System.Text;

namespace GroceryTracker.Domain.Import;

/// <summary>
/// Name-based ids, so the same imported booking gets the same id every time it is imported —
/// on this device, on another one, or after a reset of the local cache.
/// </summary>
public static class DeterministicGuid
{
  public static Guid Create(Guid scope, string name)
  {
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{scope:N}|{name}"));
    var bytes = hash.AsSpan(0, 16).ToArray();

    // Stamp the version (5, name-based) and variant bits so it reads as an ordinary UUID.
    bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
    bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

    return new Guid(bytes);
  }
}
