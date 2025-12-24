using System.Security.Cryptography;
using Freeway.Domain.Interfaces;

namespace Freeway.Infrastructure.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly string _prefix;

    public ApiKeyService()
    {
        _prefix = Environment.GetEnvironmentVariable("API_KEY_PREFIX") ?? "fw_";
    }

    public (string rawKey, string hash, string prefix) GenerateApiKey()
    {
        // Generate 32 random bytes and encode as base64url
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var rawKey = $"{_prefix}{token}";
        var hash = BCrypt.Net.BCrypt.HashPassword(rawKey);
        var keyPrefix = rawKey[..Math.Min(8, rawKey.Length)];

        return (rawKey, hash, keyPrefix);
    }

    public bool VerifyApiKey(string rawKey, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(rawKey, hash);
        }
        catch
        {
            return false;
        }
    }
}
