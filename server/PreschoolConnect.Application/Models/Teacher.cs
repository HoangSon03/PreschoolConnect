namespace PreschoolConnect.Application.Models;

public sealed class Teacher : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly Birthday { get; set; }
    public string Gender { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Degree { get; set; } = "";
    public int Graduated { get; set; }
    public string University { get; set; } = "";
    public int Experience { get; set; }
    public string Certificate { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public string Contract { get; set; } = "";
    public string Work { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class TeacherAssignment : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid ClassroomId { get; set; }
    public string Role { get; set; } = "";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
}
