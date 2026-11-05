using System.Text.Json.Serialization;

namespace PreschoolConnect.Application.Teachers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TeacherRequest(
    string Name, DateOnly Birthday, string Gender, string Phone, string Email, string? Address,
    string Degree, int Graduated, string University, int Experience, string? Certificate,
    DateOnly StartDate, string Contract, string Work, bool IsActive, Guid? UserId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignmentRequest(Guid ClassroomId, string Role);

public sealed record TeacherScope(Guid SchoolId, Guid UserId, bool IsAdmin);
public sealed record AssignmentDto(Guid Id, Guid ClassroomId, string ClassroomName, Guid SchoolYearId,
    string SchoolYearName, string Role, DateTimeOffset StartedAtUtc, DateTimeOffset? EndedAtUtc);
public sealed record TeacherDto(Guid Id, string Code, TeacherRequest Profile, AssignmentDto[] Assignments,
    DateTimeOffset LastActionAtUtc);
public sealed record TeacherAccountDto(Guid UserId, string Email);

public enum TeacherFailure { Invalid, NotFound, Forbidden, Conflict }
public sealed class TeacherException(TeacherFailure failure, string message) : Exception(message)
{
    public TeacherFailure Failure { get; } = failure;
}

public interface ITeacherService
{
    Task<TeacherDto[]> ListAsync(TeacherScope scope, CancellationToken cancellationToken);
    Task<TeacherDto> GetAsync(TeacherScope scope, Guid id, CancellationToken cancellationToken);
    Task<TeacherDto> SaveAsync(TeacherScope scope, Guid? id, TeacherRequest request, CancellationToken cancellationToken);
    Task<TeacherDto> AssignAsync(TeacherScope scope, Guid id, AssignmentRequest request, CancellationToken cancellationToken);
    Task<TeacherDto> EndAssignmentAsync(TeacherScope scope, Guid id, Guid assignmentId, CancellationToken cancellationToken);
    Task<TeacherAccountDto[]> AccountsAsync(TeacherScope scope, CancellationToken cancellationToken);
}
