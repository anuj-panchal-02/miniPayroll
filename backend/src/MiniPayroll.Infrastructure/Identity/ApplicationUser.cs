using Microsoft.AspNetCore.Identity;
using MiniPayroll.Domain.Entities;

namespace MiniPayroll.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? CompanyId { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Company? Company { get; set; }
}
