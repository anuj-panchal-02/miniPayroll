using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    public const string Purpose = "MiniPayroll.Employee.Bank.v1";

    public EncryptedStringConverter(IDataProtector protector)
        : base(
            plain => protector.Protect(plain),
            cipher => protector.Unprotect(cipher))
    {
        ArgumentNullException.ThrowIfNull(protector);
    }
}
