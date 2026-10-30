using Microsoft.AspNetCore.Identity;

namespace PreschoolConnect.Application.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? SchoolId { get; set; }
    public Guid? StudentId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
