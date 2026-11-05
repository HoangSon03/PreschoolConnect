import { useState, type FormEvent } from 'react';
import { Message, PageHeader } from '../../components/ui';
import { errorMessage, mutate, type SchoolYear } from '../../lib/api';

export default function SchoolForm({ kind, schoolId, years, canManage = true, onSaved }: {
  kind: 'class' | 'year'; schoolId: string; years: SchoolYear[]; canManage?: boolean; onSaved: () => Promise<void>;
}) {
  const [name, setName] = useState('');
  const [yearId, setYearId] = useState(years[0]?.id ?? '');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage) {
      setError('Chỉ quản trị viên trường có thể lưu thay đổi.');
      return;
    }
    setError('');
    setNotice('');
    setPending(true);
    try {
      await mutate(kind === 'class' ? '/api/classes' : '/api/school-years',
        kind === 'class' ? { name: name.trim(), schoolYearId: yearId } : { name, startDate, endDate }, schoolId);
      setName('');
      setNotice(kind === 'class' ? 'Đã lưu lớp học vào hệ thống.' : 'Đã lưu năm học vào hệ thống.');
      await onSaved();
    } catch (requestError) {
      setError(errorMessage(requestError));
    } finally {
      setPending(false);
    }
  }

  return <>
    <PageHeader title={kind === 'class' ? 'Thêm lớp học' : 'Cài đặt năm học'} subtitle="Tạo lớp theo tên và năm học. Phòng, sức chứa, lịch và lưu ý có thể khai báo trong màn chỉnh sửa sau khi tạo; phân công tại hồ sơ giáo viên." />
    <section className="card form-card">{error && <Message error>{error}</Message>}{notice && <Message>{notice}</Message>}
      {!canManage && <Message>Bạn có thể xem năm học. Chỉ quản trị viên trường có thể tạo mới.</Message>}
      <form onSubmit={submit}><fieldset disabled={pending || !canManage}>
        <legend>{kind === 'class' ? 'Thông tin lớp học' : 'Tạo năm học'}</legend>
        <label>Tên {kind === 'class' ? 'lớp học' : 'năm học'} *<input required maxLength={100} value={name} onChange={event => setName(event.target.value)} /></label>
        {kind === 'class' ? <label>Năm học *<select required value={yearId} onChange={event => setYearId(event.target.value)}><option value="" disabled>Chọn năm học</option>{years.map(year => <option key={year.id} value={year.id}>{year.name}</option>)}</select></label> :
          <div className="form-grid"><label>Ngày bắt đầu *<input required type="date" value={startDate} onChange={event => setStartDate(event.target.value)} /></label><label>Ngày kết thúc *<input required type="date" min={startDate} value={endDate} onChange={event => setEndDate(event.target.value)} /></label></div>}
        <button disabled={pending || (kind === 'class' && years.length === 0)} type="submit">{pending ? 'Đang lưu…' : 'Lưu thay đổi'}</button>
        {kind === 'class' && !years.length && <Message>Tạo năm học trong Cài đặt trước khi thêm lớp.</Message>}
      </fieldset></form>
    </section>
    {kind === 'year' && <section className="card"><h2>Năm học hiện có</h2>{years.length ? <ul className="year-list">{years.map(year => <li key={year.id}><strong>{year.name}</strong><span>{year.startDate} – {year.endDate}</span></li>)}</ul> : <p>Chưa có năm học.</p>}</section>}
  </>;
}
