using System.Security.Cryptography;
using System.Text;

namespace SimpleServiceChecker.App.Security;

public sealed class GoogleKeyStore(string filePath)
{
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("SimpleServiceChecker.GoogleMaps.v1");

    public void Save(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("The key file has no parent directory.");
        _ = Directory.CreateDirectory(directory);

        var plainText = Encoding.UTF8.GetBytes(apiKey);
        try
        {
            var protectedBytes = ProtectedData.Protect(plainText, OptionalEntropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(filePath, protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainText);
        }
    }

    public string? TryLoad()
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(filePath);
            var plainText = ProtectedData.Unprotect(protectedBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
            try
            {
                return Encoding.UTF8.GetString(plainText);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainText);
            }
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
