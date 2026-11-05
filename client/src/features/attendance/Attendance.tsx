import { useState } from 'react';
import { DataTable, Pagination, Panel, Stats } from '../../components/design';
import { Message, PageHeader } from '../../components/ui';
import { students } from '../people/data';

const states = ['Có mặt', 'Vắng có phép', 'Vắng không phép'];
export default function Attendance() {
  const [classroom, setClassroom] = useState('Mầm 2');
  const [date, setDate] = useState('2026-11-02');
  const [session, setSession] = useState('Buổi sáng');
  const [values, setValues] = useState<Record<string, string>>({});
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [page, setPage] = useState(1);
  const [notice, setNotice] = useState('');
  const rows = students.filter(row => row.classroom === classroom);
  const key = (id: string) => `${date}:${session}:${classroom}:${id}`;
  const value = (id: string) => values[key(id)] ?? (id === 'HS0127' ? 'Vắng có phép' : 'Có mặt');
  return <>
    <PageHeader title="Điểm danh" subtitle="Theo dõi sự hiện diện của từng bé · dữ liệu mẫu" actions={<button className="secondary" onClick={() => window.print()}>In điểm danh mẫu</button>} />
    <Stats items={[[ 'Có mặt toàn trường', '236', '95,2% trên tổng 248 học sinh · mẫu'], ['Vắng có phép', '9', 'Thông tin minh họa'], ['Vắng không phép', '3', 'Chưa có API xác nhận'], ['Lớp đã cập nhật', '12 / 12', 'Số liệu thiết kế, không phải đã lưu']]} />
    <Panel title={`Điểm danh lớp ${classroom}`}><div className="three-columns"><label>Ngày điểm danh<input type="date" value={date} required onChange={event => { setDate(event.target.value); setNotice(''); }} /></label><label>Lớp học<select value={classroom} onChange={event => { setClassroom(event.target.value); setPage(1); setNotice(''); }}>{['Mầm 2', 'Chồi 1', 'Lá 1'].map(item => <option key={item}>{item}</option>)}</select></label><label>Buổi học<select value={session} onChange={event => { setSession(event.target.value); setNotice(''); }}><option>Buổi sáng</option><option>Buổi chiều</option></select></label></div>
      <Message>Chỉ thao tác trên {rows.length} học sinh mẫu. Mọi thay đổi trong bộ nhớ, không có bản điểm danh thật được lưu.</Message>
      <DataTable label="Điểm danh học sinh mẫu" rows={rows.slice((page - 1) * 6, page * 6)} rowKey={row => row.id} columns={[
        { label: 'Mã học sinh', render: row => row.id }, { label: 'Họ và tên', render: row => row.name },
        { label: 'Điểm danh cá nhân', render: row => <fieldset className="attendance-options"><legend className="sr-only">Điểm danh {row.name}</legend>{states.map(state => <label key={state} className={value(row.id) === state ? 'selected' : ''}><input type="radio" name={`attendance-${row.id}`} checked={value(row.id) === state} onChange={() => { setValues(current => ({ ...current, [key(row.id)]: state })); setNotice(''); }} />{state}</label>)}</fieldset> },
        { label: 'Giờ đến / Ghi chú', render: row => <input aria-label={`Ghi chú ${row.name}`} value={notes[key(row.id)] ?? (row.id === 'HS0127' ? 'Phụ huynh xin nghỉ · Sốt nhẹ (mẫu)' : '07:30')} onChange={event => { setNotes(current => ({ ...current, [key(row.id)]: event.target.value })); setNotice(''); }} /> }
      ]} /><Pagination count={rows.length} page={page} onChange={setPage} />
      {notice && <Message>{notice}</Message>}<footer className="form-footer"><p className="muted">{rows.filter(row => value(row.id) === 'Có mặt').length} / {rows.length} có mặt trong dữ liệu mẫu</p><div className="page-actions"><button className="secondary" onClick={() => { setValues(current => ({ ...current, ...Object.fromEntries(rows.map(row => [key(row.id), 'Có mặt'])) })); setNotice('Đã đánh dấu trên bản mẫu, chưa lưu.'); }}>Đánh dấu tất cả có mặt</button><button onClick={() => setNotice('Chưa lưu điểm danh: API điểm danh chưa được triển khai. Thay đổi chỉ tồn tại trong phiên xem trước.')}>Kiểm tra điểm danh</button></div></footer>
    </Panel>
  </>;
}
