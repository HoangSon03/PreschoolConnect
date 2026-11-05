import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { EmptyState, Message, Metric } from './ui';
import { previewFormError } from '../lib/preview';

export function RouteLink({ to, navigate, children, className }: { to: string; navigate: (url: string) => void; children: ReactNode; className?: string }) {
  return <a className={className} href={to} onClick={event => {
    if (event.button || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    navigate(to);
  }}>{children}</a>;
}

export function Tabs({ items, value, onChange }: { items: string[]; value: string; onChange: (value: string) => void }) {
  return <div className="tabs" aria-label="Chọn nội dung">{items.map(item => <button key={item} type="button" className={value === item ? 'active' : ''} aria-pressed={value === item} onClick={() => onChange(item)}>{item}</button>)}</div>;
}

export function Stats({ items }: { items: [string, string, string][] }) {
  return <div className="metrics">{items.map(([label, value, note], i) => <Metric key={label} label={label} value={value} note={note} tone={['green', 'amber', 'purple', 'blue'][i]} />)}</div>;
}

export function Panel({ title, children, className = '' }: { title: string; children: ReactNode; className?: string }) {
  return <section className={`card panel-stack ${className}`}><h2>{title}</h2>{children}</section>;
}

export function Facts({ items }: { items: [string, ReactNode][] }) {
  return <dl className="facts">{items.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>;
}

export type Column<T> = { label: string; render: (row: T) => ReactNode };
export function DataTable<T>({ rows, columns, rowKey, label }: { rows: T[]; columns: Column<T>[]; rowKey: (row: T) => string; label: string }) {
  return rows.length ? <div className="table-scroll" tabIndex={0} role="region" aria-label={label}><table><caption className="sr-only">{label}</caption><thead><tr>{columns.map(column => <th scope="col" key={column.label}>{column.label}</th>)}</tr></thead><tbody>{rows.map(row => <tr key={rowKey(row)}>{columns.map(column => <td key={column.label}>{column.render(row)}</td>)}</tr>)}</tbody></table></div> : <EmptyState title="Không có kết quả">Thử thay đổi từ khóa hoặc bộ lọc.</EmptyState>;
}

export function Pagination({ count, page, onChange, size = 6, preview = true }: { count: number; page: number; onChange: (page: number) => void; size?: number; preview?: boolean }) {
  const pages = Math.max(1, Math.ceil(count / size));
  return <div className="pagination"><p className="muted">{count ? `${(page - 1) * size + 1}–${Math.min(page * size, count)}` : '0'} trong {count} kết quả{preview ? ' mẫu' : ''}</p><nav aria-label="Phân trang"><button className="secondary" disabled={page === 1} aria-label="Trang trước" onClick={() => onChange(page - 1)}>‹</button>{Array.from({ length: pages }, (_, i) => <button className={page === i + 1 ? 'selected-page' : 'secondary'} aria-current={page === i + 1 ? 'page' : undefined} key={i} onClick={() => onChange(i + 1)}>{i + 1}</button>)}<button className="secondary" disabled={page === pages} aria-label="Trang sau" onClick={() => onChange(page + 1)}>›</button></nav></div>;
}

export type FieldSpec = { name: string; label: string; value?: string; type?: 'text' | 'date' | 'time' | 'number' | 'email' | 'tel'; options?: string[]; multiline?: boolean; required?: boolean; full?: boolean; min?: string; max?: string; pattern?: string };
export type FieldGroup = { title: string; fields: FieldSpec[]; note?: string };

export function Field({ spec }: { spec: FieldSpec }) {
  return <label className={spec.full ? 'full-width' : undefined}>{spec.label}{spec.required ? ' *' : ''}
    {spec.options ? <select name={spec.name} defaultValue={spec.value ?? spec.options[0]} required={spec.required}>{spec.options.map(option => <option key={option}>{option}</option>)}</select> :
      spec.multiline ? <textarea name={spec.name} defaultValue={spec.value} required={spec.required} rows={5} /> :
        <input name={spec.name} type={spec.type ?? 'text'} defaultValue={spec.value} required={spec.required} min={spec.min} max={spec.max} pattern={spec.pattern} maxLength={spec.type === 'text' || !spec.type ? 500 : undefined} />}
  </label>;
}

export function PreviewForm({ groups, aside, submitLabel, cancel, children, onValues }: {
  groups: FieldGroup[]; aside?: ReactNode; submitLabel: string; cancel: ReactNode; children?: ReactNode; onValues?: (values: FormData) => void;
}) {
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setNotice('');
    setError('');
    const validationError = previewFormError(data);
    if (validationError) {
      setError(validationError);
      return;
    }
    setNotice('Đã kiểm tra biểu mẫu trong bản xem trước. Chưa lưu, gửi hay thay đổi dữ liệu nào; chức năng này chưa có API.');
  }
  return <form onSubmit={submit} onChange={event => { setNotice(''); setError(''); onValues?.(new FormData(event.currentTarget)); }}>
    <div className="split-layout"><div className="stack">{groups.map(group => <Panel key={group.title} title={group.title}><div className="form-grid">{group.fields.map(spec => <Field key={spec.name} spec={spec} />)}</div>{group.note && <Message>{group.note}</Message>}</Panel>)}{children}</div><aside className="stack">{aside}</aside></div>
    {error && <Message error>{error}</Message>}{notice && <Message>{notice}</Message>}
    <footer className="form-footer"><p className="muted">* Bắt buộc · Bản xem trước, không lưu dữ liệu</p><div className="page-actions">{cancel}<button type="submit">{submitLabel} · xem trước</button></div></footer>
  </form>;
}

export function FilePicker({ label }: { label: string }) {
  const [files, setFiles] = useState<string[]>([]);
  const [error, setError] = useState('');
  return <div><label className="file-picker">{label} · PDF, JPG, PNG · tối đa 10 MB/tệp<input type="file" multiple accept=".pdf,.jpg,.jpeg,.png" onChange={event => {
    const selected = Array.from(event.target.files ?? []);
    if (selected.some(file => file.size > 10 * 1024 * 1024 || !/\.(pdf|jpe?g|png)$/i.test(file.name))) {
      setError('Chỉ chọn PDF/JPG/PNG không quá 10 MB/tệp.'); setFiles([]); event.target.value = ''; return;
    }
    setError(''); setFiles(selected.map(file => file.name));
  }} /></label><p className="muted">{files.length ? files.join(', ') : 'Chưa chọn tệp.'} Tệp chỉ được chọn cục bộ, chưa tải lên.</p>{error && <Message error>{error}</Message>}</div>;
}

export function useListFilters<T>(rows: T[], text: (row: T) => string) {
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const filtered = rows.filter(row => text(row).toLocaleLowerCase('vi-VN').includes(query.trim().toLocaleLowerCase('vi-VN')));
  const count = filtered.length;
  const safePage = Math.min(page, Math.max(1, Math.ceil(count / 6)));
  useEffect(() => { setPage(1); }, [query]);
  return { query, setQuery, page: safePage, setPage, filtered, visible: filtered.slice((safePage - 1) * 6, safePage * 6) };
}

export function Search({ value, onChange, label = 'Tìm kiếm' }: { value: string; onChange: (value: string) => void; label?: string }) {
  return <label className="list-search"><span className="sr-only">{label}</span><input type="search" placeholder={label} value={value} onChange={event => onChange(event.target.value)} /></label>;
}
