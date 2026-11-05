import { useEffect, useRef, useState, type FormEvent } from 'react';
import { DataTable, Panel, RouteLink, Search, Stats } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';
import { errorMessage, mutate, type Classroom, type SchoolYear } from '../../lib/api';
import { previewYears, yearError, yearPhase, type ManagedYear, type YearValues } from './year-rules';

type Props = {
  preview: boolean; years?: SchoolYear[]; classrooms?: Classroom[]; schoolId?: string;
  canManage?: boolean; onSaved?: () => Promise<void>; navigate: (url: string) => void;
};
type Editor = { kind: 'new' } | { kind: 'edit'; year: ManagedYear } | { kind: 'delete'; year: ManagedYear };

export default function SchoolYears({ preview, years = [], classrooms = [], schoolId, canManage = false, onSaved, navigate }: Props) {
  const [samples, setSamples] = useState(previewYears);
  const [query, setQuery] = useState('');
  const [phase, setPhase] = useState('');
  const [editor, setEditor] = useState<Editor | null>(null);
  const [notice, setNotice] = useState('');
  const [refreshError, setRefreshError] = useState('');
  const source: ManagedYear[] = preview ? samples : years.map(year => ({
    ...year, classCount: year.classCount ?? classrooms.filter(item => item.schoolYearId === year.id).length
  }));
  const today = new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  const rows = source.filter(year => year.name.toLocaleLowerCase('vi-VN').includes(query.trim().toLocaleLowerCase('vi-VN')) &&
    (!phase || yearPhase(year, today) === phase)).sort((a, b) => b.startDate.localeCompare(a.startDate));
  const prefix = preview ? '/preview' : '/admin';
  const allowed = preview || canManage;

  async function refresh() {
    setRefreshError('');
    try { await onSaved?.(); }
    catch (error) { setRefreshError(`Thao tác đã thành công nhưng chưa tải lại được danh sách: ${errorMessage(error)}. Dùng Thử tải lại, không gửi lại thao tác.`); }
  }

  async function apply(values?: YearValues) {
    if (!editor) return;
    if (!allowed) throw new Error('Chỉ quản trị viên trường có thể thay đổi năm học.');
    if (editor.kind === 'delete') {
      if (editor.year.classCount) throw new Error('Không thể xóa năm học đã có lớp.');
      if (preview) setSamples(current => current.filter(year => year.id !== editor.year.id));
      else {
        if (!schoolId) throw new Error('Không có ngữ cảnh trường hợp lệ.');
        await mutate(`/api/school-years/${editor.year.id}`, undefined, schoolId, 'DELETE');
      }
    } else {
      if (!values) throw new Error('Thiếu thông tin năm học.');
      const validation = yearError(values, source, editor.kind === 'edit' ? editor.year : undefined);
      if (validation) throw new Error(validation);
      const payload: YearValues = { name: values.name.trim(), startDate: values.startDate, endDate: values.endDate };
      if (preview) {
        if (editor.kind === 'new') setSamples(current => [...current, { ...payload, id: crypto.randomUUID(), schoolId: 'sample-school', classCount: 0 }]);
        else setSamples(current => current.map(year => year.id === editor.year.id ? { ...year, ...payload } : year));
      } else {
        if (!schoolId) throw new Error('Không có ngữ cảnh trường hợp lệ.');
        await mutate(editor.kind === 'new' ? '/api/school-years' : `/api/school-years/${editor.year.id}`,
          payload, schoolId, editor.kind === 'new' ? 'POST' : 'PUT');
      }
    }
    const action = editor.kind === 'new' ? 'thêm' : editor.kind === 'edit' ? 'sửa' : 'xóa';
    setEditor(null);
    setNotice(preview ? `Đã ${action} năm học mẫu trong bộ nhớ. Không lưu vào hệ thống; tải lại trang sẽ khôi phục dữ liệu mẫu.` : `Đã ${action} năm học vào hệ thống.`);
    if (!preview) await refresh();
  }

  return <>
    <PageHeader title="Quản lý năm học" subtitle="Phân chia lớp theo từng khóa, giữ lịch sử và chuẩn bị cho năm học tiếp theo."
      actions={<button disabled={!allowed || !!refreshError} title={!allowed ? 'Chỉ SchoolAdmin được quản lý năm học' : undefined}
        onClick={() => { setNotice(''); setEditor({ kind: 'new' }); }}>+ Thêm năm học</button>} />
    <nav className="tabs settings-tabs" aria-label="Cài đặt">
      {preview && <RouteLink navigate={navigate} to="/preview/settings">Thông tin trường</RouteLink>}
      <RouteLink className="active" navigate={navigate} to={`${prefix}/settings/years`}>Năm học</RouteLink>
      <RouteLink navigate={navigate} to={`${prefix}/permissions`}>Phân quyền & tài khoản</RouteLink>
    </nav>
    {preview && <Message>Bản thử tương tác: thêm/sửa/xóa chỉ áp dụng cho danh sách mẫu trên màn này, không cập nhật lớp mẫu ở các màn khác.</Message>}
    {!allowed && <Message>Bạn có thể xem năm học. Chỉ quản trị viên trường có quyền thêm, sửa hoặc xóa.</Message>}
    {notice && <Message>{notice}</Message>}
    {refreshError && <><Message error>{refreshError}</Message><button className="secondary" onClick={() => void refresh()}>Thử tải lại</button></>}
    <Stats items={[
      ['Năm học', String(source.length), preview ? 'Dữ liệu mẫu trong phiên thử' : 'Trong trường được cấp quyền'],
      ['Trong kỳ', String(source.filter(year => yearPhase(year, today) === 'Trong kỳ').length), 'Tính theo khoảng ngày, không phải trạng thái phê duyệt'],
      ['Sắp bắt đầu', String(source.filter(year => yearPhase(year, today) === 'Sắp bắt đầu').length), 'Chuẩn bị cho khóa tiếp theo'],
      ['Chưa có lớp', String(source.filter(year => !year.classCount).length), 'Có thể sửa ngày hoặc xóa']
    ]} />
    <div className="split-layout year-layout">
      <Panel title="Danh sách năm học">
        <div className="filter-toolbar"><Search label="Tìm theo tên năm học…" value={query} onChange={setQuery} />
          <label>Khoảng thời gian<select value={phase} onChange={event => setPhase(event.target.value)}>
            <option value="">Tất cả</option>{['Trong kỳ', 'Sắp bắt đầu', 'Đã kết thúc'].map(item => <option key={item}>{item}</option>)}
          </select></label></div>
        {!source.length ? <EmptyState title="Chưa có năm học">Thêm năm học trước khi tạo lớp. Không tự sinh giá trị mặc định.</EmptyState> :
          <DataTable label="Danh sách năm học" rows={rows} rowKey={year => year.id} columns={[
            { label: 'Năm học', render: year => <><strong>{year.name}</strong><small className="block muted">{year.classCount ? 'Đã có lớp · khóa ngày' : 'Chưa có lớp'}</small></> },
            { label: 'Thời gian', render: year => <><time dateTime={year.startDate}>{formatDate(year.startDate)}</time><small className="block muted">đến {formatDate(year.endDate)}</small></> },
            { label: 'Số lớp', render: year => year.classCount },
            { label: 'Giai đoạn', render: year => <span className={`badge ${yearPhase(year, today) === 'Sắp bắt đầu' ? 'blue' : ''}`}>{yearPhase(year, today)}</span> },
            { label: 'Thao tác', render: year => <div className="year-actions">
              <button className="text-button" disabled={!allowed || !!refreshError} aria-label={`Sửa năm học ${year.name}`}
                onClick={() => { setNotice(''); setEditor({ kind: 'edit', year }); }}>Sửa</button>
              <button className="text-button danger-text" disabled={!allowed || year.classCount > 0 || !!refreshError}
                aria-label={`Xóa năm học ${year.name}`} title={year.classCount ? `Không thể xóa: đã có ${year.classCount} lớp` : 'Xóa năm học chưa có lớp'}
                onClick={() => { setNotice(''); setEditor({ kind: 'delete', year }); }}>Xóa</button>
            </div> }
          ]} />}
        <p className="muted">{rows.length} / {source.length} năm học{preview ? ' mẫu' : ''} · Năm học có lớp không thể xóa.</p>
      </Panel>
      <aside className="stack">
        <Panel title="Bảo toàn lịch sử"><ul className="year-rules">
          <li>Mỗi lớp thuộc một năm học.</li><li>Đã có lớp: chỉ sửa tên, không thay đổi ngày.</li><li>Chưa có lớp: sửa ngày hoặc xóa sau khi xác nhận.</li>
          <li>Không xóa dây chuyền hay tự chuyển lớp sang năm khác.</li>
        </ul></Panel>
        <Panel title="Quy tắc thời gian"><p className="muted">Ngày kết thúc phải sau ngày bắt đầu. Các năm học không chồng ngày, kể cả ngày tiếp giáp.</p>
          <p className="muted">Tên không trùng trong trường, không phân biệt hoa/thường và bỏ khoảng trắng đầu/cuối.</p>
          <p className="muted">“Trong kỳ” chỉ được tính từ ngày; không bổ sung trạng thái hoạt động hoặc năm học mặc định.</p></Panel>
      </aside>
    </div>
    {editor && <YearDialog key={editor.kind === 'new' ? 'new' : `${editor.kind}-${editor.year.id}`}
      editor={editor} years={source} preview={preview} onApply={apply} onClose={() => setEditor(current => current === editor ? null : current)} />}
  </>;
}

function formatDate(value: string) { return value.split('-').reverse().join('/'); }

function YearDialog({ editor, years, preview, onApply, onClose }: {
  editor: Editor; years: ManagedYear[]; preview: boolean; onApply: (values?: YearValues) => Promise<void>; onClose: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [values, setValues] = useState<YearValues>(editor.kind === 'new' ? { name: '', startDate: '', endDate: '' } :
    { name: editor.year.name, startDate: editor.year.startDate, endDate: editor.year.endDate });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const deleting = editor.kind === 'delete';
  const locked = editor.kind === 'edit' && editor.year.classCount > 0;
  useEffect(() => {
    const node = dialog.current;
    node?.showModal();
    return () => node?.close();
  }, []);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    if (!deleting) {
      const validation = yearError(values, years, editor.kind === 'edit' ? editor.year : undefined);
      if (validation) { setError(validation); return; }
    }
    setBusy(true);
    try { await onApply(deleting ? undefined : values); }
    catch (requestError) { setError(errorMessage(requestError)); setBusy(false); }
  }
  return <dialog ref={dialog} className="card year-dialog" aria-labelledby="year-dialog-title"
    onKeyDown={event => { if (event.key === 'Escape') { event.preventDefault(); if (!busy) onClose(); } }}
    onCancel={event => { event.preventDefault(); if (!busy) onClose(); }}>
    <h2 id="year-dialog-title">{deleting ? 'Xóa năm học' : editor.kind === 'new' ? 'Thêm năm học' : 'Sửa năm học'}</h2>
    <p className="muted">{preview ? 'Dữ liệu mẫu · Không lưu vào hệ thống thật' : 'Thay đổi trong trường được cấp quyền'}</p>
    <form onSubmit={event => void submit(event)}>
      <fieldset disabled={busy}><legend className="sr-only">Thông tin năm học</legend>
        {deleting ? <><p className="year-delete-question">Xóa năm học <strong>{editor.year.name}</strong> ({formatDate(editor.year.startDate)} – {formatDate(editor.year.endDate)})?</p>
          <Message>Năm học này chưa có lớp. Thao tác xóa không thể hoàn tác{preview ? ' trong phiên thử' : ''}.</Message></> :
          <><label>Tên năm học *<input autoFocus required maxLength={100} placeholder="Ví dụ: 2027–2028" value={values.name}
            onChange={event => { setValues(current => ({ ...current, name: event.target.value })); setError(''); }} /></label>
          <div className="form-grid"><label>Ngày bắt đầu *<input type="date" required disabled={locked} value={values.startDate}
            onChange={event => { setValues(current => ({ ...current, startDate: event.target.value })); setError(''); }} /></label>
            <label>Ngày kết thúc *<input type="date" required disabled={locked} value={values.endDate}
              onChange={event => { setValues(current => ({ ...current, endDate: event.target.value })); setError(''); }} /></label></div>
          {locked && <Message>Năm học đã có {editor.year.classCount} lớp; ngày bắt đầu/kết thúc được khóa. Bạn chỉ có thể sửa tên.</Message>}</>}
        {error && <Message error>{error}</Message>}
        <div className="year-dialog-footer"><button autoFocus={deleting} type="button" className="secondary" onClick={onClose}>Hủy</button>
          <button type="submit" className={deleting ? 'danger-button' : undefined}>{busy ? 'Đang xử lý…' : deleting ? `Xác nhận xóa${preview ? ' mẫu' : ''}` : `Lưu năm học${preview ? ' mẫu' : ''}`}</button></div>
      </fieldset>
    </form>
  </dialog>;
}
