namespace PreschoolConnect.Application.Models;

public class Student
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
