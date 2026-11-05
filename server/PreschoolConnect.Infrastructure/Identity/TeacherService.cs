using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Application.Teachers;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Infrastructure.Identity;

public sealed class TeacherService(ApplicationDbContext db) : ITeacherService
{
    private IQueryable<Teacher> Visible(TeacherScope scope) => db.Teachers.AsNoTracking().Where(teacher =>
        teacher.SchoolId == scope.SchoolId && (scope.IsAdmin || teacher.UserId == scope.UserId));

    public async Task<TeacherDto[]> ListAsync(TeacherScope scope, CancellationToken cancellationToken)
    {
        var teachers = await Visible(scope).OrderBy(teacher => teacher.Name).ToListAsync(cancellationToken);
        var ids = teachers.Select(teacher => teacher.Id).ToArray();
        var assignments = await AssignmentRows(scope, ids)
            .ToListAsync(cancellationToken);
        return teachers.Select(teacher => Dto(teacher, assignments.Where(row => row.TeacherId == teacher.Id).Select(row => row.Dto).ToArray())).ToArray();
    }

    public async Task<TeacherDto> GetAsync(TeacherScope scope, Guid id, CancellationToken cancellationToken)
    {
        var teacher = await Visible(scope).SingleOrDefaultAsync(teacher => teacher.Id == id, cancellationToken)
            ?? throw new TeacherException(TeacherFailure.NotFound, "Không tìm thấy hồ sơ giáo viên.");
        var assignments = await AssignmentRows(scope, [id]).ToArrayAsync(cancellationToken);
        return Dto(teacher, assignments.Select(row => row.Dto).ToArray());
    }

    private sealed record AssignmentRow(Guid TeacherId, AssignmentDto Dto);
    private IQueryable<AssignmentRow> AssignmentRows(TeacherScope scope, Guid[] teacherIds) =>
        from assignment in db.TeacherAssignments.AsNoTracking()
        join classroom in db.Classrooms.IgnoreQueryFilters().AsNoTracking() on assignment.ClassroomId equals classroom.Id
        join year in db.SchoolYears.IgnoreQueryFilters().AsNoTracking() on classroom.SchoolYearId equals year.Id
        where teacherIds.Contains(assignment.TeacherId) && assignment.SchoolId == scope.SchoolId &&
            classroom.SchoolId == scope.SchoolId && year.SchoolId == scope.SchoolId &&
            (scope.IsAdmin || (assignment.EndedAtUtc == null && assignment.LastAction != "D" &&
                classroom.LastAction != "D" && year.LastAction != "D" &&
                db.Teachers.Any(teacher => teacher.Id == assignment.TeacherId && teacher.SchoolId == scope.SchoolId &&
                    teacher.UserId == scope.UserId && teacher.IsActive && teacher.LastAction != "D")))
        orderby assignment.StartedAtUtc descending
        select new AssignmentRow(assignment.TeacherId, new AssignmentDto(assignment.Id, classroom.Id, classroom.Name,
            year.Id, year.Name, assignment.Role, assignment.StartedAtUtc, assignment.EndedAtUtc));

    public async Task<TeacherDto> SaveAsync(TeacherScope scope, Guid? id, TeacherRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(scope);
        Validate(request);
        var teacher = id.HasValue ? await db.Teachers.SingleOrDefaultAsync(row => row.Id == id && row.SchoolId == scope.SchoolId, cancellationToken)
            ?? throw new TeacherException(TeacherFailure.NotFound, "Không tìm thấy hồ sơ giáo viên.")
            : new Teacher { Id = Guid.NewGuid(), SchoolId = scope.SchoolId };
        if (teacher.UserId.HasValue && teacher.UserId != request.UserId)
            throw new TeacherException(TeacherFailure.Conflict, "Không thể gán lại danh tính tài khoản đã liên kết.");
        if (request.UserId.HasValue)
        {
            if (teacher.UserId != request.UserId && !await db.Users.AnyAsync(user => user.Id == request.UserId && user.IsActive &&
                db.SchoolMemberships.Any(member => member.UserId == user.Id && member.SchoolId == scope.SchoolId &&
                    member.IsActive && member.Roles.Any(role => role.Role == SchoolRoleNames.Teacher)), cancellationToken))
                throw new TeacherException(TeacherFailure.Invalid, "Tài khoản phải có quyền Teacher đang hoạt động trong trường.");
            if (await db.Teachers.IgnoreQueryFilters().AnyAsync(row => row.SchoolId == scope.SchoolId &&
                row.UserId == request.UserId && row.Id != teacher.Id, cancellationToken))
                throw new TeacherException(TeacherFailure.Conflict, "Tài khoản đã được liên kết với hồ sơ khác.");
        }
        teacher.Name = request.Name.Trim(); teacher.Birthday = request.Birthday; teacher.Gender = request.Gender;
        teacher.Phone = request.Phone.Trim(); teacher.Email = request.Email.Trim(); teacher.Address = request.Address?.Trim() ?? "";
        teacher.Degree = request.Degree; teacher.Graduated = request.Graduated; teacher.University = request.University.Trim();
        teacher.Experience = request.Experience; teacher.Certificate = request.Certificate?.Trim() ?? "";
        teacher.StartDate = request.StartDate; teacher.Contract = request.Contract; teacher.Work = request.Work;
        teacher.IsActive = request.IsActive; teacher.UserId = request.UserId;
        if (!id.HasValue) db.Teachers.Add(teacher);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, teacher.Id, cancellationToken);
    }

    public async Task<TeacherDto> AssignAsync(TeacherScope scope, Guid id, AssignmentRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(scope);
        var teacher = await db.Teachers.SingleOrDefaultAsync(row => row.Id == id && row.SchoolId == scope.SchoolId, cancellationToken)
            ?? throw new TeacherException(TeacherFailure.NotFound, "Không tìm thấy hồ sơ giáo viên.");
        if (!teacher.IsActive) throw new TeacherException(TeacherFailure.Invalid, "Giáo viên tạm nghỉ không thể nhận phân công mới.");
        if (request.Role is not ("Giáo viên chủ nhiệm" or "Giáo viên đồng phụ trách"))
            throw new TeacherException(TeacherFailure.Invalid, "Vai trò phân công không hợp lệ.");
        if (!await db.Classrooms.AnyAsync(row => row.Id == request.ClassroomId && row.SchoolId == scope.SchoolId &&
            db.SchoolYears.Any(year => year.Id == row.SchoolYearId && year.SchoolId == scope.SchoolId), cancellationToken))
            throw new TeacherException(TeacherFailure.Invalid, "Lớp và năm học phải còn tồn tại trong trường.");
        if (await db.TeacherAssignments.AnyAsync(row => row.TeacherId == id && row.ClassroomId == request.ClassroomId &&
            row.SchoolId == scope.SchoolId && row.EndedAtUtc == null, cancellationToken))
            throw new TeacherException(TeacherFailure.Conflict, "Giáo viên đã được phân công vào lớp này.");
        db.TeacherAssignments.Add(new TeacherAssignment { Id = Guid.NewGuid(), SchoolId = scope.SchoolId,
            TeacherId = id, ClassroomId = request.ClassroomId, Role = request.Role, StartedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<TeacherDto> EndAssignmentAsync(TeacherScope scope, Guid id, Guid assignmentId, CancellationToken cancellationToken)
    {
        RequireAdmin(scope);
        var row = await db.TeacherAssignments.SingleOrDefaultAsync(row => row.Id == assignmentId &&
            row.SchoolId == scope.SchoolId && row.TeacherId == id, cancellationToken)
            ?? throw new TeacherException(TeacherFailure.NotFound, "Không tìm thấy phân công.");
        if (row.EndedAtUtc is not null) throw new TeacherException(TeacherFailure.Conflict, "Phân công đã kết thúc.");
        row.EndedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<TeacherAccountDto[]> AccountsAsync(TeacherScope scope, CancellationToken cancellationToken)
    {
        RequireAdmin(scope);
        return await db.Users.AsNoTracking().Where(user => user.IsActive &&
            db.SchoolMemberships.Any(member => member.UserId == user.Id && member.SchoolId == scope.SchoolId &&
                member.IsActive && member.Roles.Any(role => role.Role == SchoolRoleNames.Teacher)))
            .OrderBy(user => user.Email).Select(user => new TeacherAccountDto(user.Id, user.Email!)).ToArrayAsync(cancellationToken);
    }

    private static void RequireAdmin(TeacherScope scope)
    {
        if (!scope.IsAdmin) throw new TeacherException(TeacherFailure.Forbidden, "Chỉ quản trị viên trường được thay đổi hồ sơ.");
    }

    private static TeacherDto Dto(Teacher row, AssignmentDto[] assignments) => new(row.Id, $"GV-{row.Id:N}",
        new(row.Name, row.Birthday, row.Gender, row.Phone, row.Email, row.Address, row.Degree, row.Graduated,
            row.University, row.Experience, row.Certificate, row.StartDate, row.Contract, row.Work, row.IsActive, row.UserId),
        assignments, row.LastActionAtUtc);

    private static void Validate(TeacherRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200 ||
            request.Birthday == default || request.Birthday > today || request.StartDate == default || request.StartDate < request.Birthday ||
            request.Gender is not ("Nữ" or "Nam") || !Regex.IsMatch(request.Phone?.Trim() ?? "", "^[0-9]{10}$") ||
            string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || !new EmailAddressAttribute().IsValid(request.Email.Trim()) ||
            request.Degree is not ("Đại học Sư phạm mầm non" or "Cao đẳng Sư phạm mầm non") ||
            request.Graduated < 1950 || request.Graduated > today.Year || request.Graduated < request.Birthday.Year ||
            string.IsNullOrWhiteSpace(request.University) || request.University.Length > 500 || request.Experience < 0 ||
            request.Experience > today.Year - request.Birthday.Year ||
            request.Contract is not ("Xác định thời hạn - 12 tháng" or "Không xác định thời hạn") ||
            request.Work is not ("Toàn thời gian" or "Bán thời gian") ||
            (request.Address?.Length ?? 0) > 500 || (request.Certificate?.Length ?? 0) > 500)
            throw new TeacherException(TeacherFailure.Invalid, "Thông tin giáo viên không hợp lệ. Kiểm tra các trường bắt buộc, ngày, số điện thoại và trình độ.");
    }
}
