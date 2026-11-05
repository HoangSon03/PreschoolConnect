import type { Classroom } from '../../lib/api';

export type Activity = { start: string; end: string; activity: string };
export type WeeklyNote = { weekStart: string; text: string };
export type ClassroomDetail = { classroom: Classroom; schoolYearName: string;
  students: { id: string; name: string; birthday: string }[];
  schedule: Activity[]; weeklyNotes: WeeklyNote[]; today: string; lastActionAtUtc: string };

export function classTotals(rows: Classroom[]) {
  return { students: rows.reduce((sum, row) => sum + row.studentCount, 0),
    teachers: new Set(rows.flatMap(row => row.teachers.map(teacher => teacher.id))).size,
    capacity: rows.some(row => row.capacity === null) ? null : rows.reduce((sum, row) => sum + (row.capacity ?? 0), 0) };
}

export function monday(date: string) {
  const day = new Date(`${date}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() - (day.getUTCDay() + 6) % 7);
  return day.toISOString().slice(0, 10);
}

export function classroomError(name: string, room: string, capacity: string, schedule: Activity[], notes: WeeklyNote[]) {
  if (!name.trim() || name.trim().length > 100 || room.trim().length > 100)
    return 'Tên lớp bắt buộc và tối đa 100 ký tự; phòng tối đa 100 ký tự.';
  if (capacity && (!/^\d+$/.test(capacity) || Number(capacity) < 1 || Number(capacity) > 500))
    return 'Sức chứa phải là số nguyên từ 1–500 hoặc để trống.';
  if (schedule.length > 20 || notes.length > 52) return 'Tối đa 20 hoạt động và 52 lưu ý theo tuần.';
  let previousEnd = '';
  for (const row of [...schedule].sort((a, b) => a.start.localeCompare(b.start))) {
    if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(row.start) || !/^([01]\d|2[0-3]):[0-5]\d$/.test(row.end) ||
      row.start >= row.end || previousEnd > row.start || !row.activity.trim() || row.activity.trim().length > 200)
      return 'Hoạt động cần nội dung và giờ bắt đầu–kết thúc hợp lệ, không chồng lấn.';
    previousEnd = row.end;
  }
  if (notes.some(row => !/^\d{4}-\d{2}-\d{2}$/.test(row.weekStart) ||
    Number.isNaN(Date.parse(row.weekStart)) || new Date(`${row.weekStart}T00:00:00Z`).toISOString().slice(0, 10) !== row.weekStart ||
    monday(row.weekStart) !== row.weekStart || !row.text.trim() || row.text.trim().length > 2000) ||
    new Set(notes.map(row => row.weekStart)).size !== notes.length)
    return 'Lưu ý cần tuần bắt đầu thứ Hai, không trùng tuần; nội dung tối đa 2000 ký tự.';
  return '';
}
