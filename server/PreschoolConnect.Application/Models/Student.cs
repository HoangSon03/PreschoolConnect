namespace PreschoolConnect.Application.Models;

public class Student : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "";
    public string Address { get; set; } = "";
    public string Guardian { get; set; } = "";
    public string Relationship { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Pickup { get; set; } = "";
    public string Emergency { get; set; } = "";
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
    public string Care { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;
}
