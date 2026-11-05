import { useEffect, useState, type FormEvent } from 'react';
import { DataTable, Facts, Field, Pagination, Panel, RouteLink, Search, Stats, Tabs, useListFilters, type FieldGroup } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';
import { errorMessage, mutate, request, type Classroom, type SchoolYear } from '../../lib/api';
import { ageGroups, currentEnrollment, enrollmentForm, schoolToday, studentError, studentForm, studentStatuses, studyModes, type Student } from './student-rules';

type Props = { path: string; schoolId: string; canManage: boolean; classrooms: Classroom[];
  years: SchoolYear[]; navigate: (url: string) => void };
const base = '/admin/students';
const pageSize = 15;
const date = (value: string) => new Date(`${value}T00:00:00`).toLocaleDateString('vi-VN');

export default function Students(props: Props) {
  const [rows, setRows] = useState<Student[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [tab, setTab] = useState('Tất cả');
  const [classroom, setClassroom] = useState('');
  const [status, setStatus] = useState('');
  useEffect(() => {
    let active = true;
    setLoading(true); setError('');
    request<Student[]>('/api/students', props.schoolId)
      .then(result => { if (active) setRows(result); })
      .catch(cause => { if (active) setError(errorMessage(cause)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [props.schoolId, revision]);
  const segment = props.path.split('/')[3];
  const person = rows.find(row => row.id === segment);
  const filtered = rows.filter(row => (!classroom || currentEnrollment(row)?.classroomId === classroom) &&
    (!status || row.status === status) && (tab !== 'Mới nhập học' || row.isNew));
  const list = useListFilters(filtered, row => `${row.profile.name} ${row.id} ${row.profile.guardian} ${row.profile.phone} ${row.profile.email}`, pageSize);
  if (loading) return <p role="status" className="card">Đang tải hồ sơ học sinh…</p>;
  if (error) return <Panel title="Không tải được hồ sơ"><Message error>{error}</Message><button onClick={() => setRevision(value => value + 1)}>Thử lại</button></Panel>;
  if (segment === 'new' || props.path.endsWith('/edit')) {
    if (!props.canManage) return <Message error>Chỉ quản trị viên trường được chỉnh sửa hồ sơ.</Message>;
    if (segment !== 'new' && !person) return <EmptyState title="Không tìm thấy hồ sơ" />;
    return <StudentForm {...props} person={person} />;
  }
  if (segment) return person ? <StudentProfileScreen {...props} person={person}
    onChanged={updated => setRows(current => current.map(row => row.id === updated.id ? updated : row))} /> :
    <EmptyState title="Không tìm thấy hồ sơ"><RouteLink navigate={props.navigate} to={base}>Về danh sách</RouteLink></EmptyState>;
  const current = rows.flatMap(row => { const enrollment = currentEnrollment(row); return enrollment ? [enrollment] : []; });
  return <>
    <PageHeader title="Học sinh" subtitle="Quản lý hồ sơ và hành trình đến trường của các bé." actions={<>
      <button className="secondary" onClick={() => window.print()}>In danh sách</button>
      {props.canManage && <RouteLink className="button-link" to={`${base}/new`} navigate={props.navigate}>+ Thêm học sinh</RouteLink>}
    </>} />
    <Stats items={[['Tổng số học sinh', String(rows.length), 'Hồ sơ bạn được phép xem'],
      ['Đang theo học', String(current.length), 'Theo ngày hiệu lực nhập học'],
      ['Học sinh mới tháng này', String(rows.filter(row => row.isNew).length), 'Ngày nhập học đầu tiên trong tháng'],
      ['Nhóm độ tuổi', String(new Set(current.map(row => row.ageGroup)).size), 'Nhóm của lần nhập học đang hiệu lực']]} />
    <Panel title="Danh sách học sinh">
      <Tabs items={['Tất cả', 'Mới nhập học']} value={tab} onChange={value => { setTab(value); list.setPage(1); }} />
      <div className="filter-toolbar"><Search value={list.query} onChange={list.setQuery} label="Tìm tên, mã học sinh hoặc liên hệ…" />
        <label><span className="sr-only">Lọc lớp</span><select value={classroom} onChange={event => { setClassroom(event.target.value); list.setPage(1); }}><option value="">Tất cả lớp</option>
          {Array.from(new Map(current.map(row => [row.classroomId, row])).values()).map(row => <option key={row.classroomId} value={row.classroomId}>{row.classroomName}</option>)}</select></label>
        <label><span className="sr-only">Trạng thái</span><select value={status} onChange={event => { setStatus(event.target.value); list.setPage(1); }}><option value="">Tất cả trạng thái</option>{studentStatuses.map(value => <option key={value}>{value}</option>)}</select></label>
      </div>
      <DataTable label="Danh sách học sinh" rows={list.visible} rowKey={row => row.id} columns={[
        { label: 'Mã hồ sơ', render: row => row.id }, { label: 'Họ và tên', render: row => row.profile.name },
        { label: 'Ngày sinh', render: row => date(row.profile.birthday) },
        { label: 'Lớp', render: row => currentEnrollment(row)?.classroomName ?? 'Chưa phân lớp' },
        { label: 'Phụ huynh', render: row => row.profile.guardian },
        { label: 'Trạng thái', render: row => <span className="badge">{row.status}</span> },
        { label: 'Thao tác', render: row => <RouteLink to={`${base}/${row.id}`} navigate={props.navigate}>Chi tiết</RouteLink> }
      ]} />
      <Pagination count={list.filtered.length} page={list.page} onChange={list.setPage} size={pageSize} preview={false} />
    </Panel>
  </>;
}

function EnrollmentFields({ classrooms, years, today, initialDate, fixedDate = false }: Pick<Props, 'classrooms' | 'years'> & {
  today: string; initialDate?: string; fixedDate?: boolean;
}) {
  const [year, setYear] = useState('');
  return <div className="single-column form-grid">
    <label>Năm học *<select value={year} required onChange={event => setYear(event.target.value)}><option value="">Chọn năm học</option>{years.map(row => <option key={row.id} value={row.id}>{row.name}</option>)}</select></label>
    <label>Lớp học *<select name="classroomId" key={year} required defaultValue=""><option value="">Chọn lớp</option>{classrooms.filter(row => row.schoolYearId === year).map(row => <option key={row.id} value={row.id}>{row.name}</option>)}</select></label>
    <Field spec={{ name: 'ageGroup', label: 'Nhóm tuổi', options: ageGroups, required: true }} />
    <label>Ngày nhập học *<input name="startDate" type="date" required max={today} defaultValue={initialDate ?? today} readOnly={fixedDate} /></label>
    <Field spec={{ name: 'studyMode', label: 'Hình thức học', options: studyModes, required: true }} />
  </div>;
}

function StudentForm({ person, ...props }: Props & { person?: Student }) {
  const p = person?.profile;
  const today = person?.today ?? schoolToday();
  const [enroll, setEnroll] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState<Student>();
  const groups: FieldGroup[] = [
    { title: 'Thông tin của bé', fields: [
      { name: 'name', label: 'Họ và tên', value: p?.name, required: true },
      { name: 'birthday', label: 'Ngày sinh', value: p?.birthday, type: 'date', max: today, required: true },
      { name: 'gender', label: 'Giới tính', value: p?.gender, options: ['Nam', 'Nữ'], required: true },
      { name: 'address', label: 'Địa chỉ thường trú', value: p?.address, required: true, full: true }
    ] },
    { title: 'Phụ huynh & người giám hộ', fields: [
      { name: 'guardian', label: 'Họ tên phụ huynh', value: p?.guardian, required: true },
      { name: 'relationship', label: 'Quan hệ với bé', value: p?.relationship, options: ['Bố', 'Mẹ', 'Người giám hộ'], required: true },
      { name: 'phone', label: 'Số điện thoại (10 chữ số)', value: p?.phone, type: 'tel', pattern: '[0-9]{10}', required: true },
      { name: 'email', label: 'Email', value: p?.email, type: 'email' },
      { name: 'pickup', label: 'Người đón bé', value: p?.pickup },
      { name: 'emergency', label: 'Liên hệ khẩn cấp', value: p?.emergency, type: 'tel', pattern: '[0-9]{10}', required: true }
    ] },
    { title: 'Sức khỏe & chăm sóc', fields: [
      { name: 'height', label: 'Chiều cao (cm, tùy chọn)', value: p?.height?.toString() },
      { name: 'weight', label: 'Cân nặng (kg, tùy chọn)', value: p?.weight?.toString() },
      { name: 'care', label: 'Dị ứng, bệnh lý và lưu ý (tùy chọn)', value: p?.care, multiline: true, full: true }
    ] }
  ];
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError('');
    const form = new FormData(event.currentTarget);
    const profile = studentForm(form);
    const validation = studentError(profile, today);
    if (validation) { setError(validation); return; }
    setBusy(true);
    try {
      const response = await mutate(`/api/students${person ? `/${person.id}` : ''}`,
        { profile, enrollment: !person && enroll ? enrollmentForm(form) : null }, props.schoolId, person ? 'PUT' : 'POST');
      setSaved(await response.json() as Student);
    } catch (cause) { setError(errorMessage(cause)); }
    finally { setBusy(false); }
  }
  return <>
    <PageHeader title={`${person ? 'Chỉnh sửa' : 'Thêm'} học sinh`} subtitle="Hồ sơ không tự tạo tài khoản đăng nhập." />
    <form onSubmit={submit}><fieldset disabled={busy || !!saved}><legend className="sr-only">Hồ sơ học sinh</legend>
      <div className="split-layout"><div className="stack">{groups.map(group => <Panel key={group.title} title={group.title}><div className="form-grid">{group.fields.map(spec => <Field key={spec.name} spec={spec} />)}</div></Panel>)}</div>
        <aside className="stack"><Panel title="Thông tin nhập học">
          {person ? <Message>Chỉnh sửa hồ sơ không đổi lớp. Dùng thao tác nhập học/chuyển lớp trong hồ sơ chi tiết.</Message> :
            <><label><input type="checkbox" checked={enroll} onChange={event => setEnroll(event.target.checked)} /> Nhập học ngay</label>
              {enroll && <EnrollmentFields classrooms={props.classrooms} years={props.years} today={today} />}</>}
        </Panel><Panel title="Tiếp nhận hồ sơ"><Facts items={[[ 'Mã hồ sơ', person?.id ?? 'ID bản ghi được tạo khi lưu' ],
          ['Trạng thái', person?.status ?? (enroll ? 'Theo ngày nhập học' : 'Chưa phân lớp')]]} /></Panel></aside></div>
      <footer className="form-footer"><p className="muted">* Bắt buộc</p><div className="page-actions"><RouteLink className="button-link secondary" to={base} navigate={props.navigate}>Hủy / Về danh sách</RouteLink><button type="submit">{busy ? 'Đang lưu…' : 'Lưu hồ sơ'}</button></div></footer>
    </fieldset></form>
    {error && <Message error>{error}</Message>}
    {saved && <Message>Đã lưu hồ sơ. <RouteLink navigate={props.navigate} to={`${base}/${saved.id}`}>Xem hồ sơ →</RouteLink></Message>}
  </>;
}

function StudentProfileScreen({ person, onChanged, ...props }: Props & { person: Student; onChanged: (student: Student) => void }) {
  const [tab, setTab] = useState('Thông tin chung');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const p = person.profile;
  const enrollment = currentEnrollment(person);
  async function change(path: string, data?: object, method: 'POST' | 'DELETE' = 'POST') {
    setBusy(true); setError(''); setNotice('');
    try {
      const response = await mutate(path, data, props.schoolId, method);
      onChanged(await response.json() as Student); setNotice('Đã cập nhật nhập học và giữ nguyên lịch sử.');
    } catch (cause) { setError(errorMessage(cause)); }
    finally { setBusy(false); }
  }
  function enroll(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const input = enrollmentForm(new FormData(event.currentTarget));
    if (enrollment && !window.confirm('Chuyển lớp sẽ kết thúc lần nhập học hiện tại và tạo lịch sử mới. Tiếp tục?')) return;
    void change(`/api/students/${person.id}/enrollments`, input);
  }
  return <>
    <PageHeader title="Hồ sơ học sinh" subtitle={`${p.name} · ${person.id}`} actions={<>
      <button className="secondary" onClick={() => window.print()}>In hồ sơ</button>
      {props.canManage && <RouteLink className="button-link" navigate={props.navigate} to={`${base}/${person.id}/edit`}>Chỉnh sửa hồ sơ</RouteLink>}
    </>} />
    <section className="card profile-heading"><div className="person-heading"><span className="initials">{p.name.split(' ').slice(-2).map(value => value[0]).join('')}</span>
      <div><h2>{p.name}</h2><p className="muted">{person.id} · {enrollment?.classroomName ?? 'Chưa phân lớp'}</p></div><span className="badge">{person.status}</span></div>
      <Tabs items={['Thông tin chung', 'Chuyên cần', 'Học phí', 'Tài liệu']} value={tab} onChange={setTab} /></section>
    {tab === 'Thông tin chung' ? <>
      <div className="equal-columns"><Panel title="Thông tin của bé"><Facts items={[
        ['Họ và tên', p.name], ['Ngày sinh', date(p.birthday)], ['Giới tính', p.gender],
        ['Lớp', enrollment?.classroomName ?? 'Chưa phân lớp'], ['Giáo viên hiện tại', enrollment?.teachers.join(' · ') || 'Chưa có phân công'],
        ['Ngày nhập học lớp hiện tại', enrollment ? date(enrollment.startDate) : '—']
      ]} /></Panel><Panel title="Phụ huynh & liên hệ"><Facts items={[
        ['Phụ huynh', `${p.guardian} · ${p.relationship}`], ['Điện thoại', p.phone], ['Email', p.email || '—'],
        ['Người đón bé', p.pickup || '—'], ['Địa chỉ', p.address], ['Liên hệ khẩn cấp', p.emergency]
      ]} /></Panel></div>
      <Panel title="Sức khỏe & lưu ý chăm sóc"><Facts items={[
        ['Chiều cao', p.height === null ? 'Chưa ghi nhận' : `${p.height} cm`],
        ['Cân nặng', p.weight === null ? 'Chưa ghi nhận' : `${p.weight} kg`], ['Lưu ý', p.care || 'Chưa ghi nhận']
      ]} /></Panel>
      <Panel title="Lịch sử lớp học"><DataTable label="Lịch sử nhập học được cấp quyền" rows={person.enrollments} rowKey={row => row.id} columns={[
        { label: 'Năm học', render: row => row.schoolYearName }, { label: 'Lớp', render: row => row.classroomName },
        { label: 'Từ ngày', render: row => date(row.startDate) }, { label: 'Đến trước ngày', render: row => date(row.endDate) },
        { label: 'Nhóm tuổi', render: row => row.ageGroup }, { label: 'Hình thức học', render: row => row.studyMode }
      ]} /></Panel>
      {props.canManage && <Panel title={enrollment ? 'Chuyển lớp / Kết thúc nhập học' : 'Nhập học'}>
        <Message>Ngày kết thúc không bao gồm trong khoảng nhập học. Lần nhập học tự hết hiệu lực khi năm học kết thúc. Không chỉnh sửa lịch sử đã kết thúc.</Message>
        <form onSubmit={enroll}><fieldset disabled={busy}><legend className="sr-only">Nhập học và chuyển lớp</legend>
          <EnrollmentFields key={`${person.id}:${person.enrollments.length}`} classrooms={props.classrooms} years={props.years} today={person.today} fixedDate={person.enrollments.length > 0} />
          <button type="submit">{busy ? 'Đang lưu…' : enrollment ? 'Chuyển lớp hôm nay' : 'Lưu nhập học'}</button>
          {enrollment && <button type="button" className="secondary" onClick={() => {
            if (window.confirm('Kết thúc nhập học hôm nay? Lịch sử sẽ được giữ lại.'))
              void change(`/api/students/${person.id}/enrollments/${enrollment.id}`, undefined, 'DELETE');
          }}>Kết thúc nhập học hôm nay</button>}
        </fieldset></form>
        {error && <Message error>{error}</Message>}{notice && <Message>{notice}</Message>}
      </Panel>}
    </> : <Panel title={tab}><EmptyState title="Chưa hỗ trợ trong phạm vi Student">Không hiển thị dữ liệu mẫu hoặc thực hiện thao tác chưa có API.</EmptyState></Panel>}
    <RouteLink navigate={props.navigate} to={base}>← Về danh sách</RouteLink>
  </>;
}
