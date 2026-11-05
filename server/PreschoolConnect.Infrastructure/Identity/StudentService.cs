using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Application.Students;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Infrastructure.Identity;

public sealed class StudentService(ApplicationDbContext db, TimeProvider clock) : IStudentService
{
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    private IQueryable<StudentInClass> Current(StudentScope scope, DateOnly today) =>
        from enrollment in db.StudentInClasses
        join classroom in db.Classrooms on enrollment.ClassroomId equals classroom.Id
        join year in db.SchoolYears on classroom.SchoolYearId equals year.Id
        where enrollment.SchoolId == scope.SchoolId && classroom.SchoolId == scope.SchoolId &&
            year.SchoolId == scope.SchoolId && enrollment.LastAction != "D" && classroom.LastAction != "D" &&
            year.LastAction != "D" && enrollment.StartDate <= today && today < enrollment.EndDate &&
            year.StartDate <= today && today <= year.EndDate
        select enrollment;

    private IQueryable<Guid> AssignedClasses(StudentScope scope) =>
        from assignment in db.TeacherAssignments
        join teacher in db.Teachers on assignment.TeacherId equals teacher.Id
        where assignment.SchoolId == scope.SchoolId && teacher.SchoolId == scope.SchoolId &&
            teacher.UserId == scope.UserId && teacher.IsActive && teacher.LastAction != "D" &&
            assignment.LastAction != "D" && assignment.EndedAtUtc == null
        select assignment.ClassroomId;

    private IQueryable<Student> Visible(StudentScope scope, DateOnly today) => db.Students.AsNoTracking().Where(student =>
        student.SchoolId == scope.SchoolId && (scope.IsAdmin ||
            scope.IsParent && db.StudentAccounts.Any(account => account.SchoolId == scope.SchoolId &&
                account.UserId == scope.UserId && account.StudentId == student.Id) ||
            scope.IsTeacher && Current(scope, today).Any(enrollment => enrollment.StudentId == student.Id &&
                AssignedClasses(scope).Contains(enrollment.ClassroomId))));

    public Task<StudentDto[]> ListAsync(StudentScope scope, CancellationToken ct) => LoadAsync(scope, null, ct);

    private async Task<StudentDto[]> LoadAsync(StudentScope scope, Guid? id, CancellationToken ct)
    {
        var today = Today;
        var students = await Visible(scope, today).Where(row => !id.HasValue || row.Id == id)
            .OrderBy(row => row.LastName).ThenBy(row => row.FirstName).ToArrayAsync(ct);
        var ids = students.Select(row => row.Id).ToArray();
        var parentIds = scope.IsParent ? await db.StudentAccounts.Where(row => row.SchoolId == scope.SchoolId &&
            row.UserId == scope.UserId).Select(row => row.StudentId).ToArrayAsync(ct) : [];
        var firstDates = await db.StudentInClasses.AsNoTracking().Where(row => row.SchoolId == scope.SchoolId && ids.Contains(row.StudentId))
            .GroupBy(row => row.StudentId).Select(group => new { StudentId = group.Key, Date = group.Min(row => row.StartDate) }).ToArrayAsync(ct);
        var enrollments = await (
            from enrollment in db.StudentInClasses.AsNoTracking()
            join classroom in db.Classrooms.IgnoreQueryFilters().AsNoTracking() on enrollment.ClassroomId equals classroom.Id
            join year in db.SchoolYears.IgnoreQueryFilters().AsNoTracking() on classroom.SchoolYearId equals year.Id
            where ids.Contains(enrollment.StudentId) && enrollment.SchoolId == scope.SchoolId &&
                enrollment.LastAction != "D" && classroom.SchoolId == scope.SchoolId && year.SchoolId == scope.SchoolId &&
                (scope.IsAdmin || parentIds.Contains(enrollment.StudentId) ||
                    scope.IsTeacher && enrollment.StartDate <= today && today < enrollment.EndDate &&
                    classroom.LastAction != "D" && year.LastAction != "D" &&
                    year.StartDate <= today && today <= year.EndDate && AssignedClasses(scope).Contains(classroom.Id))
            orderby enrollment.StartDate descending
            select new { Row = enrollment, ClassroomName = classroom.Name, YearId = year.Id, YearName = year.Name }
        ).ToArrayAsync(ct);
        var classIds = enrollments.Where(item => item.Row.StartDate <= today && today < item.Row.EndDate)
            .Select(item => item.Row.ClassroomId).Distinct().ToArray();
        var teachers = await (
            from assignment in db.TeacherAssignments.AsNoTracking()
            join teacher in db.Teachers.AsNoTracking() on assignment.TeacherId equals teacher.Id
            where assignment.SchoolId == scope.SchoolId && teacher.SchoolId == scope.SchoolId && teacher.IsActive &&
                assignment.EndedAtUtc == null && classIds.Contains(assignment.ClassroomId)
            select new { assignment.ClassroomId, teacher.Name }).ToArrayAsync(ct);
        return students.Select(student =>
        {
            var history = enrollments.Where(item => item.Row.StudentId == student.Id).Select(item => new EnrollmentDto(
                item.Row.Id, item.Row.ClassroomId, item.ClassroomName, item.YearId, item.YearName,
                item.Row.StartDate, item.Row.EndDate, item.Row.AgeGroup, item.Row.StudyMode,
                item.Row.StartDate <= today && today < item.Row.EndDate
                    ? teachers.Where(teacher => teacher.ClassroomId == item.Row.ClassroomId).Select(teacher => teacher.Name).Distinct().ToArray() : [])).ToArray();
            var current = history.Any(item => item.StartDate <= today && today < item.EndDate);
            var first = firstDates.Where(item => item.StudentId == student.Id).Select(item => (DateOnly?)item.Date).SingleOrDefault();
            return new StudentDto(student.Id, Profile(student), history,
                current ? "Đang theo học" : history.Length == 0 ? "Chưa phân lớp" : "Đã nghỉ học",
                first.HasValue && first.Value.Year == today.Year && first.Value.Month == today.Month, today, student.LastActionAtUtc);
        }).ToArray();
    }

    public async Task<StudentDto> GetAsync(StudentScope scope, Guid id, CancellationToken ct) =>
        (await LoadAsync(scope, id, ct)).SingleOrDefault()
        ?? throw new StudentException(StudentFailure.NotFound, "Không tìm thấy hồ sơ học sinh.");

    public async Task<StudentDto> SaveAsync(StudentScope scope, Guid? id, StudentRequest request, CancellationToken ct)
    {
        RequireAdmin(scope);
        Validate(request.Profile);
        if (id.HasValue && request.Enrollment is not null)
            throw Invalid("Dùng thao tác nhập học/chuyển lớp để giữ nguyên lịch sử.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var student = id.HasValue ? await db.Students.SingleOrDefaultAsync(row => row.Id == id && row.SchoolId == scope.SchoolId, ct)
            ?? throw new StudentException(StudentFailure.NotFound, "Không tìm thấy hồ sơ học sinh.")
            : new Student { Id = Guid.NewGuid(), SchoolId = scope.SchoolId };
        var p = request.Profile;
        var words = p.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        student.FirstName = words[^1]; student.LastName = string.Join(' ', words[..^1]);
        student.DateOfBirth = p.Birthday; student.Gender = p.Gender; student.Address = p.Address.Trim();
        student.Guardian = p.Guardian.Trim(); student.Relationship = p.Relationship; student.Phone = p.Phone.Trim();
        student.Email = p.Email?.Trim() ?? ""; student.Pickup = p.Pickup?.Trim() ?? ""; student.Emergency = p.Emergency.Trim();
        student.Height = p.Height; student.Weight = p.Weight; student.Care = p.Care?.Trim() ?? "";
        if (await db.StudentInClasses.AnyAsync(row => row.SchoolId == scope.SchoolId &&
            row.StudentId == student.Id && row.StartDate < p.Birthday, ct))
            throw Invalid("Ngày sinh không được sau ngày nhập học.");
        if (!id.HasValue) db.Students.Add(student);
        if (request.Enrollment is not null)
            await ValidateEnrollment(scope, student, request.Enrollment, ct);
        await db.SaveChangesAsync(ct);
        if (request.Enrollment is not null)
        {
            await AddEnrollment(scope, student, request.Enrollment, ct);
            await db.SaveChangesAsync(ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetAsync(scope, student.Id, ct);
    }

    public async Task<StudentDto> EnrollAsync(StudentScope scope, Guid id, EnrollmentRequest request, CancellationToken ct)
    {
        RequireAdmin(scope);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var student = await db.Students.SingleOrDefaultAsync(row => row.SchoolId == scope.SchoolId && row.Id == id, ct)
            ?? throw new StudentException(StudentFailure.NotFound, "Không tìm thấy hồ sơ học sinh.");
        await ValidateEnrollment(scope, student, request, ct);
        var history = await db.StudentInClasses.Where(row => row.SchoolId == scope.SchoolId && row.StudentId == id).ToArrayAsync(ct);
        if (history.Length > 0 && request.StartDate != Today)
            throw Invalid("Nhập học lại/chuyển lớp chỉ có hiệu lực hôm nay.");
        var active = history.SingleOrDefault(row => row.StartDate <= Today && Today < row.EndDate);
        if (active is not null)
        {
            if (active.ClassroomId == request.ClassroomId)
                throw new StudentException(StudentFailure.Conflict, "Học sinh đang học lớp này.");
            if (active.StartDate >= Today) throw Invalid("Không thể chuyển lớp trong ngày bắt đầu nhập học.");
            active.EndDate = Today;
            await db.SaveChangesAsync(ct);
        }
        await AddEnrollment(scope, student, request, ct);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetAsync(scope, id, ct);
    }

    public async Task<StudentDto> EndAsync(StudentScope scope, Guid id, Guid enrollmentId, CancellationToken ct)
    {
        RequireAdmin(scope);
        var row = await db.StudentInClasses.SingleOrDefaultAsync(item => item.SchoolId == scope.SchoolId &&
            item.StudentId == id && item.Id == enrollmentId, ct)
            ?? throw new StudentException(StudentFailure.NotFound, "Không tìm thấy lịch sử nhập học.");
        if (row.EndDate <= Today) throw new StudentException(StudentFailure.Conflict, "Lần nhập học đã kết thúc.");
        if (row.StartDate >= Today) throw Invalid("Không thể kết thúc trong ngày bắt đầu nhập học.");
        row.EndDate = Today;
        await db.SaveChangesAsync(ct);
        return await GetAsync(scope, id, ct);
    }

    private async Task<Classroom> ValidateEnrollment(StudentScope scope, Student student, EnrollmentRequest request, CancellationToken ct)
    {
        if (request.AgeGroup is not ("Mầm · 3–4 tuổi" or "Chồi · 4–5 tuổi" or "Lá · 5–6 tuổi") ||
            request.StudyMode is not ("Bán trú" or "Cả ngày")) throw Invalid("Nhóm tuổi hoặc hình thức học không hợp lệ.");
        var classroom = await db.Classrooms.SingleOrDefaultAsync(row => row.Id == request.ClassroomId && row.SchoolId == scope.SchoolId, ct)
            ?? throw Invalid("Lớp học không tồn tại trong trường.");
        var year = await db.SchoolYears.SingleOrDefaultAsync(row => row.Id == classroom.SchoolYearId && row.SchoolId == scope.SchoolId, ct)
            ?? throw Invalid("Năm học không còn tồn tại.");
        if (year.EndDate == DateOnly.MaxValue || request.StartDate < year.StartDate || request.StartDate > year.EndDate ||
            request.StartDate > Today || request.StartDate < student.DateOfBirth)
            throw Invalid("Ngày nhập học phải nằm trong năm học, không trước ngày sinh và không sau hôm nay.");
        return classroom;
    }

    private async Task AddEnrollment(StudentScope scope, Student student, EnrollmentRequest request, CancellationToken ct)
    {
        var classroom = await ValidateEnrollment(scope, student, request, ct);
        var end = (await db.SchoolYears.SingleAsync(row => row.Id == classroom.SchoolYearId && row.SchoolId == scope.SchoolId, ct)).EndDate.AddDays(1);
        if (await db.StudentInClasses.AnyAsync(row => row.StudentId == student.Id && row.SchoolId == scope.SchoolId &&
            row.StartDate < end && request.StartDate < row.EndDate, ct))
            throw new StudentException(StudentFailure.Conflict, "Khoảng nhập học bị chồng lấn.");
        db.StudentInClasses.Add(new StudentInClass { Id = Guid.NewGuid(), SchoolId = scope.SchoolId, StudentId = student.Id,
            ClassroomId = request.ClassroomId, StartDate = request.StartDate, EndDate = end,
            AgeGroup = request.AgeGroup, StudyMode = request.StudyMode });
    }

    private void Validate(StudentProfile? p)
    {
        if (p is null) throw Invalid("Thiếu thông tin học sinh.");
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Trim().Length > 100 ||
            p.Birthday == default || p.Birthday > Today || p.Gender is not ("Nam" or "Nữ") ||
            string.IsNullOrWhiteSpace(p.Address) || p.Address.Length > 500 ||
            string.IsNullOrWhiteSpace(p.Guardian) || p.Guardian.Length > 200 ||
            p.Relationship is not ("Bố" or "Mẹ" or "Người giám hộ") ||
            p.Phone is null || !Regex.IsMatch(p.Phone, "^[0-9]{10}$") ||
            p.Emergency is null || !Regex.IsMatch(p.Emergency, "^[0-9]{10}$") ||
            p.Email?.Length > 256 || !string.IsNullOrWhiteSpace(p.Email) && !new EmailAddressAttribute().IsValid(p.Email.Trim()) ||
            p.Pickup?.Length > 200 || p.Care?.Length > 2000 ||
            p.Height is <= 0 or > 250 || p.Weight is <= 0 or > 300 ||
            p.Height.HasValue && decimal.Round(p.Height.Value, 2) != p.Height ||
            p.Weight.HasValue && decimal.Round(p.Weight.Value, 2) != p.Weight)
            throw Invalid("Kiểm tra họ tên, ngày sinh, liên hệ và các trường sức khỏe (tối đa hai chữ số thập phân).");
    }

    private static StudentProfile Profile(Student row) => new(string.Join(' ', new[] { row.LastName, row.FirstName }.Where(value => value.Length > 0)),
        row.DateOfBirth, row.Gender, row.Address, row.Guardian, row.Relationship, row.Phone, row.Email, row.Pickup, row.Emergency, row.Height, row.Weight, row.Care);
    private static StudentException Invalid(string message) => new(StudentFailure.Invalid, message);
    private static void RequireAdmin(StudentScope scope)
    {
        if (!scope.IsAdmin) throw new StudentException(StudentFailure.Forbidden, "Chỉ quản trị viên trường được thay đổi hồ sơ/nhập học.");
    }
}
