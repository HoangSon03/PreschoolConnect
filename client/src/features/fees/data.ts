import { students } from '../people/data';

export const money = (value: number) => `${value.toLocaleString('vi-VN')} đ`;
export const feeFixtures = students.map((student, i) => ({
  ...student,
  due: 3450000,
  paid: i === 0 ? 2000000 : i < 4 ? 3450000 : 0,
  status: i === 0 ? 'Một phần' : i < 4 ? 'Đã thanh toán' : 'Chưa thanh toán'
}));
