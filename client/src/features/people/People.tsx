import { useState } from 'react';
import { DataTable, Facts, Field, FilePicker, Pagination, Panel, PreviewForm, RouteLink, Search, Stats, Tabs, useListFilters, type FieldGroup } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';
import { students, teachers, type Person } from './data';
import { feeFixtures, money } from '../fees/data';

type Props = { kind: 'students' | 'teachers'; id?: string; mode?: string; navigate: (url: string) => void };
export default function People({ kind, id, mode, navigate }: Props) {
  const teacher = kind === 'teachers';
  const rows = teacher ? teachers : students;
  const base = `/preview/${kind}`;
  const label = teacher ? 'giáo viên' : 'học sinh';
  const [tab, setTab] = useState('Tất cả');
  const [classroom, setClassroom] = useState('');
  const [status, setStatus] = useState('');
  const filteredRows = rows.filter(row => (!classroom || row.classroom === classroom) && (!status || row.status === status) && (tab !== 'Mới nhập học' || row.id === 'HS0261') && (tab !== 'Tạm nghỉ' || row.status === 'Tạm nghỉ') && (tab !== 'Đang công tác' || row.status === 'Đang công tác'));
  const list = useListFilters(filteredRows, row => `${row.name} ${row.id} ${row.contact}`);
  const person = rows.find(row => row.id === id);
  if (mode === 'edit' && !person) return <EmptyState title="Không tìm thấy hồ sơ mẫu"><RouteLink navigate={navigate} to={base}>Quay về danh sách</RouteLink></EmptyState>;
  if (mode === 'new' || mode === 'edit') return <PersonForm teacher={teacher} person={mode === 'edit' ? person : undefined} navigate={navigate} />;
  if (id) return person ? <Profile person={person} teacher={teacher} navigate={navigate} /> : <EmptyState title="Không tìm thấy hồ sơ mẫu"><RouteLink navigate={navigate} to={base}>Quay về danh sách</RouteLink></EmptyState>;
  return <>
    <PageHeader title={teacher ? 'Giáo viên' : 'Học sinh'} subtitle={teacher ? 'Đội ngũ đồng hành cùng các bé trong năm học 2026–2027.' : 'Quản lý hồ sơ và hành trình đến trường của các bé.'} actions={<><button className="secondary" onClick={() => window.print()}>In danh sách mẫu</button><RouteLink className="button-link" to={`${base}/new`} navigate={navigate}>+ Thêm {label}</RouteLink></>} />
    <Stats items={teacher ? [['Tổng số giáo viên', '24', '24 hồ sơ nhân sự · số liệu thiết kế'], ['Đang công tác', '24', 'Đủ nhân sự tại 12 lớp'], ['Lớp đã phân công', '12 / 12', '2 giáo viên mỗi lớp · mẫu'], ['Chưa phân lớp', '1', 'Có thêm hồ sơ mẫu chưa phân lớp']] : [['Tổng số học sinh', '248', 'Năm học 2026–2027 · số liệu thiết kế'], ['Đang theo học', '248', 'Trong 12 lớp học'], ['Học sinh mới tháng 11', '8', 'Đã hoàn tất nhập học · mẫu'], ['Nhóm độ tuổi', '4', 'Nhà trẻ · Mầm · Chồi · Lá']]} />
    <Panel title={`Danh sách ${label}`}>
      <p className="muted">Chỉ chứa {rows.length} hồ sơ minh họa, không phải toàn bộ số liệu trên thẻ thiết kế.</p>
      <Tabs items={teacher ? ['Tất cả', 'Đang công tác', 'Tạm nghỉ'] : ['Tất cả', 'Mới nhập học']} value={tab} onChange={value => { setTab(value); list.setPage(1); }} />
      <div className="filter-toolbar"><Search value={list.query} onChange={list.setQuery} label={`Tìm tên, mã ${label} hoặc liên hệ…`} /><label><span className="sr-only">Lọc lớp</span><select value={classroom} onChange={event => { setClassroom(event.target.value); list.setPage(1); }}><option value="">Tất cả lớp</option>{Array.from(new Set(rows.map(row => row.classroom))).map(item => <option key={item}>{item}</option>)}</select></label><label><span className="sr-only">Trạng thái</span><select value={status} onChange={event => { setStatus(event.target.value); list.setPage(1); }}><option value="">Tất cả trạng thái</option>{Array.from(new Set(rows.map(row => row.status))).map(item => <option key={item}>{item}</option>)}</select></label></div>
      <DataTable label={`Danh sách ${label} mẫu`} rowKey={row => row.id} rows={list.visible} columns={[
        { label: 'Mã hồ sơ', render: row => row.id }, { label: 'Họ và tên', render: row => row.name },
        { label: teacher ? 'Lớp phụ trách' : 'Ngày sinh', render: row => teacher ? row.classroom : row.date },
        { label: teacher ? 'Trình độ' : 'Lớp', render: row => teacher ? row.contact : row.classroom },
        { label: teacher ? 'Ngày vào làm' : 'Phụ huynh', render: row => teacher ? row.date : row.contact },
        { label: 'Trạng thái', render: row => <span className="badge">{row.status}</span> },
        { label: 'Thao tác', render: row => <RouteLink to={`${base}/${row.id}`} navigate={navigate}>Chi tiết</RouteLink> }
      ]} />
      <Pagination count={list.filtered.length} page={list.page} onChange={list.setPage} />
    </Panel>
  </>;
}

function Profile({ person, teacher, navigate }: { person: Person; teacher: boolean; navigate: Props['navigate'] }) {
  const [tab, setTab] = useState('Thông tin chung');
  const base = `/preview/${teacher ? 'teachers' : 'students'}`;
  const fee = feeFixtures.find(row => row.id === person.id);
  return <>
    <PageHeader title={`Hồ sơ ${teacher ? 'giáo viên' : 'học sinh'}`} subtitle={`${person.name} · Hồ sơ minh họa`} actions={<><button className="secondary" onClick={() => window.print()}>In hồ sơ mẫu</button><RouteLink className="button-link" navigate={navigate} to={`${base}/${person.id}/edit`}>Chỉnh sửa hồ sơ</RouteLink></>} />
    <section className="card profile-heading"><div className="person-heading"><span className="initials">{person.name.split(' ').slice(-2).map(s => s[0]).join('')}</span><div><h2>{person.name}</h2><p className="muted">{person.id} · {person.classroom}</p></div><span className="badge">{person.status}</span></div><Tabs items={teacher ? ['Thông tin chung', 'Lịch giảng dạy', 'Hồ sơ & chứng chỉ'] : ['Thông tin chung', 'Chuyên cần', 'Học phí', 'Tài liệu']} value={tab} onChange={setTab} /></section>
    {tab === 'Thông tin chung' ? teacher ? <>
      <div className="split-layout"><div className="stack"><Panel title="Thông tin cá nhân & liên hệ"><Facts items={[[ 'Ngày sinh', '18/09/1993'], ['Điện thoại mẫu', '0900 100 001'], ['Email mẫu', `${person.id.toLowerCase()}@example.test`], ['Địa chỉ mẫu', '36 Đường Hoa Ban, Hà Nội'], ['Ngày vào làm', person.date], ['Hình thức làm việc', 'Toàn thời gian · Hợp đồng không xác định thời hạn']]} /></Panel><Panel title="Trình độ & chuyên môn"><Facts items={[[ 'Bằng cấp', person.contact], ['Cơ sở đào tạo', 'Trường Đại học Sư phạm Hà Nội'], ['Năm tốt nghiệp', '2015'], ['Chứng chỉ', 'Sơ cấp cứu trẻ em · Mẫu'], ['Kinh nghiệm', '11 năm giảng dạy mầm non']]} /></Panel></div><aside className="stack"><Panel title="Lớp được phân công"><h2>{person.classroom}</h2><Facts items={[[ 'Sĩ số', '20 / 30 học sinh · mẫu'], ['Đồng phụ trách', 'Trần Thị Hương'], ['Năm học', '2026–2027']]} /></Panel><Panel title="Lịch làm việc"><Facts items={[[ 'Ngày làm việc', 'Thứ Hai – Thứ Sáu'], ['Ca trực', '07:00 – 16:30'], ['Hoạt động học', '08:30 – 09:15'], ['Trả trẻ', '15:30 – 16:30']]} /></Panel></aside></div><TeachingSchedule />
    </> : <>
      <div className="equal-columns"><Panel title="Thông tin của bé"><Facts items={[[ 'Họ và tên', person.name], ['Ngày sinh', person.date], ['Giới tính', 'Nam · mẫu'], ['Lớp / Phòng', `${person.classroom} / P.202`], ['Giáo viên', 'Đỗ Phương Thảo · Phan Thùy Dung'], ['Ngày nhập học', '02/11/2026']]} /></Panel><Panel title="Phụ huynh & liên hệ"><Facts items={[[ 'Bố · Người liên hệ chính', person.contact], ['Điện thoại mẫu', '0900 000 261'], ['Email mẫu', 'phuhuynh@example.test'], ['Mẹ · Người đón bé', 'Lê Thảo An · 0900 000 262'], ['Địa chỉ mẫu', '12 Đường Hoa Ban, Hà Nội'], ['Liên hệ khẩn cấp', `${person.contact} · 0900 000 261`]]} /></Panel></div>
      <Panel title="Sức khỏe & lưu ý chăm sóc"><Message>Dị ứng đậu phộng · Thông tin minh họa, không dùng làm hướng dẫn chăm sóc cho trẻ thật.</Message><Facts items={[[ 'Thể trạng', 'Cao 96 cm · Nặng 14,5 kg · Số liệu mẫu'], ['Chăm sóc', 'Ngủ trưa 12:00–14:00; cần hỗ trợ làm quen trong tuần đầu.']]} /></Panel>
      <div className="equal-columns"><AttendanceSummary />{fee && <Panel title="Học phí tháng 11"><Facts items={[[ 'Tổng phải thu', money(fee.due)], ['Đã thanh toán', money(fee.paid)], ['Còn phải thu', money(fee.due - fee.paid)], ['Hạn thanh toán', '10/11/2026']]} /><RouteLink navigate={navigate} to={`/preview/fees/${person.id}`}>Xem phiếu mẫu →</RouteLink></Panel>}</div>
    </> : tab === 'Chuyên cần' ? <AttendanceSummary /> : tab === 'Học phí' ? <Panel title="Học phí minh họa"><RouteLink navigate={navigate} to={`/preview/fees/${person.id}`}>Xem phiếu học phí mẫu →</RouteLink></Panel> : tab === 'Lịch giảng dạy' ? <TeachingSchedule /> : <Panel title={tab}><EmptyState title="Chưa có tài liệu mẫu">Không tải lên hay cấp chứng chỉ thật trong bản xem trước.</EmptyState><FilePicker label="Chọn tài liệu để xem tên tệp" /></Panel>}
    <RouteLink navigate={navigate} to={base}>← Về danh sách</RouteLink>
  </>;
}

function AttendanceSummary() {
  return <Panel title="Chuyên cần tháng 11"><p className="large-green">1 / 1 ngày có mặt · 100%</p><DataTable label="Chuyên cần mẫu" rows={[{ day: '02/11/2026' }]} rowKey={r => r.day} columns={[{ label: 'Ngày', render: r => r.day }, { label: 'Trạng thái', render: () => <span className="badge">Có mặt</span> }, { label: 'Giờ đến', render: () => '07:45' }]} /></Panel>;
}
function TeachingSchedule() {
  return <Panel title="Lịch giảng dạy tuần 02–06/11/2026"><div className="week-schedule">{['Làm quen chữ cái', 'Khám phá số đếm', 'Âm nhạc: Cây xanh', 'Khám phá thiên nhiên', 'Kể chuyện sáng tạo'].map((title, i) => <div key={title}><p className="muted">Thứ {i + 2} · 0{i + 2}/11</p><h3>{title}</h3><small>08:30–09:15</small></div>)}</div></Panel>;
}

function PersonForm({ teacher, person, navigate }: { teacher: boolean; person?: Person; navigate: Props['navigate'] }) {
  const base = `/preview/${teacher ? 'teachers' : 'students'}`;
  const groups: FieldGroup[] = teacher ? [
    { title: 'Thông tin cá nhân & liên hệ', fields: [
      { name: 'name', label: 'Họ và tên', value: person?.name ?? 'Nguyễn Thảo Nguyên', required: true }, { name: 'birthday', label: 'Ngày sinh', value: '1998-04-20', type: 'date', required: true },
      { name: 'gender', label: 'Giới tính', options: ['Nữ', 'Nam'], required: true }, { name: 'phone', label: 'Số điện thoại', value: '0900100025', type: 'tel', pattern: '[0-9]{10}', required: true },
      { name: 'email', label: 'Email', value: 'thao.nguyen@example.test', type: 'email', required: true, full: true }, { name: 'address', label: 'Địa chỉ liên hệ', value: '48 Đường Hoa Ban, Hà Nội (địa chỉ mẫu)', full: true }
    ] },
    { title: 'Trình độ & chuyên môn', fields: [
      { name: 'degree', label: 'Trình độ', options: ['Đại học Sư phạm mầm non', 'Cao đẳng Sư phạm mầm non'], required: true }, { name: 'graduated', label: 'Năm tốt nghiệp', type: 'number', value: '2020', min: '1950', max: String(new Date().getFullYear()), required: true },
      { name: 'university', label: 'Cơ sở đào tạo', value: 'Trường Đại học Sư phạm Hà Nội', full: true, required: true }, { name: 'experience', label: 'Số năm kinh nghiệm', type: 'number', min: '0', value: '6' }, { name: 'certificate', label: 'Chứng chỉ', value: 'Sơ cấp cứu trẻ em' }
    ] }
  ] : [
    { title: 'Thông tin của bé', fields: [
      { name: 'name', label: 'Họ và tên', value: person?.name ?? 'Nguyễn An Bình', required: true }, { name: 'birthday', label: 'Ngày sinh', value: person?.date.split('/').reverse().join('-') ?? '2023-08-16', type: 'date', max: new Date().toISOString().slice(0, 10), required: true },
      { name: 'gender', label: 'Giới tính', options: ['Nam', 'Nữ'], required: true }, { name: 'code', label: 'Mã hồ sơ', value: person?.id ?? 'Tự động tạo khi lưu (chưa có API)' }, { name: 'address', label: 'Địa chỉ thường trú', value: '24 Đường Hoa Ban, Hà Nội (địa chỉ mẫu)', required: true, full: true }
    ] },
    { title: 'Phụ huynh & người giám hộ', fields: [
      { name: 'guardian', label: 'Họ tên phụ huynh', value: person?.contact ?? 'Nguyễn Văn Bình', required: true }, { name: 'relationship', label: 'Quan hệ với bé', options: ['Bố', 'Mẹ', 'Người giám hộ'], required: true },
      { name: 'phone', label: 'Số điện thoại (10 chữ số)', value: '0900000264', type: 'tel', pattern: '[0-9]{10}', required: true }, { name: 'email', label: 'Email', type: 'email', value: 'an.binh@example.test' },
      { name: 'pickup', label: 'Người đón bé', value: 'Trần Ngọc An' }, { name: 'emergency', label: 'Liên hệ khẩn cấp', type: 'tel', pattern: '[0-9]{10}', value: '0900000264', required: true }
    ] },
    { title: 'Sức khỏe & chăm sóc', fields: [
      { name: 'height', label: 'Chiều cao (cm)', type: 'number', min: '1', max: '250', value: '97' }, { name: 'weight', label: 'Cân nặng (kg)', value: '14,8' },
      { name: 'care', label: 'Dị ứng, bệnh lý và lưu ý', value: 'Chưa ghi nhận dị ứng. Bé cần hỗ trợ làm quen giờ ngủ trưa.', multiline: true, full: true }
    ] }
  ];
  return <>
    <PageHeader title={`${person ? 'Chỉnh sửa' : 'Thêm'} ${teacher ? 'giáo viên' : 'học sinh'}`} subtitle="Biểu mẫu xem trước · Không tạo hồ sơ hay tài khoản thật." />
    <PreviewForm groups={groups} submitLabel="Kiểm tra hồ sơ" cancel={<RouteLink className="button-link secondary" to={base} navigate={navigate}>Hủy</RouteLink>} aside={teacher ? <>
      <Panel title="Thông tin công tác"><div className="form-grid single-column">{[
        { name: 'startDate', label: 'Ngày bắt đầu', value: person?.date.split('/').reverse().join('-') ?? '2026-11-16', type: 'date' as const, required: true },
        { name: 'contract', label: 'Loại hợp đồng', options: ['Xác định thời hạn - 12 tháng', 'Không xác định thời hạn'] },
        { name: 'work', label: 'Hình thức làm việc', options: ['Toàn thời gian', 'Bán thời gian'] },
        { name: 'class', label: 'Lớp phụ trách', options: Array.from(new Set(['Chưa phân lớp', 'Lá 1', 'Mầm 2', ...(person ? [person.classroom] : [])])), value: person?.classroom },
        { name: 'role', label: 'Vai trò dự kiến', options: ['Giáo viên chủ nhiệm', 'Giáo viên đồng phụ trách'] }
      ].map(field => <Field key={field.name} spec={field} />)}</div></Panel><Message>Chưa thay đổi nhân sự hiện tại. Các lựa chọn không tạo phân công thật.</Message><Panel title="Quyền truy cập"><p className="muted">Hồ sơ giáo viên không tự tạo tài khoản. Quản lý ở Phân quyền & tài khoản.</p><RouteLink navigate={navigate} to="/preview/permissions">Xem quyền mẫu →</RouteLink></Panel>
    </> : <><Panel title="Thông tin nhập học"><div className="single-column form-grid">{[
      { name: 'year', label: 'Năm học', options: ['2026–2027'], required: true }, { name: 'age', label: 'Nhóm tuổi', options: ['Mầm · 3–4 tuổi', 'Chồi · 4–5 tuổi', 'Lá · 5–6 tuổi'], required: true },
      { name: 'class', label: 'Lớp học', options: ['Mầm 2', 'Chồi 1', 'Lá 1'], value: person?.classroom, required: true }, { name: 'enrolled', label: 'Ngày nhập học', type: 'date' as const, value: '2026-11-03', required: true },
      { name: 'mode', label: 'Hình thức học', options: ['Bán trú', 'Cả ngày'], required: true }
    ].map(field => <Field key={field.name} spec={field} />)}</div></Panel><Message>Kiểm tra thông tin liên hệ. Dữ liệu nhập chỉ tồn tại trên biểu mẫu này.</Message><Panel title="Tiếp nhận hồ sơ"><Facts items={[[ 'Phụ trách', 'Nguyễn Mai Lan · mẫu'], ['Trạng thái dự kiến', 'Đang theo học'], ['Lưu hồ sơ', 'Chưa có API']]} /></Panel></>}>
      {teacher && <Panel title="Tài liệu hồ sơ"><FilePicker label="Thêm bằng cấp, chứng chỉ và hợp đồng" /></Panel>}
    </PreviewForm>
  </>;
}
