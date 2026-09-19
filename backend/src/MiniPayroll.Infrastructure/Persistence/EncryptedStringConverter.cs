using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    public const string Purpose = "MiniPayroll.Employee.Bank.v1";

    public EncryptedStringConverter(IDataProtector protector)
        : base(
            plain => Protect(protector, plain),
            cipher => Unprotect(protector, cipher))
    {
        ArgumentNullException.ThrowIfNull(protector);
    }

    private static string Protect(IDataProtector protector, string? plain) =>
        protector.Protect(plain ?? string.Empty);

    private static string Unprotect(IDataProtector protector, string? cipher)
    {
        if (string.IsNullOrEmpty(cipher))
        {
            return string.Empty;
        }

        try
        {
            return protector.Unprotect(cipher);
        }
        catch (CryptographicException)
        {
            // Stale or foreign key-ring ciphertext must not take down employee/payroll reads.
            return string.Empty;
        }
    }
}
