import { useEffect, useState, type FormEvent } from 'react';
import { DataTable, Facts, Field, Pagination, Panel, RouteLink, Search, Stats, Tabs, useListFilters, type FieldGroup } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';
import { errorMessage, mutate, request, type Classroom } from '../../lib/api';
import { activeAssignments, assignmentRoles, contractOptions, degreeOptions, teacherError, teacherForm, teacherStatus, workOptions, type Teacher, type TeacherProfile } from './teacher-rules';

type Props = { path: string; schoolId: string; canManage: boolean; classrooms: Classroom[];
  navigate: (url: string) => void; onAssignmentsChanged: () => Promise<void> };
const base = '/admin/teachers';
const date = (value: string) => new Date(value).toLocaleDateString('vi-VN');

export default function Teachers(props: Props) {
  const [rows, setRows] = useState<Teacher[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [tab, setTab] = useState('Tất cả');
  const [classroom, setClassroom] = useState('');
  const [status, setStatus] = useState('');
  const segment = props.path.split('/')[3];
  const creating = segment === 'new';
  const editing = props.path.endsWith('/edit');
  const [accounts, setAccounts] = useState<{ userId: string; email: string }[]>([]);
  useEffect(() => {
    let active = true;
    setLoading(true); setError('');
    Promise.all([
      request<Teacher[]>(base.replace('/admin', '/api'), props.schoolId),
      props.canManage ? request<{ userId: string; email: string }[]>('/api/teachers/accounts', props.schoolId) : Promise.resolve([])
    ]).then(([teachers, choices]) => { if (active) { setRows(teachers); setAccounts(choices); } })
      .catch(cause => { if (active) setError(errorMessage(cause)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [props.schoolId, props.canManage, revision]);
  const visible = rows.filter(row => (!classroom || activeAssignments(row).some(item => item.classroomId === classroom)) &&
    (!status || teacherStatus(row) === status) && (tab === 'Tất cả' || teacherStatus(row) === tab));
  const list = useListFilters(visible, row => `${row.profile.name} ${row.code} ${row.profile.email} ${row.profile.phone}`);
  const person = rows.find(row => row.id === segment);
  if (loading) return <p role="status" className="card">Đang tải hồ sơ giáo viên…</p>;
  if (error) return <Panel title="Không tải được hồ sơ"><Message error>{error}</Message><button onClick={() => setRevision(value => value + 1)}>Thử lại</button></Panel>;
  if (creating || editing) {
    if (!props.canManage) return <Message error>Chỉ quản trị viên trường được chỉnh sửa hồ sơ.</Message>;
    if (editing && !person) return <EmptyState title="Không tìm thấy hồ sơ" />;
    return <TeacherForm key={person?.id ?? 'new'} person={person} {...props} accounts={accounts} />;
  }
  if (segment) return person ? <TeacherProfileScreen key={person.id} person={person} {...props}
    onChanged={async updated => {
      setRows(current => current.map(row => row.id === updated.id ? updated : row));
      await props.onAssignmentsChanged();
    }} /> :
    <EmptyState title="Không tìm thấy hồ sơ"><RouteLink navigate={props.navigate} to={base}>Về danh sách</RouteLink></EmptyState>;
  const assignedClasses = new Set(rows.filter(row => row.profile.isActive).flatMap(row => activeAssignments(row).map(item => item.classroomId))).size;
  return <>
    <PageHeader title="Giáo viên" subtitle="Hồ sơ và phân công trong trường được cấp quyền." actions={<>
      <button className="secondary" onClick={() => window.print()}>In danh sách</button>
      {props.canManage && <RouteLink className="button-link" to={`${base}/new`} navigate={props.navigate}>+ Thêm giáo viên</RouteLink>}
    </>} />
    <Stats items={[['Tổng số giáo viên', String(rows.length), 'Hồ sơ bạn được phép xem'],
      ['Đang công tác', String(rows.filter(row => row.profile.isActive).length), 'Theo trạng thái hồ sơ'],
      ['Lớp đã phân công', String(assignedClasses), 'Phân công đang hiệu lực'],
      ['Chưa phân lớp', String(rows.filter(row => !activeAssignments(row).length).length), 'Chưa có phân công đang hiệu lực']]} />
    <Panel title="Danh sách giáo viên">
      <Tabs items={['Tất cả', 'Đang công tác', 'Tạm nghỉ']} value={tab} onChange={value => { setTab(value); list.setPage(1); }} />
      <div className="filter-toolbar"><Search value={list.query} onChange={list.setQuery} label="Tìm tên, mã giáo viên hoặc liên hệ…" />
        <label><span className="sr-only">Lọc lớp</span><select value={classroom} onChange={event => { setClassroom(event.target.value); list.setPage(1); }}><option value="">Tất cả lớp</option>{props.classrooms.map(row => <option key={row.id} value={row.id}>{row.name}</option>)}</select></label>
        <label><span className="sr-only">Trạng thái</span><select value={status} onChange={event => { setStatus(event.target.value); list.setPage(1); }}><option value="">Tất cả trạng thái</option>{['Đang công tác', 'Tạm nghỉ'].map(value => <option key={value}>{value}</option>)}</select></label>
      </div>
      <DataTable label="Danh sách giáo viên" rows={list.visible} rowKey={row => row.id} columns={[
        { label: 'Mã hồ sơ', render: row => row.code }, { label: 'Họ và tên', render: row => row.profile.name },
        { label: 'Lớp phụ trách', render: row => activeAssignments(row).map(item => item.classroomName).join(', ') || 'Chưa phân lớp' },
        { label: 'Trình độ', render: row => row.profile.degree }, { label: 'Ngày vào làm', render: row => date(row.profile.startDate) },
        { label: 'Trạng thái', render: row => <span className="badge">{teacherStatus(row)}</span> },
        { label: 'Thao tác', render: row => <RouteLink to={`${base}/${row.id}`} navigate={props.navigate}>Chi tiết</RouteLink> }
      ]} />
      <Pagination count={list.filtered.length} page={list.page} onChange={list.setPage} preview={false} />
    </Panel>
  </>;
}

function TeacherForm({ person, accounts, ...props }: Props & { person?: Teacher; accounts: { userId: string; email: string }[] }) {
  const p = person?.profile;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const groups: FieldGroup[] = [
    { title: 'Thông tin cá nhân & liên hệ', fields: [
      { name: 'name', label: 'Họ và tên', value: p?.name, required: true },
      { name: 'birthday', label: 'Ngày sinh', value: p?.birthday, type: 'date', max: new Date().toISOString().slice(0, 10), required: true },
      { name: 'gender', label: 'Giới tính', value: p?.gender, options: ['Nữ', 'Nam'], required: true },
      { name: 'phone', label: 'Số điện thoại', value: p?.phone, type: 'tel', pattern: '[0-9]{10}', required: true },
      { name: 'email', label: 'Email', value: p?.email, type: 'email', required: true, full: true },
      { name: 'address', label: 'Địa chỉ liên hệ', value: p?.address, full: true }
    ] },
    { title: 'Trình độ & chuyên môn', fields: [
      { name: 'degree', label: 'Trình độ', value: p?.degree, options: degreeOptions, required: true },
      { name: 'graduated', label: 'Năm tốt nghiệp', value: p?.graduated.toString(), type: 'number', min: '1950', max: String(new Date().getFullYear()), required: true },
      { name: 'university', label: 'Cơ sở đào tạo', value: p?.university, required: true, full: true },
      { name: 'experience', label: 'Số năm kinh nghiệm', value: String(p?.experience ?? 0), type: 'number', min: '0' },
      { name: 'certificate', label: 'Chứng chỉ', value: p?.certificate }
    ] }
  ];
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const profile = teacherForm(new FormData(event.currentTarget));
    const invalid = teacherError(profile);
    if (invalid) { setError(invalid); return; }
    setBusy(true); setError('');
    try {
      const response = await mutate(`/api/teachers${person ? `/${person.id}` : ''}`, profile, props.schoolId, person ? 'PUT' : 'POST');
      const saved = await response.json() as Teacher;
      await props.onAssignmentsChanged();
      props.navigate(`${base}/${saved.id}`);
    } catch (cause) { setError(errorMessage(cause)); }
    finally { setBusy(false); }
  }
  return <>
    <PageHeader title={`${person ? 'Chỉnh sửa' : 'Thêm'} giáo viên`} subtitle="Hồ sơ không tự tạo tài khoản đăng nhập." />
    <form onSubmit={submit}><fieldset disabled={busy}><legend className="sr-only">Hồ sơ giáo viên</legend>
      <div className="split-layout"><div className="stack">{groups.map(group => <Panel key={group.title} title={group.title}><div className="form-grid">{group.fields.map(spec => <Field key={spec.name} spec={spec} />)}</div></Panel>)}
        <Panel title="Tài liệu hồ sơ"><p className="muted">Tải tệp bằng cấp, chứng chỉ và hợp đồng chưa được hỗ trợ.</p></Panel>
      </div><aside className="stack"><Panel title="Thông tin công tác"><div className="form-grid single-column">
        <Field spec={{ name: 'startDate', label: 'Ngày bắt đầu', value: p?.startDate, type: 'date', required: true }} />
        <Field spec={{ name: 'contract', label: 'Loại hợp đồng', value: p?.contract, options: contractOptions }} />
        <Field spec={{ name: 'work', label: 'Hình thức làm việc', value: p?.work, options: workOptions }} />
        <Field spec={{ name: 'status', label: 'Trạng thái', value: p?.isActive === false ? 'Tạm nghỉ' : 'Đang công tác', options: ['Đang công tác', 'Tạm nghỉ'] }} />
        <p className="muted">Quản lý các lớp phụ trách tại trang chi tiết sau khi lưu hồ sơ.</p>
      </div></Panel><Panel title="Quyền truy cập">
        <label>Tài khoản Teacher cùng trường<select name="userId" defaultValue={p?.userId ?? ''} disabled={!!p?.userId}>
          <option value="">Chưa liên kết tài khoản</option>{accounts.map(account => <option key={account.userId} value={account.userId}>{account.email}</option>)}
          {p?.userId && !accounts.some(account => account.userId === p.userId) && <option value={p.userId}>Tài khoản đã liên kết</option>}
        </select></label>
        {p?.userId && <input type="hidden" name="userId" value={p.userId} />}
        <p className="muted">Không tự ghép email hoặc tạo tài khoản. Liên kết đã lưu không được chuyển sang người khác.</p>
      </Panel></aside></div>
      {error && <Message error>{error}</Message>}
      <footer className="form-footer"><p className="muted">* Bắt buộc</p><div className="page-actions"><RouteLink className="button-link secondary" to={base} navigate={props.navigate}>Hủy</RouteLink><button type="submit">{busy ? 'Đang lưu…' : 'Lưu hồ sơ'}</button></div></footer>
    </fieldset></form>
  </>;
}

function TeacherProfileScreen({ person, onChanged, ...props }: Props & { person: Teacher; onChanged: (updated: Teacher) => Promise<void> }) {
  const [tab, setTab] = useState('Thông tin chung');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const p: TeacherProfile = person.profile;
  async function change(path: string, data?: object, method: 'POST' | 'DELETE' = 'POST') {
    setBusy(true); setError(''); setNotice('');
    try {
      const response = await mutate(`/api/teachers/${person.id}/assignments${path}`, data, props.schoolId, method);
      await onChanged(await response.json() as Teacher);
      setNotice('Đã lưu thay đổi phân công.');
    } catch (cause) { setError(errorMessage(cause)); }
    finally { setBusy(false); }
  }
  const current = activeAssignments(person);
  return <>
    <PageHeader title="Hồ sơ giáo viên" subtitle={`${p.name} · ${person.code}`} actions={<>
      <button className="secondary" onClick={() => window.print()}>In hồ sơ</button>
      {props.canManage && <RouteLink className="button-link" navigate={props.navigate} to={`${base}/${person.id}/edit`}>Chỉnh sửa hồ sơ</RouteLink>}
    </>} />
    <section className="card profile-heading"><div className="person-heading"><span className="initials">{p.name.split(' ').slice(-2).map(s => s[0]).join('')}</span><div><h2>{p.name}</h2><p className="muted">{person.code} · {current.map(row => row.classroomName).join(', ') || 'Chưa phân lớp'}</p></div><span className="badge">{teacherStatus(person)}</span></div>
      <Tabs items={['Thông tin chung', 'Lịch giảng dạy', 'Hồ sơ & chứng chỉ']} value={tab} onChange={setTab} />
    </section>
    {tab === 'Thông tin chung' ? <div className="split-layout"><div className="stack">
      <Panel title="Thông tin cá nhân & liên hệ"><Facts items={[['Ngày sinh', date(p.birthday)], ['Giới tính', p.gender], ['Điện thoại', p.phone], ['Email', p.email], ['Địa chỉ', p.address || 'Chưa cung cấp'], ['Ngày vào làm', date(p.startDate)], ['Hình thức làm việc', `${p.work} · ${p.contract}`]]} /></Panel>
      <Panel title="Trình độ & chuyên môn"><Facts items={[['Bằng cấp', p.degree], ['Cơ sở đào tạo', p.university], ['Năm tốt nghiệp', String(p.graduated)], ['Chứng chỉ', p.certificate || 'Chưa cung cấp'], ['Kinh nghiệm', `${p.experience} năm`]]} /></Panel>
      <Panel title="Lịch sử phân công"><DataTable label="Lịch sử phân công" rows={person.assignments} rowKey={row => row.id} columns={[
        { label: 'Lớp / Năm học', render: row => `${row.classroomName} · ${row.schoolYearName}` },
        { label: 'Vai trò', render: row => row.role }, { label: 'Bắt đầu', render: row => date(row.startedAtUtc) },
        { label: 'Kết thúc', render: row => row.endedAtUtc ? date(row.endedAtUtc) : 'Đang hiệu lực' }
      ]} /></Panel>
    </div><aside className="stack"><Panel title="Lớp được phân công">
      {!current.length && <EmptyState title="Chưa phân lớp" />}
      {current.map(row => <div key={row.id}><h3>{row.classroomName}</h3><p>{row.schoolYearName} · {row.role}</p>
        {props.canManage && <button className="secondary" disabled={busy} onClick={() => {
          if (window.confirm(`Kết thúc phân công lớp ${row.classroomName}? Lịch sử sẽ được giữ lại.`)) void change(`/${row.id}`, undefined, 'DELETE');
        }}>Kết thúc phân công</button>}</div>)}
      {props.canManage && p.isActive && <form onSubmit={event => {
        event.preventDefault(); const data = new FormData(event.currentTarget);
        void change('', { classroomId: data.get('classroomId'), role: data.get('role') });
      }}><fieldset disabled={busy || !props.classrooms.length}><legend>Thêm phân công</legend>
        <label>Lớp phụ trách<select name="classroomId" required><option value="">Chọn lớp</option>{props.classrooms.filter(row => !current.some(item => item.classroomId === row.id)).map(row => <option key={row.id} value={row.id}>{row.name}</option>)}</select></label>
        <Field spec={{ name: 'role', label: 'Vai trò', options: assignmentRoles, required: true }} />
        <button type="submit">{busy ? 'Đang lưu…' : 'Phân công'}</button>
      </fieldset></form>}
    </Panel><Panel title="Lịch làm việc"><p className="muted">Lịch làm việc và hoạt động giảng dạy chưa được hỗ trợ.</p></Panel></aside></div> :
      <Panel title={tab}><EmptyState title={tab === 'Lịch giảng dạy' ? 'Chưa hỗ trợ lịch giảng dạy' : 'Chưa hỗ trợ tải tài liệu'}>Không hiển thị dữ liệu minh họa trong hồ sơ thật.</EmptyState></Panel>}
    {error && <Message error>{error}</Message>}{notice && <Message>{notice}</Message>}
    <RouteLink navigate={props.navigate} to={base}>← Về danh sách</RouteLink>
  </>;
}
