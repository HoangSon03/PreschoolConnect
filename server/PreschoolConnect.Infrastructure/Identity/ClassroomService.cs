using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Classrooms;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Infrastructure.Identity;

public static class ClassroomQueries
{
    public static IQueryable<Classroom> Visible(ApplicationDbContext db, ClassroomScope scope, bool includeDeleted = false)
    {
        var query = (includeDeleted ? db.Classrooms.IgnoreQueryFilters() : db.Classrooms)
            .AsNoTracking().Where(row => row.SchoolId == scope.SchoolId);
        if (!scope.IsAdmin)
            query = query.Where(row => row.LastAction != "D" &&
                db.SchoolYears.Any(year => year.Id == row.SchoolYearId && year.SchoolId == scope.SchoolId && year.LastAction != "D") &&
                db.TeacherAssignments.Any(assignment => assignment.SchoolId == scope.SchoolId &&
                    assignment.ClassroomId == row.Id && assignment.EndedAtUtc == null && assignment.LastAction != "D" &&
                    db.Teachers.Any(teacher => teacher.Id == assignment.TeacherId && teacher.SchoolId == scope.SchoolId &&
                        teacher.UserId == scope.UserId && teacher.IsActive && teacher.LastAction != "D")));
        return query;
    }
}

public sealed class ClassroomService(ApplicationDbContext db, TimeProvider clock) : IClassroomService
{
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(clock.GetUtcNow(), "Asia/Ho_Chi_Minh").DateTime);

    private IQueryable<StudentInClass> Enrollments(ClassroomScope scope, DateOnly today) =>
        from enrollment in db.StudentInClasses.AsNoTracking()
        join student in db.Students on enrollment.StudentId equals student.Id
        join classroom in db.Classrooms on enrollment.ClassroomId equals classroom.Id
        join year in db.SchoolYears on classroom.SchoolYearId equals year.Id
        where enrollment.SchoolId == scope.SchoolId && student.SchoolId == scope.SchoolId &&
            classroom.SchoolId == scope.SchoolId && year.SchoolId == scope.SchoolId &&
            enrollment.StartDate <= today && today < enrollment.EndDate &&
            year.StartDate <= today && today <= year.EndDate
        select enrollment;

    public async Task<ClassroomSummary[]> ListAsync(ClassroomScope scope, CancellationToken ct)
    {
        var classes = await ClassroomQueries.Visible(db, scope).OrderBy(row => row.Name).ToArrayAsync(ct);
        var ids = classes.Select(row => row.Id).ToArray();
        var counts = await Enrollments(scope, Today).Where(row => ids.Contains(row.ClassroomId))
            .GroupBy(row => row.ClassroomId).Select(group => new { Id = group.Key, Count = group.Select(row => row.StudentId).Distinct().Count() })
            .ToDictionaryAsync(row => row.Id, row => row.Count, ct);
        var teachers = await (
            from assignment in db.TeacherAssignments.AsNoTracking()
            join teacher in db.Teachers on assignment.TeacherId equals teacher.Id
            where assignment.SchoolId == scope.SchoolId && teacher.SchoolId == scope.SchoolId &&
                ids.Contains(assignment.ClassroomId) && assignment.EndedAtUtc == null && teacher.IsActive
            orderby teacher.Name
            select new { assignment.ClassroomId, Teacher = new ClassroomTeacher(teacher.Id, teacher.Name, assignment.Role) }).ToArrayAsync(ct);
        return classes.Select(row => new ClassroomSummary(row.Id, row.SchoolId, row.SchoolYearId, row.Name,
            row.Room, row.Capacity, counts.GetValueOrDefault(row.Id),
            teachers.Where(item => item.ClassroomId == row.Id).Select(item => item.Teacher).ToArray())).ToArray();
    }

    public async Task<ClassroomDetail> GetAsync(ClassroomScope scope, Guid id, CancellationToken ct)
    {
        var summary = (await ListAsync(scope, ct)).SingleOrDefault(row => row.Id == id)
            ?? throw new ClassroomException(ClassroomFailure.NotFound, "Không tìm thấy lớp học.");
        var row = await db.Classrooms.AsNoTracking().SingleAsync(row => row.Id == id && row.SchoolId == scope.SchoolId, ct);
        var year = await db.SchoolYears.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(year => year.Id == row.SchoolYearId && year.SchoolId == scope.SchoolId, ct);
        var today = Today;
        var students = await (from enrollment in Enrollments(scope, today)
            join student in db.Students on enrollment.StudentId equals student.Id
            where enrollment.ClassroomId == id && student.SchoolId == scope.SchoolId
            select new { student.Id, Name = student.LastName + " " + student.FirstName, Birthday = student.DateOfBirth })
            .Distinct().OrderBy(student => student.Name).ThenBy(student => student.Id).ToArrayAsync(ct);
        return new(summary, year.Name, students.Select(student => new ClassroomStudent(student.Id, student.Name, student.Birthday)).ToArray(),
            JsonSerializer.Deserialize<ClassroomActivity[]>(row.ScheduleJson)!,
            JsonSerializer.Deserialize<ClassroomNote[]>(row.WeeklyNotesJson)!, today, row.LastActionAtUtc);
    }

    public async Task<ClassroomDetail> UpdateAsync(ClassroomScope scope, Guid id, ClassroomUpdate request, CancellationToken ct)
    {
        if (!scope.IsAdmin) throw new ClassroomException(ClassroomFailure.Forbidden, "Chỉ quản trị viên trường được chỉnh sửa lớp.");
        ClassroomRules.Validate(request.Name, request.Room, request.Capacity, request.Schedule, request.WeeklyNotes);
        var row = await db.Classrooms.SingleOrDefaultAsync(row => row.Id == id && row.SchoolId == scope.SchoolId, ct)
            ?? throw new ClassroomException(ClassroomFailure.NotFound, "Không tìm thấy lớp học.");
        if (row.LastActionAtUtc != request.LastActionAtUtc)
            throw new ClassroomException(ClassroomFailure.Conflict, "Lớp học đã thay đổi. Vui lòng tải lại trước khi lưu.");
        var name = request.Name.Trim();
        var others = db.Classrooms.Where(item => item.SchoolId == scope.SchoolId &&
            item.SchoolYearId == row.SchoolYearId && item.Id != id);
        var normalized = name.ToLowerInvariant();
        var duplicate = db.Database.IsNpgsql()
            ? await others.AnyAsync(item => EF.Functions.Collate(item.Name.Trim(), "pg_c_utf8").ToLower() == normalized, ct)
            : await others.AnyAsync(item => item.Name.Trim().ToLower() == normalized, ct);
        if (duplicate) throw new ClassroomException(ClassroomFailure.Conflict, "Tên lớp đã tồn tại trong năm học.");
        row.Name = name; row.Room = request.Room.Trim(); row.Capacity = request.Capacity;
        row.ScheduleJson = JsonSerializer.Serialize(request.Schedule.OrderBy(item => item.Start)
            .Select(item => item with { Activity = item.Activity.Trim() }));
        row.WeeklyNotesJson = JsonSerializer.Serialize(request.WeeklyNotes.OrderByDescending(item => item.WeekStart)
            .Select(item => item with { Text = item.Text.Trim() }));
        await db.SaveChangesAsync(ct);
        return await GetAsync(scope, id, ct);
    }
}
