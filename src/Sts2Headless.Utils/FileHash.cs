using System.Security.Cryptography;

namespace Sts2Headless.Utils;

// File hashing helpers. Currently just the SHA-256 used to cross-check that
// the GAME_VERSION pin matches the actual vendor/sts2.dll bytes.
public static class FileHash
{
    // Lowercase hex SHA-256 of a file's contents.
    public static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }
}
