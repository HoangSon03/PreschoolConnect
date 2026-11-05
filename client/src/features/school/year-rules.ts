import type { SchoolYear } from '../../lib/api';

export type YearValues = Pick<SchoolYear, 'name' | 'startDate' | 'endDate'>;
export type ManagedYear = SchoolYear & { classCount: number };

export function yearError(values: YearValues, years: ManagedYear[], editing?: ManagedYear): string | undefined {
  const name = values.name.trim();
  if (!name || name.length > 100) return 'Tên năm học phải từ 1–100 ký tự.';
  if (!values.startDate || !values.endDate || values.endDate <= values.startDate)
    return 'Ngày kết thúc phải sau ngày bắt đầu.';
  if (editing?.classCount && (values.startDate !== editing.startDate || values.endDate !== editing.endDate))
    return 'Năm học đã có lớp; chỉ được sửa tên, không được thay đổi ngày.';
  const others = years.filter(year => year.id !== editing?.id);
  if (others.some(year => year.name.trim().toLocaleLowerCase('vi-VN') === name.toLocaleLowerCase('vi-VN')))
    return 'Tên năm học đã tồn tại (không phân biệt hoa/thường).';
  if (others.some(year => year.startDate <= values.endDate && year.endDate >= values.startDate))
    return 'Khoảng ngày trùng với năm học khác. Ngày bắt đầu phải sau ngày kết thúc của năm trước.';
}

export function yearPhase(year: YearValues, today: string): string {
  return today < year.startDate ? 'Sắp bắt đầu' : today > year.endDate ? 'Đã kết thúc' : 'Trong kỳ';
}

export const previewYears: ManagedYear[] = [
  { id: 'sample-year-2025', schoolId: 'sample-school', name: '2025–2026', startDate: '2025-09-01', endDate: '2026-05-31', classCount: 10 },
  { id: 'sample-year-2026', schoolId: 'sample-school', name: '2026–2027', startDate: '2026-09-01', endDate: '2027-05-31', classCount: 12 },
  { id: 'sample-year-2027', schoolId: 'sample-school', name: '2027–2028', startDate: '2027-09-01', endDate: '2028-05-31', classCount: 0 }
];
