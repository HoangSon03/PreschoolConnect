export type TeacherProfile = {
  name: string; birthday: string; gender: string; phone: string; email: string; address: string;
  degree: string; graduated: number; university: string; experience: number; certificate: string;
  startDate: string; contract: string; work: string; isActive: boolean; userId: string | null;
};
export type TeacherAssignment = {
  id: string; classroomId: string; classroomName: string; schoolYearId: string; schoolYearName: string;
  role: string; startedAtUtc: string; endedAtUtc: string | null;
};
export type Teacher = { id: string; code: string; profile: TeacherProfile; assignments: TeacherAssignment[]; lastActionAtUtc: string };
export const degreeOptions = ['Đại học Sư phạm mầm non', 'Cao đẳng Sư phạm mầm non'];
export const contractOptions = ['Xác định thời hạn - 12 tháng', 'Không xác định thời hạn'];
export const workOptions = ['Toàn thời gian', 'Bán thời gian'];
export const assignmentRoles = ['Giáo viên chủ nhiệm', 'Giáo viên đồng phụ trách'];

export function activeAssignments(teacher: Teacher) { return teacher.assignments.filter(row => !row.endedAtUtc); }
export function teacherStatus(teacher: Teacher) { return teacher.profile.isActive ? 'Đang công tác' : 'Tạm nghỉ'; }
export function teacherForm(data: FormData): TeacherProfile {
  const text = (name: string) => String(data.get(name) ?? '').trim();
  return {
    name: text('name'), birthday: text('birthday'), gender: text('gender'), phone: text('phone'), email: text('email'),
    address: text('address'), degree: text('degree'), graduated: Number(text('graduated')),
    university: text('university'), experience: Number(text('experience')), certificate: text('certificate'),
    startDate: text('startDate'), contract: text('contract'), work: text('work'),
    isActive: text('status') === 'Đang công tác', userId: text('userId') || null
  };
}
export function teacherError(profile: TeacherProfile, today = new Date().toISOString().slice(0, 10)) {
  if (!profile.name || profile.name.length > 200) return 'Họ và tên bắt buộc, tối đa 200 ký tự.';
  if (!profile.birthday || profile.birthday > today || !profile.startDate || profile.startDate < profile.birthday)
    return 'Ngày sinh và ngày bắt đầu không hợp lệ.';
  if (!/^[0-9]{10}$/.test(profile.phone) || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(profile.email) || profile.email.length > 256)
    return 'Kiểm tra số điện thoại 10 chữ số và email.';
  const age = Number(today.slice(0, 4)) - Number(profile.birthday.slice(0, 4));
  if (!Number.isInteger(profile.graduated) || profile.graduated < 1950 || profile.graduated > Number(today.slice(0, 4)) ||
    profile.graduated < Number(profile.birthday.slice(0, 4)) || !Number.isInteger(profile.experience) || profile.experience < 0 || profile.experience > age)
    return 'Năm tốt nghiệp hoặc số năm kinh nghiệm không hợp lệ.';
  if (!profile.university || [profile.address, profile.university, profile.certificate].some(value => value.length > 500) ||
    !degreeOptions.includes(profile.degree) || !contractOptions.includes(profile.contract) || !workOptions.includes(profile.work) ||
    !['Nữ', 'Nam'].includes(profile.gender)) return 'Kiểm tra thông tin trình độ, công tác và các trường bắt buộc.';
}
