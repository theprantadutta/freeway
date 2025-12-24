namespace Freeway.Domain.Interfaces;

public interface IApiKeyService
{
    (string rawKey, string hash, string prefix) GenerateApiKey();
    bool VerifyApiKey(string rawKey, string hash);
}
