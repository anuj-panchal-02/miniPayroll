using System.Text;

namespace MiniPayroll.Domain.Auth;

public static class JwtSigningKeyRules
{
    public const int MinimumUtf8ByteLength = 32;

    public static string Require(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Jwt:Key is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(key) < MinimumUtf8ByteLength)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 UTF-8 bytes.");
        }

        return key;
    }
}
