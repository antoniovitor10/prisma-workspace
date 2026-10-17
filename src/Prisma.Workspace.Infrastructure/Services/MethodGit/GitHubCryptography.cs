using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public static class GitHubCryptography
{
    public static bool HasSignatureFormat(string? signature) => signature is { Length: 71 }
        && signature.StartsWith("sha256=", StringComparison.Ordinal) && signature.AsSpan(7).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    public static bool VerifySignature(ReadOnlySpan<byte> body, string signature, string secret)
    {
        if (string.IsNullOrEmpty(secret) || !HasSignatureFormat(signature)) return false;
        byte[] supplied;
        try { supplied = Convert.FromHexString(signature[7..]); }
        catch (FormatException) { return false; }
        var key = Encoding.UTF8.GetBytes(secret);
        try { return CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, body), supplied); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public static string CreateAppJwt(long appId, string privateKeyPem, DateTimeOffset now)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        { iss = appId.ToString(System.Globalization.CultureInfo.InvariantCulture), iat = now.AddSeconds(-60).ToUnixTimeSeconds(), exp = now.AddMinutes(9).ToUnixTimeSeconds() }));
        var message = $"{header}.{payload}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.Replace("\\n", "\n", StringComparison.Ordinal));
        return $"{message}.{Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(message), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))}";
    }
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
