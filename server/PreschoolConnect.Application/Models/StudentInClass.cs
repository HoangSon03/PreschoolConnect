namespace PreschoolConnect.Application.Models;

public class StudentInClass : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ClassroomId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string AgeGroup { get; set; } = "";
    public string StudyMode { get; set; } = "";
}
