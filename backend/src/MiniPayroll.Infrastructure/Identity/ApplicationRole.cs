using Microsoft.AspNetCore.Identity;

namespace MiniPayroll.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string name) : base(name)
    {
    }
}
