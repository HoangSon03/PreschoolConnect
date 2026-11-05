import { useEffect, useState, type FormEvent } from 'react';
import { DataTable, Facts, Pagination, Panel, RouteLink, Search, Tabs, useListFilters } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';
import { errorMessage, mutate, request } from '../../lib/api';
import { classroomError, monday, type Activity, type ClassroomDetail, type WeeklyNote } from './classroom-rules';
import './classrooms.css';

type Props = { path: string; schoolId: string; canManage: boolean; navigate: (url: string) => void; onSaved: () => Promise<void> };
const base = '/admin/classes';
const displayDate = (date: string) => new Date(`${date}T00:00:00`).toLocaleDateString('vi-VN');

export default function ClassroomDetails(props: Props) {
  const id = props.path.split('/')[3];
  const [detail, setDetail] = useState<ClassroomDetail>();
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setLoading(true); setError(''); setDetail(undefined);
    request<ClassroomDetail>(`/api/classes/${id}`, props.schoolId)
      .then(result => { if (active) setDetail(result); })
      .catch(cause => { if (active) setError(errorMessage(cause)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [id, props.schoolId, revision]);
  if (loading) return <p className="card" role="status">Đang tải chi tiết lớp học…</p>;
  if (error) return <Panel title="Không tải được lớp học"><Message error>{error}</Message><button onClick={() => setRevision(value => value + 1)}>Thử lại</button></Panel>;
  if (!detail) return <EmptyState title="Không tìm thấy lớp học" />;
  if (props.path.endsWith('/edit')) return props.canManage ? <ClassroomEdit key={detail.lastActionAtUtc}
    detail={detail} {...props} /> : <Message error>Chỉ quản trị viên trường được chỉnh sửa lớp.</Message>;
  return <ClassroomProfile detail={detail} {...props} />;
}

function ClassMetric({ label, value, note, icon }: { label: string; value: string; note: string; icon: string }) {
  return <article className="metric card"><div className="metric-label">{label}<img src={`/assets/classes/${icon}.svg`} alt="" /></div>
    <div className="metric-value">{value}</div><p className="muted">{note}</p></article>;
}

function ClassroomProfile({ detail, ...props }: Props & { detail: ClassroomDetail }) {
  const c = detail.classroom;
  const [tab, setTab] = useState('Danh sách học sinh');
  const list = useListFilters(detail.students, row => `${row.id} ${row.name}`, 15);
  const week = monday(detail.today);
  const note = detail.weeklyNotes.find(row => row.weekStart === week);
  return <div className="classroom-detail">
    <PageHeader title="Chi tiết lớp" subtitle={`${c.name} · Năm học ${detail.schoolYearName}`} actions={<>
      <button className="secondary class-print" onClick={() => window.print()}><img src="/assets/classes/export.svg" alt="" />In danh sách</button>
      {props.canManage && <RouteLink className="button-link" to={`${base}/${c.id}/edit`} navigate={props.navigate}>Chỉnh sửa lớp</RouteLink>}
    </>} />
    <div className="metrics">
      <ClassMetric label={`Sĩ số ${c.name}`} value={`${detail.students.length}${c.capacity !== null ? ` / ${c.capacity}` : ''}`}
        note={`${c.room ? `Phòng ${c.room}` : 'Chưa khai báo phòng'} · ${c.capacity === null ? 'Chưa khai báo sức chứa' : `${Math.max(0, c.capacity - detail.students.length)} chỗ trống (tham khảo)`}`} icon="students" />
      <ClassMetric label="Giáo viên phụ trách" value={String(c.teachers.length)} note="Phân công đang hiệu lực" icon="teachers" />
      <ClassMetric label="Có mặt hôm nay" value="—" note="Chưa triển khai điểm danh" icon="present" />
      <ClassMetric label="Vắng hôm nay" value="—" note="Chưa triển khai điểm danh" icon="absent" />
    </div>
    <Tabs items={['Danh sách học sinh', 'Thông tin lớp', 'Tài liệu']} value={tab} onChange={setTab} />
    <div className="classroom-columns">
      <section>
        {tab === 'Danh sách học sinh' ? <Panel title={`Học sinh lớp ${c.name} (${detail.students.length})`}>
          <Search value={list.query} onChange={list.setQuery} label="Tìm học sinh trong lớp…" />
          <DataTable label="Học sinh trong lớp" rows={list.visible} rowKey={row => row.id} columns={[
            { label: 'Mã học sinh', render: row => row.id },
            { label: 'Họ và tên', render: row => <RouteLink to={`/admin/students/${row.id}`} navigate={props.navigate}>{row.name}</RouteLink> },
            { label: 'Ngày sinh', render: row => displayDate(row.birthday) },
            { label: 'Trạng thái', render: () => <span className="badge">Đang theo học</span> }
          ]} />
          <Pagination count={list.filtered.length} page={list.page} onChange={list.setPage} size={15} preview={false} />
        </Panel> : tab === 'Thông tin lớp' ? <Panel title="Thông tin lớp"><Facts items={[
          ['Tên lớp', c.name], ['Năm học', detail.schoolYearName], ['Phòng', c.room || 'Chưa khai báo'],
          ['Sức chứa', c.capacity === null ? 'Chưa khai báo' : String(c.capacity)],
          ['Sĩ số hôm nay', String(detail.students.length)], ['Ngày dữ liệu (giờ Việt Nam)', displayDate(detail.today)]
        ]} /><p className="muted">Sức chứa chỉ để tham khảo, không chặn nhập học. Lịch sử học sinh được giữ tại hồ sơ học sinh.</p></Panel> :
          <Panel title="Tài liệu"><EmptyState title="Chưa hỗ trợ lưu tài liệu">Không có thao tác tải lên hoặc dữ liệu giả.</EmptyState></Panel>}
      </section>
      <aside className="stack">
        <Panel title="Giáo viên phụ trách">
          {c.teachers.length ? c.teachers.map(teacher => <div className="classroom-teacher" key={teacher.id}>
            <span className="avatar" aria-hidden="true">{teacher.name.split(' ').slice(-2).map(word => word[0]).join('')}</span>
            <div>{props.canManage ? <RouteLink to={`/admin/teachers/${teacher.id}`} navigate={props.navigate}>{teacher.name}</RouteLink> : <strong>{teacher.name}</strong>}
              <p className="muted">{teacher.role}</p></div>
          </div>) : <p className="muted">Chưa có giáo viên được phân công.</p>}
          {props.canManage && <RouteLink to="/admin/teachers" navigate={props.navigate}>Quản lý phân công giáo viên →</RouteLink>}
        </Panel>
        <Panel title="Lịch sinh hoạt hằng ngày"><p className="muted">Thứ Hai – Thứ Sáu</p>
          {detail.schedule.length ? <DataTable label="Lịch sinh hoạt" rows={detail.schedule} rowKey={row => row.start} columns={[
            { label: 'Giờ', render: row => `${row.start.slice(0, 5)}–${row.end.slice(0, 5)}` },
            { label: 'Hoạt động', render: row => row.activity }
          ]} /> : <p className="muted">Chưa khai báo lịch sinh hoạt.</p>}
        </Panel>
        <Panel title="Lưu ý trong tuần"><p className="muted">Tuần bắt đầu {displayDate(week)}</p>
          <p className="classroom-note">{note?.text ?? 'Chưa có lưu ý cho tuần này.'}</p>
        </Panel>
      </aside>
    </div>
    <div className="classroom-print-roster"><h2>Danh sách học sinh lớp {c.name} · {displayDate(detail.today)}</h2>
      <DataTable label="Danh sách in toàn bộ lớp" rows={detail.students} rowKey={row => row.id} columns={[
        { label: 'Mã học sinh', render: row => row.id }, { label: 'Họ và tên', render: row => row.name },
        { label: 'Ngày sinh', render: row => displayDate(row.birthday) }
      ]} />
    </div>
  </div>;
}

function ClassroomEdit({ detail, ...props }: Props & { detail: ClassroomDetail }) {
  const c = detail.classroom;
  const [name, setName] = useState(c.name);
  const [room, setRoom] = useState(c.room);
  const [capacity, setCapacity] = useState(c.capacity === null ? '' : String(c.capacity));
  const [schedule, setSchedule] = useState<Activity[]>(detail.schedule.map(row => ({ ...row, start: row.start.slice(0, 5), end: row.end.slice(0, 5) })));
  const [notes, setNotes] = useState<WeeklyNote[]>(detail.weeklyNotes);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const invalid = classroomError(name, room, capacity, schedule, notes);
    if (invalid) { setError(invalid); return; }
    setPending(true); setError('');
    try {
      await mutate(`/api/classes/${c.id}`, { name: name.trim(), room: room.trim(), capacity: capacity ? Number(capacity) : null,
        schedule: schedule.map(row => ({ ...row, start: `${row.start}:00`, end: `${row.end}:00` })),
        weeklyNotes: notes, lastActionAtUtc: detail.lastActionAtUtc }, props.schoolId, 'PUT');
      setSaved(true);
      await props.onSaved();
    } catch (cause) { setError(errorMessage(cause)); }
    finally { setPending(false); }
  }
  return <>
    <PageHeader title="Chỉnh sửa lớp học" subtitle={`${c.name} · ${detail.schoolYearName} · Giữ nguyên năm học và phân công hiện tại`} />
    {error && <Message error>{error}</Message>}
    {saved && <Message>Đã lưu lớp học. <RouteLink to={`${base}/${c.id}`} navigate={props.navigate}>Xem chi tiết lớp →</RouteLink></Message>}
    {!saved && <form className="stack" onSubmit={submit}><fieldset disabled={pending}>
      <div className="classroom-columns">
        <div className="stack"><Panel title="Thông tin lớp học"><div className="form-grid">
          <label>Tên lớp *<input required maxLength={100} value={name} onChange={event => setName(event.target.value)} /></label>
          <label>Năm học<input value={detail.schoolYearName} readOnly /></label>
          <label>Phòng học<input maxLength={100} value={room} onChange={event => setRoom(event.target.value)} /></label>
          <label>Sức chứa tối đa<input type="number" min={1} max={500} step={1} value={capacity} onChange={event => setCapacity(event.target.value)} /></label>
        </div></Panel>
        <Panel title="Lịch sinh hoạt · Thứ Hai–Thứ Sáu">
          {schedule.map((row, index) => <div className="classroom-activity" key={index}>
            <label>Bắt đầu *<input aria-label={`Bắt đầu hoạt động ${index + 1}`} type="time" required value={row.start} onChange={event => setSchedule(rows => rows.map((item, i) => i === index ? { ...item, start: event.target.value } : item))} /></label>
            <label>Kết thúc *<input aria-label={`Kết thúc hoạt động ${index + 1}`} type="time" required value={row.end} onChange={event => setSchedule(rows => rows.map((item, i) => i === index ? { ...item, end: event.target.value } : item))} /></label>
            <label>Hoạt động *<input aria-label={`Nội dung hoạt động ${index + 1}`} required maxLength={200} value={row.activity} onChange={event => setSchedule(rows => rows.map((item, i) => i === index ? { ...item, activity: event.target.value } : item))} /></label>
            <button type="button" className="secondary" aria-label={`Xóa hoạt động ${index + 1}`} onClick={() => setSchedule(rows => rows.filter((_, i) => i !== index))}>Xóa</button>
          </div>)}
          <button type="button" className="secondary" disabled={schedule.length >= 20} onClick={() => setSchedule(rows => [...rows, { start: '', end: '', activity: '' }])}>+ Thêm hoạt động</button>
        </Panel>
        <Panel title="Lưu ý theo tuần">
          {notes.map((row, index) => <div className="classroom-note-editor" key={index}>
            <label>Thứ Hai bắt đầu tuần *<input aria-label={`Tuần lưu ý ${index + 1}`} type="date" required value={row.weekStart} onChange={event => setNotes(rows => rows.map((item, i) => i === index ? { ...item, weekStart: event.target.value } : item))} /></label>
            <label>Nội dung *<textarea aria-label={`Nội dung lưu ý ${index + 1}`} required maxLength={2000} value={row.text} onChange={event => setNotes(rows => rows.map((item, i) => i === index ? { ...item, text: event.target.value } : item))} /></label>
            <button type="button" className="secondary" aria-label={`Xóa lưu ý ${index + 1}`} onClick={() => setNotes(rows => rows.filter((_, i) => i !== index))}>Xóa lưu ý</button>
          </div>)}
          <button type="button" className="secondary" disabled={notes.length >= 52} onClick={() => setNotes(rows => [...rows, { weekStart: monday(detail.today), text: '' }])}>+ Thêm lưu ý tuần</button>
        </Panel></div>
        <aside><Panel title="Bảo toàn dữ liệu"><p>Năm học, học sinh và phân công giáo viên không thay đổi khi lưu màn hình này.</p><p className="muted">Sức chứa chỉ hiển thị. Không ghi thông tin riêng tư của trẻ vào lịch hoặc lưu ý lớp.</p></Panel></aside>
      </div>
      <div className="page-actions"><RouteLink className="button-link secondary" to={`${base}/${c.id}`} navigate={props.navigate}>Hủy</RouteLink>
        <button type="submit" disabled={pending}>{pending ? 'Đang lưu…' : 'Lưu lớp học'}</button></div>
    </fieldset></form>}
  </>;
}
