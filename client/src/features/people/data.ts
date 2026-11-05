// Explicitly synthetic fixtures for /preview only; never merge with API records.
export type Person = { id: string; name: string; date: string; classroom: string; contact: string; status: string };
export const students: Person[] = [
  { id: 'HS0261', name: 'Trần Minh An', date: '12/03/2023', classroom: 'Mầm 2', contact: 'Trần Quốc Minh', status: 'Đang theo học' },
  { id: 'HS0108', name: 'Nguyễn Bảo Ngọc', date: '21/06/2023', classroom: 'Mầm 2', contact: 'Nguyễn Hoàng Nam', status: 'Đang theo học' },
  { id: 'HS0112', name: 'Lê Gia Huy', date: '05/09/2023', classroom: 'Mầm 2', contact: 'Lê Thanh Bình', status: 'Đang theo học' },
  { id: 'HS0115', name: 'Phạm Khánh Linh', date: '18/01/2023', classroom: 'Mầm 2', contact: 'Phạm Minh Tuấn', status: 'Đang theo học' },
  { id: 'HS0121', name: 'Hoàng Đức Anh', date: '23/04/2023', classroom: 'Mầm 2', contact: 'Hoàng Văn Sơn', status: 'Đang theo học' },
  { id: 'HS0127', name: 'Vũ Hà My', date: '09/07/2023', classroom: 'Mầm 2', contact: 'Vũ Ngọc Mai', status: 'Đang theo học' },
  { id: 'HS0130', name: 'Đỗ Minh Khang', date: '02/08/2022', classroom: 'Chồi 1', contact: 'Đỗ Minh Đức', status: 'Đang theo học' },
  { id: 'HS0131', name: 'Bùi An Nhiên', date: '07/06/2021', classroom: 'Lá 1', contact: 'Bùi Thanh An', status: 'Đang theo học' }
];
export const teachers: Person[] = [
  { id: 'GV001', name: 'Nguyễn Thu Hà', date: '15/08/2021', classroom: 'Lá 1', contact: 'Đại học Sư phạm MN', status: 'Đang công tác' },
  { id: 'GV002', name: 'Trần Thị Hương', date: '01/08/2023', classroom: 'Lá 1', contact: 'Cao đẳng Sư phạm MN', status: 'Đang công tác' },
  { id: 'GV003', name: 'Lê Minh Thu', date: '01/08/2023', classroom: 'Nhà trẻ 1', contact: 'Cao đẳng Sư phạm MN', status: 'Đang công tác' },
  { id: 'GV004', name: 'Phạm Ngọc Lan', date: '01/08/2023', classroom: 'Nhà trẻ 1', contact: 'Đại học Sư phạm MN', status: 'Đang công tác' },
  { id: 'GV005', name: 'Hoàng Thanh Mai', date: '01/08/2023', classroom: 'Nhà trẻ 2', contact: 'Cao đẳng Sư phạm MN', status: 'Đang công tác' },
  { id: 'GV006', name: 'Vũ Thảo Linh', date: '01/08/2023', classroom: 'Nhà trẻ 2', contact: 'Cao đẳng Sư phạm MN', status: 'Đang công tác' },
  { id: 'GV025', name: 'Nguyễn Thảo Nguyên', date: '16/11/2026', classroom: 'Chưa phân lớp', contact: 'Đại học Sư phạm MN', status: 'Chưa phân lớp' },
  { id: 'GV007', name: 'Đỗ Phương Thảo', date: '01/08/2023', classroom: 'Mầm 2', contact: 'Đại học Sư phạm MN', status: 'Đang công tác' },
  { id: 'GV008', name: 'Phan Thùy Dung', date: '01/08/2023', classroom: 'Mầm 2', contact: 'Cao đẳng Sư phạm MN', status: 'Đang công tác' }
];
