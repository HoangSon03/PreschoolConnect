namespace PreschoolConnect.Application.Models;

public class Classroom : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid SchoolYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Room { get; set; } = "";
    public int? Capacity { get; set; }
    public string ScheduleJson { get; set; } = "[]";
    public string WeeklyNotesJson { get; set; } = "[]";
}
