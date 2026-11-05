import { useState } from 'react';
import { EmptyState, Metric, PageHeader } from '../../components/ui';
import type { Classroom, SchoolYear } from '../../lib/api';
import { previewGroups } from './preview-data';
import { RouteLink } from '../../components/design';
import { classTotals } from './classroom-rules';

export default function Classes({ preview, classrooms, years, search, onSearch, canCreate, onCreate, navigate }: {
  preview: boolean; classrooms: Classroom[]; years: SchoolYear[]; search: string; onSearch: (value: string) => void; canCreate: boolean; onCreate: () => void; navigate: (url: string) => void;
}) {
  const [yearId, setYearId] = useState('');
  const matches = (name: string) => name.toLocaleLowerCase('vi-VN').includes(search.trim().toLocaleLowerCase('vi-VN'));
  const filtered = classrooms.filter(item => matches(item.name) && (!yearId || item.schoolYearId === yearId));
  const totals = classTotals(classrooms.filter(item => !yearId || item.schoolYearId === yearId));
  return <>
    <PageHeader title="Lớp học" subtitle={preview ? '12 lớp học, 4 nhóm độ tuổi — cùng chăm sóc 248 mầm xanh.' : `${classrooms.length} lớp học trong không gian trường.`} actions={<button disabled={!canCreate && !preview} title={!canCreate && !preview ? 'Cần quyền SchoolAdmin' : undefined} onClick={onCreate}>+ Thêm lớp học</button>} />
    <label className="mobile-search">Tìm lớp học<input type="search" value={search} onChange={event => onSearch(event.target.value)} placeholder="Tìm lớp học…" /></label>
    <div className="metrics">
      <Metric label={preview ? 'Lớp đang hoạt động' : 'Lớp học'} value={preview ? '12' : String(classrooms.length)} note={preview ? 'Năm học: 2026 – 2027' : 'Dữ liệu từ API lớp học'} />
      <Metric label="Tổng số học sinh" value={preview ? '248' : String(totals.students)} note={preview ? 'Tất cả đang theo học' : 'Đang nhập học hôm nay · theo bộ lọc năm học'} tone="amber" />
      <Metric label="Giáo viên phụ trách" value={preview ? '24' : String(totals.teachers)} note={preview ? '2 giáo viên / lớp' : 'Phân công đang hiệu lực · không đếm trùng'} tone="purple" />
      <Metric label="Sức chứa các lớp" value={preview ? '325' : totals.capacity === null ? '—' : String(totals.capacity)} note={preview ? 'Còn 77 chỗ' : totals.capacity === null ? 'Có lớp chưa khai báo sức chứa' : 'Chỉ hiển thị, không chặn nhập học'} tone="blue" />
    </div>
    {preview ? <div className="class-groups">{previewGroups.map((group, groupIndex) => {
      const entries = group.sizes.map((size, index) => ({ size, name: `${group.name} ${index + 1}` })).filter(item => matches(item.name));
      return entries.length > 0 && <section className="card" key={group.name}><h2>{group.name} · {group.age}</h2><p className="muted section-caption">{group.total} học sinh · {group.sizes.length} lớp · 2 giáo viên mỗi lớp</p><div className="class-grid">{entries.map(item => <article className="class-tile" key={item.name}><div className="class-title"><h3>{item.name}</h3><span className="badge">Đang hoạt động</span></div><p className="muted">Năm học 2026–2027 · Dữ liệu mẫu</p><div className="capacity-label"><span>{item.size} / {group.capacity} học sinh</span><small>Còn {group.capacity - item.size} chỗ</small></div><progress value={item.size} max={group.capacity} aria-label={`Sĩ số ${item.name}`} /><p className="teacher-names">Giáo viên mẫu: Lê Minh Thu<br />Nguyễn Hoàng An</p><RouteLink navigate={navigate} to={`/preview/classes/${groupIndex}-${Number(item.name.split(' ').at(-1)) - 1}`}>Xem chi tiết lớp →</RouteLink></article>)}</div></section>;
    })}{!previewGroups.some(group => group.sizes.some((_, i) => matches(`${group.name} ${i + 1}`))) && <section className="card"><EmptyState title="Chưa tìm thấy lớp học">Thay đổi từ khóa tìm kiếm.</EmptyState></section>}</div> :
      <section className="card"><div className="filter-row"><h2>Danh sách lớp học</h2><label>Năm học<select value={yearId} onChange={event => setYearId(event.target.value)}><option value="">Tất cả năm học</option>{years.map(year => <option key={year.id} value={year.id}>{year.name}</option>)}</select></label></div>
        {filtered.length ? <div className="class-grid">{filtered.map(item => <article className="class-tile" key={item.id}><h3>{item.name}</h3><p className="muted">{years.find(year => year.id === item.schoolYearId)?.name ?? 'Năm học không khả dụng'}</p>
          <p>{item.studentCount}{item.capacity !== null ? ` / ${item.capacity}` : ''} học sinh</p>
          {item.capacity !== null && <progress value={item.studentCount} max={item.capacity} aria-label={`Sĩ số ${item.name}`} />}
          <p className="muted">{item.room ? `Phòng: ${item.room}` : 'Chưa khai báo phòng'}</p>
          <p className="teacher-names">{item.teachers.length ? item.teachers.map(teacher => teacher.name).join(' · ') : 'Chưa có giáo viên được phân công'}</p>
          <RouteLink navigate={navigate} to={`/admin/classes/${item.id}`}>Xem chi tiết lớp →</RouteLink></article>)}</div> : <EmptyState title="Chưa có lớp học phù hợp">Thử thay đổi tìm kiếm, bộ lọc hoặc tạo lớp học mới.</EmptyState>}
      </section>}
  </>;
}
