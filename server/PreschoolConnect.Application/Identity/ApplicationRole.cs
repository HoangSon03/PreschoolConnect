using Microsoft.AspNetCore.Identity;

namespace PreschoolConnect.Application.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public Guid? SchoolId { get; set; }
}
