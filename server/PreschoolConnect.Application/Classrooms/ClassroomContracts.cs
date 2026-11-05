using System.Text.Json;
using System.Text.Json.Serialization;

namespace PreschoolConnect.Application.Classrooms;

public sealed record ClassroomScope(Guid SchoolId, Guid UserId, bool IsAdmin);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClassroomActivity(TimeOnly Start, TimeOnly End, string Activity);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClassroomNote(DateOnly WeekStart, string Text);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClassroomUpdate(string Name, string Room, int? Capacity,
    ClassroomActivity[] Schedule, ClassroomNote[] WeeklyNotes, DateTimeOffset LastActionAtUtc);
public sealed record ClassroomTeacher(Guid Id, string Name, string Role);
public sealed record ClassroomStudent(Guid Id, string Name, DateOnly Birthday);
public sealed record ClassroomSummary(Guid Id, Guid SchoolId, Guid SchoolYearId, string Name,
    string Room, int? Capacity, int StudentCount, ClassroomTeacher[] Teachers);
public sealed record ClassroomDetail(ClassroomSummary Classroom, string SchoolYearName,
    ClassroomStudent[] Students, ClassroomActivity[] Schedule, ClassroomNote[] WeeklyNotes,
    DateOnly Today, DateTimeOffset LastActionAtUtc);
public enum ClassroomFailure { Invalid, NotFound, Forbidden, Conflict }
public sealed class ClassroomException(ClassroomFailure failure, string message) : Exception(message)
{
    public ClassroomFailure Failure { get; } = failure;
}
public interface IClassroomService
{
    Task<ClassroomSummary[]> ListAsync(ClassroomScope scope, CancellationToken ct);
    Task<ClassroomDetail> GetAsync(ClassroomScope scope, Guid id, CancellationToken ct);
    Task<ClassroomDetail> UpdateAsync(ClassroomScope scope, Guid id, ClassroomUpdate request, CancellationToken ct);
}

public static class ClassroomRules
{
    public static void Validate(string name, string room, int? capacity,
        ClassroomActivity[]? schedule, ClassroomNote[]? notes)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 ||
            room is null || room.Trim().Length > 100 || capacity is < 1 or > 500 ||
            schedule is null || schedule.Length > 20 || notes is null || notes.Length > 52)
            throw Invalid("Tên lớp tối đa 100 ký tự; phòng tối đa 100; sức chứa từ 1–500; tối đa 20 hoạt động và 52 lưu ý.");
        TimeOnly? previousEnd = null;
        foreach (var row in schedule.OrderBy(row => row?.Start))
        {
            if (row is null || row.End <= row.Start || row.Start.Second != 0 || row.End.Second != 0 ||
                row.Start.Ticks % TimeSpan.TicksPerMinute != 0 || row.End.Ticks % TimeSpan.TicksPerMinute != 0 ||
                string.IsNullOrWhiteSpace(row.Activity) || row.Activity.Trim().Length > 200 ||
                previousEnd > row.Start)
                throw Invalid("Hoạt động cần nội dung tối đa 200 ký tự và giờ HH:mm tăng dần, không chồng lấn.");
            previousEnd = row.End;
        }
        if (notes.Any(row => row is null || row.WeekStart.DayOfWeek != DayOfWeek.Monday ||
            string.IsNullOrWhiteSpace(row.Text) || row.Text.Trim().Length > 2000) ||
            notes.Select(row => row.WeekStart).Distinct().Count() != notes.Length)
            throw Invalid("Lưu ý tối đa 2000 ký tự/tuần, ngày bắt đầu là thứ Hai và không trùng tuần.");
    }

    public static void ValidateStored(string name, string room, int? capacity, string schedule, string notes)
    {
        try
        {
            Validate(name, room, capacity, JsonSerializer.Deserialize<ClassroomActivity[]>(schedule),
                JsonSerializer.Deserialize<ClassroomNote[]>(notes));
        }
        catch (JsonException)
        {
            throw Invalid("Lịch sinh hoạt hoặc lưu ý không đúng định dạng.");
        }
    }

    private static ClassroomException Invalid(string message) => new(ClassroomFailure.Invalid, message);
}
