import { useState } from 'react';
import { DataTable, Facts, Panel, PreviewForm, RouteLink, Search, Stats, Tabs, useListFilters } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';
import { students, teachers } from '../people/data';
import { previewGroups } from './preview-data';

export default function ClassScreens({ id, mode, navigate }: { id?: string; mode?: string; navigate: (url: string) => void }) {
  const [tab, setTab] = useState('Danh sách học sinh');
  const classroom = previewGroups.flatMap((group, groupIndex) => group.sizes.map((size, index) => ({ id: `${groupIndex}-${index}`, name: `${group.name} ${index + 1}`, size, group }))).find(row => row.id === id);
  const list = useListFilters(students.filter(row => row.classroom === classroom?.name), row => `${row.name} ${row.id}`);
  if (mode === 'edit' && !classroom) return <EmptyState title="Không tìm thấy lớp mẫu"><RouteLink navigate={navigate} to="/preview/classes">Về danh sách lớp</RouteLink></EmptyState>;
  if (mode === 'new' || mode === 'edit') return <>
    <PageHeader title={mode === 'edit' ? 'Chỉnh sửa lớp học' : 'Thêm lớp học'} subtitle="Chuẩn bị lớp học cho năm học 2026–2027 · Bản xem trước" />
    <PreviewForm groups={[
      { title: 'Thông tin lớp học', fields: [
        { name: 'name', label: 'Tên lớp', value: classroom?.name ?? 'Mầm 4', required: true },
        { name: 'age', label: 'Nhóm độ tuổi', options: ['Mầm · 3–4 tuổi', 'Nhà trẻ · 18–36 tháng', 'Chồi · 4–5 tuổi', 'Lá · 5–6 tuổi'], value: classroom ? `${classroom.group.name} · ${classroom.group.age}` : undefined, required: true },
        { name: 'year', label: 'Năm học', options: ['2026–2027'], required: true }, { name: 'room', label: 'Phòng học', options: ['P.204 · Tầng 2', 'P.207 · Tầng 2'], required: true },
        { name: 'capacity', label: 'Sức chứa tối đa', type: 'number', min: '1', value: String(classroom?.group.capacity ?? 25), required: true }, { name: 'startDate', label: 'Ngày bắt đầu dự kiến', type: 'date', value: '2026-11-16', required: true },
        { name: 'note', label: 'Ghi chú', value: 'Lớp dự kiến mở khi đủ giáo viên và hồ sơ nhập học.', multiline: true, full: true }
      ] },
      { title: 'Phân công hai giáo viên', fields: [
        { name: 'teacher', label: 'Giáo viên chủ nhiệm', options: ['Chưa có giáo viên sẵn sàng'] }, { name: 'coTeacher', label: 'Giáo viên đồng phụ trách', options: ['Chưa có giáo viên sẵn sàng'] }
      ], note: 'Chưa có giáo viên sẵn sàng theo mẫu. Bản xem trước không tạo phân công; điều kiện mở lớp chưa được xác nhận là quy tắc backend.' }
    ]} submitLabel="Kiểm tra lớp học" cancel={<RouteLink className="button-link secondary" to="/preview/classes" navigate={navigate}>Hủy</RouteLink>} aside={<><Panel title="Điều kiện mở lớp"><Facts items={[[ 'Nhóm Mầm', '3–4 tuổi'], ['Sĩ số hiện tại', '66 học sinh / 3 lớp · mẫu'], ['Định biên', '2 giáo viên / lớp · theo thiết kế'], ['Phòng P.204', 'Sẵn sàng · 25 chỗ · mẫu']]} /><span className="badge amber">Chưa mở lớp</span></Panel><Message>Bảo toàn phân công hiện tại. Chưa tạo lớp, điều chuyển học sinh hoặc giáo viên.</Message></>} />
  </>;
  if (!classroom) return <EmptyState title="Không tìm thấy lớp mẫu"><RouteLink navigate={navigate} to="/preview/classes">Về danh sách lớp</RouteLink></EmptyState>;
  return <>
    <PageHeader title="Chi tiết lớp" subtitle={`${classroom.name} · ${classroom.group.age} · Năm học 2026–2027`} actions={<><button className="secondary" onClick={() => window.print()}>In danh sách mẫu</button><RouteLink className="button-link" to={`/preview/classes/${id}/edit`} navigate={navigate}>Chỉnh sửa lớp</RouteLink></>} />
    <Stats items={[[ `Sĩ số ${classroom.name}`, `${classroom.size} / ${classroom.group.capacity}`, 'Số liệu thiết kế · mẫu'], ['Giáo viên phụ trách', '2', 'Đủ nhân sự · mẫu'], ['Có mặt hôm nay', String(classroom.size - 1), 'Số liệu mẫu'], ['Vắng hôm nay', '1', 'Có phép · mẫu']]} />
    <Tabs items={['Danh sách học sinh', 'Thông tin lớp', 'Tài liệu']} value={tab} onChange={setTab} />
    {tab === 'Danh sách học sinh' ? <div className="split-layout"><Panel title={`Học sinh lớp ${classroom.name}`}><p className="muted">Chỉ chứa hồ sơ minh họa có lớp {classroom.name}, không phải toàn bộ sĩ số trên thẻ thiết kế.</p><Search value={list.query} onChange={list.setQuery} label="Tìm học sinh trong lớp…" /><DataTable label="Học sinh trong lớp mẫu" rows={list.visible} rowKey={row => row.id} columns={[
      { label: 'Mã học sinh', render: row => row.id }, { label: 'Họ và tên', render: row => <RouteLink to={`/preview/students/${row.id}`} navigate={navigate}>{row.name}</RouteLink> },
      { label: 'Ngày sinh', render: row => row.date }, { label: 'Trạng thái', render: row => <span className="badge">{row.status}</span> }
    ]} /></Panel><aside className="stack"><Panel title="Giáo viên phụ trách">
      {teachers.filter(teacher => teacher.classroom === classroom.name).map(teacher => <RouteLink key={teacher.id} navigate={navigate} to={`/preview/teachers/${teacher.id}`}>{teacher.name} · mẫu</RouteLink>)}
      {!teachers.some(teacher => teacher.classroom === classroom.name) && <p className="muted">Chưa có hồ sơ giáo viên mẫu cho lớp này.</p>}
    </Panel><Panel title="Lịch sinh hoạt hằng ngày"><Facts items={[[ '07:00–08:00', 'Đón trẻ · Ăn sáng'], ['08:00–08:30', 'Thể dục sáng'], ['08:30–09:15', 'Hoạt động học'], ['09:15–10:30', 'Vui chơi ngoài trời'], ['10:30–12:00', 'Ăn trưa · Vệ sinh'], ['12:00–14:00', 'Ngủ trưa'], ['14:00–16:30', 'Ăn xế · Chơi · Trả trẻ']]} /></Panel><Panel title="Lưu ý trong tuần"><RouteLink navigate={navigate} to="/preview/events/nature">05/11 · Bé khám phá thiên nhiên →</RouteLink></Panel></aside></div> :
      tab === 'Thông tin lớp' ? <Panel title="Thông tin lớp"><Facts items={[[ 'Tên lớp', classroom.name], ['Nhóm tuổi', classroom.group.age], ['Sĩ số mẫu', String(classroom.size)], ['Sức chứa mẫu', String(classroom.group.capacity)], ['Năm học', '2026–2027']]} /></Panel> : <Panel title="Tài liệu"><EmptyState title="Chưa có tài liệu mẫu">Không tải tài liệu thật trong bản xem trước.</EmptyState></Panel>}
    <RouteLink navigate={navigate} to="/preview/classes">← Về danh sách lớp</RouteLink>
  </>;
}
