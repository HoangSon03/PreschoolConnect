import { useEffect, useRef, useState } from 'react';
import { EmptyState, Metric, PageHeader } from '../../components/ui';
import type { Classroom, SchoolYear } from '../../lib/api';
import { previewGroups } from './preview-data';
import { RouteLink } from '../../components/design';

export default function Classes({ preview, classrooms, years, search, onSearch, canCreate, onCreate, navigate }: {
  preview: boolean; classrooms: Classroom[]; years: SchoolYear[]; search: string; onSearch: (value: string) => void; canCreate: boolean; onCreate: () => void; navigate: (url: string) => void;
}) {
  const [yearId, setYearId] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = classrooms.find(item => item.id === selectedId);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    if (selected) dialog.current?.showModal();
  }, [selected]);
  const matches = (name: string) => name.toLocaleLowerCase('vi-VN').includes(search.trim().toLocaleLowerCase('vi-VN'));
  const filtered = classrooms.filter(item => matches(item.name) && (!yearId || item.schoolYearId === yearId));
  return <>
    <PageHeader title="Lớp học" subtitle={preview ? '12 lớp học, 4 nhóm độ tuổi — cùng chăm sóc 248 mầm xanh.' : `${classrooms.length} lớp học trong không gian trường.`} actions={<button disabled={!canCreate && !preview} title={!canCreate && !preview ? 'Cần quyền SchoolAdmin' : undefined} onClick={onCreate}>+ Thêm lớp học</button>} />
    <label className="mobile-search">Tìm lớp học<input type="search" value={search} onChange={event => onSearch(event.target.value)} placeholder="Tìm lớp học…" /></label>
    <div className="metrics">
      <Metric label={preview ? 'Lớp đang hoạt động' : 'Lớp học'} value={preview ? '12' : String(classrooms.length)} note={preview ? 'Năm học: 2026 – 2027' : 'Dữ liệu từ API lớp học'} />
      <Metric label="Tổng số học sinh" value={preview ? '248' : '—'} note={preview ? 'Tất cả đang theo học' : 'Chưa có API sĩ số'} tone="amber" />
      <Metric label="Giáo viên phụ trách" value={preview ? '24' : '—'} note={preview ? '2 giáo viên / lớp' : 'Chưa có API phân công'} tone="purple" />
      <Metric label="Sức chứa toàn trường" value={preview ? '325' : '—'} note={preview ? 'Còn 77 chỗ' : 'Chưa có API sức chứa'} tone="blue" />
    </div>
    {preview ? <div className="class-groups">{previewGroups.map((group, groupIndex) => {
      const entries = group.sizes.map((size, index) => ({ size, name: `${group.name} ${index + 1}` })).filter(item => matches(item.name));
      return entries.length > 0 && <section className="card" key={group.name}><h2>{group.name} · {group.age}</h2><p className="muted section-caption">{group.total} học sinh · {group.sizes.length} lớp · 2 giáo viên mỗi lớp</p><div className="class-grid">{entries.map(item => <article className="class-tile" key={item.name}><div className="class-title"><h3>{item.name}</h3><span className="badge">Đang hoạt động</span></div><p className="muted">Năm học 2026–2027 · Dữ liệu mẫu</p><div className="capacity-label"><span>{item.size} / {group.capacity} học sinh</span><small>Còn {group.capacity - item.size} chỗ</small></div><progress value={item.size} max={group.capacity} aria-label={`Sĩ số ${item.name}`} /><p className="teacher-names">Giáo viên mẫu: Lê Minh Thu<br />Nguyễn Hoàng An</p><RouteLink navigate={navigate} to={`/preview/classes/${groupIndex}-${Number(item.name.split(' ').at(-1)) - 1}`}>Xem chi tiết lớp →</RouteLink></article>)}</div></section>;
    })}{!previewGroups.some(group => group.sizes.some((_, i) => matches(`${group.name} ${i + 1}`))) && <section className="card"><EmptyState title="Chưa tìm thấy lớp học">Thay đổi từ khóa tìm kiếm.</EmptyState></section>}</div> :
      <section className="card"><div className="filter-row"><h2>Danh sách lớp học</h2><label>Năm học<select value={yearId} onChange={event => setYearId(event.target.value)}><option value="">Tất cả năm học</option>{years.map(year => <option key={year.id} value={year.id}>{year.name}</option>)}</select></label></div>
        {filtered.length ? <div className="class-grid">{filtered.map(item => <article className="class-tile" key={item.id}><h3>{item.name}</h3><p className="muted">{years.find(year => year.id === item.schoolYearId)?.name ?? 'Năm học không khả dụng'}</p><p className="muted">Sĩ số và phân công giáo viên chưa có dữ liệu.</p><button className="text-button" onClick={() => setSelectedId(item.id)}>Xem thông tin lớp →</button></article>)}</div> : <EmptyState title="Chưa có lớp học phù hợp">Thử thay đổi tìm kiếm, bộ lọc hoặc tạo lớp học mới.</EmptyState>}
      </section>}
    {selected && <dialog ref={dialog} className="card detail-dialog" aria-labelledby="class-dialog-title" onClose={() => setSelectedId(null)}><h2 id="class-dialog-title">{selected.name}</h2><p>Năm học: {years.find(year => year.id === selected.schoolYearId)?.name}</p><p className="muted">Thông tin hiện có từ API. Đây không phải màn chi tiết đầy đủ trong Figma.</p><button autoFocus onClick={() => dialog.current?.close()}>Đóng</button></dialog>}
  </>;
}
