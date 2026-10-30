namespace PreschoolConnect.Application.Models;

public class Classroom
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid SchoolYearId { get; set; }
    public string Name { get; set; } = string.Empty;
}
