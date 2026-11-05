export type StudentProfile = {
  name: string; birthday: string; gender: string; address: string; guardian: string; relationship: string;
  phone: string; email: string; pickup: string; emergency: string; height: number | null; weight: number | null; care: string;
};
export type Enrollment = { id: string; classroomId: string; classroomName: string; schoolYearId: string;
  schoolYearName: string; startDate: string; endDate: string; ageGroup: string; studyMode: string; teachers: string[] };
export type Student = { id: string; profile: StudentProfile; enrollments: Enrollment[]; status: string;
  isNew: boolean; today: string; lastActionAtUtc: string };
export type EnrollmentInput = { classroomId: string; startDate: string; ageGroup: string; studyMode: string };
export const ageGroups = ['Mầm · 3–4 tuổi', 'Chồi · 4–5 tuổi', 'Lá · 5–6 tuổi'];
export const studyModes = ['Bán trú', 'Cả ngày'];
export const studentStatuses = ['Đang theo học', 'Chưa phân lớp', 'Đã nghỉ học'];
export function schoolToday() {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
  const part = (type: string) => parts.find(item => item.type === type)?.value;
  return `${part('year')}-${part('month')}-${part('day')}`;
}
export function currentEnrollment(student: Student) {
  return student.enrollments.find(row => row.startDate <= student.today && student.today < row.endDate);
}
const text = (form: FormData, key: string) => String(form.get(key) ?? '').trim();
const measurement = (form: FormData, key: string) => {
  const value = text(form, key).replace(',', '.');
  return value === '' ? null : Number(value);
};
export function studentForm(form: FormData): StudentProfile {
  return { name: text(form, 'name'), birthday: text(form, 'birthday'), gender: text(form, 'gender'),
    address: text(form, 'address'), guardian: text(form, 'guardian'), relationship: text(form, 'relationship'),
    phone: text(form, 'phone'), email: text(form, 'email'), pickup: text(form, 'pickup'),
    emergency: text(form, 'emergency'), height: measurement(form, 'height'), weight: measurement(form, 'weight'), care: text(form, 'care') };
}
export function enrollmentForm(form: FormData): EnrollmentInput {
  return { classroomId: text(form, 'classroomId'), startDate: text(form, 'startDate'),
    ageGroup: text(form, 'ageGroup'), studyMode: text(form, 'studyMode') };
}
export function studentError(p: StudentProfile, today: string) {
  if (!p.name || p.name.length > 100 || !p.address || p.address.length > 500 || !p.guardian || p.guardian.length > 200)
    return 'Nhập họ tên, địa chỉ và tên phụ huynh trong giới hạn cho phép.';
  if (!p.birthday || !/^\d{4}-\d{2}-\d{2}$/.test(p.birthday) || !Number.isFinite(Date.parse(p.birthday)) || p.birthday > today)
    return 'Ngày sinh không hợp lệ hoặc sau hôm nay.';
  if (!['Nam', 'Nữ'].includes(p.gender) || !['Bố', 'Mẹ', 'Người giám hộ'].includes(p.relationship))
    return 'Giới tính hoặc quan hệ phụ huynh không hợp lệ.';
  if (!/^[0-9]{10}$/.test(p.phone) || !/^[0-9]{10}$/.test(p.emergency)) return 'Số điện thoại phải có 10 chữ số.';
  if (p.email.length > 256 || p.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(p.email)) return 'Email không hợp lệ.';
  if (p.pickup.length > 200 || p.care.length > 2000) return 'Người đón bé tối đa 200 ký tự; lưu ý chăm sóc tối đa 2000 ký tự.';
  if ([p.height, p.weight].some(value => value !== null && (!Number.isFinite(value) || value <= 0 || Math.abs(value * 100 - Math.round(value * 100)) > 0.000001)) ||
    p.height !== null && p.height > 250 || p.weight !== null && p.weight > 300)
    return 'Chiều cao/cân nặng phải là số dương, tối đa hai chữ số thập phân (250 cm / 300 kg).';
  return '';
}
