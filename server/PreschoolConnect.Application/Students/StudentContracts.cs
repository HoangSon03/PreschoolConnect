using System.Text.Json.Serialization;

namespace PreschoolConnect.Application.Students;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudentProfile(string Name, DateOnly Birthday, string Gender, string Address,
    string Guardian, string Relationship, string Phone, string? Email, string? Pickup, string Emergency,
    decimal? Height, decimal? Weight, string? Care);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EnrollmentRequest(Guid ClassroomId, DateOnly StartDate, string AgeGroup, string StudyMode);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudentRequest(StudentProfile Profile, EnrollmentRequest? Enrollment);

public sealed record StudentScope(Guid SchoolId, Guid UserId, bool IsAdmin, bool IsTeacher, bool IsParent);
public sealed record EnrollmentDto(Guid Id, Guid ClassroomId, string ClassroomName, Guid SchoolYearId,
    string SchoolYearName, DateOnly StartDate, DateOnly EndDate, string AgeGroup, string StudyMode, string[] Teachers);
public sealed record StudentDto(Guid Id, StudentProfile Profile, EnrollmentDto[] Enrollments,
    string Status, bool IsNew, DateOnly Today, DateTimeOffset LastActionAtUtc);
public enum StudentFailure { Invalid, Forbidden, NotFound, Conflict }
public sealed class StudentException(StudentFailure failure, string message) : Exception(message)
{
    public StudentFailure Failure { get; } = failure;
}
public interface IStudentService
{
    Task<StudentDto[]> ListAsync(StudentScope scope, CancellationToken ct);
    Task<StudentDto> GetAsync(StudentScope scope, Guid id, CancellationToken ct);
    Task<StudentDto> SaveAsync(StudentScope scope, Guid? id, StudentRequest request, CancellationToken ct);
    Task<StudentDto> EnrollAsync(StudentScope scope, Guid id, EnrollmentRequest request, CancellationToken ct);
    Task<StudentDto> EndAsync(StudentScope scope, Guid id, Guid enrollmentId, CancellationToken ct);
}
